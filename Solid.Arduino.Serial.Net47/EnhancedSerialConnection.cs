using System;
using System.IO.Ports;
using System.Linq;
using Solid.Arduino.Firmata;
using SerialData = Solid.Arduino.Firmata.SerialData;
using SerialDataReceivedEventArgs = Solid.Arduino.Firmata.SerialDataReceivedEventArgs;
using SerialDataReceivedEventHandler = Solid.Arduino.Firmata.SerialDataReceivedEventHandler;

namespace Solid.Arduino
{
    /// <summary>
    /// Represents a serial port connection, supporting Mono.
    /// </summary>
    /// <seealso href="http://www.mono-project.com/">The official Mono project site</seealso>
    /// <inheritdoc />
    public class EnhancedSerialConnection : EnhancedSerialPort, ISerialConnection
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="EnhancedSerialConnection"/> class using the highest serial port available at 115,200 bits per second.
        /// </summary>
        public EnhancedSerialConnection()
            : this(GetLastPortName(), SerialBaudRate.Bps_115200)
        {
        }

        private void OnDataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
          DataReceived?.Invoke(sender, new SerialDataReceivedEventArgs((SerialData)e.EventType));
        }

        /// <summary>
        /// Initializes a new instance of <see cref="EnhancedSerialConnection"/> class on the given serial port and at the given baud rate.
        /// </summary>
        /// <param name="portName">The port name (e.g. 'COM3')</param>
        /// <param name="baudRate">The baud rate</param>
        public EnhancedSerialConnection(string portName, SerialBaudRate baudRate)
            : base(portName, (int)baudRate)
        {
            ReadTimeout = 100;
            WriteTimeout = 100;
            base.DataReceived += OnDataReceived;
    }

        #endregion

        #region Fields

        #endregion

        #region Public Methods & Properties

        /// <inheritdoc cref="SerialConnection.Find()"/>
        public static ISerialConnection Find()
        {
            return SerialConnection.Find();
        }

        /// <inheritdoc cref="SerialConnection.Find(string, string)"/>
        public static ISerialConnection Find(string query, string expectedReply)
        {
            return SerialConnection.Find(query, expectedReply);
        }

        public new int InfiniteTimeout => SerialPort.InfiniteTimeout;
        public new event SerialDataReceivedEventHandler DataReceived;

        /// <inheritdoc cref="SerialPort.Close"/>
        public new void Close()
        {
            if (IsOpen)
            {
                BaseStream.Flush();
                DiscardInBuffer();
                base.Close();
            }
        }

        #endregion

        #region Private Methods

        private static string GetLastPortName()
        {
            return (from p in SerialPort.GetPortNames()
                            where (p.StartsWith(@"/dev/ttyUSB") || p.StartsWith(@"/dev/ttyAMA") || p.StartsWith(@"/dev/ttyACM") || p.StartsWith("COM"))
                            orderby p descending
                            select p).First();
        }

        #endregion
    }
}
