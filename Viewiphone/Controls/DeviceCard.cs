using Viewiphone.Models;
using Viewiphone.Services;

namespace Viewiphone.Controls
{
    public class DeviceCard : UserControl
    {
        public DeviceSession Session { get; }
        public VideoViewer Viewer { get; private set; } = null!;

        public event Action<DeviceCard>? MaximizeRequested;
        public event Action<DeviceCard>? SettingsRequested;
        public event Action<DeviceCard>? AspectRatioChanged;

        private float _lastAspect = 0f;

        private Panel _headerPanel = null!;
        private Panel _footerPanel = null!;
        private Label _lblDeviceName = null!;
        private Label _lblStatusPill = null!;
        private Label _lblStats = null!;
        private Button _btnToggleConnect = null!;
        private Button _btnSettings = null!;
        private Button _btnMaximize = null!;
        private ToolTip _toolTip = null!;

        public bool IsMaximized { get; set; }

        public float AspectRatio
        {
            get
            {
                int w = Session.Decoder.DecodedWidth > 0 ? Session.Decoder.DecodedWidth : (int)(Session.Info?.StreamWidth ?? 620);
                int h = Session.Decoder.DecodedHeight > 0 ? Session.Decoder.DecodedHeight : (int)(Session.Info?.StreamHeight ?? 1104);
                if (w > 0 && h > 0)
                {
                    return (float)w / h;
                }
                return 9f / 16f; // Standard portrait iPhone aspect ratio
            }
        }

        public DeviceCard(DeviceSession session)
        {
            Session = session;
            InitializeComponent();
            WireSessionEvents();
            UpdateUiState();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            BackColor = Color.FromArgb(28, 28, 34);
            Padding = new Padding(1);
            Margin = new Padding(4);
            _toolTip = new ToolTip();

            // Header Panel (Height: 32px)
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Color.FromArgb(34, 34, 42),
                Padding = new Padding(6, 4, 6, 4)
            };

            _btnMaximize = new Button
            {
                Text = "⛶",
                Size = new Size(24, 24),
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(50, 50, 60),
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8f, FontStyle.Regular),
                Margin = new Padding(2, 0, 0, 0)
            };
            _btnMaximize.FlatAppearance.BorderSize = 0;
            _btnMaximize.Click += (s, e) => MaximizeRequested?.Invoke(this);
            _toolTip.SetToolTip(_btnMaximize, "Maximize / Restore");

            _btnSettings = new Button
            {
                Text = "⚙",
                Size = new Size(24, 24),
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(50, 50, 60),
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                Margin = new Padding(2, 0, 0, 0)
            };
            _btnSettings.FlatAppearance.BorderSize = 0;
            _btnSettings.Click += (s, e) => SettingsRequested?.Invoke(this);
            _toolTip.SetToolTip(_btnSettings, "Stream & Quality Settings");

            _btnToggleConnect = new Button
            {
                Text = "Connect",
                Size = new Size(54, 24),
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(41, 128, 185),
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Margin = new Padding(2, 0, 2, 0)
            };
            _btnToggleConnect.FlatAppearance.BorderSize = 0;
            _btnToggleConnect.Click += OnToggleConnectClicked;

            var leftFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                Padding = new Padding(2, 3, 0, 0),
                Margin = new Padding(0)
            };

            _lblDeviceName = new Label
            {
                Text = Session.Config.Name,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 1, 4, 0)
            };

            _lblStatusPill = new Label
            {
                Text = "●",
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 1, 0, 0)
            };

            leftFlow.Controls.Add(_lblDeviceName);
            leftFlow.Controls.Add(_lblStatusPill);
            leftFlow.DoubleClick += (s, e) => MaximizeRequested?.Invoke(this);

            _headerPanel.Controls.Add(leftFlow);
            _headerPanel.Controls.Add(_btnToggleConnect);
            _headerPanel.Controls.Add(_btnSettings);
            _headerPanel.Controls.Add(_btnMaximize);

            // Footer Panel for stats & controls (Height: 26px)
            _footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                BackColor = Color.FromArgb(24, 24, 30),
                Padding = new Padding(4, 2, 4, 2)
            };

            _lblStats = new Label
            {
                Text = "● Offline",
                ForeColor = Color.FromArgb(160, 205, 235),
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true
            };
            _lblStats.DoubleClick += (s, e) => MaximizeRequested?.Invoke(this);
            _footerPanel.DoubleClick += (s, e) => MaximizeRequested?.Invoke(this);
            _footerPanel.Controls.Add(_lblStats);

            // Video Viewer (Clean, unobstructed)
            Viewer = new VideoViewer
            {
                Dock = DockStyle.Fill
            };
            Viewer.DoubleClick += (s, e) => MaximizeRequested?.Invoke(this);
            _headerPanel.DoubleClick += (s, e) => MaximizeRequested?.Invoke(this);

            Controls.Add(Viewer);
            Controls.Add(_footerPanel);
            Controls.Add(_headerPanel);

            ResumeLayout(false);
        }

        private void WireSessionEvents()
        {
            Session.StatusUpdated += (status, msg) =>
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    BeginInvoke(new Action(UpdateUiState));
                }
            };

            Session.StatsUpdated += (fps, br, lat) =>
            {
                Viewer.DisplayFps = fps;
                Viewer.DisplayBitrateKbps = br;
                Viewer.DisplayLatencyMs = lat;

                if (IsHandleCreated && !IsDisposed)
                {
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            int w = Session.Decoder.DecodedWidth > 0 ? Session.Decoder.DecodedWidth : (int)(Session.Info?.StreamWidth ?? 0);
                            int h = Session.Decoder.DecodedHeight > 0 ? Session.Decoder.DecodedHeight : (int)(Session.Info?.StreamHeight ?? 0);
                            string resStr = (w > 0 && h > 0) ? $"{w}x{h}" : "--";
                            _lblStats.Text = $"⚡ {fps:0.0} FPS   📶 {br / 1000.0:0.00} Mbps   📐 {resStr}";
                        }));
                    }
                    catch { }
                }
            };

            Session.FrameReady += (bmp) =>
            {
                Viewer.SetFrame(bmp);
                if (bmp.Width > 0 && bmp.Height > 0)
                {
                    float curAspect = (float)bmp.Width / bmp.Height;
                    if (Math.Abs(curAspect - _lastAspect) > 0.01f)
                    {
                        _lastAspect = curAspect;
                        if (IsHandleCreated && !IsDisposed)
                        {
                            try { BeginInvoke(new Action(() => AspectRatioChanged?.Invoke(this))); } catch { }
                        }
                    }
                }
            };
        }

        public void UpdateUiState()
        {
            _lblDeviceName.Text = Session.Config.Name;
            _toolTip.SetToolTip(_lblDeviceName, $"{Session.Config.Name} ({Session.Config.IpAddress}:{Session.Config.Port})");
            Viewer.ConnectionStatus = Session.Status;
            Viewer.StatusMessage = Session.StatusMessage;

            switch (Session.Status)
            {
                case DeviceConnectionStatus.Streaming:
                    _lblStatusPill.Text = "●";
                    _lblStatusPill.ForeColor = Color.FromArgb(46, 204, 113);
                    _toolTip.SetToolTip(_lblStatusPill, "Streaming live video");
                    _btnToggleConnect.Text = "Stop";
                    _btnToggleConnect.BackColor = Color.FromArgb(192, 57, 43);
                    break;

                case DeviceConnectionStatus.Connecting:
                case DeviceConnectionStatus.Handshaking:
                case DeviceConnectionStatus.Authenticating:
                    _lblStatusPill.Text = "●";
                    _lblStatusPill.ForeColor = Color.FromArgb(241, 196, 15);
                    _toolTip.SetToolTip(_lblStatusPill, $"Connecting: {Session.StatusMessage}");
                    _btnToggleConnect.Text = "Cancel";
                    _btnToggleConnect.BackColor = Color.FromArgb(127, 140, 141);
                    _lblStats.Text = "Connecting...";
                    break;

                case DeviceConnectionStatus.Reconnecting:
                    _lblStatusPill.Text = "●";
                    _lblStatusPill.ForeColor = Color.FromArgb(230, 126, 34);
                    _toolTip.SetToolTip(_lblStatusPill, "Reconnecting...");
                    _btnToggleConnect.Text = "Stop";
                    _btnToggleConnect.BackColor = Color.FromArgb(192, 57, 43);
                    _lblStats.Text = "Reconnecting in 3s...";
                    break;

                case DeviceConnectionStatus.Error:
                    _lblStatusPill.Text = "●";
                    _lblStatusPill.ForeColor = Color.FromArgb(231, 76, 60);
                    _toolTip.SetToolTip(_lblStatusPill, $"Error: {Session.StatusMessage}");
                    _btnToggleConnect.Text = "Retry";
                    _btnToggleConnect.BackColor = Color.FromArgb(41, 128, 185);
                    _lblStats.Text = "⚠️ Connection Error";
                    break;

                default:
                    _lblStatusPill.Text = "●";
                    _lblStatusPill.ForeColor = Color.FromArgb(149, 165, 166);
                    _toolTip.SetToolTip(_lblStatusPill, "Disconnected");
                    _btnToggleConnect.Text = "Connect";
                    _btnToggleConnect.BackColor = Color.FromArgb(41, 128, 185);
                    _lblStats.Text = "● Disconnected";
                    break;
            }

            Viewer.Invalidate();
        }

        private async void OnToggleConnectClicked(object? sender, EventArgs e)
        {
            if (Session.Status == DeviceConnectionStatus.Streaming ||
                Session.Status == DeviceConnectionStatus.Connecting ||
                Session.Status == DeviceConnectionStatus.Handshaking ||
                Session.Status == DeviceConnectionStatus.Authenticating ||
                Session.Status == DeviceConnectionStatus.Reconnecting)
            {
                await Session.DisconnectAsync();
                Viewer.ClearFrame();
            }
            else
            {
                Session.Connect();
            }
        }
    }
}
