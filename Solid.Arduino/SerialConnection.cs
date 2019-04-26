using System;
using System.IO.Ports;
using System.Threading;
using Solid.Arduino.Firmata;
using Solid.Arduino.Serial;
using DataReceivedEventHandler = Solid.Arduino.Firmata.DataReceivedEventHandler;
using SerialData = Solid.Arduino.Serial.SerialData;
using SerialDataReceivedEventArgs = Solid.Arduino.Serial.SerialDataReceivedEventArgs;

//using DataReceivedEventHandler = Solid.Arduino.Firmata.DataReceivedEventHandler;

namespace Solid.Arduino
{
    /// <summary>
    /// Represents a serial port connection.
    /// </summary>
    /// <inheritdoc cref="IDataConnection" />
    public class SerialConnection : SerialPort, IDataConnection
    {
        #region Fields

        private const int DefaultTimeoutMs = 100;

        private bool _isDisposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="SerialConnection"/> class on the given serial port and at the given baud rate.
        /// </summary>
        /// <param name="portName">The port name (e.g. 'COM3')</param>
        /// <param name="baudRate">The baud rate</param>
        public SerialConnection(string portName, SerialBaudRate baudRate)
            : base(portName, (int)baudRate)
        {
            ReadTimeout = DefaultTimeoutMs;
            WriteTimeout = DefaultTimeoutMs;

            base.DataReceived += OnSerialPortDataReceived;
        }

        #endregion

        #region Public Methods & Properties

        /// <inheritdoc cref="IDataConnection"/>
        public new int InfiniteTimeout => SerialPort.InfiniteTimeout;

        /// <inheritdoc cref="IDataConnection"/>
        public new event DataReceivedEventHandler DataReceived;

        /// <inheritdoc cref="IDataConnection"/>
        public string Name => PortName;


        /// <inheritdoc cref="SerialPort" />
        public new void Open()
        {
            if (IsOpen)
                return;

            try
            {
                base.Open();
            }
            catch (UnauthorizedAccessException)
            {
                // Connection closure has probably not yet been finalized.
                // Wait 250 ms and try again once.
                Thread.Sleep(250);
                base.Open();
            }
        }

        /// <inheritdoc cref="SerialPort.Close"/>
        public new void Close()
        {
            if (!IsOpen)
                return;

            Thread.Sleep(250);
            BaseStream.Flush();
            DiscardInBuffer();
            BaseStream.Close();
            base.Close();
        }

        /// <inheritdoc cref="SerialPort.Dispose"/>
        public new void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            base.DataReceived -= OnSerialPortDataReceived;

            base.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private Methods

        private void OnSerialPortDataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            DataReceived?.Invoke(sender, new SerialDataReceivedEventArgs((SerialData)e.EventType));
        }

        #endregion
    }
}
