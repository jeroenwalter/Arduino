using System;

namespace Solid.Arduino.Firmata
{
  public class FirmataOverflowException : Exception
  {
    public FirmataOverflowException(string message)
      : base(message)
    {

    }

  }
}