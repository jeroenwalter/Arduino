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
            var result = new byte[payload.Length*2];
            
            for (var i = 0; i < payload.Length; i++)
            {
                result[i * 2] = (byte)(payload[i] & 0x7F);
                result[i * 2 + 1] = (byte)((payload[i] >> 7) & 0x7F);
            }

            return result;
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

        /// <summary>
        /// Returns the next 4 bytes as an UInt32. Assumes the 4 bytes are stored as little endian in the byte array.
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="startIndex">Offset in payload</param>
        /// <returns></returns>
        public static uint GetUInt32(this byte[] payload, int startIndex)
        {
            return (uint)GetInt32(payload, startIndex);
        }

        /// <summary>
        /// Returns the next 4 bytes as an Int32. Assumes the 4 bytes are stored as little endian in the byte array.
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="startIndex">Offset in payload</param>
        /// <returns></returns>
        public static int GetInt32(this byte[] payload, int startIndex)
        {
            return payload[startIndex] | (payload[startIndex + 1] << 8) | (payload[startIndex + 2] << 16) | (payload[startIndex + 3] << 24);
        }

        /// <summary>
        /// Sets the next 4 bytes as an Int32. The int32 is stored as little endian in the byte array.
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="startIndex">Offset in payload</param>
        /// <param name="value">Value to set</param>
        public static void SetInt32(this byte[] payload, int startIndex, int value)
        {
            payload[startIndex] = (byte)(value & 0xFF);
            payload[startIndex + 1] = (byte)((value >> 8) & 0xFF);
            payload[startIndex + 2] = (byte)((value >> 16) & 0xFF);
            payload[startIndex + 3] = (byte)((value >> 24) & 0xFF);
        }

        /// <summary>
        /// Sets the next 4 bytes as an UInt32. The int32 is stored as little endian in the byte array.
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="startIndex">Offset in payload</param>
        /// <param name="value">Value to set</param>
        public static void SetUInt32(this byte[] payload, int startIndex, uint value)
        {
            payload[startIndex] = (byte)(value & 0xFF);
            payload[startIndex + 1] = (byte)((value >> 8) & 0xFF);
            payload[startIndex + 2] = (byte)((value >> 16) & 0xFF);
            payload[startIndex + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}