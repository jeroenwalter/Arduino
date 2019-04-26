using System.Collections.Generic;

namespace Solid.Arduino.Firmata
{
    public interface IDataConnectionFactory
    {
        IReadOnlyList<string> GetDeviceNames();

        IDataConnection Create(string deviceName, IDataConnectionConfiguration configuration);
    }
}