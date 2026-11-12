namespace Viewiphone.Models
{
    public class VideoFrame
    {
        public uint FrameId { get; set; }
        public ulong TimestampUs { get; set; }
        public bool IsKeyframe { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    }
}
