namespace Solid.Arduino.Firmata
{
  /// <summary>
  /// Firmata commands
  /// </summary>
  /// <seealso cref="https://github.com/firmata/protocol/blob/master/protocol.md">Official Firmata Protocol</seealso>
  public enum FirmataCommand : byte
  {
    AnalogState = 0xE0,
    DigitalState = 0x90,
    ReportAnalogPin = 0xC0,
    ReportDigitalPort = 0xD0,
    SysExStart = 0xF0,
    SetPinMode = 0xF4,
    SetDigitalPin = 0xF5,
    ProtocolVersion = 0xF9,
    SysExEnd = 0xF7,
    SystemReset = 0xFF
  }
}