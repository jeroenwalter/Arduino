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
        public IReadOnlyList<string> GetDeviceNames() => SerialPort.GetPortNames();

        public IDataConnection Create(string deviceName, IDataConnectionConfiguration configuration)
        {
            if (!GetDeviceNames().Contains(deviceName))
                throw new ArgumentOutOfRangeException(nameof(deviceName));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            return new SerialConnection(deviceName, ((SerialConnectionConfiguration) configuration).BaudRate);
        }
    }
}