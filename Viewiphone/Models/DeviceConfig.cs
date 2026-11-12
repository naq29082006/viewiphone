namespace Viewiphone.Models
{
    public class DeviceConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "iPhone";
        public string IpAddress { get; set; } = "192.168.1.100";
        public int Port { get; set; } = 5000;
        public string Token { get; set; } = "viewiphone_secret";
        public bool AutoConnect { get; set; } = true;
        public StreamConfig StreamSettings { get; set; } = new StreamConfig();
    }
}
