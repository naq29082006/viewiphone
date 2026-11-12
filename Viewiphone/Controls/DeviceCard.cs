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

        private Panel _headerPanel = null!;
        private Label _lblDeviceName = null!;
        private Label _lblStatusPill = null!;
        private Button _btnToggleConnect = null!;
        private Button _btnSettings = null!;
        private Button _btnMaximize = null!;

        public bool IsMaximized { get; set; }

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

            // Header Panel
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Color.FromArgb(36, 36, 44),
                Padding = new Padding(8, 4, 8, 4)
            };

            _lblDeviceName = new Label
            {
                Text = $"{Session.Config.Name} ({Session.Config.IpAddress})",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(8, 8)
            };

            _lblStatusPill = new Label
            {
                Text = "● Disconnected",
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(220, 9)
            };

            _btnMaximize = new Button
            {
                Text = "⛶",
                Size = new Size(28, 26),
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(50, 50, 60),
                Cursor = Cursors.Hand
            };
            _btnMaximize.FlatAppearance.BorderSize = 0;
            _btnMaximize.Click += (s, e) => MaximizeRequested?.Invoke(this);

            _btnSettings = new Button
            {
                Text = "⚙",
                Size = new Size(28, 26),
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(50, 50, 60),
                Cursor = Cursors.Hand
            };
            _btnSettings.FlatAppearance.BorderSize = 0;
            _btnSettings.Click += (s, e) => SettingsRequested?.Invoke(this);

            _btnToggleConnect = new Button
            {
                Text = "Connect",
                Size = new Size(70, 26),
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(41, 128, 185),
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            _btnToggleConnect.FlatAppearance.BorderSize = 0;
            _btnToggleConnect.Click += OnToggleConnectClicked;

            _headerPanel.Controls.Add(_lblDeviceName);
            _headerPanel.Controls.Add(_lblStatusPill);
            _headerPanel.Controls.Add(_btnToggleConnect);
            _headerPanel.Controls.Add(_btnSettings);
            _headerPanel.Controls.Add(_btnMaximize);

            // Video Viewer
            Viewer = new VideoViewer
            {
                Dock = DockStyle.Fill
            };
            Viewer.DoubleClick += (s, e) => MaximizeRequested?.Invoke(this);
            _headerPanel.DoubleClick += (s, e) => MaximizeRequested?.Invoke(this);

            Controls.Add(Viewer);
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
            };

            Session.FrameReady += (bmp) =>
            {
                Viewer.SetFrame(bmp);
            };
        }

        public void UpdateUiState()
        {
            _lblDeviceName.Text = $"{Session.Config.Name} ({Session.Config.IpAddress}:{Session.Config.Port})";
            Viewer.ConnectionStatus = Session.Status;
            Viewer.StatusMessage = Session.StatusMessage;

            switch (Session.Status)
            {
                case DeviceConnectionStatus.Streaming:
                    _lblStatusPill.Text = "● Streaming";
                    _lblStatusPill.ForeColor = Color.FromArgb(46, 204, 113);
                    _btnToggleConnect.Text = "Stop";
                    _btnToggleConnect.BackColor = Color.FromArgb(192, 57, 43);
                    break;

                case DeviceConnectionStatus.Connecting:
                case DeviceConnectionStatus.Handshaking:
                case DeviceConnectionStatus.Authenticating:
                    _lblStatusPill.Text = "● Connecting...";
                    _lblStatusPill.ForeColor = Color.FromArgb(241, 196, 15);
                    _btnToggleConnect.Text = "Cancel";
                    _btnToggleConnect.BackColor = Color.FromArgb(127, 140, 141);
                    break;

                case DeviceConnectionStatus.Reconnecting:
                    _lblStatusPill.Text = "● Reconnecting";
                    _lblStatusPill.ForeColor = Color.FromArgb(230, 126, 34);
                    _btnToggleConnect.Text = "Stop";
                    _btnToggleConnect.BackColor = Color.FromArgb(192, 57, 43);
                    break;

                case DeviceConnectionStatus.Error:
                    _lblStatusPill.Text = "● Error";
                    _lblStatusPill.ForeColor = Color.FromArgb(231, 76, 60);
                    _btnToggleConnect.Text = "Retry";
                    _btnToggleConnect.BackColor = Color.FromArgb(41, 128, 185);
                    break;

                default:
                    _lblStatusPill.Text = "● Disconnected";
                    _lblStatusPill.ForeColor = Color.FromArgb(149, 165, 166);
                    _btnToggleConnect.Text = "Connect";
                    _btnToggleConnect.BackColor = Color.FromArgb(41, 128, 185);
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
