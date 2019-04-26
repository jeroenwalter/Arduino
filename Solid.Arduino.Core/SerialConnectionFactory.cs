using System.Collections.Generic;
using System.IO.Ports;
using Solid.Arduino.Firmata;
using Solid.Arduino.Serial;

namespace Solid.Arduino.Core
{
    public class SerialConnectionFactory : IDataConnectionFactory
    {
        public IReadOnlyList<string> GetDeviceNames() => SerialPort.GetPortNames();

        public IDataConnection Create(string deviceName, IDataConnectionConfiguration configuration)
        {
            return new SerialConnection(deviceName, ((SerialConnectionConfiguration) configuration).BaudRate);
        }
    }
}