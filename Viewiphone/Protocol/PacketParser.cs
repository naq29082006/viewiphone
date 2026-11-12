using System.Text;
using Viewiphone.Models;

namespace Viewiphone.Protocol
{
    public static class PacketParser
    {
        public static DeviceInfo ParseDeviceInfo(ReadOnlySpan<byte> payload)
        {
            var info = new DeviceInfo();
            if (payload.Length < 148) // 64 + 32 + 32 + 4 + 4 + 4 + 4 + 2 + 2
                return info;

            // Extract null-terminated strings
            info.DeviceName = ExtractFixedString(payload.Slice(0, 64));
            info.Model = ExtractFixedString(payload.Slice(64, 32));
            info.IosVersion = ExtractFixedString(payload.Slice(96, 32));

            info.NativeWidth = BitConverter.ToUInt32(payload.Slice(128, 4));
            info.NativeHeight = BitConverter.ToUInt32(payload.Slice(132, 4));
            info.StreamWidth = BitConverter.ToUInt32(payload.Slice(136, 4));
            info.StreamHeight = BitConverter.ToUInt32(payload.Slice(140, 4));
            info.Fps = BitConverter.ToUInt16(payload.Slice(144, 2));

            return info;
        }

        public static (byte[] sps, byte[] pps) ParseVideoConfig(ReadOnlySpan<byte> payload)
        {
            if (payload.Length < 8)
                return (Array.Empty<byte>(), Array.Empty<byte>());

            int spsLen = (int)BitConverter.ToUInt32(payload.Slice(0, 4));
            if (payload.Length < 4 + spsLen + 4)
                return (Array.Empty<byte>(), Array.Empty<byte>());

            byte[] sps = payload.Slice(4, spsLen).ToArray();
            int offset = 4 + spsLen;

            int ppsLen = (int)BitConverter.ToUInt32(payload.Slice(offset, 4));
            if (payload.Length < offset + 4 + ppsLen)
                return (sps, Array.Empty<byte>());

            byte[] pps = payload.Slice(offset + 4, ppsLen).ToArray();
            return (sps, pps);
        }

        public static (uint code, string message) ParseError(ReadOnlySpan<byte> payload)
        {
            if (payload.Length < 4)
                return (0, string.Empty);

            uint code = BitConverter.ToUInt32(payload.Slice(0, 4));
            string message = payload.Length > 4 ? ExtractFixedString(payload.Slice(4)) : string.Empty;
            return (code, message);
        }

        private static string ExtractFixedString(ReadOnlySpan<byte> span)
        {
            int nullIdx = span.IndexOf((byte)0);
            if (nullIdx >= 0)
            {
                span = span.Slice(0, nullIdx);
            }
            return Encoding.UTF8.GetString(span).Trim();
        }
    }
}
