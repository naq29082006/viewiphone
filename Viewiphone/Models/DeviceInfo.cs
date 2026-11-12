namespace Viewiphone.Models
{
    public class DeviceInfo
    {
        public string DeviceName { get; set; } = "iPhone";
        public string Model { get; set; } = "iPhone";
        public string IosVersion { get; set; } = "15.0";
        public uint NativeWidth { get; set; }
        public uint NativeHeight { get; set; }
        public uint StreamWidth { get; set; }
        public uint StreamHeight { get; set; }
        public ushort Fps { get; set; }

        public override string ToString()
        {
            return $"{DeviceName} ({Model}, iOS {IosVersion}) - {StreamWidth}x{StreamHeight} @ {Fps}fps";
        }
    }
}
