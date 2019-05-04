using System;
using System.Text;

namespace Solid.Arduino.Firmata
{
    public static class MoreByteArrayExtensions
    {
        public static byte[] ConvertFrom14BitPerBytePackets(this byte[] payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            var result = new byte[payload.Length / 2];
            for (var x = 0; x < result.Length; x++)
                result[x] = (byte)(payload[x * 2] | payload[x * 2 + 1] << 7);

            return result;
        }

        public static byte[] ConvertTo14BitPerBytePackets(this byte[] payload)
        {
            throw new NotImplementedException();
        }

        public static byte[] DecodeFrom7BitPerByteStream(this byte[] payload)
        {
            throw new NotImplementedException();
        }

        public static byte[] EncodeTo7BitPerByteStream(this byte[] payload)
        {
            throw new NotImplementedException();
        }

        public static string ConvertFrom14BitPerBytePacketsToString(this byte[] payload, int startIndex)
        {
            var builder = new StringBuilder((payload.Length - startIndex) / 2);

            for (var x = startIndex; x < payload.Length; x += 2)
                builder.Append((char)(payload[x] | (payload[x + 1] << 7)));

            return builder.ToString();
        }
    }
}