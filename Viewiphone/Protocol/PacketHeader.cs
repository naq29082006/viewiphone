using System.Runtime.InteropServices;

namespace Viewiphone.Protocol
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketHeader
    {
        public const uint MagicValue = 0x56534352; // "VSCR"
        public const ushort CurrentVersion = 0x0001;
        public const int HeaderSize = 28;

        public uint Magic;
        public ushort Version;
        public ushort TypeRaw;
        public ushort FlagsRaw;
        public ushort Reserved;
        public uint FrameId;
        public ulong TimestampUs;
        public uint PayloadLength;

        public PacketType Type => (PacketType)TypeRaw;
        public PacketFlags Flags => (PacketFlags)FlagsRaw;
        public bool IsKeyframe => (Flags & PacketFlags.Keyframe) != 0;

        public static PacketHeader Read(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < HeaderSize)
                throw new ArgumentException("Buffer smaller than HeaderSize", nameof(buffer));

            return MemoryMarshal.Read<PacketHeader>(buffer);
        }

        public void Write(Span<byte> destination)
        {
            if (destination.Length < HeaderSize)
                throw new ArgumentException("Destination span too small", nameof(destination));

            MemoryMarshal.Write(destination, in this);
        }
    }
}
