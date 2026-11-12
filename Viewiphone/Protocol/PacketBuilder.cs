using System.Text;

namespace Viewiphone.Protocol
{
    public static class PacketBuilder
    {
        public static byte[] BuildHandshake()
        {
            return BuildPacket(PacketType.Handshake, PacketFlags.None, 0, 0, ReadOnlySpan<byte>.Empty);
        }

        public static byte[] BuildAuth(string token)
        {
            byte[] tokenBytes = Encoding.UTF8.GetBytes(token ?? string.Empty);
            return BuildPacket(PacketType.Auth, PacketFlags.None, 0, 0, tokenBytes);
        }

        public static byte[] BuildHeartbeat(bool isPing = true)
        {
            PacketFlags flags = isPing ? PacketFlags.Ping : PacketFlags.Pong;
            return BuildPacket(PacketType.Heartbeat, flags, 0, 0, ReadOnlySpan<byte>.Empty);
        }

        public static byte[] BuildClientConfig(ushort targetFps, ushort targetScalePct, uint targetBitrate, bool requestKeyframe)
        {
            byte[] payload = new byte[12];
            using (var ms = new MemoryStream(payload))
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(targetFps);
                bw.Write(targetScalePct);
                bw.Write(targetBitrate);
                bw.Write((byte)(requestKeyframe ? 1 : 0));
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((byte)0);
            }

            return BuildPacket(PacketType.ClientConfig, PacketFlags.None, 0, 0, payload);
        }

        public static byte[] BuildDisconnect()
        {
            return BuildPacket(PacketType.Disconnect, PacketFlags.None, 0, 0, ReadOnlySpan<byte>.Empty);
        }

        public static byte[] BuildPacket(PacketType type, PacketFlags flags, uint frameId, ulong timestampUs, ReadOnlySpan<byte> payload)
        {
            byte[] packet = new byte[PacketHeader.HeaderSize + payload.Length];

            var header = new PacketHeader
            {
                Magic = PacketHeader.MagicValue,
                Version = PacketHeader.CurrentVersion,
                TypeRaw = (ushort)type,
                FlagsRaw = (ushort)flags,
                Reserved = 0,
                FrameId = frameId,
                TimestampUs = timestampUs,
                PayloadLength = (uint)payload.Length
            };

            header.Write(packet.AsSpan(0, PacketHeader.HeaderSize));

            if (!payload.IsEmpty)
            {
                payload.CopyTo(packet.AsSpan(PacketHeader.HeaderSize));
            }

            return packet;
        }
    }
}
