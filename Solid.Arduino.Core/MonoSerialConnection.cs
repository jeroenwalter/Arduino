using System;
using System.IO.Ports.Mono;
using System.Threading;
using Solid.Arduino.Core;
using Solid.Arduino.Firmata;
using Solid.Arduino.Serial;

namespace Solid.Arduino
{
  /// <summary>
  /// Represents a serial port connection using System.IO.Ports.Mono.
  /// Has less overhead than the Microsoft implementation and is actually
  /// usable on the Raspberry Pi.
  /// As a bonus, it can also be used on Windows.
  /// </summary>
  public class MonoSerialConnection : SerialPort, ISerialConnection
  {
    private const int DefaultTimeoutMs = 100;

    /// <summary>
    /// Initializes a new instance of <see cref="MonoSerialConnection"/> class on
    /// the given serial port and at the given baud rate.
    /// </summary>
    /// <param name="portName">The port name (e.g. 'COM3')</param>
    /// <param name="baudRate">The baud rate</param>
    public MonoSerialConnection(string portName, SerialBaudRate baudRate)
        : base(portName, (int)baudRate)
    {
      ReadTimeout = DefaultTimeoutMs;
      WriteTimeout = DefaultTimeoutMs;

      base.DataReceived += OnSerialPortDataReceived;
    }

    #region Public Methods & Properties

    public int InfiniteTimeout => -1;

    public event DataReceivedEventHandler DataReceived;

    public string Name => PortName;
    
    /// <inheritdoc cref="SerialPort.Close"/>
    public new void Close()
    {
      if (!IsOpen) 
        return;

      BaseStream.Flush();
      DiscardInBuffer();
      base.Close();
    }

    private void OnSerialPortDataReceived(object sender, System.IO.Ports.Mono.SerialDataReceivedEventArgs e)
    {
      DataReceived?.Invoke(sender, new Serial.SerialDataReceivedEventArgs((Serial.SerialData)e.EventType));
    }

    #endregion
  }
}
