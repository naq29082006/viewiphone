using System.Collections.Concurrent;
using Serilog;
using Viewiphone.Models;

namespace Viewiphone.Services
{
    public class DeviceSession : IDisposable
    {
        public DeviceConfig Config { get; set; }
        public DeviceInfo? Info { get; set; }
        public DeviceConnectionStatus Status { get; set; } = DeviceConnectionStatus.Disconnected;
        public string StatusMessage { get; set; } = "Ready to connect";
        public double CurrentFps { get; set; }
        public double CurrentBitrateKbps { get; set; }
        public double CurrentLatencyMs { get; set; }

        public TcpClientService TcpClient { get; }
        public VideoDecoderService Decoder { get; }

        public event Action<Bitmap>? FrameReady;
        public event Action<DeviceConnectionStatus, string>? StatusUpdated;
        public event Action<double, double, double>? StatsUpdated;

        public DeviceSession(DeviceConfig config)
        {
            Config = config;
            TcpClient = new TcpClientService(config.Id, config.IpAddress, config.Port, config.Token);
            Decoder = new VideoDecoderService(config.Id);

            // Wire events
            TcpClient.StatusChanged += OnTcpStatusChanged;
            TcpClient.DeviceInfoReceived += OnDeviceInfoReceived;
            TcpClient.VideoConfigReceived += OnVideoConfigReceived;
            TcpClient.VideoFrameReceived += OnVideoFrameReceived;
            TcpClient.StatsUpdated += OnStatsUpdated;

            Decoder.FrameDecoded += (bmp, frameId, pts) =>
            {
                FrameReady?.Invoke(bmp);
            };
        }

        private void OnTcpStatusChanged(DeviceConnectionStatus status, string message)
        {
            Status = status;
            StatusMessage = message;
            StatusUpdated?.Invoke(status, message);

            if (status == DeviceConnectionStatus.Disconnected || status == DeviceConnectionStatus.Error)
            {
                CurrentFps = 0;
                CurrentBitrateKbps = 0;
                Decoder.Stop();
            }
        }

        private void OnDeviceInfoReceived(DeviceInfo info)
        {
            Info = info;
        }

        private void OnVideoConfigReceived(byte[] sps, byte[] pps)
        {
            Decoder.ConfigureSpsPps(sps, pps);
        }

        private void OnVideoFrameReceived(VideoFrame frame)
        {
            Decoder.EnqueueFrame(frame);
        }

        private void OnStatsUpdated(double fps, double bitrateKbps, double latencyMs)
        {
            CurrentFps = fps;
            CurrentBitrateKbps = bitrateKbps;
            CurrentLatencyMs = latencyMs;
            StatsUpdated?.Invoke(fps, bitrateKbps, latencyMs);
        }

        public void Connect()
        {
            TcpClient.IpAddress = Config.IpAddress;
            TcpClient.Port = Config.Port;
            TcpClient.Token = Config.Token;
            TcpClient.Start();
        }

        public async Task DisconnectAsync()
        {
            await TcpClient.StopAsync();
            Decoder.Stop();
        }

        public void Dispose()
        {
            TcpClient.Dispose();
            Decoder.Dispose();
        }
    }

    public class DeviceManager : IDisposable
    {
        private readonly ConfigurationService _configService;
        private readonly ConcurrentDictionary<string, DeviceSession> _sessions = new ConcurrentDictionary<string, DeviceSession>();
        private readonly object _lock = new object();

        public event Action<DeviceSession>? DeviceAdded;
        public event Action<string>? DeviceRemoved;
        public event Action<string, DeviceConnectionStatus, string>? DeviceStatusChanged;
        public event Action<string, double, double, double>? DeviceStatsChanged;
        public event Action<string, Bitmap>? FrameReceived;
        public event Action<int, int, int, int>? GlobalStatsChanged; // total, online, streaming, offline

        public IReadOnlyCollection<DeviceSession> Sessions => _sessions.Values.ToList();

        public DeviceManager(ConfigurationService configService)
        {
            _configService = configService;
        }

        public void Initialize()
        {
            var configs = _configService.LoadDevices();
            foreach (var cfg in configs)
            {
                AddSessionInternal(cfg);
            }
            BroadcastGlobalStats();
        }

        public DeviceSession AddDevice(DeviceConfig config)
        {
            lock (_lock)
            {
                var session = AddSessionInternal(config);
                SaveAllConfigs();
                BroadcastGlobalStats();
                return session;
            }
        }

        private DeviceSession AddSessionInternal(DeviceConfig config)
        {
            var session = new DeviceSession(config);

            session.StatusUpdated += (status, msg) =>
            {
                DeviceStatusChanged?.Invoke(config.Id, status, msg);
                BroadcastGlobalStats();
            };

            session.StatsUpdated += (fps, br, lat) =>
            {
                DeviceStatsChanged?.Invoke(config.Id, fps, br, lat);
            };

            session.FrameReady += (bmp) =>
            {
                FrameReceived?.Invoke(config.Id, bmp);
            };

            _sessions[config.Id] = session;
            DeviceAdded?.Invoke(session);

            if (config.AutoConnect)
            {
                session.Connect();
            }

            return session;
        }

        public void UpdateDevice(DeviceConfig config)
        {
            lock (_lock)
            {
                if (_sessions.TryGetValue(config.Id, out var session))
                {
                    session.Config = config;
                    session.TcpClient.IpAddress = config.IpAddress;
                    session.TcpClient.Port = config.Port;
                    session.TcpClient.Token = config.Token;
                    SaveAllConfigs();
                }
            }
        }

        public async Task RemoveDeviceAsync(string deviceId)
        {
            DeviceSession? session;
            lock (_lock)
            {
                _sessions.TryRemove(deviceId, out session);
                SaveAllConfigs();
                BroadcastGlobalStats();
            }

            if (session != null)
            {
                await session.DisconnectAsync();
                session.Dispose();
                DeviceRemoved?.Invoke(deviceId);
            }
        }

        public void ConnectDevice(string deviceId)
        {
            if (_sessions.TryGetValue(deviceId, out var session))
            {
                session.Connect();
            }
        }

        public async Task DisconnectDeviceAsync(string deviceId)
        {
            if (_sessions.TryGetValue(deviceId, out var session))
            {
                await session.DisconnectAsync();
            }
        }

        public void ConnectAll()
        {
            foreach (var session in _sessions.Values)
            {
                session.Connect();
            }
        }

        public async Task DisconnectAllAsync()
        {
            var tasks = _sessions.Values.Select(s => s.DisconnectAsync());
            await Task.WhenAll(tasks);
        }

        private void SaveAllConfigs()
        {
            var configs = _sessions.Values.Select(s => s.Config).ToList();
            _configService.SaveDevices(configs);
        }

        public void BroadcastGlobalStats()
        {
            int total = _sessions.Count;
            int online = 0;
            int streaming = 0;
            int offline = 0;

            foreach (var s in _sessions.Values)
            {
                if (s.Status == DeviceConnectionStatus.Streaming)
                {
                    online++;
                    streaming++;
                }
                else if (s.Status == DeviceConnectionStatus.Connecting ||
                         s.Status == DeviceConnectionStatus.Handshaking ||
                         s.Status == DeviceConnectionStatus.Authenticating)
                {
                    online++;
                }
                else
                {
                    offline++;
                }
            }

            GlobalStatsChanged?.Invoke(total, online, streaming, offline);
        }

        public void Dispose()
        {
            foreach (var session in _sessions.Values)
            {
                session.Dispose();
            }
            _sessions.Clear();
        }
    }
}
