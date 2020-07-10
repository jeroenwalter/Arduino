using System;
using System.IO.Ports;
using System.Threading;
using Solid.Arduino.Firmata;
using Solid.Arduino.Serial;
using DataReceivedEventHandler = Solid.Arduino.Firmata.DataReceivedEventHandler;
using SerialData = Solid.Arduino.Serial.SerialData;
using SerialDataReceivedEventArgs = Solid.Arduino.Serial.SerialDataReceivedEventArgs;

namespace Solid.Arduino.Core
{
  /// <summary>
  /// Represents a serial port connection using the Microsoft implementation of
  /// SerialPort in System.IO.Ports.
  /// This implementation uses a lot of Tasks and overhead in order to read a single byte.
  /// It's unusable on a Raspberry Pi.
  /// <seealso cref="https://github.com/dotnet/runtime/issues/2379">SerialPort - high CPU usage</seealso>
  /// </summary>
  /// <inheritdoc cref="IDataConnection" />
  public class MicrosoftSerialConnection : SerialPort, ISerialConnection
  {
    #region Fields

    private const int DefaultTimeoutMs = 100;

    private bool _isDisposed;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of <see cref="MicrosoftSerialConnection"/> class on the given serial port and at the given baud rate.
    /// </summary>
    /// <param name="portName">The port name (e.g. 'COM3')</param>
    /// <param name="baudRate">The baud rate</param>
    public MicrosoftSerialConnection(string portName, SerialBaudRate baudRate)
        : base(portName, (int)baudRate)
    {
      ReadTimeout = DefaultTimeoutMs;
      WriteTimeout = DefaultTimeoutMs;

      base.DataReceived += OnSerialPortDataReceived;
    }

    #endregion

    #region Public Methods & Properties

    public new int InfiniteTimeout => SerialPort.InfiniteTimeout;
    public new event DataReceivedEventHandler DataReceived;
    public string Name => PortName;


    /// <inheritdoc cref="SerialPort" />
    public new void Open()
    {
      if (IsOpen)
        return;

      try
      {
        base.Open();

        // My observation on the Raspberry Pi was, that the serial port (Arduino connected on /dev/ttyUSB0) already had data
        // in its buffer, even though the connected Arduino hadn't sent anything.
        // The data in the buffer appeared to be from a previous run of the program.
        // So apparently the buffer is remembered even across consecutive runs of the program and after the serial port is closed.
        // This means that the serial port OS driver is at fault here, as imo it should have cleared 
        // its buffer after a Close or directly when Opened.
        // NOPE: RawDump on Rpi shows that data is correctly received, so no buffers are "remembered".

        BaseStream.Flush();
        DiscardOutBuffer();
        DiscardInBuffer();
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
