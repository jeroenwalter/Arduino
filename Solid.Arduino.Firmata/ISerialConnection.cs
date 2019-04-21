using System;

namespace Solid.Arduino.Firmata
{
  // <summary>Specifies the type of character that was received on the serial port of the <see cref="T:System.IO.Ports.SerialPort" /> object.</summary>
  public enum SerialData
  {
    /// <summary>A character was received and placed in the input buffer.</summary>
    Chars = 1,
    /// <summary>The end of file character was received and placed in the input buffer.</summary>
    Eof = 2,
  }

  /// <summary>Provides data for the <see cref="E:System.IO.Ports.SerialPort.DataReceived" /> event.</summary>
  public class SerialDataReceivedEventArgs : EventArgs
  {
    public SerialDataReceivedEventArgs(SerialData eventCode)
    {
      EventType = eventCode;
    }

    /// <summary>Gets or sets the event type.</summary>
    /// <returns>One of the <see cref="T:System.IO.Ports.SerialData" /> values.</returns>
    public SerialData EventType { get; }
  }

  /// <summary>Represents the method that will handle the <see cref="E:System.IO.Ports.SerialPort.DataReceived" /> event of a <see cref="T:System.IO.Ports.SerialPort" /> object.</summary>
  /// <param name="sender">The sender of the event, which is the <see cref="T:System.IO.Ports.SerialPort" /> object. </param>
  /// <param name="e">A <see cref="T:System.IO.Ports.SerialDataReceivedEventArgs" /> object that contains the event data. </param>
  public delegate void SerialDataReceivedEventHandler(object sender, SerialDataReceivedEventArgs e);


  /// <summary>
  /// Defines a serial port connection.
  /// </summary>
  /// <seealso href="http://arduino.cc/en/Reference/Serial">Serial reference for Arduino</seealso>
  public interface ISerialConnection: IDisposable
  {
        /// <summary>Indicates that no time-out should occur.</summary>
        int InfiniteTimeout { get; }

        /// <summary>
        ///  Represents the method that will handle the data received event of a <see cref="ISerialConnection"/> object.
        /// </summary>
        event SerialDataReceivedEventHandler DataReceived;

        /// <inheritdoc cref="SerialPort.BaudRate"/>
        int BaudRate { get; set; }

        /// <inheritdoc cref="SerialPort.PortName"/>
        string PortName { get; set; }

        /// <summary>
        /// Gets a value indicating the open or closed status of the <see cref="ISerialConnection"/> object.
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Gets or sets the value used to interpret the end of strings received and sent
        /// using <see cref="IStringProtocol.ReadLine"/> and <see cref="IStringProtocol.WriteLine"/> methods.
        /// </summary>
        /// <remarks>
        /// The default is a line feed, (<see cref="Environment.NewLine"/>).
        /// </remarks>
        string NewLine { get; set; }

        /// <summary>
        /// Gets the number of bytes of data in the receive buffer.
        /// </summary>
        int BytesToRead { get; }

        /// <summary>
        /// Opens the connection.
        /// </summary>
        void Open();

        /// <summary>
        /// Closes the connection.
        /// </summary>
        void Close();

        /// <summary>
        /// Reads a byte from the underlying serial input data stream.
        /// </summary>
        /// <returns>A byte value</returns>
        int ReadByte();

        /// <summary>
        /// Writes a string to the serial output data stream.
        /// </summary>
        /// <param name="text">A string to be written</param>
        void Write(string text);

        /// <summary>
        /// Writes a specified number of bytes to the serial output stream using data from a byte array.
        /// </summary>
        /// <param name="buffer">The byte array that contains the data to write</param>
        /// <param name="offset">The zero-based byte offset in the array at which to begin copying bytes</param>
        /// <param name="count">The number of bytes to write</param>
        void Write(byte[] buffer, int offset, int count);

        /// <summary>
        /// Writes the specified string and the <see cref="SerialPort.NewLine"/> value to the serial output stream.
        /// </summary>
        /// <param name="text">The string to write</param>
        void WriteLine(string text);
    }
}
