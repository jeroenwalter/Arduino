//#define DEBUG_OUTPUT
//#define OUTPUT_ALL_BYTES

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Solid.Arduino.Firmata.I2c;

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
  /// var session = new FirmataSession(connection, timeOut: 250);
  /// // Cast to interface done, just for the sake of this demo.
  /// IFirmataProtocol firmata = (IFirmataProtocol)session;
  ///
  /// Firmware firm = firmata.GetFirmware();
  /// Debug.WriteLine("Firmware: {0} {1}.{2}", firm.Name, firm.MajorVersion, firm.MinorVersion);
  ///
  /// ProtocolVersion version = firmata.GetProtocolVersion();
  /// Debug.WriteLine("Protocol version: {0}.{1}", version.Major, version.Minor);
  ///
  /// BoardCapability caps = firmata.GetBoardCapability();
  /// Debug.WriteLine("Board Capabilities:");
  ///
  /// foreach (var pincap in caps.PinCapabilities)
  /// {
  ///    Debug.WriteLine("Pin {0}: Input: {1}, Output: {2}, Analog: {3}, Analog-Res: {4}, PWM: {5}, PWM-Res: {6}, Servo: {7}, Servo-Res: {8}",
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
  /// Debug.WriteLine();
  /// Debug.ReadLine();
  /// </code>
  /// </example>
  public class FirmataSession : IFirmataProtocol, IDisposable
  {
    private readonly ILogger _logger;

#if DEBUG_OUTPUT
    private readonly Stopwatch _stopWatch = new Stopwatch();
#endif

    private const int BufferSize = 2048;
    private readonly bool _gotOpenConnection;

    private int _messageTimeout = -1;
    private Action<byte> _processMessageFunction;
    private int _messageBufferIndex;
    private readonly byte[] _messageBuffer = new byte[BufferSize];

    private readonly object _readLock = new object();

    /// <summary>
    /// Initializes a new instance of the <see cref="FirmataSession"/> class.
    /// </summary>
    /// <param name="connection">The serial port connection</param>
    /// <param name="logger">The logger for diagnostics output</param>
    /// <exception cref="System.ArgumentNullException">connection</exception>
    public FirmataSession(IDataConnection connection, ILogger logger)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      Connection = connection ?? throw new ArgumentNullException(nameof(connection));

      if (!connection.IsOpen)
      {
        _logger.Info("FirmataSession opening connection '{0}'", connection.Name);
        connection.Open();
      }
      else
      {
        _gotOpenConnection = true;
        _logger.Warn("FirmataSession created while connection '{0}' was already open", connection.Name);
      }

      Connection.DataReceived += SerialDataReceived;

#if DEBUG_OUTPUT
      _stopWatch.Start();
#endif
    }

    public bool WaitForStartup(Func<FirmataSession, bool> isDeviceAvailable, int startupTimeoutMs)
    {
      if (_messageTimeout == Connection.InfiniteTimeout)
        throw new InvalidOperationException("message timeout is infinite.");
      var stopwatch = new Stopwatch();
      var success = false;
      stopwatch.Start();
      do
      {
        try
        {
          if (!isDeviceAvailable(this))
            continue;

          success = true;
        }
        catch (TimeoutException)
        {
          _logger.Info($"Timeout while waiting for Firmata startup on port {Connection.Name}");
        }
        
      } while (!success && stopwatch.ElapsedMilliseconds < startupTimeoutMs);

      stopwatch.Stop();
      return success;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FirmataSession"/> class.
    /// </summary>
    /// <param name="connection">The serial port connection</param>
    /// <param name="logger">The logger for diagnostics output</param>
    /// <param name="timeOut">The response time out in milliseconds</param>
    /// <exception cref="System.ArgumentNullException">connection</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">timeOut</exception>
    public FirmataSession(IDataConnection connection, ILogger logger, int timeOut)
        : this(connection, logger)
    {
      if (timeOut < connection.InfiniteTimeout)
        throw new ArgumentOutOfRangeException(nameof(timeOut));

      _messageTimeout = timeOut;
    }

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

    /// <inheritdoc cref="IFirmataProtocol.MessageReceived"/>
    public event MessageReceivedHandler MessageReceived;

    /// <inheritdoc cref="IFirmataProtocol.AnalogStateReceived"/>
    public event AnalogStateReceivedHandler AnalogStateReceived;

    /// <inheritdoc cref="IFirmataProtocol.DigitalStateReceived"/>
    public event DigitalStateReceivedHandler DigitalStateReceived;

    public IObservable<AnalogState> CreateAnalogStateMonitor()
    {
      throw new NotImplementedException();
    }

    public IObservable<AnalogState> CreateAnalogStateMonitor(int channel)
    {
      throw new NotImplementedException();
    }

    public IObservable<DigitalPortState> CreateDigitalStateMonitor()
    {
      throw new NotImplementedException();
    }

    public IObservable<DigitalPortState> CreateDigitalStateMonitor(int port)
    {
      throw new NotImplementedException();
    }

    /// <inheritdoc cref="IFirmataProtocol.ResetBoard"/>
    public void ResetBoard()
    {
      Connection.Write(new[] { (byte)FirmataCommand.SystemReset }, 0, 1);
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
                    (byte)((byte)FirmataCommand.AnalogState | pinNumber),
                    (byte)(value & 0x7F),
                    (byte)((value >> 7) & 0x7F)
                };
        Connection.Write(message, 0, 3);
        return;
      }

      // Send long value in an Extended Analog Message.
      message = new byte[14];
      message[0] = (byte)FirmataCommand.SysExStart;
      message[1] = 0x6F;
      message[2] = (byte)pinNumber;
      var index = 3;

      do
      {
        message[index] = (byte)(value & 0x7F);
        value >>= 7;
        index++;
      } while (value > 0 || index < 5);

      message[index] = (byte)FirmataCommand.SysExEnd;
      Connection.Write(message, 0, index + 1);
    }

    /// <inheritdoc cref="IFirmataProtocol.SetDigitalPin(int,bool)"/>
    public void SetDigitalPin(int pinNumber, bool value)
    {
      if (pinNumber < 0 || pinNumber > 127)
        throw new ArgumentOutOfRangeException(nameof(pinNumber), Messages.ArgumentEx_PinRange0_127);

      Connection.Write(new[] { (byte)FirmataCommand.SetDigitalPin, (byte)pinNumber, (byte)(value ? 1 : 0) }, 0, 3);
    }

    /// <inheritdoc cref="IFirmataProtocol.SetAnalogReportMode"/>
    public void SetAnalogReportMode(int channel, bool enable)
    {
      if (channel < 0 || channel > 15)
        throw new ArgumentOutOfRangeException(nameof(channel), Messages.ArgumentEx_ChannelRange0_15);

      Connection.Write(new[] { (byte)((byte)FirmataCommand.ReportAnalogPin | channel), (byte)(enable ? 1 : 0) }, 0, 2);
    }

    /// <inheritdoc cref="IFirmataProtocol.SetDigitalPort"/>
    public void SetDigitalPort(int portNumber, int pins)
    {
      if (portNumber < 0 || portNumber > 15)
        throw new ArgumentOutOfRangeException(nameof(portNumber), Messages.ArgumentEx_PortRange0_15);

      if (pins < 0 || pins > 0xFF)
        throw new ArgumentOutOfRangeException(nameof(pins), Messages.ArgumentEx_ValueRange0_255);

      Connection.Write(new[] { (byte)((byte)FirmataCommand.DigitalState | portNumber), (byte)(pins & 0x7F), (byte)((pins >> 7) & 0x03) }, 0, 3);
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

      Connection.Write(new byte[] { (byte)FirmataCommand.SetPinMode, (byte)pinNumber, (byte)mode }, 0, 3);
    }

    /// <inheritdoc cref="IFirmataProtocol.SetSamplingInterval"/>
    public void SetSamplingInterval(int milliseconds)
    {
      if (milliseconds < 0 || milliseconds > 0x3FFF)
        throw new ArgumentOutOfRangeException(nameof(milliseconds), Messages.ArgumentEx_SamplingInterval);

      var command = new[]
      {
        (byte) FirmataCommand.SysExStart,
        (byte) SysExCommand.SamplingInterval,
        (byte) (milliseconds & 0x7F),
        (byte) ((milliseconds >> 7) & 0x7F),
        (byte) FirmataCommand.SysExEnd
      };
      Connection.Write(command, 0, 5);
    }

    /// <inheritdoc cref="IFirmataProtocol.SendStringData"/>
    public void SendStringData(string data)
    {
      if (data == null)
        data = string.Empty;

      byte[] command = new byte[data.Length * 2 + 3];
      command[0] = (byte)FirmataCommand.SysExStart;
      command[1] = 0x71;

      for (int x = 0; x < data.Length; x++)
      {
        short c = Convert.ToInt16(data[x]);
        command[x * 2 + 2] = (byte)(c & 0x7F);
        command[x * 2 + 3] = (byte)((c >> 7) & 0x7F);
      }

      command[command.Length - 1] = (byte)FirmataCommand.SysExEnd;

      Connection.Write(command, 0, command.Length);
    }

    /// <inheritdoc cref="IFirmataProtocol.RequestFirmware"/>
    public void RequestFirmware()
    {
#if DEBUG_OUTPUT
      Debug.WriteLine($"{_stopWatch.ElapsedMilliseconds}: RequestFirmware()");
#endif

      SendSysExCommand(SysExCommand.ReportFirmware);
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
#if DEBUG_OUTPUT
      Debug.WriteLine($"{_stopWatch.ElapsedMilliseconds}: RequestProtocolVersion()");
#endif
      Connection.Write(new byte[] { (byte)FirmataCommand.ProtocolVersion }, 0, 1);
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
      SendSysExCommand(SysExCommand.CapabilityQuery);
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
      SendSysExCommand(SysExCommand.AnalogMappingQuery);
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
                (byte)FirmataCommand.SysExStart,
                (byte)SysExCommand.PinStateQuery,
                (byte)pinNumber,
                (byte)FirmataCommand.SysExEnd
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


    public SysEx SendSysExWithReply(SysEx message, Func<SysEx, bool> replyCheck)
      => SendSysExWithReply(message, replyCheck, _messageTimeout);

    public SysEx SendSysExWithReply(SysEx message, Func<SysEx, bool> replyCheck, int? timeoutMs)
    {
      if (replyCheck == null)
        throw new ArgumentNullException(nameof(replyCheck));

      SendSysEx(message);

      bool CheckIfMessageMatches(IFirmataMessage firmataMessage) => (firmataMessage is FirmataMessage<SysEx> sysExMessage) && replyCheck(sysExMessage.Value);

      if (WaitForMessage(CheckIfMessageMatches, timeoutMs ?? _messageTimeout) is FirmataMessage<SysEx> reply)
        return reply.Value;

      throw new TimeoutException(string.Format(Messages.TimeoutEx_WaitMessage, typeof(SysEx).Name));
    }

    public Task<SysEx> SendSysExWithReplyAsync(SysEx message, Func<SysEx, bool> replyCheck)
    {
      throw new NotImplementedException();
    }

    private void SendSysEx(SysExCommand command, byte[] payload)
    {
      if (payload == null || payload.Length == 0)
      {
        SendSysExCommand(command);
        return;
      }

      var message = new byte[3 + payload.Length];
      message[0] = (byte)FirmataCommand.SysExStart;
      message[1] = (byte)command;
      Array.Copy(payload, 0, message, 2, payload.Length);
      message[^1] = (byte)FirmataCommand.SysExEnd;

      Connection.Write(message, 0, message.Length);
    }

    public void SendSysEx(SysEx message)
    {
#if DEBUG_OUTPUT
      var subCommand = message.Command == 1 ? $"{message.Payload[0]:X2}" : string.Empty;
      _logger.Debug($"{_stopWatch.ElapsedMilliseconds}: SendSysEx() {message.Command:X2} {subCommand}");
      Debug.WriteLine($"{_stopWatch.ElapsedMilliseconds}: SendSysEx() {message.Command:X2} {subCommand}");
#endif
      SendSysEx((SysExCommand)message.Command, message.Payload);
    }


    private void AddToMessageBuffer(byte dataByte)
    {
      if (_messageBufferIndex == BufferSize)
        throw new FirmataOverflowException(Messages.OverflowEx_CmdBufferFull);

      _messageBuffer[_messageBufferIndex] = dataByte;
      _messageBufferIndex++;
    }

    private FirmataMessage<T> GetMessageFromQueue<T>()
      where T : struct
    {
      var message = WaitForMessage(firmataMessage => firmataMessage.GetType() == typeof(FirmataMessage<T>), _messageTimeout);
      if (message is FirmataMessage<T> result)
        return result;

      throw new TimeoutException(string.Format(Messages.TimeoutEx_WaitMessage, typeof(T).Name));
    }


    private IFirmataMessage WaitForMessage(Func<IFirmataMessage, bool> messagePredicate, int timeOutInMs)
    {
      if (messagePredicate == null)
        throw new ArgumentNullException(nameof(messagePredicate));

      var messageReceivedEvent = new ManualResetEventSlim(false);
      var message = default(IFirmataMessage);

      void OnMessageReceived(object sender, FirmataMessageEventArgs args)
      {
        if (!messagePredicate.Invoke(args.Value))
        {
          var sysEx = args.Value as FirmataMessage<SysEx>;
          _logger.Error($"Nope message not the right one: { args.Value.Name } command { sysEx?.Value.Command }.");
          return;
        }

        message = args.Value;
        // ReSharper disable once AccessToDisposedClosure
        messageReceivedEvent.Set(); // Doesn't throw even when disposed.
      }

      MessageReceived += OnMessageReceived;
      var success = messageReceivedEvent.Wait(timeOutInMs);
      MessageReceived -= OnMessageReceived;
      messageReceivedEvent.Dispose();
      return success ? message : null;
    }

    private void SendSysExCommand(SysExCommand command)
    {
      var message = new[]
      {
        (byte) FirmataCommand.SysExStart,
        (byte) command,
        (byte) FirmataCommand.SysExEnd
      };
      Connection.Write(message, 0, 3);
    }

    private static bool IsCommandByte(byte serialByte) => (serialByte & 0x80) != 0;

    /// <summary>
    /// Event handler processing data bytes received on the serial port.
    /// </summary>
    private void SerialDataReceived(object sender, DataReceivedEventArgs e)
    {
      lock (_readLock)
      {
        while (Connection.IsOpen && Connection.BytesToRead > 0)
        {
          byte serialByte;

          try
          {
            var data = Connection.ReadByte();
            if (data == -1)
            {
              _logger.Error("SerialDataReceived end of stream reached");
              return;
            }

            serialByte = (byte)data;
          }
          catch (Exception exception)
          {
            // Possible cause: Connection is closed while entering this loop.
            //   This happens when disposing of the FirmataSession while still receiving data.
            // However, there are many more causes for an exception here.
            _logger.Error(exception, string.Empty);
            return;
          }

#if DEBUG_OUTPUT && OUTPUT_ALL_BYTES
          if (_messageBufferIndex > 0 && _messageBufferIndex % 8 == 0)
            Debug.WriteLine(string.Empty);

          Debug.Write($"{serialByte:x2} ");
#endif

          if (_processMessageFunction != null)
          {
            _processMessageFunction(serialByte);
          }
          else
          {
            if (IsCommandByte(serialByte))
              StartMessage(serialByte);
            else
              ProcessOutOfMessageData(serialByte);
          }
        }
      }
    }

    private void ProcessOutOfMessageData(int serialByte)
    {
      //Debug.WriteLine("Out-of-message byte received.");
    }

    private void StartMessage(byte command, Action<byte> processor)
    {
      _messageBuffer[0] = command;
      _messageBufferIndex = 1;
      _processMessageFunction = processor;
    }

    private void ResetMessage()
    {
      _messageBuffer[0] = 0;
      _messageBufferIndex = 0;
      _processMessageFunction = null;
    }

    private void StartMessage(byte serialByte)
    {
      ResetMessage();

      // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
      switch ((FirmataCommand)(serialByte & 0xF0))
      {
        case FirmataCommand.AnalogState:
          StartMessage(serialByte, ProcessAnalogStateMessage);
          return;

        case FirmataCommand.DigitalState:
          StartMessage(serialByte, ProcessDigitalStateMessage);
          return;

        case FirmataCommand.SysExStart:
          // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
          switch ((FirmataCommand)serialByte)
          {
            case FirmataCommand.SysExStart:
              StartMessage(serialByte, ProcessSysExMessage);
              return;

            case FirmataCommand.ProtocolVersion:
              StartMessage(serialByte, ProcessProtocolVersionMessage);
              return;

            default:
              break;
          }
          break;

        default:
          break;
      }

      // Stream is most likely out of sync or the baudrate is incorrect.
      // Don't throw an exception here, as we're in the middle of handling an event and
      // have no way of catching an exception, other than a global unhandled exception handler.
      // Just skip these bytes, until sync is found when a new message starts.
#if DEBUG_OUTPUT
      Debug.WriteLine($"r\n------------------\r\nCommand not supported {serialByte:X2}\r\n\r\n------------------\r\n");
#endif
    }

    private void ProcessAnalogStateMessage(byte messageByte)
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

    private void ProcessDigitalStateMessage(byte messageByte)
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

    private void ProcessProtocolVersionMessage(byte messageByte)
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

    private void ProcessSysExMessage(byte messageByte)
    {
      if (messageByte != (byte)FirmataCommand.SysExEnd)
      {
        AddToMessageBuffer(messageByte);
        return;
      }

      switch ((SysExCommand)_messageBuffer[1])
      {
        case SysExCommand.AnalogMappingResponse:
          DeliverMessage(CreateAnalogMappingResponse());
          return;

        case SysExCommand.CapabilityResponse:
          DeliverMessage(CreateCapabilityResponse());
          return;

        case SysExCommand.PinStateResponse:
          DeliverMessage(CreatePinStateResponse());
          return;

        case SysExCommand.StringData:
          DeliverMessage(CreateStringDataMessage());
          return;

        case SysExCommand.I2CReply:
          DeliverMessage(CreateI2CReply());
          return;

        case SysExCommand.ReportFirmware:
          DeliverMessage(CreateFirmwareResponse());
          return;

        case var n when ((byte)n <= 0x0F): // User-defined command
          DeliverMessage(CreateUserDefinedSysExMessage());
          return;

        default: // Unknown or unsupported message
          DeliverMessage(CreateUnknownSysExMessage());
          return;
      }
    }

    private void DeliverMessage(IFirmataMessage message)
    {
#if DEBUG_OUTPUT
      var sysExCommand = message is FirmataMessage<SysEx> sysEx ? $"{sysEx.Value.Command:X2} {sysEx.Value.Payload[0]:X2}" : string.Empty;
      _logger.Debug($"{_stopWatch.ElapsedMilliseconds}: Received {message.Name} {sysExCommand}");
      Debug.WriteLine($"{_stopWatch.ElapsedMilliseconds}: Received {message.Name} {sysExCommand}");
#endif

      ResetMessage();

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

      //I2CReplyReceived?.Invoke(this, new I2CEventArgs(reply));

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

      Array.Copy(_messageBuffer, 2, payload, 0, payload.Length);

      return new FirmataMessage<SysEx>(new SysEx((byte)_messageBuffer[1], payload));
    }

    private FirmataMessage<SysEx> CreateUnknownSysExMessage()
    {
      _logger.Warn("Unsupported SysEx command {0:X2}", _messageBuffer[1]);
      return CreateUserDefinedSysExMessage();
    }

    public void Dispose()
    {
      Connection.DataReceived -= SerialDataReceived;

      if (!_gotOpenConnection)
        Connection.Close();

      GC.SuppressFinalize(this);
    }
  }
}
