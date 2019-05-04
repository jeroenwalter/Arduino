namespace Solid.Arduino.Firmata
{
    /// <summary>
    /// SysEx message
    /// </summary>
    /// <remarks>
    /// Used for sending and receiving SysEx messages.
    /// </remarks>
    public struct SysEx
    {
        /// <summary>
        /// SysEx message without payload.
        /// </summary>
        /// <param name="command"></param>
        public SysEx(byte command)
        {
            Command = command;
            Payload = null;
        }
        
        /// <summary>
        /// SysEx message with payload.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="payload"></param>
        public SysEx(byte command, byte[] payload)
        {
            Command = command;
            Payload = payload;
        }

        /// <summary>
        /// The SysEx command
        /// </summary>
        public byte Command { get; }

        /// <summary>
        /// The unprocessed content of the SysEx message
        /// </summary>
        /// <remarks>
        /// May be null or 0-length.
        /// </remarks>
        public byte[] Payload { get; }
        
        
    }
}