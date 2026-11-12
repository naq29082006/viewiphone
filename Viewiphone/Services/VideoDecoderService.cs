using System.Collections.Concurrent;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using Serilog;
using Viewiphone.Models;

namespace Viewiphone.Services
{
    public unsafe class VideoDecoderService : IDisposable
    {
        public string DeviceId { get; }
        public bool IsRunning { get; private set; }
        public bool IsInitialized { get; private set; }
        public int DecodedWidth { get; private set; }
        public int DecodedHeight { get; private set; }

        public event Action<Bitmap, uint, ulong>? FrameDecoded;
        public event Action<string>? DecoderError;

        private readonly ConcurrentQueue<VideoFrame> _frameQueue = new ConcurrentQueue<VideoFrame>();
        private readonly AutoResetEvent _frameEvent = new AutoResetEvent(false);
        private Thread? _decodeThread;
        private CancellationTokenSource? _cts;

        private AVCodecContext* _codecContext = null;
        private AVCodecParserContext* _parserContext = null;
        private AVFrame* _decodedFrame = null;
        private AVFrame* _rgbFrame = null;
        private AVPacket* _packet = null;
        private SwsContext* _swsContext = null;
        private byte* _rgbBuffer = null;
        private int _rgbBufferSize = 0;

        private byte[]? _cachedSps;
        private byte[]? _cachedPps;
        private bool _isDisposed;
        private readonly object _decoderLock = new object();

        private static bool? _isFFmpegAvailable;
        private static readonly object _initLock = new object();

        public VideoDecoderService(string deviceId)
        {
            DeviceId = deviceId;
        }

        public static bool CheckFFmpegAvailable()
        {
            lock (_initLock)
            {
                if (_isFFmpegAvailable.HasValue)
                    return _isFFmpegAvailable.Value;

                try
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string ffmpegDir = Path.Combine(baseDir, "ffmpeg");
                    if (Directory.Exists(ffmpegDir))
                    {
                        ffmpeg.RootPath = ffmpegDir;
                    }
                    else
                    {
                        ffmpeg.RootPath = baseDir;
                    }

                    // Probe an avcodec call
                    uint version = ffmpeg.avcodec_version();
                    Log.Information("FFmpeg native libraries loaded successfully. avcodec version: {Version}", version);
                    _isFFmpegAvailable = true;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "FFmpeg native libraries (avcodec-*.dll) not found or failed to load. Software decoding will be simulated/notified.");
                    _isFFmpegAvailable = false;
                }

                return _isFFmpegAvailable.Value;
            }
        }

        public bool Start()
        {
            lock (_decoderLock)
            {
                if (IsRunning) return true;

                if (!CheckFFmpegAvailable())
                {
                    Log.Warning("[{DeviceId}] Cannot start VideoDecoderService: FFmpeg native binaries missing", DeviceId);
                    DecoderError?.Invoke("FFmpeg DLLs not found in application directory.");
                    return false;
                }

                if (!InitializeFFmpegDecoder())
                {
                    return false;
                }

                _cts = new CancellationTokenSource();
                _decodeThread = new Thread(DecodeLoop)
                {
                    Name = $"DecoderThread_{DeviceId}",
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal
                };

                IsRunning = true;
                _decodeThread.Start();
                Log.Information("[{DeviceId}] VideoDecoderService started successfully", DeviceId);
                return true;
            }
        }

        private bool InitializeFFmpegDecoder()
        {
            try
            {
                var codec = ffmpeg.avcodec_find_decoder(AVCodecID.AV_CODEC_ID_H264);
                if (codec == null)
                {
                    Log.Error("[{DeviceId}] H.264 decoder not found in FFmpeg", DeviceId);
                    return false;
                }

                _codecContext = ffmpeg.avcodec_alloc_context3(codec);
                if (_codecContext == null)
                {
                    Log.Error("[{DeviceId}] Failed to allocate AVCodecContext", DeviceId);
                    return false;
                }

                _parserContext = ffmpeg.av_parser_init(AVCodecID.AV_CODEC_ID_H264);

                _codecContext->flags |= ffmpeg.AV_CODEC_FLAG_LOW_DELAY;
                _codecContext->flags2 |= ffmpeg.AV_CODEC_FLAG2_FAST;
                _codecContext->thread_count = 1; // Single thread per iPhone stream for lowest latency

                int ret = ffmpeg.avcodec_open2(_codecContext, codec, null);
                if (ret < 0)
                {
                    Log.Error("[{DeviceId}] avcodec_open2 failed with code {Code}", DeviceId, ret);
                    CleanupDecoderResources();
                    return false;
                }

                _decodedFrame = ffmpeg.av_frame_alloc();
                _rgbFrame = ffmpeg.av_frame_alloc();
                _packet = ffmpeg.av_packet_alloc();

                IsInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[{DeviceId}] Exception during FFmpeg decoder initialization", DeviceId);
                CleanupDecoderResources();
                return false;
            }
        }

        public void ConfigureSpsPps(byte[] sps, byte[] pps)
        {
            _cachedSps = sps;
            _cachedPps = pps;

            // Feed SPS & PPS to decoder as Annex-B NALUs
            if (sps.Length > 0 && pps.Length > 0)
            {
                byte[] configPayload = new byte[4 + sps.Length + 4 + pps.Length];
                configPayload[0] = 0; configPayload[1] = 0; configPayload[2] = 0; configPayload[3] = 1;
                Buffer.BlockCopy(sps, 0, configPayload, 4, sps.Length);

                int ppsOffset = 4 + sps.Length;
                configPayload[ppsOffset] = 0; configPayload[ppsOffset + 1] = 0; configPayload[ppsOffset + 2] = 0; configPayload[ppsOffset + 3] = 1;
                Buffer.BlockCopy(pps, 0, configPayload, ppsOffset + 4, pps.Length);

                EnqueueFrame(new VideoFrame
                {
                    IsKeyframe = true,
                    Data = configPayload,
                    FrameId = 0,
                    TimestampUs = 0
                });
            }
        }

        public void EnqueueFrame(VideoFrame frame)
        {
            if (!IsRunning)
            {
                Start();
            }

            // Real-time Queue Control:
            // If queue is backing up (> 3 frames), drop non-keyframes to maintain zero-latency
            if (_frameQueue.Count > 3 && !frame.IsKeyframe)
            {
                // Discard stale P-frame
                return;
            }

            _frameQueue.Enqueue(frame);
            _frameEvent.Set();
        }

        private void DecodeLoop()
        {
            var ct = _cts?.Token ?? CancellationToken.None;

            while (!ct.IsCancellationRequested && IsRunning)
            {
                _frameEvent.WaitOne(50);

                while (_frameQueue.TryDequeue(out var frame))
                {
                    if (ct.IsCancellationRequested) break;
                    DecodeFrameData(frame);
                }
            }
        }

        private void DecodeFrameData(VideoFrame frame)
        {
            if (!IsInitialized || _codecContext == null || _packet == null) return;

            try
            {
                fixed (byte* dataPtr = frame.Data)
                {
                    ffmpeg.av_packet_unref(_packet);
                    _packet->data = dataPtr;
                    _packet->size = frame.Data.Length;

                    int sendRet = ffmpeg.avcodec_send_packet(_codecContext, _packet);
                    if (sendRet < 0) return;

                    while (true)
                    {
                        int recvRet = ffmpeg.avcodec_receive_frame(_codecContext, _decodedFrame);
                        if (recvRet == ffmpeg.AVERROR(ffmpeg.EAGAIN) || recvRet == ffmpeg.AVERROR_EOF)
                        {
                            break;
                        }
                        if (recvRet < 0)
                        {
                            break;
                        }

                        // Successfully received decoded YUV frame, convert to BGRA Bitmap
                        RenderDecodedFrame(_decodedFrame, frame.FrameId, frame.TimestampUs);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[{DeviceId}] Error decoding frame #{FrameId}", DeviceId, frame.FrameId);
            }
        }

        private void RenderDecodedFrame(AVFrame* srcFrame, uint frameId, ulong timestampUs)
        {
            int width = srcFrame->width;
            int height = srcFrame->height;

            if (width <= 0 || height <= 0) return;

            DecodedWidth = width;
            DecodedHeight = height;

            // Setup or resize SwsContext if resolution changes
            EnsureSwsContext(width, height, (AVPixelFormat)srcFrame->format);

            if (_swsContext == null || _rgbBuffer == null) return;

            byte_ptrArray8 srcData = srcFrame->data;
            int_array8 srcLinesize = srcFrame->linesize;

            byte_ptrArray8 dstData = new byte_ptrArray8();
            dstData[0] = _rgbBuffer;
            int_array8 dstLinesize = new int_array8();
            dstLinesize[0] = width * 4;

            ffmpeg.sws_scale(
                _swsContext,
                srcData,
                srcLinesize,
                0,
                height,
                dstData,
                dstLinesize
            );

            // Create Bitmap from 32-bit BGRA buffer
            var bitmap = new Bitmap(
                width,
                height,
                width * 4,
                PixelFormat.Format32bppRgb,
                (IntPtr)_rgbBuffer
            );

            // Clone bitmap so caller owns its memory lifecycle
            Bitmap clonedBitmap = new Bitmap(bitmap);
            bitmap.Dispose();

            FrameDecoded?.Invoke(clonedBitmap, frameId, timestampUs);
        }

        private void EnsureSwsContext(int width, int height, AVPixelFormat srcFormat)
        {
            if (_swsContext == null || DecodedWidth != width || DecodedHeight != height)
            {
                if (_swsContext != null)
                {
                    ffmpeg.sws_freeContext(_swsContext);
                    _swsContext = null;
                }

                if (_rgbBuffer != null)
                {
                    Marshal.FreeHGlobal((IntPtr)_rgbBuffer);
                    _rgbBuffer = null;
                }

                _swsContext = ffmpeg.sws_getContext(
                    width,
                    height,
                    srcFormat,
                    width,
                    height,
                    AVPixelFormat.AV_PIX_FMT_BGRA,
                    1, // SWS_FAST_BILINEAR
                    null,
                    null,
                    null
                );

                _rgbBufferSize = width * height * 4;
                _rgbBuffer = (byte*)Marshal.AllocHGlobal(_rgbBufferSize);
            }
        }

        private void CleanupDecoderResources()
        {
            lock (_decoderLock)
            {
                IsInitialized = false;

                if (_parserContext != null)
                {
                    ffmpeg.av_parser_close(_parserContext);
                    _parserContext = null;
                }

                if (_codecContext != null)
                {
                    fixed (AVCodecContext** ctx = &_codecContext)
                    {
                        ffmpeg.avcodec_free_context(ctx);
                    }
                    _codecContext = null;
                }

                if (_decodedFrame != null)
                {
                    fixed (AVFrame** f = &_decodedFrame)
                    {
                        ffmpeg.av_frame_free(f);
                    }
                    _decodedFrame = null;
                }

                if (_rgbFrame != null)
                {
                    fixed (AVFrame** f = &_rgbFrame)
                    {
                        ffmpeg.av_frame_free(f);
                    }
                    _rgbFrame = null;
                }

                if (_packet != null)
                {
                    fixed (AVPacket** p = &_packet)
                    {
                        ffmpeg.av_packet_free(p);
                    }
                    _packet = null;
                }

                if (_swsContext != null)
                {
                    ffmpeg.sws_freeContext(_swsContext);
                    _swsContext = null;
                }

                if (_rgbBuffer != null)
                {
                    Marshal.FreeHGlobal((IntPtr)_rgbBuffer);
                    _rgbBuffer = null;
                }
            }
        }

        public void Stop()
        {
            lock (_decoderLock)
            {
                if (!IsRunning) return;
                IsRunning = false;

                _cts?.Cancel();
                _frameEvent.Set();

                if (_decodeThread != null && _decodeThread.IsAlive)
                {
                    _decodeThread.Join(500);
                    _decodeThread = null;
                }

                while (_frameQueue.TryDequeue(out _)) { }

                CleanupDecoderResources();
                Log.Information("[{DeviceId}] VideoDecoderService stopped", DeviceId);
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Stop();
            _frameEvent.Dispose();
            _cts?.Dispose();
        }
    }
}
