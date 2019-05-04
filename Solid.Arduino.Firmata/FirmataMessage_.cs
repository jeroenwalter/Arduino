using System;

namespace Solid.Arduino.Firmata
{
    /// <inheritdoc />
    public class FirmataMessage<T> : IFirmataMessage
        where T : struct
    {
        /// <summary>
        ///     Initializes a new <see cref="FirmataMessage{T}" /> instance.
        /// </summary>
        /// <param name="value"></param>
        public FirmataMessage(T value)
        {
            Value = value;
        }

        /// <summary>
        ///     Gets the specific value delivered by the message.
        /// </summary>
        public T Value { get; internal set; }
        
        /// <inheritdoc />
        public DateTime Time { get; } = DateTime.UtcNow;
    }
}