using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Solid.Arduino.Firmata;

namespace Solid.Arduino.Serial
{
    /// <summary>
    /// Represents a serial port connection.
    /// </summary>
    public class FirmataFinder
    {
        private readonly ILogger _logger;
        public IDataConnectionFactory Factory { get; }

        public int MillisecondsToWaitAfterOpen { get; set; } = 0;

        #region Fields

        private const int DefaultTimeoutMs = 1000;

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

        public FirmataFinder(IDataConnectionFactory factory, ILogger logger)
        {
            _logger = logger;
            Factory = factory;
        }


        #region Public Methods & Properties


        /// <summary>
        /// Finds a serial connection to a device supporting the Firmata protocol.
        /// </summary>
        /// <returns>A <see cref="FirmataSession"/> instance or <c>null</c> if no Firmata device is found</returns>
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
        public FirmataSession FindFirmata()
        {
            var portNames = Factory.GetDeviceNames();
            return FindConnection(IsFirmataAvailable, portNames, PopularBaudRates, DefaultTimeoutMs)
                   ?? FindConnection(IsFirmataAvailable, portNames, OtherBaudRates, DefaultTimeoutMs);
        }

        public FirmataSession FindFirmata(string portName, SerialBaudRate baudRate, int timeOut)
        {
            string[] portNames = { portName };
            SerialBaudRate[] baudRates = { baudRate };
            return FindConnection(IsFirmataAvailable, portNames, baudRates, timeOut);
        }

        #endregion

        #region Private Methods

        private static bool IsFirmataAvailable(FirmataSession session)
        { 
            //TODO: Fix IsFirmataAvailable, will now always return true......
            return true;

            Firmware firmware = session.GetFirmware();
            return firmware.MajorVersion >= 2;
        }

        private FirmataSession FindConnection(Func<FirmataSession, bool> isDeviceAvailable, IEnumerable<string> portNames, SerialBaudRate[] baudRates, int timeOut)
        {
            foreach (var portName in portNames.Reverse())
            {
                foreach (var baudRate in baudRates)
                {
                    IDataConnection connection = null;
                    FirmataSession session = null;

                    try
                    {
                        connection = Factory.Create(portName, new SerialConnectionConfiguration() { BaudRate = baudRate });
                        session = new FirmataSession(connection, _logger, timeOut);

                        _logger?.Debug("FindConnection: Checking for Firmata on Port {0}:{1}", portName, (int)baudRate);

                        if (MillisecondsToWaitAfterOpen > 0)
                          Thread.Sleep(MillisecondsToWaitAfterOpen);

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