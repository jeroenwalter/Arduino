using System;
using System.Diagnostics;
using System.IO.Ports.Mono;
using System.Linq;
using System.Threading;
using Solid.Arduino.Firmata;
using Solid.Arduino.Firmata.I2c;
using Solid.Arduino.Serial;
using SerialDataReceivedEventArgs = System.IO.Ports.Mono.SerialDataReceivedEventArgs;

namespace Solid.Arduino.Core.Run
{
  /// <summary>
  ///     Load the sketch TestStandardFirmata.ino in the Arduino to run this test.
  /// </summary>
  internal class Program
  {
    private static readonly IDataConnectionFactory _serialConnectionFactory = new SerialConnectionFactory();
    private static readonly ILogger _logger = new ConsoleLogger();

    private const byte UserDefinedSysExCommandStart = 0;
    private const byte EchoPayloadCommand = UserDefinedSysExCommandStart;
    private const byte SendCounterCommand = UserDefinedSysExCommandStart + 1;

    private static bool IsWindows => Environment.OSVersion.Platform == PlatformID.Win32Windows || Environment.OSVersion.Platform == PlatformID.Win32NT;
    private static bool HasConsoleInput => !Console.IsInputRedirected;

    private static void WaitForKeyIfPossible()
    {
      if (!HasConsoleInput) return;

      Console.WriteLine("Press a key to continue.");
      Console.ReadKey(true);
    }

    private static string GetPortName()
    {
      string[] portNames = SerialPort.GetPortNames();
      var portName = string.Empty;

      if (HasConsoleInput)
      {
        do
        {
          for (var portNameIndex = 0; portNameIndex < portNames.Length; portNameIndex++)
            Console.WriteLine($"{portNameIndex + 1}: {portNames[portNameIndex]}");
          Console.WriteLine("Choose port to test:");
          var port = Console.ReadKey(false).KeyChar;
          var success = int.TryParse(port.ToString(), out var index);
          index--;
          if (!success || index < 0 || index >= portNames.Length)
            Console.WriteLine("Invalid port");
          else
            portName = portNames[index];
        } while (portName == string.Empty);
      }
      else
        portName = portNames.Last();

      return portName;
    }

    private static void Main(string[] args)
    {
      var option = '3';
      var port = IsWindows ? "COM6" : "/dev/ttyUSB0";
      //var port = "COM7";
      //var port = "/dev/ttyUSB0";
      //var port = "/dev/ttyACM0";

      port = GetPortName();

      if (HasConsoleInput)
      {
        Console.WriteLine("--------------------------------------");
        Console.WriteLine("1: RawDump");
        Console.WriteLine("2: RawDumpViaReceivedEvent");
        Console.WriteLine("3: Test some messages");
        Console.WriteLine("4: Firmata Test");
        Console.WriteLine("Choose one of the above options (1..4)");
        option = Console.ReadKey(false).KeyChar;
        Console.WriteLine();
        Console.WriteLine();
      }

      switch (option)
      {
        case '1':
          RawDump(port);
          break;
        case '2':
          RawDumpViaEvent(port);
          break;
        case '3':
          TestSomeMessages(port);
          break;
        case '4':
        default:
          TestFirmata(port);
          break;
      }

      Console.WriteLine();
      WaitForKeyIfPossible();
    }

    private static void RawDump(string port)
    {
      Console.WriteLine("\r\n\r\n--------------------------------------\r\nRawDump\r\n");
      using var serial = new SerialPort(port, 57600) { ReadTimeout = 1000, WriteTimeout = 1000 };

      var count = 0;
      var stopwatch = new Stopwatch();
      stopwatch.Start();

      serial.Open();
      Console.WriteLine($"Serial port '{port}' opened, waiting 5s for Arduino startup...");
      Thread.Sleep(5000);

      while (serial.IsOpen && stopwatch.ElapsedMilliseconds < 6000)
      {
        try
        {
          var serialByte = serial.ReadByte();
          Console.Write($"{serialByte:X2} ");
          if (++count % 8 == 0)
            Console.WriteLine();
        }
        catch (TimeoutException)
        {
          Console.WriteLine("Timeout");
        }
        catch (Exception exception)
        {
          // Connection is closed while entering this loop.
          // This happens when disposing of the ArduinoSession while still receiving data.
          Console.WriteLine(exception);
          return;
        }
      }

      serial.Close();
    }

    private static void RawDumpViaEvent(string port)
    {
      Console.WriteLine("\r\n\r\n--------------------------------------\r\nRawDumpViaEvent\r\n");

      using var serial = new SerialPort(port, 57600) { ReadTimeout = 1000, WriteTimeout = 1000 };

      var count = 0;

      void OnSerialOnDataReceived(object sender, SerialDataReceivedEventArgs args)
      {
        while (serial.IsOpen && serial.BytesToRead > 0)
        {
          try
          {
            var serialByte = serial.ReadByte();
            Console.Write($"{serialByte:X2} ");
            if (++count % 8 == 0) Console.WriteLine();
          }
          catch (TimeoutException)
          {
            Console.WriteLine("Timeout while data received????");
          }
          catch (Exception exception)
          {
            // Connection is closed while entering this loop.
            // This happens when disposing of the ArduinoSession while still receiving data.
            Console.WriteLine(exception);
            return;
          }
        }
      }

      serial.DataReceived += OnSerialOnDataReceived;
      serial.Open();
      Console.WriteLine($"Serial port '{port}' opened, waiting 5s for Arduino startup...");
      Thread.Sleep(5000);
      serial.DataReceived -= OnSerialOnDataReceived;
      serial.Close();
    }


    private static bool RetryIfTimeout(Action action, int retries = 3)
    {
      var count = 0;
      while (count++ < retries)
      {
        try
        {
          action();
          return true;
        }
        catch (Exception e)
        {
          Console.WriteLine(e);
        }

        Console.WriteLine($"retry : {count}");
      }

      return false;
    }

    private static void TestSomeMessages(string port)
    {
      using FirmataSession firmata = GetFirmataSession();
      firmata.MessageReceived += OnFirmataMessageReceived;
      bool success;
      bool mustStop = false;
      while (!mustStop)
      {
        success = RetryIfTimeout(() =>
        {
          var firm = firmata.GetFirmware();
          Console.WriteLine("Firmware: {0} {1}.{2}", firm.Name, firm.MajorVersion, firm.MinorVersion);
        });

        success = RetryIfTimeout(() =>
        {
          var version = firmata.GetProtocolVersion();
          Console.WriteLine("Protocol version: {0}.{1}", version.Major, version.Minor);
        });

        success = RetryIfTimeout(() =>
        {
          var analogMapping = firmata.GetBoardAnalogMapping();
          Console.WriteLine("Analog channel mappings:");
          foreach (var mapping in analogMapping.PinMappings)
          {
            Console.WriteLine("Channel {0} is mapped to pin {1}.", mapping.Channel, mapping.PinNumber);
          }
        });

        success = RetryIfTimeout(() =>
        {

          BoardCapability cap = firmata.GetBoardCapability();
          Console.WriteLine();
          Console.WriteLine("Board Capability:");

          foreach (var pin in cap.Pins)
          {
            Console.WriteLine("Pin {0}: Input: {1}, Output: {2}, Analog: {3}, Analog-Res: {4}, PWM: {5}, PWM-Res: {6}, Servo: {7}, Servo-Res: {8}, Serial: {9}, Encoder: {10}, Input-pullup: {11}",
              pin.PinNumber,
              pin.DigitalInput,
              pin.DigitalOutput,
              pin.Analog,
              pin.AnalogResolution,
              pin.Pwm,
              pin.PwmResolution,
              pin.Servo,
              pin.ServoResolution,
              pin.Serial,
              pin.Encoder,
              pin.InputPullup);
          }

          Console.WriteLine();
          Console.WriteLine("Digital port states:");

          foreach (var pincap in cap.Pins.Where(c => (c.DigitalInput || c.DigitalOutput) && !c.Analog))
          {
            var pinState = firmata.GetPinState(pincap.PinNumber);
            Console.WriteLine("Pin {0}: Mode = {1}, Value = {2}", pincap.PinNumber, pinState.Mode, pinState.Value);
          }
        });

        if (HasConsoleInput)
        {
          Console.WriteLine("Press q to quit or any other key to repeat.");
          if ('q' == Console.ReadKey(false).KeyChar)
            mustStop = true;
        }
        else
        {
          Console.WriteLine("Waiting 2 seconds for next iteration");
          Thread.Sleep(2000);
        }
      }

      firmata.MessageReceived -= OnFirmataMessageReceived;
      firmata.Dispose();
    }

    private static void TestFirmata(string port)
    {
      Console.WriteLine("\r\n\r\n--------------------------------------\r\nTestFirmata\r\n");

      using FirmataSession session = GetFirmataSession();
      if (session == null)
      {
        Console.WriteLine("Failed to open Firmata connection");
        return;
      }

      session.MessageReceived += OnFirmataMessageReceived;
      SendUserDefinedSysExEchoCommand(session, false);
      SendUserDefinedSysExEchoCommand(session, true);

      WaitForKeyIfPossible();

      DisplayPortCapabilities(session);

      WaitForKeyIfPossible();

      PerformBasicTest(session);

      session.MessageReceived -= OnFirmataMessageReceived;
    }

    private static void OnFirmataMessageReceived(object sender, FirmataMessageEventArgs eventArgs)
    {
      Console.WriteLine($"Message received: {eventArgs.Value.Name}");
    }

    private static void SendUserDefinedSysExEchoCommand(IFirmataProtocol session, bool forceTimeOut)
    {
      Console.WriteLine($"SendUserDefinedSysExEchoCommand forceTimeOut = {forceTimeOut}");

      try
      {
        var payload = new byte[] { 1, 2, 3, 4 };
        var sysEx = new SysEx(EchoPayloadCommand, payload);
        var reply = session.SendSysExWithReply(sysEx, result => result.Command == EchoPayloadCommand && result.Payload[0] == (forceTimeOut ? 127 : 1));

        Console.WriteLine($"SysEx reply: {reply.Command} length: {reply.Payload.Length}");
        foreach (var value in reply.Payload)
          Console.Write($"{value:X2} ");
        Console.WriteLine();
      }
      catch (TimeoutException exception)
      {
        Console.WriteLine(exception.Message);
      }
    }


    private static FirmataSession GetFirmataSession()
    {
      var finder = new FirmataFinder(_serialConnectionFactory, _logger)
      {
        StartupTimeoutMs = 5000
      };

      Console.WriteLine("Searching Arduino connection...");
      FirmataSession session;

      if (true)
      {
        session = finder.FindFirmata();
      }
      else
      {
        if (IsWindows)
        {
          session ??= finder.FindFirmata("COM6", SerialBaudRate.Bps_57600, 200);
          session ??= finder.FindFirmata("COM7", SerialBaudRate.Bps_57600, 200);
        }
        else
        {
          session ??= finder.FindFirmata("/dev/ttyUSB0", SerialBaudRate.Bps_57600, 500);
          session ??= finder.FindFirmata("/dev/ttyACM0", SerialBaudRate.Bps_57600, 500);
        }
      }

      if (session == null)
        Console.WriteLine("No connection found. Make sure your Arduino board is attached to a USB port.");
      else
        Console.WriteLine($"Connected to port {session.Connection.Name} at {((ISerialConnection)session.Connection).BaudRate} Baud.");

      if (session == null)
        return session;


      try
      {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        var version = session.GetProtocolVersion();
        Console.WriteLine($"\r\nReplyReceived {stopWatch.ElapsedMilliseconds} GetProtocolVersion: {version}");
        var firmware = session.GetFirmware();
        Console.WriteLine($"\r\nReplyReceived {stopWatch.ElapsedMilliseconds} GetFirmware: {firmware}");
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }

      return session;
    }

    private static void PerformBasicTest(IFirmataProtocol session)
    {
      var firmware = session.GetFirmware();
      Console.WriteLine($"Firmware: {firmware.Name} version {firmware.MajorVersion}.{firmware.MinorVersion}");
      var protocolVersion = session.GetProtocolVersion();
      Console.WriteLine($"Firmata protocol version {protocolVersion.Major}.{protocolVersion.Minor}");

      session.SetDigitalPinMode(10, PinMode.DigitalOutput);
      session.SetDigitalPin(10, true);
      Console.WriteLine("Command sent: Light on (pin 10)");
      WaitForKeyIfPossible();
      session.SetDigitalPin(10, false);
      Console.WriteLine("Command sent: Light off");
    }


    private static void DisplayPortCapabilities(IFirmataProtocol session)
    {
      BoardCapability cap = session.GetBoardCapability();
      Console.WriteLine();
      Console.WriteLine("Board Capability:");

      foreach (var pin in cap.Pins)
      {
        Console.WriteLine("Pin {0}: Input: {1}, Output: {2}, Analog: {3}, Analog-Res: {4}, PWM: {5}, PWM-Res: {6}, Servo: {7}, Servo-Res: {8}, Serial: {9}, Encoder: {10}, Input-pullup: {11}",
            pin.PinNumber,
            pin.DigitalInput,
            pin.DigitalOutput,
            pin.Analog,
            pin.AnalogResolution,
            pin.Pwm,
            pin.PwmResolution,
            pin.Servo,
            pin.ServoResolution,
            pin.Serial,
            pin.Encoder,
            pin.InputPullup);
      }
    }

    private void TimeTest(ArduinoSession session)
    {
      session.MessageReceived += Session_OnMessageReceived;

      var firmata = (II2CProtocol)session;
      var x = firmata.GetI2CReply(0x68, 7);

      Console.WriteLine();
      Console.WriteLine("{0} bytes received.", x.Data.Length);
      Console.WriteLine("Starting");
      WaitForKeyIfPossible();

      session.MessageReceived -= Session_OnMessageReceived;
    }

    private void Session_OnMessageReceived(object sender, FirmataMessageEventArgs eventArgs)
    {
      string o;

      switch (eventArgs.Value)
      {
        case FirmataMessage<StringData> stringDataMessage:
          o = stringDataMessage.Value.Text;
          break;

        default:
          o = "?";
          break;
      }

      Console.WriteLine("Message {0} received: {1}", eventArgs.Value.GetType().Name, o);
    }

    private static void SimpleTest(IDataConnection connection)
    {
      var session = new ArduinoSession(connection, timeOut: 2500);
      IFirmataProtocol firmata = session;

      firmata.AnalogStateReceived += Session_OnAnalogStateReceived;
      firmata.DigitalStateReceived += Session_OnDigitalStateReceived;

      Firmware firm = firmata.GetFirmware();
      Console.WriteLine();
      Console.WriteLine("Firmware: {0} {1}.{2}", firm.Name, firm.MajorVersion, firm.MinorVersion);
      Console.WriteLine();

      ProtocolVersion version = firmata.GetProtocolVersion();
      Console.WriteLine();
      Console.WriteLine("Protocol version: {0}.{1}", version.Major, version.Minor);
      Console.WriteLine();

      BoardCapability cap = firmata.GetBoardCapability();
      Console.WriteLine();
      Console.WriteLine("Board Capability:");

      foreach (var pin in cap.Pins)
      {
        Console.WriteLine("Pin {0}: Input: {1}, Output: {2}, Analog: {3}, Analog-Res: {4}, PWM: {5}, PWM-Res: {6}, Servo: {7}, Servo-Res: {8}, Serial: {9}, Encoder: {10}, Input-pullup: {11}",
            pin.PinNumber,
            pin.DigitalInput,
            pin.DigitalOutput,
            pin.Analog,
            pin.AnalogResolution,
            pin.Pwm,
            pin.PwmResolution,
            pin.Servo,
            pin.ServoResolution,
            pin.Serial,
            pin.Encoder,
            pin.InputPullup);
      }
      Console.WriteLine();

      var analogMapping = firmata.GetBoardAnalogMapping();
      Console.WriteLine("Analog channel mappings:");

      foreach (var mapping in analogMapping.PinMappings)
      {
        Console.WriteLine("Channel {0} is mapped to pin {1}.", mapping.Channel, mapping.PinNumber);
      }

      firmata.ResetBoard();

      Console.WriteLine();
      Console.WriteLine("Digital port states:");

      foreach (var pincap in cap.Pins.Where(c => (c.DigitalInput || c.DigitalOutput) && !c.Analog))
      {
        var pinState = firmata.GetPinState(pincap.PinNumber);
        Console.WriteLine("Pin {0}: Mode = {1}, Value = {2}", pincap.PinNumber, pinState.Mode, pinState.Value);
      }
      Console.WriteLine();

      firmata.SetDigitalPort(0, 0x04);
      firmata.SetDigitalPort(1, 0xff);
      firmata.SetDigitalPinMode(10, PinMode.DigitalOutput);
      firmata.SetDigitalPinMode(11, PinMode.ServoControl);
      firmata.SetDigitalPin(11, 90);
      Thread.Sleep(500);
      int hi = 0;

      for (int a = 0; a <= 179; a += 1)
      {
        firmata.SetDigitalPin(11, a);
        Thread.Sleep(100);
        firmata.SetDigitalPort(1, hi);
        hi ^= 4;
        Console.Write("{0};", a);
      }
      Console.WriteLine();
      Console.WriteLine();

      firmata.SetDigitalPinMode(6, PinMode.DigitalInput);

      //firmata.SetDigitalPortState(2, 255);
      //firmata.SetDigitalPortState(3, 255);

      firmata.SetSamplingInterval(500);
      firmata.SetAnalogReportMode(0, false);

      Console.WriteLine("Setting digital report modes:");
      firmata.SetDigitalReportMode(0, true);
      firmata.SetDigitalReportMode(1, true);
      firmata.SetDigitalReportMode(2, true);
      Console.WriteLine();

      foreach (var pinCap in cap.Pins.Where(c => (c.DigitalInput || c.DigitalOutput) && !c.Analog))
      {
        PinState state = firmata.GetPinState(pinCap.PinNumber);
        Console.WriteLine("Digital {1} pin {0}: {2}", state.PinNumber, state.Mode, state.Value);
      }
      Console.WriteLine();

      Console.ReadLine();
      firmata.SetAnalogReportMode(0, false);
      firmata.SetDigitalReportMode(0, false);
      firmata.SetDigitalReportMode(1, false);
      firmata.SetDigitalReportMode(2, false);
      Console.WriteLine("Ready.");
    }

    private static void Session_OnDigitalStateReceived(object sender, FirmataEventArgs<DigitalPortState> eventArgs)
    {
      Console.WriteLine("Digital level of port {0}: {1}", eventArgs.Value.Port, eventArgs.Value.IsSet(6) ? 'X' : 'O');
    }

    private static void Session_OnAnalogStateReceived(object sender, FirmataEventArgs<AnalogState> eventArgs)
    {
      Console.WriteLine("Analog level of pin {0}: {1}", eventArgs.Value.Channel, eventArgs.Value.Level);
    }
  }
}
