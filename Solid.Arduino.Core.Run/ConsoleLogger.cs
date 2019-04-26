using System;
using System.Diagnostics;
using Solid.Arduino.Firmata;

namespace Solid.Arduino.Core.Run
{
  public class ConsoleLogger : ILogger
  {
    public void Debug(string message, params object[] args)
    {
      LogDebug(message, args);
    }

    [Conditional("DEBUG")]
    private void LogDebug(string message, params object[] args)
    {
      Console.WriteLine("DEBUG: " + message, args);
    }

    public void Trace(string message, params object[] args)
    {
      Console.WriteLine("TRACE: " + message, args);
    }

    public void Info(string message, params object[] args)
    {
      Console.WriteLine("INFO: " + message, args);
    }

    public void Warn(string message, params object[] args)
    {
      Console.WriteLine("WARN: " + message, args);
    }

    public void Error(string message, params object[] args)
    {
      Console.WriteLine("ERROR: " + message, args);
    }

    public void Fatal(string message, params object[] args)
    {
      Console.WriteLine("FATAL: " + message, args);
    }
  }
}