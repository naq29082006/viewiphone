using System.Diagnostics;
using System.Net.Sockets;
using Serilog;
using Viewiphone.Models;
using Viewiphone.Protocol;

namespace Viewiphone.Services
{
    public class TcpClientService : IDisposable
    {
        public string DeviceId { get; }
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public string Token { get; set; }
        public bool AutoReconnect { get; set; } = true;

        public DeviceConnectionStatus Status { get; private set; } = DeviceConnectionStatus.Disconnected;

        public event Action<DeviceConnectionStatus, string>? StatusChanged;
        public event Action<DeviceInfo>? DeviceInfoReceived;
        public event Action<byte[], byte[]>? VideoConfigReceived;
        public event Action<VideoFrame>? VideoFrameReceived;
        public event Action<double, double, double>? StatsUpdated; // fps, bitrateKbps, latencyMs
        public event Action<string>? ErrorOccurred;

        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;
        private Task? _readTask;
        private Task? _heartbeatTask;

        private readonly object _stateLock = new object();
        private bool _isDisposed;

        // Statistics tracking
        private int _framesReceivedCount;
        private long _bytesReceivedCount;
        private readonly Stopwatch _statsStopwatch = new Stopwatch();

        public TcpClientService(string deviceId, string ipAddress, int port, string token)
        {
            DeviceId = deviceId;
            IpAddress = ipAddress;
            Port = port;
            Token = token;
        }

        public void Start()
        {
            lock (_stateLock)
            {
                if (Status != DeviceConnectionStatus.Disconnected && Status != DeviceConnectionStatus.Error)
                    return;

                _cts = new CancellationTokenSource();
                _readTask = Task.Run(() => ConnectionLoopAsync(_cts.Token));
            }
        }

        public async Task StopAsync()
        {
            CancellationTokenSource? cts;
            lock (_stateLock)
            {
                cts = _cts;
                _cts = null;
            }

            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
            }

            CloseSocket();
            UpdateStatus(DeviceConnectionStatus.Disconnected, "User disconnected");

            if (_readTask != null)
            {
                try { await _readTask.ConfigureAwait(false); } catch { }
                _readTask = null;
            }
        }

        private void UpdateStatus(DeviceConnectionStatus status, string message)
        {
            Status = status;
            StatusChanged?.Invoke(status, message);
            Log.Information("[{DeviceId}] Status: {Status} - {Message}", DeviceId, status, message);
        }

        private async Task ConnectionLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    UpdateStatus(DeviceConnectionStatus.Connecting, $"Connecting to {IpAddress}:{Port}...");

                    _tcpClient = new TcpClient();
                    _tcpClient.NoDelay = true;
                    _tcpClient.ReceiveBufferSize = 512 * 1024;
                    _tcpClient.SendBufferSize = 64 * 1024;

                    using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    connectCts.CancelAfter(TimeSpan.FromSeconds(5));

                    await _tcpClient.ConnectAsync(IpAddress, Port, connectCts.Token).ConfigureAwait(false);
                    _stream = _tcpClient.GetStream();

                    UpdateStatus(DeviceConnectionStatus.Handshaking, "Connected. Sending Handshake...");

                    // 1. Send Handshake
                    byte[] handshakePkt = PacketBuilder.BuildHandshake();
                    await _stream.WriteAsync(handshakePkt, ct).ConfigureAwait(false);

                    // 2. Send Auth
                    UpdateStatus(DeviceConnectionStatus.Authenticating, "Authenticating with Agent token...");
                    byte[] authPkt = PacketBuilder.BuildAuth(Token);
                    await _stream.WriteAsync(authPkt, ct).ConfigureAwait(false);

                    // Start Heartbeat loop
                    _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(ct), ct);

                    // Start stats stopwatch
                    _statsStopwatch.Restart();
                    _framesReceivedCount = 0;
                    _bytesReceivedCount = 0;

                    // 3. Receive packets loop
                    await ReceivePacketsAsync(_stream, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[{DeviceId}] Connection error with {Ip}:{Port}", DeviceId, IpAddress, Port);
                    ErrorOccurred?.Invoke(ex.Message);
                }
                finally
                {
                    CloseSocket();
                }

                if (!AutoReconnect || ct.IsCancellationRequested)
                {
                    UpdateStatus(DeviceConnectionStatus.Disconnected, "Disconnected");
                    break;
                }

                UpdateStatus(DeviceConnectionStatus.Reconnecting, "Connection lost. Reconnecting in 3 seconds...");
                try
                {
                    await Task.Delay(3000, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ReceivePacketsAsync(NetworkStream stream, CancellationToken ct)
        {
            byte[] readBuffer = new byte[64 * 1024];
            var accumulator = new MemoryStream();

            while (!ct.IsCancellationRequested)
            {
                int bytesRead = await stream.ReadAsync(readBuffer, 0, readBuffer.Length, ct).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    Log.Warning("[{DeviceId}] Remote host closed connection", DeviceId);
                    break;
                }

                _bytesReceivedCount += bytesRead;
                accumulator.Write(readBuffer, 0, bytesRead);

                // Process all complete packets in accumulator
                ProcessAccumulatedPackets(accumulator);

                // Update stats every 1 second
                if (_statsStopwatch.ElapsedMilliseconds >= 1000)
                {
                    double elapsedSec = _statsStopwatch.Elapsed.TotalSeconds;
                    double fps = _framesReceivedCount / elapsedSec;
                    double bitrateKbps = (_bytesReceivedCount * 8.0) / (elapsedSec * 1000.0);

                    _framesReceivedCount = 0;
                    _bytesReceivedCount = 0;
                    _statsStopwatch.Restart();

                    StatsUpdated?.Invoke(fps, bitrateKbps, 0);
                }
            }
        }

        private void ProcessAccumulatedPackets(MemoryStream accumulator)
        {
            byte[] buffer = accumulator.GetBuffer();
            int totalBytes = (int)accumulator.Length;
            int offset = 0;

            while (totalBytes - offset >= PacketHeader.HeaderSize)
            {
                var headerSpan = new ReadOnlySpan<byte>(buffer, offset, PacketHeader.HeaderSize);
                var header = PacketHeader.Read(headerSpan);

                if (header.Magic != PacketHeader.MagicValue || header.Version != PacketHeader.CurrentVersion)
                {
                    Log.Error("[{DeviceId}] Stream sync lost! Invalid magic 0x{Magic:X}", DeviceId, header.Magic);
                    // Clear buffer to avoid getting stuck
                    accumulator.SetLength(0);
                    return;
                }

                int packetTotalLen = PacketHeader.HeaderSize + (int)header.PayloadLength;
                if (totalBytes - offset < packetTotalLen)
                {
                    // Incomplete packet payload, wait for more data
                    break;
                }

                var payloadSpan = new ReadOnlySpan<byte>(buffer, offset + PacketHeader.HeaderSize, (int)header.PayloadLength);
                DispatchPacket(header, payloadSpan);

                offset += packetTotalLen;
            }

            // Compact remaining unprocessed bytes in accumulator
            if (offset > 0)
            {
                int remaining = totalBytes - offset;
                if (remaining > 0)
                {
                    Buffer.BlockCopy(buffer, offset, buffer, 0, remaining);
                }
                accumulator.SetLength(remaining);
                accumulator.Position = remaining;
            }
        }

        private void DispatchPacket(PacketHeader header, ReadOnlySpan<byte> payload)
        {
            switch (header.Type)
            {
                case PacketType.Handshake:
                    Log.Information("[{DeviceId}] Handshake ACK received", DeviceId);
                    break;

                case PacketType.Auth:
                    if ((header.Flags & PacketFlags.AuthSuccess) != 0)
                    {
                        UpdateStatus(DeviceConnectionStatus.Streaming, "Authentication successful. Ready for video stream.");
                    }
                    else
                    {
                        UpdateStatus(DeviceConnectionStatus.Error, "Authentication failed! Invalid token.");
                        CloseSocket();
                    }
                    break;

                case PacketType.DeviceInfo:
                    var devInfo = PacketParser.ParseDeviceInfo(payload);
                    Log.Information("[{DeviceId}] Device Info: {Info}", DeviceId, devInfo);
                    DeviceInfoReceived?.Invoke(devInfo);
                    break;

                case PacketType.VideoConfig:
                    var (sps, pps) = PacketParser.ParseVideoConfig(payload);
                    Log.Information("[{DeviceId}] Video Config received: SPS={SpsLen}B, PPS={PpsLen}B", DeviceId, sps.Length, pps.Length);
                    VideoConfigReceived?.Invoke(sps, pps);
                    break;

                case PacketType.VideoFrame:
                    _framesReceivedCount++;
                    var frame = new VideoFrame
                    {
                        FrameId = header.FrameId,
                        TimestampUs = header.TimestampUs,
                        IsKeyframe = header.IsKeyframe,
                        Data = payload.ToArray(),
                        ReceivedAt = DateTime.UtcNow
                    };
                    VideoFrameReceived?.Invoke(frame);
                    break;

                case PacketType.Heartbeat:
                    // Pong received
                    break;

                case PacketType.Error:
                    var (code, msg) = PacketParser.ParseError(payload);
                    Log.Warning("[{DeviceId}] Server error {Code}: {Msg}", DeviceId, code, msg);
                    ErrorOccurred?.Invoke($"Server error {code}: {msg}");
                    break;

                case PacketType.Disconnect:
                    UpdateStatus(DeviceConnectionStatus.Disconnected, "Server requested disconnect");
                    CloseSocket();
                    break;
            }
        }

        private async Task HeartbeatLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(3000, ct).ConfigureAwait(false);
                    if (_stream != null && _tcpClient != null && _tcpClient.Connected)
                    {
                        byte[] pingPkt = PacketBuilder.BuildHeartbeat(isPing: true);
                        await _stream.WriteAsync(pingPkt, ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "[{DeviceId}] Heartbeat error", DeviceId);
                    break;
                }
            }
        }

        public async Task SendConfigAsync(ushort fps, ushort scalePct, uint bitrate, bool requestKeyframe)
        {
            if (_stream == null || _tcpClient == null || !_tcpClient.Connected) return;

            try
            {
                byte[] configPkt = PacketBuilder.BuildClientConfig(fps, scalePct, bitrate, requestKeyframe);
                await _stream.WriteAsync(configPkt).ConfigureAwait(false);
                Log.Information("[{DeviceId}] Sent config request: FPS={Fps}, Scale={Scale}%, Bitrate={Bitrate}, Keyframe={Key}",
                    DeviceId, fps, scalePct, bitrate, requestKeyframe);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[{DeviceId}] Failed to send config", DeviceId);
            }
        }

        public static async Task<(bool success, DeviceInfo? info, string message)> TestConnectionAsync(string ip, int port, string token, int timeoutMs = 4000)
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);

            try
            {
                await client.ConnectAsync(ip, port, cts.Token).ConfigureAwait(false);
                var stream = client.GetStream();

                // Send Handshake
                byte[] handshake = PacketBuilder.BuildHandshake();
                await stream.WriteAsync(handshake, cts.Token).ConfigureAwait(false);

                // Send Auth
                byte[] auth = PacketBuilder.BuildAuth(token);
                await stream.WriteAsync(auth, cts.Token).ConfigureAwait(false);

                byte[] buffer = new byte[4096];
                int totalRead = 0;
                DeviceInfo? receivedInfo = null;
                bool authSuccess = false;

                while (!cts.IsCancellationRequested && (!authSuccess || receivedInfo == null))
                {
                    int read = await stream.ReadAsync(buffer, totalRead, buffer.Length - totalRead, cts.Token).ConfigureAwait(false);
                    if (read == 0) break;
                    totalRead += read;

                    var (success, failed, info) = ParseTestResponse(buffer, totalRead);
                    if (failed)
                    {
                        return (false, null, "Authentication rejected by iPhone Agent! Check Token.");
                    }

                    if (success) authSuccess = true;
                    if (info != null) receivedInfo = info;

                    if (authSuccess && receivedInfo != null)
                    {
                        return (true, receivedInfo, $"Successfully connected to {receivedInfo.DeviceName} ({receivedInfo.Model}, iOS {receivedInfo.IosVersion})!");
                    }
                }

                if (!authSuccess)
                {
                    return (false, null, "Handshake or authentication timed out without response.");
                }

                return (true, receivedInfo, "Agent connected and authenticated successfully.");
            }
            catch (Exception ex)
            {
                return (false, null, $"Connection failed: {ex.Message}");
            }
        }

        private static (bool authSuccess, bool authFailed, DeviceInfo? info) ParseTestResponse(byte[] buffer, int totalRead)
        {
            bool authSuccess = false;
            bool authFailed = false;
            DeviceInfo? receivedInfo = null;

            int offset = 0;
            while (totalRead - offset >= PacketHeader.HeaderSize)
            {
                var hdr = PacketHeader.Read(buffer.AsSpan(offset, PacketHeader.HeaderSize));
                if (hdr.Magic != PacketHeader.MagicValue) break;

                int pktLen = PacketHeader.HeaderSize + (int)hdr.PayloadLength;
                if (totalRead - offset < pktLen) break;

                var payload = buffer.AsSpan(offset + PacketHeader.HeaderSize, (int)hdr.PayloadLength);

                if (hdr.Type == PacketType.Auth)
                {
                    if ((hdr.Flags & PacketFlags.AuthSuccess) != 0)
                        authSuccess = true;
                    else
                        authFailed = true;
                }
                else if (hdr.Type == PacketType.DeviceInfo)
                {
                    receivedInfo = PacketParser.ParseDeviceInfo(payload);
                }

                offset += pktLen;
            }

            return (authSuccess, authFailed, receivedInfo);
        }

        private void CloseSocket()
        {
            try { _stream?.Dispose(); } catch { }
            try { _tcpClient?.Close(); } catch { }
            _stream = null;
            _tcpClient = null;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _ = StopAsync();
        }
    }
}
