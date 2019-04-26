using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Solid.Arduino.Firmata;

namespace Solid.Arduino.Serial
{
    /// <summary>
    /// Represents a serial port connection.
    /// </summary>
    public class SerialConnectionFinder
    {
        private readonly ILogger _logger;
        public IDataConnectionFactory Factory { get; }

        #region Fields

        private const int DefaultTimeoutMs = 100;

        private static readonly SerialBaudRate[] PopularBaudRates =
        {
            SerialBaudRate.Bps_57600, // This is the default baud rate of StandardFirmata.ino, so try this one first.
            SerialBaudRate.Bps_115200,
            SerialBaudRate.Bps_9600
        };

        private static readonly SerialBaudRate[] OtherBaudRates =
        {
            SerialBaudRate.Bps_28800,
            SerialBaudRate.Bps_14400,
            SerialBaudRate.Bps_38400,
            SerialBaudRate.Bps_31250,
            SerialBaudRate.Bps_4800,
            SerialBaudRate.Bps_2400
        };


        #endregion

        public SerialConnectionFinder(IDataConnectionFactory factory, ILogger logger)
        {
            _logger = logger;
            Factory = factory;
        }


        #region Public Methods & Properties


        /// <summary>
        /// Finds a serial connection to a device supporting the Firmata protocol.
        /// </summary>
        /// <returns>A <see cref="ArduinoSession"/> instance or <c>null</c> if no Firmata device is found</returns>
        /// <remarks>
        /// <para>
        /// This method searches all available serial ports until it finds a working serial connection.
        /// For every available serial port an attempt is made to open a connection at a range of common baudrates.
        /// The connection is tested by issueing an <see cref="IFirmataProtocol.GetFirmware()"/> command.
        /// (I.e. a Firmata SysEx Firmware query (0xF0 0x79 0xF7).)
        /// </para>
        /// <para>
        /// The connected device is expected to respond by sending the version number of the supported protocol.
        /// When a major version of 2 or higher is received, the connection is regarded to be valid.
        /// </para>
        /// </remarks>
        /// <seealso cref="IFirmataProtocol"/>
        /// <seealso href="http://www.firmata.org/wiki/Protocol#Query_Firmware_Name_and_Version">Query Firmware Name and Version</seealso>
        public ArduinoSession FindFirmata()
        {
            var portNames = Factory.GetDeviceNames();
            return FindConnection(IsFirmataAvailable, portNames, PopularBaudRates, DefaultTimeoutMs)
                   ?? FindConnection(IsFirmataAvailable, portNames, OtherBaudRates, DefaultTimeoutMs);
        }

        public ArduinoSession FindFirmata(string portName, SerialBaudRate baudRate, int timeOut)
        {
            string[] portNames = { portName };
            SerialBaudRate[] baudRates = { baudRate };
            return FindConnection(IsFirmataAvailable, portNames, baudRates, timeOut);
        }

        /// <summary>
        /// Finds a serial connection to a device supporting plain serial communications.
        /// </summary>
        /// <param name="query">The query text used to inquire the connection</param>
        /// <param name="expectedReply">The reply text the connected device is expected to respond with</param>
        /// <returns>A <see cref="IDataConnection"/> instance or <c>null</c> if no connection is found</returns>
        /// <remarks>
        /// <para>
        /// This method searches all available serial ports until it finds a working serial connection.
        /// For every available serial port an attempt is made to open a connection at a range of common baudrates.
        /// The connection is tested by sending the query string passed to this method.
        /// </para>
        /// <para>
        /// The connected device is expected to respond by sending the reply string passed to this method.
        /// When the string received is equal to the expected reply string, the connection is regarded to be valid.
        /// </para>
        /// </remarks>
        /// <example>
        /// The Arduino sketch below can be used to demonstrate this method.
        /// Upload the sketch to your Arduino device.
        /// <code lang="Arduino Sketch">
        /// char query[] = "Hello?";
        /// char reply[] = "Arduino!";
        ///
        /// void setup()
        /// {
        ///   Serial.begin(9600);
        ///   while (!Serial) {}
        /// }
        ///
        /// void loop()
        /// {
        ///   if (Serial.find(query))
        ///   {
        ///     Serial.println(reply);
        ///   }
        ///   else
        ///   {
        ///     Serial.println("Listening...");
        ///     Serial.flush();
        ///   }
        ///
        ///   delay(25);
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="IStringProtocol"/>
        public ArduinoSession FindNonFirmata(string query, string expectedReply)
        {
            if (string.IsNullOrEmpty(query))
                throw new ArgumentNullException(nameof(query));

            if (string.IsNullOrEmpty(expectedReply))
                throw new ArgumentNullException(nameof(expectedReply));

            bool IsAvailableFunc(ArduinoSession session)
            {
                session.Write(query);
                return session.Read(expectedReply.Length) == expectedReply;
            }

            var portNames = Factory.GetDeviceNames();
            var connection = FindConnection(IsAvailableFunc, portNames, PopularBaudRates, DefaultTimeoutMs);
            return connection ?? FindConnection(IsAvailableFunc, portNames, OtherBaudRates, DefaultTimeoutMs);
        }

        #endregion

        #region Private Methods

        private static bool IsFirmataAvailable(ArduinoSession session)
        {
            Firmware firmware = session.GetFirmware();
            return firmware.MajorVersion >= 2;
        }

        private ArduinoSession FindConnection(Func<ArduinoSession, bool> isDeviceAvailable, IEnumerable<string> portNames, SerialBaudRate[] baudRates, int timeOut)
        {
            foreach (var portName in portNames.Reverse())
            {
                foreach (var baudRate in baudRates)
                {
                    IDataConnection connection = null;
                    ArduinoSession session = null;

                    try
                    {
                        connection = Factory.Create(portName, new SerialConnectionConfiguration() { BaudRate = baudRate });
                        session = new ArduinoSession(connection, timeOut);

                        _logger?.Debug("FindConnection: Checking for Firmata on Port {0}:{1}", portName, (int)baudRate);

                        if (isDeviceAvailable(session))
                        {
                            _logger?.Info("FindConnection: Firmata found on Port {0}:{1}", portName, (int)baudRate);
                            return session;
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        _logger?.Warn(
                            "FindConnection: UnauthorizedAccess on Port {0}:{1}, the port is possibly already opened by other process", portName, (int)baudRate);
                        break;
                    }
                    catch (TimeoutException)
                    {
                        _logger?.Warn("FindConnection: Timeout on Port {0}:{1} Baud rate or protocol error", portName, (int)baudRate);
                    }
                    catch (IOException ex)
                    {
                        _logger?.Error("FindConnection: IOException on Port {0}:{1}, HResult 0x{2:X} - {3}", portName, (int)baudRate, ex.HResult, ex.Message);
                    }
                    catch (Exception ex)
                    {
                        // Opening a connection, with a baud rate other than that of the Arduino, may result in receiving incorrect bytes. 
                        // This may result in incorrectly interpreted and unknown Firmata commands. Those will throw an exception.
                        _logger?.Error("FindConnection: Unexpected exception on Port {0}:{1}: {2}", portName, (int)baudRate, ex);
                    }

                    session?.Dispose();
                    connection?.Dispose();
                }
            }
            return null;
        }

        #endregion
    }
}