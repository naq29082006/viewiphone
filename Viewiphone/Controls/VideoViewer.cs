using System.Drawing.Drawing2D;
using Viewiphone.Models;

namespace Viewiphone.Controls
{
    public class VideoViewer : Control
    {
        private Bitmap? _currentFrame;
        private readonly object _frameLock = new object();

        public double DisplayFps { get; set; }
        public double DisplayBitrateKbps { get; set; }
        public double DisplayLatencyMs { get; set; }
        public DeviceConnectionStatus ConnectionStatus { get; set; } = DeviceConnectionStatus.Disconnected;
        public string StatusMessage { get; set; } = "Offline";

        private readonly Font _overlayFont = new Font("Segoe UI", 9f, FontStyle.Bold);
        private readonly Brush _overlayBgBrush = new SolidBrush(Color.FromArgb(180, 20, 20, 20));
        private readonly Brush _overlayTextBrush = new SolidBrush(Color.White);
        private readonly Brush _statusBrush = new SolidBrush(Color.LightGray);

        public VideoViewer()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Opaque,
                true);

            BackColor = Color.FromArgb(18, 18, 22);
            UpdateStyles();
        }

        public void SetFrame(Bitmap? newFrame)
        {
            if (newFrame == null) return;

            Bitmap? oldFrame;
            lock (_frameLock)
            {
                oldFrame = _currentFrame;
                _currentFrame = newFrame;
            }

            oldFrame?.Dispose();

            // Request UI redraw
            if (IsHandleCreated && !IsDisposed)
            {
                try
                {
                    BeginInvoke(new Action(Invalidate));
                }
                catch { }
            }
        }

        public void ClearFrame()
        {
            Bitmap? oldFrame;
            lock (_frameLock)
            {
                oldFrame = _currentFrame;
                _currentFrame = null;
            }
            oldFrame?.Dispose();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            // Fill background
            g.Clear(BackColor);

            Bitmap? frameToDraw = null;
            lock (_frameLock)
            {
                if (_currentFrame != null)
                {
                    frameToDraw = (Bitmap)_currentFrame.Clone();
                }
            }

            if (frameToDraw != null)
            {
                try
                {
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    g.PixelOffsetMode = PixelOffsetMode.HighSpeed;

                    // Calculate aspect ratio preserving destination rectangle
                    float srcRatio = (float)frameToDraw.Width / frameToDraw.Height;
                    float dstRatio = (float)Width / Height;

                    Rectangle destRect;
                    if (dstRatio > srcRatio)
                    {
                        // Pillarboxed (bars on left/right)
                        int targetW = (int)(Height * srcRatio);
                        int x = (Width - targetW) / 2;
                        destRect = new Rectangle(x, 0, targetW, Height);
                    }
                    else
                    {
                        // Letterboxed (bars on top/bottom)
                        int targetH = (int)(Width / srcRatio);
                        int y = (Height - targetH) / 2;
                        destRect = new Rectangle(0, y, Width, targetH);
                    }

                    g.DrawImage(frameToDraw, destRect);

                    // Note: OSD overlay is now displayed in the DeviceCard footer
                    // to prevent obstructing the phone screen (dock icons, keypad, etc.)
                }
                finally
                {
                    frameToDraw.Dispose();
                }
            }
            else
            {
                // Draw Standby / Status screen
                DrawStandbyScreen(g);
            }
        }

        private void DrawOsdOverlay(Graphics g, int streamW, int streamH)
        {
            string osdText = $"{DisplayFps:0.0} FPS | {DisplayBitrateKbps / 1000.0:0.00} Mbps | {streamW}x{streamH}";
            SizeF textSize = g.MeasureString(osdText, _overlayFont);

            Rectangle overlayRect = new Rectangle(8, Height - (int)textSize.Height - 12, (int)textSize.Width + 12, (int)textSize.Height + 6);
            using (var path = GetRoundedRectPath(overlayRect, 4))
            {
                g.FillPath(_overlayBgBrush, path);
            }

            g.DrawString(osdText, _overlayFont, _overlayTextBrush, overlayRect.X + 6, overlayRect.Y + 3);
        }

        private void DrawStandbyScreen(Graphics g)
        {
            string title = ConnectionStatus switch
            {
                DeviceConnectionStatus.Disconnected => "NO SIGNAL",
                DeviceConnectionStatus.Connecting => "CONNECTING...",
                DeviceConnectionStatus.Handshaking => "HANDSHAKING...",
                DeviceConnectionStatus.Authenticating => "AUTHENTICATING...",
                DeviceConnectionStatus.Streaming => "WAITING FOR KEYFRAME...",
                DeviceConnectionStatus.Reconnecting => "RECONNECTING...",
                DeviceConnectionStatus.Error => "CONNECTION ERROR",
                _ => "STANDBY"
            };

            using var titleFont = new Font("Segoe UI", 12f, FontStyle.Bold);
            using var msgFont = new Font("Segoe UI", 9f, FontStyle.Regular);

            SizeF titleSize = g.MeasureString(title, titleFont);
            SizeF msgSize = g.MeasureString(StatusMessage, msgFont);

            float totalH = titleSize.Height + msgSize.Height + 10;
            float startY = (Height - totalH) / 2;

            Color statusColor = ConnectionStatus switch
            {
                DeviceConnectionStatus.Streaming => Color.FromArgb(46, 204, 113),
                DeviceConnectionStatus.Connecting or DeviceConnectionStatus.Handshaking or DeviceConnectionStatus.Authenticating => Color.FromArgb(241, 196, 15),
                DeviceConnectionStatus.Reconnecting => Color.FromArgb(230, 126, 34),
                DeviceConnectionStatus.Error => Color.FromArgb(231, 76, 60),
                _ => Color.FromArgb(149, 165, 166)
            };

            using var titleBrush = new SolidBrush(statusColor);
            g.DrawString(title, titleFont, titleBrush, (Width - titleSize.Width) / 2, startY);
            g.DrawString(StatusMessage, msgFont, _statusBrush, (Width - msgSize.Width) / 2, startY + titleSize.Height + 6);
        }

        private static GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearFrame();
                _overlayFont.Dispose();
                _overlayBgBrush.Dispose();
                _overlayTextBrush.Dispose();
                _statusBrush.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
