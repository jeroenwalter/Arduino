using Solid.Arduino.Firmata;

namespace Solid.Arduino.Serial
{
    public class SerialConnectionConfiguration : IDataConnectionConfiguration
    {
        public SerialBaudRate BaudRate { get; set; }
    }
}