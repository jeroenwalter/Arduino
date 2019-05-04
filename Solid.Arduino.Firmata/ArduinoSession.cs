using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Solid.Arduino.Firmata.I2c;
using Solid.Arduino.Firmata.Servo;

namespace Solid.Arduino.Firmata
{
    /// <summary>
    /// Represents an active layer for serial communication with an Arduino board.
    /// </summary>
    /// <remarks>
    /// This class supports a few common protocols used for communicating with Arduino boards.
    /// The protocols can be used simultaneous and independently of each other.
    /// </remarks>
    /// <seealso href="http://arduino.cc">Official Arduino website</seealso>
    /// <seealso href="https://github.com/SolidSoils/Arduino">SolidSoils4Arduino project on GitHub</seealso>
    /// <example>
    /// <code language="C#">
    /// var connection = new SerialConnection("COM3", SerialBaudRate.Bps_57600);
    /// var session = new ArduinoSession(connection, timeOut: 250);
    /// // Cast to interface done, just for the sake of this demo.
    /// IFirmataProtocol firmata = (IFirmataProtocol)session;
    ///
    /// Firmware firm = firmata.GetFirmware();
    /// Console.WriteLine("Firmware: {0} {1}.{2}", firm.Name, firm.MajorVersion, firm.MinorVersion);
    ///
    /// ProtocolVersion version = firmata.GetProtocolVersion();
    /// Console.WriteLine("Protocol version: {0}.{1}", version.Major, version.Minor);
    ///
    /// BoardCapability caps = firmata.GetBoardCapability();
    /// Console.WriteLine("Board Capabilities:");
    ///
    /// foreach (var pincap in caps.PinCapabilities)
    /// {
    ///    Console.WriteLine("Pin {0}: Input: {1}, Output: {2}, Analog: {3}, Analog-Res: {4}, PWM: {5}, PWM-Res: {6}, Servo: {7}, Servo-Res: {8}",
    ///        pincap.PinNumber,
    ///        pincap.DigitalInput,
    ///        pincap.DigitalOutput,
    ///        pincap.Analog,
    ///        pincap.AnalogResolution,
    ///        pincap.Pwm,
    ///        pincap.PwmResolution,
    ///        pincap.Servo,
    ///        pincap.ServoResolution);
    /// }
    /// Console.WriteLine();
    /// Console.ReadLine();
    /// </code>
    /// </example>
    public class ArduinoSession : IFirmataProtocol, IServoProtocol, II2CProtocol, IStringProtocol, IDisposable
    {
        #region Type declarations

        private enum MessageHeader
        {
            AnalogState = 0xE0, // 224
            DigitalState = 0x90, // 144
            SystemExtension = 0xF0,
            ProtocolVersion = 0xF9
        }

        private enum StringReadMode
        {
            ReadLine,
            ReadToTerminator,
            ReadBlock
        }

        private class StringRequest
        {
            public static StringRequest CreateReadLineRequest()
            {
                return new StringRequest(StringReadMode.ReadLine, '\\', 0);
            }

            public static StringRequest CreateReadRequest(int blockLength)
            {
                return new StringRequest(StringReadMode.ReadBlock, '\\', blockLength);
            }

            public static StringRequest CreateReadRequest(char terminator)
            {
                return new StringRequest(StringReadMode.ReadToTerminator, terminator, 0);
            }

            private StringRequest(StringReadMode mode, char terminator, int blockLength)
            {
                Mode = mode;
                BlockLength = blockLength;
                Terminator = terminator;
            }

            public char Terminator { get; }
            public int BlockLength { get; }
            public StringReadMode Mode { get; }
        }

        #endregion

        #region Fields

        private const byte AnalogMessage = 0xE0;
        private const byte DigitalMessage = 0x90;
        private const byte VersionReportHeader = 0xF9;
        private const byte SysExStart = 0xF0;
        private const byte SysExEnd = 0xF7;

        private const int BufferSize = 2048;
        private const int MaxQueueLength = 100;

        private readonly bool _gotOpenConnection;
        private readonly LinkedList<IFirmataMessage> _receivedMessageList = new LinkedList<IFirmataMessage>();
        private readonly Queue<string> _receivedStringQueue = new Queue<string>();
        private ConcurrentQueue<StringRequest> _awaitedStringsQueue = new ConcurrentQueue<StringRequest>();
        private StringRequest _currentStringRequest;

        private int _messageTimeout = -1;
        private Action<int> _processMessageFunction;
        private int _messageBufferIndex, _stringBufferIndex;
        // TODO: make _messageBuffer byte[] instead of int[]
        private readonly int[] _messageBuffer = new int[BufferSize];
        // TODO: remove string messaging from ArduinoSession
        private readonly char[] _stringBuffer = new char[BufferSize];

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ArduinoSession"/> class.
        /// </summary>
        /// <param name="connection">The serial port connection</param>
        /// <exception cref="System.ArgumentNullException">connection</exception>
        public ArduinoSession(IDataConnection connection)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _gotOpenConnection = connection.IsOpen;

            if (!connection.IsOpen)
                connection.Open();

            Connection.DataReceived += SerialDataReceived;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArduinoSession"/> class.
        /// </summary>
        /// <param name="connection">The serial port connection</param>
        /// <param name="timeOut">The response time out in milliseconds</param>
        /// <exception cref="System.ArgumentNullException">connection</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">timeOut</exception>
        public ArduinoSession(IDataConnection connection, int timeOut)
            : this(connection)
        {
            if (timeOut < connection.InfiniteTimeout)
                throw new ArgumentOutOfRangeException(nameof(timeOut));

            _messageTimeout = timeOut;
        }

        #endregion

        #region Public Events, Methods & Properties

        public IDataConnection Connection { get; }

        /// <summary>
        /// Gets or sets the number of milliseconds before a time-out occurs when a read operation does not finish.
        /// </summary>
        /// <remarks>
        /// The default is a <see cref="SerialPort.InfiniteTimeout"/> value (-1).
        /// </remarks>
        public int TimeOut
        {
            get => _messageTimeout;
            set
            {
                if (value < Connection.InfiniteTimeout)
                    throw new ArgumentOutOfRangeException();

                _messageTimeout = value;
            }
        }

        /// <summary>
        /// Closes and reopens the underlying connection and clears all buffers and queues.
        /// </summary>
        public void Clear()
        {
            lock (_receivedMessageList)
            {
                Connection.Close();
                _receivedMessageList.Clear();
                _processMessageFunction = null;
                _awaitedStringsQueue = new ConcurrentQueue<StringRequest>();
                Connection.Open();
            }
        }

        #region IStringProtocol

        /// <inheritdoc cref="IStringProtocol.StringReceived"/>
        public event StringReceivedHandler StringReceived;

        /// <inheritdoc cref="IStringProtocol.CreateReceivedStringMonitor"/>
        public IObservable<string> CreateReceivedStringMonitor()
        {
            return new ReceivedStringTracker(this);
        }

        /// <inheritdoc cref="IStringProtocol.NewLine"/>
        /// <remarks>
        /// The value of this property is mapped to the <see cref="IDataConnection.NewLine"/> property of the
        /// connection the <see cref="ArduinoSession"/> instance is relying on.
        /// </remarks>
        public string NewLine
        {
            get => Connection.NewLine;
            set => Connection.NewLine = value;
        }

        /// <inheritdoc cref="IStringProtocol.Write"/>
        public void Write(string value)
        {
            if (!string.IsNullOrEmpty(value))
                Connection.Write(value);
        }

        /// <inheritdoc cref="IStringProtocol.WriteLine"/>
        public void WriteLine(string value)
        {
            Connection.WriteLine(value);
        }

        /// <inheritdoc cref="IStringProtocol.ReadLine"/>
        public string ReadLine()
        {
            return GetStringFromQueue(StringRequest.CreateReadLineRequest());
        }

        /// <inheritdoc cref="IStringProtocol.ReadLineAsync"/>
        public async Task<string> ReadLineAsync()
        {
            return await Task.Run(() => GetStringFromQueue(StringRequest.CreateReadLineRequest())).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IStringProtocol.Read"/>
        public string Read(int length = 1)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), Messages.ArgumentEx_PositiveValue);

            return GetStringFromQueue(StringRequest.CreateReadRequest(length));
        }

        /// <inheritdoc cref="IStringProtocol.ReadAsync"/>
        public async Task<string> ReadAsync(int length = 1)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), Messages.ArgumentEx_PositiveValue);

            return await Task.Run(() => GetStringFromQueue(StringRequest.CreateReadRequest(length))).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IStringProtocol.ReadTo"/>
        public string ReadTo(char terminator = char.MinValue)
        {
            return GetStringFromQueue(StringRequest.CreateReadRequest(terminator));
        }

        /// <inheritdoc cref="IStringProtocol.ReadToAsync"/>
        public async Task<string> ReadToAsync(char terminator = char.MinValue)
        {
            return await Task.Run(() => GetStringFromQueue(StringRequest.CreateReadRequest(terminator))).ConfigureAwait(false);
        }

        #endregion

        #region IFirmataProtocol

        /// <inheritdoc cref="IFirmataProtocol.MessageReceived"/>
        public event MessageReceivedHandler MessageReceived;
        /// <inheritdoc cref="IFirmataProtocol.AnalogStateReceived"/>
        public event AnalogStateReceivedHandler AnalogStateReceived;
        /// <inheritdoc cref="IFirmataProtocol.DigitalStateReceived"/>
        public event DigitalStateReceivedHandler DigitalStateReceived;

        /// <inheritdoc cref="IFirmataProtocol.CreateDigitalStateMonitor()"/>
        public IObservable<DigitalPortState> CreateDigitalStateMonitor()
        {
            return new DigitalStateTracker(this);
        }

        /// <inheritdoc cref="IFirmataProtocol.CreateDigitalStateMonitor(int)"/>
        public IObservable<DigitalPortState> CreateDigitalStateMonitor(int port)
        {
            if (port < 0 || port > 15)
                throw new ArgumentOutOfRangeException(nameof(port), Messages.ArgumentEx_PortRange0_15);

            return new DigitalStateTracker(this, port);
        }

        /// <inheritdoc cref="IFirmataProtocol.CreateAnalogStateMonitor()"/>
        public IObservable<AnalogState> CreateAnalogStateMonitor()
        {
            return new AnalogStateTracker(this);
        }

        /// <inheritdoc cref="IFirmataProtocol.CreateAnalogStateMonitor(int)"/>
        public IObservable<AnalogState> CreateAnalogStateMonitor(int channel)
        {
            if (channel < 0 || channel > 15)
                throw new ArgumentOutOfRangeException(nameof(channel), Messages.ArgumentEx_ChannelRange0_15);

            return new AnalogStateTracker(this, channel);
        }

        /// <inheritdoc cref="IFirmataProtocol.ResetBoard"/>
        public void ResetBoard()
        {
            Connection.Write(new[] { (byte)0xFF }, 0, 1);
        }

        /// <inheritdoc cref="IFirmataProtocol.SetDigitalPin(int,long)"/>
        public void SetDigitalPin(int pinNumber, long value)
        {
            if (pinNumber < 0 || pinNumber > 127)
                throw new ArgumentOutOfRangeException(nameof(pinNumber), Messages.ArgumentEx_PinRange0_127);

            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), Messages.ArgumentEx_NoNegativeValue);

            byte[] message;

            if (pinNumber < 16 && value < 0x4000)
            {
                // Send value in a conventional Analog Message.
                message = new[] {
                    (byte)(AnalogMessage | pinNumber),
                    (byte)(value & 0x7F),
                    (byte)((value >> 7) & 0x7F)
                };
                Connection.Write(message, 0, 3);
                return;
            }

            // Send long value in an Extended Analog Message.
            message = new byte[14];
            message[0] = SysExStart;
            message[1] = 0x6F;
            message[2] = (byte)pinNumber;
            int index = 3;

            do
            {
                message[index] = (byte)(value & 0x7F);
                value >>= 7;
                index++;
            } while (value > 0 || index < 5);

            message[index] = SysExEnd;
            Connection.Write(message, 0, index + 1);
        }

        /// <inheritdoc cref="IFirmataProtocol.SetDigitalPin(int,bool)"/>
        public void SetDigitalPin(int pinNumber, bool value)
        {
            if (pinNumber < 0 || pinNumber > 127)
                throw new ArgumentOutOfRangeException(nameof(pinNumber), Messages.ArgumentEx_PinRange0_127);

            Connection.Write(new[] { (byte)0xF5, (byte)pinNumber, (byte)(value ? 1 : 0) }, 0, 3);
        }

        /// <inheritdoc cref="IFirmataProtocol.SetAnalogReportMode"/>
        public void SetAnalogReportMode(int channel, bool enable)
        {
            if (channel < 0 || channel > 15)
                throw new ArgumentOutOfRangeException(nameof(channel), Messages.ArgumentEx_ChannelRange0_15);

            Connection.Write(new[] { (byte)(0xC0 | channel), (byte)(enable ? 1 : 0) }, 0, 2);
        }

        /// <inheritdoc cref="IFirmataProtocol.SetDigitalPort"/>
        public void SetDigitalPort(int portNumber, int pins)
        {
            if (portNumber < 0 || portNumber > 15)
                throw new ArgumentOutOfRangeException(nameof(portNumber), Messages.ArgumentEx_PortRange0_15);

            if (pins < 0 || pins > 0xFF)
                throw new ArgumentOutOfRangeException(nameof(pins), Messages.ArgumentEx_ValueRange0_255);

            Connection.Write(new[] { (byte)(DigitalMessage | portNumber), (byte)(pins & 0x7F), (byte)((pins >> 7) & 0x03) }, 0, 3);
        }

        /// <inheritdoc cref="IFirmataProtocol.SetDigitalReportMode"/>
        public void SetDigitalReportMode(int portNumber, bool enable)
        {
            if (portNumber < 0 || portNumber > 15)
                throw new ArgumentOutOfRangeException(nameof(portNumber), Messages.ArgumentEx_PortRange0_15);

            Connection.Write(new[] { (byte)(0xD0 | portNumber), (byte)(enable ? 1 : 0) }, 0, 2);
        }

        /// <inheritdoc cref="IFirmataProtocol.SetDigitalPinMode"/>
        public void SetDigitalPinMode(int pinNumber, PinMode mode)
        {
            if (pinNumber < 0 || pinNumber > 127)
                throw new ArgumentOutOfRangeException(nameof(pinNumber), Messages.ArgumentEx_PinRange0_127);

            Connection.Write(new byte[] { 0xF4, (byte)pinNumber, (byte)mode }, 0, 3);
        }

        /// <inheritdoc cref="IFirmataProtocol.SetSamplingInterval"/>
        public void SetSamplingInterval(int milliseconds)
        {
            if (milliseconds < 0 || milliseconds > 0x3FFF)
                throw new ArgumentOutOfRangeException(nameof(milliseconds), Messages.ArgumentEx_SamplingInterval);

            var command = new[]
            {
                SysExStart,
                (byte)0x7A,
                (byte)(milliseconds & 0x7F),
                (byte)((milliseconds >> 7) & 0x7F),
                SysExEnd
            };
            Connection.Write(command, 0, 5);
        }

        /// <inheritdoc cref="IFirmataProtocol.SendStringData"/>
        public void SendStringData(string data)
        {
            if (data == null)
                data = string.Empty;

            byte[] command = new byte[data.Length * 2 + 3];
            command[0] = SysExStart;
            command[1] = 0x71;

            for (int x = 0; x < data.Length; x++)
            {
                short c = Convert.ToInt16(data[x]);
                command[x * 2 + 2] = (byte)(c & 0x7F);
                command[x * 2 + 3] = (byte)((c >> 7) & 0x7F);
            }

            command[command.Length - 1] = SysExEnd;

            Connection.Write(command, 0, command.Length);
        }

        /// <inheritdoc cref="IFirmataProtocol.RequestFirmware"/>
        public void RequestFirmware()
        {
            SendSysExCommand(0x79);
        }

        /// <inheritdoc cref="IFirmataProtocol.GetFirmware"/>
        public Firmware GetFirmware()
        {
            RequestFirmware();
            return GetMessageFromQueue<Firmware>().Value;
        }

        /// <inheritdoc cref="IFirmataProtocol.GetFirmwareAsync"/>
        public async Task<Firmware> GetFirmwareAsync()
        {
            RequestFirmware();
            return await Task.Run(() => GetMessageFromQueue<Firmware>().Value).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IFirmataProtocol.RequestProtocolVersion"/>
        public void RequestProtocolVersion()
        {
            Connection.Write(new byte[] { 0xF9 }, 0, 1);
        }

        /// <inheritdoc cref="IFirmataProtocol.GetProtocolVersion"/>
        public ProtocolVersion GetProtocolVersion()
        {
            RequestProtocolVersion();
            return GetMessageFromQueue<ProtocolVersion>().Value;
        }

        /// <inheritdoc cref="IFirmataProtocol.GetProtocolVersionAsync"/>
        public async Task<ProtocolVersion> GetProtocolVersionAsync()
        {
            RequestProtocolVersion();
            return await Task.Run(() => GetMessageFromQueue<ProtocolVersion>().Value).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IFirmataProtocol.RequestBoardCapability"/>
        public void RequestBoardCapability()
        {
            SendSysExCommand(0x6B);
        }

        /// <inheritdoc cref="IFirmataProtocol.GetBoardCapability"/>
        public BoardCapability GetBoardCapability()
        {
            RequestBoardCapability();
            return GetMessageFromQueue<BoardCapability>().Value;
        }

        /// <inheritdoc cref="IFirmataProtocol.GetBoardCapabilityAsync"/>
        public async Task<BoardCapability> GetBoardCapabilityAsync()
        {
            RequestBoardCapability();
            return await Task.Run(() => GetMessageFromQueue<BoardCapability>().Value).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IFirmataProtocol.RequestBoardAnalogMapping"/>
        public void RequestBoardAnalogMapping()
        {
            SendSysExCommand(0x69);
        }

        /// <inheritdoc cref="IFirmataProtocol.GetBoardAnalogMapping"/>
        public BoardAnalogMapping GetBoardAnalogMapping()
        {
            RequestBoardAnalogMapping();
            return GetMessageFromQueue<BoardAnalogMapping>().Value;
        }

        /// <inheritdoc cref="IFirmataProtocol.GetBoardAnalogMappingAsync"/>
        public async Task<BoardAnalogMapping> GetBoardAnalogMappingAsync()
        {
            RequestBoardAnalogMapping();
            return await Task.Run(() => GetMessageFromQueue<BoardAnalogMapping>().Value).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IFirmataProtocol.RequestPinState"/>
        public void RequestPinState(int pinNumber)
        {
            if (pinNumber < 0 || pinNumber > 127)
                throw new ArgumentOutOfRangeException(nameof(pinNumber), Messages.ArgumentEx_PinRange0_127);

            var command = new[]
            {
                SysExStart,
                (byte)0x6D,
                (byte)pinNumber,
                SysExEnd
            };
            Connection.Write(command, 0, 4);
        }

        /// <inheritdoc cref="IFirmataProtocol.GetPinState"/>
        public PinState GetPinState(int pinNumber)
        {
            RequestPinState(pinNumber);
            return GetMessageFromQueue<PinState>().Value;
        }

        /// <inheritdoc cref="IFirmataProtocol.GetPinStateAsync"/>
        public async Task<PinState> GetPinStateAsync(int pinNumber)
        {
            RequestPinState(pinNumber);
            return await Task.Run(() => GetMessageFromQueue<PinState>().Value).ConfigureAwait(false);
        }

        #endregion

        #region IServoProtocol

        /// <inheritdoc cref="IServoProtocol.ConfigureServo"/>
        public void ConfigureServo(int pinNumber, int minPulse, int maxPulse)
        {
            if (pinNumber < 0 || pinNumber > 127)
                throw new ArgumentOutOfRangeException(nameof(pinNumber), Messages.ArgumentEx_PinRange0_127);

            if (minPulse < 0 || minPulse > 0x3FFF)
                throw new ArgumentOutOfRangeException(nameof(minPulse), Messages.ArgumentEx_MinPulseWidth);

            if (maxPulse < 0 || maxPulse > 0x3FFF)
                throw new ArgumentOutOfRangeException(nameof(maxPulse), Messages.ArgumentEx_MaxPulseWidth);

            if (minPulse > maxPulse)
                throw new ArgumentException(Messages.ArgumentEx_MinMaxPulse);

            var command = new[]
            {
                SysExStart,
                (byte)0x70,
                (byte)pinNumber,
                (byte)(minPulse & 0x7F),
                (byte)((minPulse >> 7) & 0x7F),
                (byte)(maxPulse & 0x7F),
                (byte)((maxPulse >> 7) & 0x7F),
                SysExEnd
            };
            Connection.Write(command, 0, 8);
        }

        #endregion

        #region II2cProtocol

        /// <inheritdoc cref="II2CProtocol.I2CReplyReceived"/>
        public event I2CReplyReceivedHandler I2CReplyReceived;

        /// <inheritdoc cref="II2CProtocol.CreateI2CReplyMonitor"/>
        public IObservable<I2CReply> CreateI2CReplyMonitor()
        {
            return new I2CReplyTracker(this);
        }

        /// <inheritdoc cref="II2CProtocol.SetI2CReadInterval"/>
        public void SetI2CReadInterval(int microseconds)
        {
            if (microseconds < 0 || microseconds > 0x3FFF)
                throw new ArgumentOutOfRangeException(nameof(microseconds), Messages.ArgumentEx_I2cInterval);

            var command = new[]
            {
                SysExStart,
                (byte)0x78,
                (byte)(microseconds & 0x7F),
                (byte)((microseconds >> 7) & 0x7F),
                SysExEnd
            };
            Connection.Write(command, 0, 5);
        }

        /// <inheritdoc cref="II2CProtocol.WriteI2C"/>
        public void WriteI2C(int slaveAddress, params byte[] data)
        {
            if (slaveAddress < 0 || slaveAddress > 0x3FF)
                throw new ArgumentOutOfRangeException(nameof(slaveAddress), Messages.ArgumentEx_I2cAddressRange);

            byte[] command = new byte[data.Length * 2 + 5];
            command[0] = SysExStart;
            command[1] = 0x76;
            command[2] = (byte)(slaveAddress & 0x7F);
            command[3] = (byte)(slaveAddress < 0x80 ? 0 : ((slaveAddress >> 7) & 0x07) | 0x20);

            for (int x = 0; x < data.Length; x++)
            {
                command[x * 2 + 4] = (byte)(data[x] & 0x7F);
                command[x * 2 + 5] = (byte)((data[x] >> 7) & 0x7F);
            }

            command[command.Length - 1] = SysExEnd;

            Connection.Write(command, 0, command.Length);
        }

        /// <inheritdoc cref="II2CProtocol.ReadI2COnce(int,int)"/>
        public void ReadI2COnce(int slaveAddress, int bytesToRead)
        {
            I2CRead(false, slaveAddress, -1, bytesToRead);
        }

        /// <inheritdoc cref="II2CProtocol.GetI2CReply(int,int)"/>
        public I2CReply GetI2CReply(int slaveAddress, int bytesToRead)
        {
            ReadI2COnce(slaveAddress, bytesToRead);
            
            return GetMessageFromQueue<I2CReply>().Value;
        }

        /// <inheritdoc cref="II2CProtocol.GetI2CReplyAsync(int,int)"/>
        public async Task<I2CReply> GetI2CReplyAsync(int slaveAddress, int bytesToRead)
        {
            ReadI2COnce(slaveAddress, bytesToRead);

            return await Task.Run(() => GetMessageFromQueue<I2CReply>().Value).ConfigureAwait(false);
        }

        /// <inheritdoc cref="II2CProtocol.ReadI2COnce(int,int,int)"/>
        public void ReadI2COnce(int slaveAddress, int slaveRegister, int bytesToRead)
        {
            I2CSlaveRead(false, slaveAddress, slaveRegister, bytesToRead);
        }

        /// <inheritdoc cref="II2CProtocol.GetI2CReply(int,int,int)"/>
        public I2CReply GetI2CReply(int slaveAddress, int slaveRegister, int bytesToRead)
        {
            ReadI2COnce(slaveAddress, slaveRegister, bytesToRead);
            return GetMessageFromQueue<I2CReply>().Value;
        }

        /// <inheritdoc cref="II2CProtocol.GetI2CReplyAsync(int,int,int)"/>
        public async Task<I2CReply> GetI2CReplyAsync(int slaveAddress, int slaveRegister, int bytesToRead)
        {
            ReadI2COnce(slaveAddress, slaveRegister, bytesToRead);
            return await Task.Run(() => GetMessageFromQueue<I2CReply>().Value).ConfigureAwait(false);
        }

        /// <inheritdoc cref="II2CProtocol.ReadI2CContinuous(int,int)"/>
        public void ReadI2CContinuous(int slaveAddress, int bytesToRead)
        {
            I2CRead(true, slaveAddress, -1, bytesToRead);
        }

        /// <inheritdoc cref="II2CProtocol.ReadI2CContinuous(int,int,int)"/>
        public void ReadI2CContinuous(int slaveAddress, int slaveRegister, int bytesToRead)
        {
            I2CSlaveRead(true, slaveAddress, slaveRegister, bytesToRead);
        }

        /// <inheritdoc cref="II2CProtocol.StopI2CReading"/>
        /// <remarks>
        /// <para>
        /// Please note:
        /// The Firmata specification states that the I2C_READ_STOP message
        /// should only stop the specified query. However, the current Firmata.h implementation
        /// stops all registered queries.
        /// </para>
        /// </remarks>
        public void StopI2CReading()
        {
            byte[] command = new byte[5];
            command[0] = SysExStart;
            command[1] = 0x76;
            command[2] = 0x00;
            command[3] = 0x18;
            command[4] = SysExEnd;

            Connection.Write(command, 0, command.Length);
        }


        public SysEx SendSysExWithReply(SysEx message, Func<SysEx, bool> replyCheck)
        {
            if (replyCheck == null)
                throw new ArgumentNullException(nameof(replyCheck));

            SendSysEx(message);

            bool CheckIfMessageMatches(IFirmataMessage firmataMessage) => (firmataMessage is FirmataMessage<SysEx> sysExMessage) && replyCheck(sysExMessage.Value);

            if (WaitForMessageFromQueue(CheckIfMessageMatches, _messageTimeout) is FirmataMessage<SysEx> reply)
                return reply.Value;

            throw new TimeoutException(string.Format(Messages.TimeoutEx_WaitMessage, typeof(SysEx).Name));
        }

        public Task<SysEx> SendSysExWithReplyAsync(SysEx message, Func<SysEx, bool> replyCheck)
        {
            SendSysEx(message);

            //_awaitedMessagesQueue.Enqueue(new FirmataMessage(MessageType.UserDefinedSysEx));

            //return await Task.Run(() =>
            //    (I2CReply)((FirmataMessage)GetMessageFromQueue(new FirmataMessage(MessageType.I2CReply))).Value);

            throw new NotImplementedException();
        }

        private void SendSysEx(byte command, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                SendSysExCommand(command);
                return;
            }

            var message = new byte[3 + payload.Length];
            message[0] = SysExStart;
            message[1] = command;
            Array.Copy(payload, 0, message, 2, payload.Length);
            message[message.Length - 1] = SysExEnd;

            Connection.Write(message, 0, message.Length);
        }

        public void SendSysEx(SysEx message)
        {
            SendSysEx(message.Command, message.Payload);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_gotOpenConnection)
                Connection.Close();

            GC.SuppressFinalize(this);
        }

        #endregion

        #endregion

        #region Private Methods

        private void AddToMessageBuffer(int dataByte)
        {
            if (_messageBufferIndex == BufferSize)
                throw new OverflowException(Messages.OverflowEx_CmdBufferFull);

            _messageBuffer[_messageBufferIndex] = dataByte;
            _messageBufferIndex++;
        }

        private string GetStringFromQueue(StringRequest request)
        {
            _awaitedStringsQueue.Enqueue(request);
            bool lockTaken = false;

            try
            {
                Monitor.TryEnter(_receivedStringQueue, _messageTimeout, ref lockTaken);

                while (lockTaken)
                {
                    if (_receivedStringQueue.Count > 0)
                    {
                        string message = _receivedStringQueue.Dequeue();
                        Monitor.PulseAll(_receivedStringQueue);
                        return message;
                    }

                    lockTaken = Monitor.Wait(_receivedStringQueue, _messageTimeout);
                }

                throw new TimeoutException(string.Format(Messages.TimeoutEx_WaitStringRequest, request.Mode));
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(_receivedStringQueue);
                }
            }
        }

        private FirmataMessage<T> GetMessageFromQueue<T>()
        where T : struct
        {
            var message = WaitForMessageFromQueue(firmataMessage => firmataMessage.GetType() == typeof(FirmataMessage<T>), _messageTimeout);
            if (message is FirmataMessage<T> result)
                return result;

            throw new TimeoutException(string.Format(Messages.TimeoutEx_WaitMessage, typeof(T).Name));
        }

        private IFirmataMessage WaitForMessageFromQueue(Func<IFirmataMessage, bool> messagePredicate, int timeOutInMs)
        {
            bool lockTaken = false;

            try
            {
                Monitor.TryEnter(_receivedMessageList, timeOutInMs, ref lockTaken);

                while (lockTaken)
                {
                    if (_receivedMessageList.Count > 0)
                    {
                        var message = (from firmataMessage in _receivedMessageList
                                       where messagePredicate(firmataMessage)
                                       select firmataMessage).FirstOrDefault();

                        if (message != null)
                        {
                            _receivedMessageList.Remove(message);
                            Monitor.PulseAll(_receivedMessageList);
                            return message;
                        }
                    }

                    lockTaken = Monitor.Wait(_receivedMessageList, timeOutInMs);
                }

                return null;
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(_receivedMessageList);
                }
            }
        }

        private void SendSysExCommand(byte command)
        {
            var message = new[]
            {
                SysExStart,
                command,
                SysExEnd
            };
            Connection.Write(message, 0, 3);
        }

        private void I2CSlaveRead(bool continuous, int slaveAddress, int slaveRegister = -1, int bytesToRead = 0)
        {
            if (slaveRegister < 0 || slaveRegister > 0x3FFF)
                throw new ArgumentOutOfRangeException(nameof(slaveRegister), Messages.ArgumentEx_ValueRange0_16383);

            I2CRead(continuous, slaveAddress, slaveRegister, bytesToRead);
        }

        private void I2CRead(bool continuous, int slaveAddress, int slaveRegister = -1, int bytesToRead = 0)
        {
            if (slaveAddress < 0 || slaveAddress > 0x3FF)
                throw new ArgumentOutOfRangeException(nameof(slaveAddress), Messages.ArgumentEx_I2cAddressRange);

            if (bytesToRead < 0 || bytesToRead > 0x3FFF)
                throw new ArgumentOutOfRangeException(nameof(bytesToRead), Messages.ArgumentEx_ValueRange0_16383);

            byte[] command = new byte[(slaveRegister == -1 ? 7 : 9)];
            command[0] = SysExStart;
            command[1] = 0x76;
            command[2] = (byte)(slaveAddress & 0x7F);
            command[3] = (byte)(((slaveAddress >> 7) & 0x07) | (slaveAddress < 128 ? (continuous ? 0x10 : 0x08) : (continuous ? 0x30 : 0x28)));

            if (slaveRegister != -1)
            {
                command[4] = (byte)(slaveRegister & 0x7F);
                command[5] = (byte)(slaveRegister >> 7);
                command[6] = (byte)(bytesToRead & 0x7F);
                command[7] = (byte)(bytesToRead >> 7);
            }
            else
            {
                command[4] = (byte)(bytesToRead & 0x7F);
                command[5] = (byte)(bytesToRead >> 7);
            }

            command[command.Length - 1] = SysExEnd;

            Connection.Write(command, 0, command.Length);
        }

        /// <summary>
        /// Event handler processing data bytes received on the serial port.
        /// </summary>
        private void SerialDataReceived(object sender, DataReceivedEventArgs e)
        {
            while (Connection.IsOpen && Connection.BytesToRead > 0)
            {
                int serialByte = 0;

                try
                {
                    serialByte = Connection.ReadByte();
                }
                catch (Exception exception)
                {
                    // Connection is closed while entering this loop.
                    // This happens when disposing of the ArduinoSession while still receiving data.
                    Debug.WriteLine(exception);
                    return;
                }

                /*
                #if DEBUG
                                if (_messageBufferIndex > 0 && _messageBufferIndex % 8 == 0)
                                    Debug.WriteLine(string.Empty);

                                Debug.Write(string.Format("{0:x2} ", serialByte));
                #endif
                */
                if (_processMessageFunction != null)
                {
                    _processMessageFunction(serialByte);
                    /*
                    #if DEBUG
                                      if (_processMessageFunction == null)
                                        Debug.WriteLine(string.Empty);
                    #endif
                    */
                }
                else
                {
                    if ((serialByte & 0x80) != 0)
                    {
                        // Process Firmata command byte.
                        ProcessCommand(serialByte);
                    }
                    else
                    {
                        // Process ASCII character.
                        ProcessAsciiString(serialByte);
                    }
                }
            }
        }

        private void ProcessAsciiString(int serialByte)
        {
            if (_stringBufferIndex == BufferSize)
                throw new OverflowException(Messages.OverflowEx_StringBufferFull);

            char c = Convert.ToChar(serialByte);
            _stringBuffer[_stringBufferIndex] = c;
            _stringBufferIndex++;

            if (_currentStringRequest == null)
            {
                if (!_awaitedStringsQueue.TryDequeue(out _currentStringRequest))
                {
                    // No pending Read/ReadLine/ReadTo requests.
                    // Handle StringReceived event.
                    if (c == Connection.NewLine[Connection.NewLine.Length - 1]
                        || serialByte == 0x1A
                        || serialByte == 0x00) // NewLine, EOF or terminating 0-byte?
                    {
                        if (StringReceived != null)
                            StringReceived(this, new StringEventArgs(new string(_stringBuffer, 0, _stringBufferIndex - 1)));

                        _stringBufferIndex = 0;
                    }
                    return;
                }
            }

            switch (_currentStringRequest.Mode)
            {
                case StringReadMode.ReadLine:
                    if (c == Connection.NewLine[0] || serialByte == 0x1A)
                        EnqueueReceivedString(new string(_stringBuffer, 0, _stringBufferIndex - 1));
                    else if (c == '\n') // Ignore linefeed, just in case cr+lf pair was expected.
                        _stringBufferIndex--;
                    break;

                case StringReadMode.ReadBlock:
                    if (_stringBufferIndex == _currentStringRequest.BlockLength)
                        EnqueueReceivedString(new string(_stringBuffer, 0, _stringBufferIndex));
                    break;

                case StringReadMode.ReadToTerminator:
                    if (c == _currentStringRequest.Terminator)
                        EnqueueReceivedString(new string(_stringBuffer, 0, _stringBufferIndex - 1));
                    break;
            }
        }

        private void EnqueueReceivedString(string value)
        {
            bool lockTaken = false;

            try
            {
                Monitor.TryEnter(_receivedStringQueue, _messageTimeout, ref lockTaken);

                if (!lockTaken)
                    throw new TimeoutException();

                if (_receivedStringQueue.Count >= MaxQueueLength)
                    throw new OverflowException(Messages.OverflowEx_StringBufferFull);

                _receivedStringQueue.Enqueue(value);
                Monitor.PulseAll(_receivedStringQueue);
                _currentStringRequest = null;
                _stringBufferIndex = 0;
            }
            finally
            {
                if (lockTaken)
                    Monitor.Exit(_receivedStringQueue);
            }
        }

        private void ProcessCommand(int serialByte)
        {
            _messageBuffer[0] = serialByte;
            _messageBufferIndex = 1;
            MessageHeader header = (MessageHeader)(serialByte & 0xF0);

            switch (header)
            {
                case MessageHeader.AnalogState:
                    _processMessageFunction = ProcessAnalogStateMessage;
                    break;

                case MessageHeader.DigitalState:
                    _processMessageFunction = ProcessDigitalStateMessage;
                    break;

                case MessageHeader.SystemExtension:
                    header = (MessageHeader)serialByte;

                    switch (header)
                    {
                        case MessageHeader.SystemExtension:
                            _processMessageFunction = ProcessSysExMessage;
                            break;

                        case MessageHeader.ProtocolVersion:
                            _processMessageFunction = ProcessProtocolVersionMessage;
                            break;

                        //case MessageHeader.SetPinMode:
                        //case MessageHeader.SystemReset:
                        default:
                            // 0xF? command not supported.
                            //throw new NotImplementedException(string.Format(Messages.NotImplementedEx_Command, serialByte));

                            // Stream is most likely out of sync or the baudrate is incorrect.
                            // Don't throw an exception here, as we're in the middle of handling an event and
                            // have no way of catching an exception, other than a global unhandled exception handler.
                            // Just skip these bytes, until sync is found when a new message starts.
                            return;
                    }
                    break;

                default:
                    // Command not supported.
                    //throw new NotImplementedException(string.Format(Messages.NotImplementedEx_Command, serialByte));

                    // Stream is most likely out of sync or the baudrate is incorrect.
                    // Don't throw an exception here, as we're in the middle of handling an event from the serial port and
                    // have no way of catching an exception, other than a global unhandled exception handler.
                    // Just skip these bytes, until sync is found when a new message starts.
                    return;
            }
        }

        private void ProcessAnalogStateMessage(int messageByte)
        {
            if (_messageBufferIndex < 2)
            {
                AddToMessageBuffer(messageByte);
            }
            else
            {
                var currentState = new AnalogState
                {
                    Channel = _messageBuffer[0] & 0x0F,
                    Level = (_messageBuffer[1] | (messageByte << 7))
                };
                _processMessageFunction = null;

                MessageReceived?.Invoke(this, new FirmataMessageEventArgs(new FirmataMessage<AnalogState>(currentState)));

                AnalogStateReceived?.Invoke(this, new FirmataEventArgs<AnalogState>(currentState));
            }
        }

        private void ProcessDigitalStateMessage(int messageByte)
        {
            if (_messageBufferIndex < 2)
            {
                AddToMessageBuffer(messageByte);
            }
            else
            {
                var currentState = new DigitalPortState
                {
                    Port = _messageBuffer[0] & 0x0F,
                    Pins = _messageBuffer[1] | (messageByte << 7)
                };
                _processMessageFunction = null;

                MessageReceived?.Invoke(this, new FirmataMessageEventArgs(new FirmataMessage<DigitalPortState>(currentState)));

                DigitalStateReceived?.Invoke(this, new FirmataEventArgs<DigitalPortState>(currentState));
            }
        }

        private void ProcessProtocolVersionMessage(int messageByte)
        {
            if (_messageBufferIndex < 2)
            {
                AddToMessageBuffer(messageByte);
            }
            else
            {
                DeliverMessage(new FirmataMessage<ProtocolVersion>(new ProtocolVersion
                {
                    Major = _messageBuffer[1],
                    Minor = messageByte
                }));
            }
        }

        private void ProcessSysExMessage(int messageByte)
        {
            if (messageByte != SysExEnd)
            {
                AddToMessageBuffer(messageByte);
                return;
            }

            // Check if someone is waiting for this message.

            switch (_messageBuffer[1])
            {
                case 0x6A: // AnalogMappingResponse
                    DeliverMessage(CreateAnalogMappingResponse());
                    return;

                case 0x6C: // CapabilityResponse
                    DeliverMessage(CreateCapabilityResponse());
                    return;

                case 0x6E: // PinStateResponse
                    DeliverMessage(CreatePinStateResponse());
                    return;

                case 0x71: // StringData
                    DeliverMessage(CreateStringDataMessage());
                    return;

                case 0x77: // I2cReply
                    DeliverMessage(CreateI2CReply());
                    return;

                case 0x79: // FirmwareResponse
                    DeliverMessage(CreateFirmwareResponse());
                    return;

                case int n when (n >= 0x01 && n <= 0x0F): // User-defined command
                    DeliverMessage(CreateUserDefinedSysExMessage());
                    return;

                default: // Unknown or unsupported message
                    throw new NotImplementedException();

            }
        }

        private void DeliverMessage(IFirmataMessage message)
        {
            _processMessageFunction = null;

            lock (_receivedMessageList)
            {
                if (_receivedMessageList.Count >= MaxQueueLength)
                    throw new OverflowException(Messages.OverflowEx_MsgBufferFull);

                // Remove all unprocessed and timed-out messages.
                while (_receivedMessageList.Count > 0 &&
                    ((DateTime.UtcNow - _receivedMessageList.First.Value.Time).TotalMilliseconds > TimeOut))
                {
                    _receivedMessageList.RemoveFirst();
                }

                _receivedMessageList.AddLast(message);
                Monitor.PulseAll(_receivedMessageList);
            }

            if (message.GetType() != typeof(FirmataMessage<I2CReply>))
                MessageReceived?.Invoke(this, new FirmataMessageEventArgs(message));
        }

        private FirmataMessage<I2CReply> CreateI2CReply()
        {
            var reply = new I2CReply
            {
                Address = _messageBuffer[2] | (_messageBuffer[3] << 7),
                Register = _messageBuffer[4] | (_messageBuffer[5] << 7)
            };

            var data = new byte[(_messageBufferIndex - 5) / 2];

            for (int x = 0; x < data.Length; x++)
            {
                data[x] = (byte)(_messageBuffer[x * 2 + 6] | _messageBuffer[x * 2 + 7] << 7);
            }

            reply.Data = data;

            I2CReplyReceived?.Invoke(this, new I2CEventArgs(reply));

            return new FirmataMessage<I2CReply>(reply);
        }

        private FirmataMessage<PinState> CreatePinStateResponse()
        {
            if (_messageBufferIndex < 5)
                throw new InvalidOperationException(Messages.InvalidOpEx_PinNotSupported);

            int value = 0;

            for (int x = _messageBufferIndex - 1; x > 3; x--)
                value = (value << 7) | _messageBuffer[x];

            return new FirmataMessage<PinState>(new PinState
            {
                PinNumber = _messageBuffer[2],
                Mode = (PinMode)_messageBuffer[3],
                Value = value
            });
        }

        private FirmataMessage<BoardAnalogMapping> CreateAnalogMappingResponse()
        {
            var pins = new List<AnalogPinMapping>(8);

            for (int x = 2; x < _messageBufferIndex; x++)
            {
                if (_messageBuffer[x] != 0x7F)
                {
                    pins.Add
                    (
                        new AnalogPinMapping
                        {
                            PinNumber = x - 2,
                            Channel = _messageBuffer[x]
                        }
                    );
                }
            }

            return new FirmataMessage<BoardAnalogMapping>(new BoardAnalogMapping { PinMappings = pins.ToArray() });
        }

        private FirmataMessage<BoardCapability> CreateCapabilityResponse()
        {
            var pins = new List<PinCapability>(12);
            int pinIndex = 0;
            int x = 2;

            while (x < _messageBufferIndex)
            {
                if (_messageBuffer[x] != 127)
                {
                    var capability = new PinCapability { PinNumber = pinIndex };

                    while (x < _messageBufferIndex && _messageBuffer[x] != 127)
                    {
                        PinMode pinMode = (PinMode)_messageBuffer[x];
                        bool isCapable = (_messageBuffer[x + 1] != 0);

                        switch (pinMode)
                        {
                            case PinMode.AnalogInput:
                                capability.Analog = true;
                                capability.AnalogResolution = _messageBuffer[x + 1];
                                break;

                            case PinMode.DigitalInput:
                                capability.DigitalInput = true;
                                break;

                            case PinMode.DigitalOutput:
                                capability.DigitalOutput = true;
                                break;

                            case PinMode.PwmOutput:
                                capability.Pwm = true;
                                capability.PwmResolution = _messageBuffer[x + 1];
                                break;

                            case PinMode.ServoControl:
                                capability.Servo = true;
                                capability.ServoResolution = _messageBuffer[x + 1];
                                break;

                            case PinMode.I2C:
                                capability.I2C = true;
                                break;

                            case PinMode.OneWire:
                                capability.OneWire = true;
                                break;

                            case PinMode.StepperControl:
                                capability.StepperControl = true;
                                capability.MaxStepNumber = _messageBuffer[x + 1];
                                break;

                            case PinMode.Encoder:
                                capability.Encoder = true;
                                break;

                            case PinMode.Serial:
                                capability.Serial = true;
                                break;

                            case PinMode.InputPullup:
                                capability.InputPullup = true;
                                break;

                            default:
                                throw new NotImplementedException();
                        }

                        x += 2;
                    }

                    pins.Add(capability);
                }

                pinIndex++;
                x++;
            }

            return new FirmataMessage<BoardCapability>(new BoardCapability { Pins = pins.ToArray() });
        }

        private FirmataMessage<StringData> CreateStringDataMessage()
        {
            var builder = new StringBuilder(_messageBufferIndex >> 1);

            for (int x = 2; x < _messageBufferIndex; x += 2)
            {
                builder.Append((char)(_messageBuffer[x] | (_messageBuffer[x + 1] << 7)));
            }

            return new FirmataMessage<StringData>(new StringData
            {
                Text = builder.ToString()
            });
        }

        private FirmataMessage<Firmware> CreateFirmwareResponse()
        {
            var builder = new StringBuilder(_messageBufferIndex);

            for (int x = 4; x < _messageBufferIndex; x += 2)
                builder.Append((char)(_messageBuffer[x] | (_messageBuffer[x + 1] << 7)));

            return new FirmataMessage<Firmware>(new Firmware
            {
                MajorVersion = _messageBuffer[2],
                MinorVersion = _messageBuffer[3],
                Name = builder.ToString()
            });
        }


        private FirmataMessage<SysEx> CreateUserDefinedSysExMessage()
        {
            var payload = new byte[_messageBufferIndex - 2];
            for (var i = 2; i < _messageBufferIndex; i++)
                payload[i - 2] = (byte)_messageBuffer[i];
            // TODO: make _messageBuffer byte[] instead of int[]
            //Array.Copy(_messageBuffer, 2, payload, 0, payload.Length);

            return new FirmataMessage<SysEx>(new SysEx((byte)_messageBuffer[1], payload));
        }

        #endregion

    }
}
