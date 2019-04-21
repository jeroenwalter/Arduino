namespace Solid.Arduino.Firmata
{
  /// <summary>
  /// User-defined SysEx command
  /// </summary>
  public struct CustomSysEx
  {
    /// <summary>
    /// The user-defined SysEx command (0..7)
    /// </summary>
    public int Command { get; internal set; }

    /// <summary>
    /// The content of the SysEx message
    /// </summary>
    public byte[] Content { get; internal set; }
  }
}