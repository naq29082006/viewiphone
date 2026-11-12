using System.Text.Json;
using Serilog;
using Viewiphone.Models;

namespace Viewiphone.Services
{
    public class ConfigurationService
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "devices.json");
        private readonly object _lock = new object();

        public List<DeviceConfig> LoadDevices()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(ConfigPath))
                    {
                        var defaultList = new List<DeviceConfig>
                        {
                            new DeviceConfig
                            {
                                Name = "iPhone Dopamine 01",
                                IpAddress = "192.168.1.100",
                                Port = 5000,
                                Token = "viewiphone_secret",
                                AutoConnect = false
                            }
                        };
                        SaveDevices(defaultList);
                        return defaultList;
                    }

                    string json = File.ReadAllText(ConfigPath);
                    var list = JsonSerializer.Deserialize<List<DeviceConfig>>(json);
                    return list ?? new List<DeviceConfig>();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to load device configurations from {ConfigPath}", ConfigPath);
                    return new List<DeviceConfig>();
                }
            }
        }

        public void SaveDevices(List<DeviceConfig> devices)
        {
            lock (_lock)
            {
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(devices, options);
                    File.WriteAllText(ConfigPath, json);
                    Log.Information("Saved {Count} devices to {ConfigPath}", devices.Count, ConfigPath);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to save device configurations to {ConfigPath}", ConfigPath);
                }
            }
        }
    }
}
