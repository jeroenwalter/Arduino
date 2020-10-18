using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using Solid.Arduino.Firmata;
using Solid.Arduino.Serial;

namespace Solid.Arduino.Core
{
  public class SerialConnectionFactory : IDataConnectionFactory
  {
    private static bool IsWindows => Environment.OSVersion.Platform == PlatformID.Win32Windows || Environment.OSVersion.Platform == PlatformID.Win32NT;

    public IReadOnlyList<string> GetDeviceNames() => SerialPort.GetPortNames();

    public IDataConnection Create(string deviceName, IDataConnectionConfiguration configuration)
    {
      if (!GetDeviceNames().Contains(deviceName))
        throw new ArgumentOutOfRangeException(nameof(deviceName));

      if (configuration == null)
        throw new ArgumentNullException(nameof(configuration));

      // Unless Microsoft fixes their implementation, don't use it, not even for Windows.
      //if (IsWindows)
      //  return new MicrosoftSerialConnection(deviceName, ((SerialConnectionConfiguration) configuration).BaudRate);
      //else
      return new MonoSerialConnection(deviceName, ((SerialConnectionConfiguration)configuration).BaudRate);
    }
  }
}