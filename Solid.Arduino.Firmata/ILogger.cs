using System;

namespace Solid.Arduino.Firmata
{
    /// <summary>
    /// Simple logger interface.
    /// </summary>
    /// <remarks>
    /// This class enables logging, without having to rely on performance hogging Debug.WriteLine statements (unless you implement the Debug method that way....).
    /// </remarks>
    public interface ILogger
    {
        void Debug(string message, params object[] args);
        void Trace(string message, params object[] args);
        void Info(string message, params object[] args);
        void Warn(string message, params object[] args);
        void Error(string message, params object[] args);
        void Fatal(string message, params object[] args);

        void Debug(Exception exception, string message, params object[] args);
        void Trace(Exception exception, string message, params object[] args);
        void Info(Exception exception, string message, params object[] args);
        void Warn(Exception exception, string message, params object[] args);
        void Error(Exception exception, string message, params object[] args);
        void Fatal(Exception exception, string message, params object[] args);
  }
}