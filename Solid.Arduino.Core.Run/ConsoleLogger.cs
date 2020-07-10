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

    public void Debug(Exception exception, string message, params object[] args)
    {
      Debug(message, args);
      Console.WriteLine(exception);
    }

    public void Trace(Exception exception, string message, params object[] args)
    {
      Trace(message, args);
      Console.WriteLine(exception);
    }

    public void Info(Exception exception, string message, params object[] args)
    {
      Info(message, args);
      Console.WriteLine(exception);
    }

    public void Warn(Exception exception, string message, params object[] args)
    {
      Warn(message, args);
      Console.WriteLine(exception);
    }

    public void Error(Exception exception, string message, params object[] args)
    {
      Error(message, args);
      Console.WriteLine(exception);
    }

    public void Fatal(Exception exception, string message, params object[] args)
    {
      Fatal(message, args);
      Console.WriteLine(exception);
    }
  }
}