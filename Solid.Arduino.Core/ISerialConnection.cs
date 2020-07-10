using Solid.Arduino.Firmata;

namespace Solid.Arduino.Core
{
  public interface ISerialConnection : IDataConnection
  {
    int BaudRate { get; }
  }
}