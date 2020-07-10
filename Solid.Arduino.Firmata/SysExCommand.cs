namespace Solid.Arduino.Firmata
{
  /// <summary>
  /// SysEx commands
  /// </summary>
  /// <seealso cref="https://github.com/firmata/protocol/blob/master/protocol.md">Official Firmata Protocol</seealso>
  public enum SysExCommand : byte
  {
    ExtendedId = 0x00, // A value of 0x00 indicates the next 2 bytes define the extended ID
    //Reserved = 0x01-0x0F // IDs 0x01 - 0x0F are reserved for user defined commands
    AnalogMappingQuery = 0x69, // ask for mapping of analog to pin numbers
    AnalogMappingResponse = 0x6A, // reply with mapping info
    CapabilityQuery = 0x6B, // ask for supported modes and resolution of all pins
    CapabilityResponse = 0x6C, // reply with supported modes and resolution
    PinStateQuery = 0x6D, // ask for a pin's current mode and state (different than value)
    PinStateResponse = 0x6E, // reply with a pin's current mode and state (different than value)
    ExtendedAnalog = 0x6F, // analog write (PWM, Servo, etc) to any pin
    StringData = 0x71, // a string message with 14-bits per char
    I2CRequest = 0x76,
    I2CReply = 0x77,
    ReportFirmware = 0x79, // report name and version of the firmware
    SamplingInterval = 0x7A, // the interval at which analog input is sampled (default = 19ms)
    SysexNonRealtime = 0x7E, // MIDI Reserved for non-realtime messages
    SysexRealtime = 0X7F // MIDI Reserved for realtime messages
  }
}