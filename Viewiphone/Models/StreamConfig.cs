namespace Viewiphone.Models
{
    public class StreamConfig
    {
        public int TargetFps { get; set; } = 15;
        public int TargetBitrate { get; set; } = 1500000; // 1.5 Mbps
        public int ScalePercentage { get; set; } = 50; // 50%
        public string ResolutionPreset { get; set; } = "720p";
        public bool HardwareAcceleration { get; set; } = true;
    }
}
