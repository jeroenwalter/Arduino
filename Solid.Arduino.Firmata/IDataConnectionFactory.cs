using System.Collections;
using System.Collections.Generic;
using Solid.Arduino.Firmata;

namespace Solid.Arduino.Serial
{
    public interface IDataConnectionFactory
    {
        IReadOnlyList<string> GetDeviceNames();

        IDataConnection Create(string deviceName, IDataConnectionConfiguration configuration);
    }
}