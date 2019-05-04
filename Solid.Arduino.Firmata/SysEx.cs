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
        public byte Command { get; internal set; }

        /// <summary>
        /// The content of the SysEx message
        /// </summary>
        /// <remarks>
        /// May be null or 0-length.
        /// </remarks>
        public byte[] Payload { get; internal set; }
        
        /// <summary>
        /// Convert every 2 bytes of the payload to 1 byte.
        /// </summary>
        /// <remarks>
        /// If the SysEx message was sent from the Arduino via the Firmata::sendSysEx() method, then the payload was chopped up
        /// in 7-bit per byte packets.
        /// </remarks>
        /// <returns></returns>
        public byte[] ConvertFrom27BitPerBytePackets()
        {
            var result = new byte[Payload.Length / 2];
            for (var x = 0; x < result.Length; x++)
                result[x] = (byte) (Payload[x * 2] | Payload[x * 2 + 1] << 7);

            return result;
        }

        public void SetPayloadTo27BitPerBytePackets(byte[] payload)
        {

        }

        public byte[] Decode7BitPerByteStream()
        {
            return null;
        }

        public void EncodeTo7BitPerByteStream(byte[] payload)
        {

        }
    }
}