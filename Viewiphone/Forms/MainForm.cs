using Serilog;
using Viewiphone.Controls;
using Viewiphone.Models;
using Viewiphone.Services;

namespace Viewiphone.Forms
{
    public class MainForm : Form
    {
        private readonly ConfigurationService _configService;
        private readonly DeviceManager _deviceManager;

        private Panel _sidebarPanel = null!;
        private Panel _headerPanel = null!;
        private Panel _contentPanel = null!;

        private Label _lblStatTotal = null!;
        private Label _lblStatOnline = null!;
        private Label _lblStatStreaming = null!;
        private Label _lblStatOffline = null!;

        private Button _btnNavDashboard = null!;
        private Button _btnNavDevices = null!;
        private Button _btnNavLogs = null!;
        private Button _btnNavSettings = null!;
        private Button _btnAddDevice = null!;

        private ComboBox _cboGridLayout = null!;
        private Button _btnConnectAll = null!;
        private Button _btnDisconnectAll = null!;

        private DashboardControl _dashboardControl = null!;
        private Panel _devicesListPanel = null!;
        private TextBox _txtLogsView = null!;

        public MainForm()
        {
            _configService = new ConfigurationService();
            _deviceManager = new DeviceManager(_configService);

            InitializeComponent();
            SetupDashboard();
            SetupDevicesList();
            SetupLogsView();

            _deviceManager.GlobalStatsChanged += OnGlobalStatsChanged;
            _deviceManager.Initialize();

            ShowView("Dashboard");
        }

        private void InitializeComponent()
        {
            Text = "ViewiPhone - Jailbreak Multi Screen Manager (H.264 Real-time)";
            Size = new Size(1280, 800);
            MinimumSize = new Size(960, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(18, 18, 22);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            // 1. Sidebar Panel
            _sidebarPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 200,
                BackColor = Color.FromArgb(24, 24, 30),
                Padding = new Padding(8)
            };

            var lblLogo = new Label
            {
                Text = "📱 ViewiPhone",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 152, 219),
                Dock = DockStyle.Top,
                Height = 48,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };
            _sidebarPanel.Controls.Add(lblLogo);

            _btnNavDashboard = CreateSidebarButton("📺 Dashboard", () => ShowView("Dashboard"));
            _btnNavDevices = CreateSidebarButton("📋 Devices", () => ShowView("Devices"));
            _btnNavLogs = CreateSidebarButton("📜 Logs", () => ShowView("Logs"));
            _btnNavSettings = CreateSidebarButton("⚙ Settings", () => ShowView("Settings"));

            _btnAddDevice = new Button
            {
                Text = "➕ Add Device",
                Dock = DockStyle.Top,
                Height = 42,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(39, 174, 96),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 8, 0, 8)
            };
            _btnAddDevice.FlatAppearance.BorderSize = 0;
            _btnAddDevice.Click += (s, e) => ShowAddDeviceDialog();

            _sidebarPanel.Controls.Add(_btnNavSettings);
            _sidebarPanel.Controls.Add(_btnNavLogs);
            _sidebarPanel.Controls.Add(_btnNavDevices);
            _sidebarPanel.Controls.Add(_btnNavDashboard);
            _sidebarPanel.Controls.Add(_btnAddDevice);

            // 2. Header Panel
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(32, 32, 40),
                Padding = new Padding(16, 12, 16, 12)
            };

            // Stat pills
            _lblStatTotal = CreateStatPill("Total: 0", Color.FromArgb(149, 165, 166));
            _lblStatOnline = CreateStatPill("Online: 0", Color.FromArgb(52, 152, 219));
            _lblStatStreaming = CreateStatPill("Streaming: 0", Color.FromArgb(46, 204, 113));
            _lblStatOffline = CreateStatPill("Offline: 0", Color.FromArgb(231, 76, 60));

            var statsFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight
            };
            statsFlow.Controls.Add(_lblStatTotal);
            statsFlow.Controls.Add(_lblStatOnline);
            statsFlow.Controls.Add(_lblStatStreaming);
            statsFlow.Controls.Add(_lblStatOffline);

            _headerPanel.Controls.Add(statsFlow);

            // Right header controls (Grid layout & Actions)
            var rightFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight
            };

            var lblGrid = new Label { Text = "Grid:", AutoSize = true, Margin = new Padding(0, 6, 4, 0), ForeColor = Color.LightGray };
            _cboGridLayout = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 140,
                BackColor = Color.FromArgb(45, 45, 55),
                ForeColor = Color.White
            };
            _cboGridLayout.Items.AddRange(new object[] {
                "Auto (Phone Ratio)",
                "1 Phone (1x1)",
                "2 Phones (2x1)",
                "3 Phones (3x1)",
                "4 Phones (4x1)",
                "4 Phones (2x2)",
                "6 Phones (3x2)",
                "8 Phones (4x2)",
                "10 Phones (5x2)",
                "9 Phones (3x3)",
                "16 Phones (4x4)"
            });
            _cboGridLayout.SelectedIndex = 0;
            _cboGridLayout.SelectedIndexChanged += OnGridLayoutChanged;

            _btnConnectAll = new Button
            {
                Text = "▶ Connect All",
                Size = new Size(110, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(41, 128, 185),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            _btnConnectAll.FlatAppearance.BorderSize = 0;
            _btnConnectAll.Click += (s, e) => _deviceManager.ConnectAll();

            _btnDisconnectAll = new Button
            {
                Text = "⏹ Disconnect All",
                Size = new Size(120, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(192, 57, 43),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            _btnDisconnectAll.FlatAppearance.BorderSize = 0;
            _btnDisconnectAll.Click += async (s, e) => await _deviceManager.DisconnectAllAsync();

            rightFlow.Controls.Add(lblGrid);
            rightFlow.Controls.Add(_cboGridLayout);
            rightFlow.Controls.Add(_btnConnectAll);
            rightFlow.Controls.Add(_btnDisconnectAll);

            _headerPanel.Controls.Add(rightFlow);

            // 3. Content Panel
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 18, 22)
            };

            Controls.Add(_contentPanel);
            Controls.Add(_headerPanel);
            Controls.Add(_sidebarPanel);
        }

        private Button CreateSidebarButton(string text, Action onClick)
        {
            var btn = new Button
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 44,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(24, 24, 30),
                ForeColor = Color.LightGray,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private Label CreateStatPill(string text, Color color)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                BackColor = Color.FromArgb(40, color.R / 4, color.G / 4, color.B / 4),
                ForeColor = color,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Padding = new Padding(8, 4, 8, 4),
                Margin = new Padding(4, 2, 4, 2)
            };
        }

        private void SetupDashboard()
        {
            _dashboardControl = new DashboardControl(_deviceManager);
            _dashboardControl.SettingsRequested += (card) =>
            {
                using var form = new SettingsForm(card.Session);
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    card.Session.Config.StreamSettings = form.ResultConfig;
                    _deviceManager.UpdateDevice(card.Session.Config);
                }
            };
        }

        private void SetupDevicesList()
        {
            _devicesListPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(22, 22, 28),
                Padding = new Padding(24)
            };
        }

        private void SetupLogsView()
        {
            _txtLogsView = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(14, 14, 18),
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 10f, FontStyle.Regular),
                BorderStyle = BorderStyle.None,
                Padding = new Padding(12)
            };
        }

        private void ShowView(string viewName)
        {
            _contentPanel.Controls.Clear();

            _btnNavDashboard.BackColor = Color.FromArgb(24, 24, 30);
            _btnNavDevices.BackColor = Color.FromArgb(24, 24, 30);
            _btnNavLogs.BackColor = Color.FromArgb(24, 24, 30);
            _btnNavSettings.BackColor = Color.FromArgb(24, 24, 30);

            switch (viewName)
            {
                case "Dashboard":
                    _btnNavDashboard.BackColor = Color.FromArgb(40, 40, 50);
                    _contentPanel.Controls.Add(_dashboardControl);
                    break;

                case "Devices":
                    _btnNavDevices.BackColor = Color.FromArgb(40, 40, 50);
                    RenderDevicesList();
                    _contentPanel.Controls.Add(_devicesListPanel);
                    break;

                case "Logs":
                    _btnNavLogs.BackColor = Color.FromArgb(40, 40, 50);
                    LoadRecentLogs();
                    _contentPanel.Controls.Add(_txtLogsView);
                    break;

                case "Settings":
                    _btnNavSettings.BackColor = Color.FromArgb(40, 40, 50);
                    using (var form = new SettingsForm(null))
                    {
                        form.ShowDialog(this);
                    }
                    ShowView("Dashboard");
                    break;
            }
        }

        private void RenderDevicesList()
        {
            _devicesListPanel.Controls.Clear();

            var titleLabel = new Label
            {
                Text = "Managed iPhone Devices",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                Height = 36
            };
            _devicesListPanel.Controls.Add(titleLabel);

            var listFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0, 12, 0, 0)
            };

            foreach (var session in _deviceManager.Sessions)
            {
                var rowPanel = new Panel
                {
                    Width = 800,
                    Height = 60,
                    BackColor = Color.FromArgb(32, 32, 40),
                    Margin = new Padding(0, 0, 0, 10),
                    Padding = new Padding(16, 12, 16, 12)
                };

                var lblInfo = new Label
                {
                    Text = $"{session.Config.Name}   |   {session.Config.IpAddress}:{session.Config.Port}   |   Status: {session.Status}",
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    AutoSize = true,
                    Location = new Point(16, 18)
                };
                rowPanel.Controls.Add(lblInfo);

                var btnEdit = new Button
                {
                    Text = "Edit",
                    Size = new Size(70, 30),
                    Location = new Point(620, 14),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(52, 73, 94),
                    ForeColor = Color.White,
                    Cursor = Cursors.Hand
                };
                btnEdit.FlatAppearance.BorderSize = 0;
                btnEdit.Click += (s, e) => EditDevice(session.Config);
                rowPanel.Controls.Add(btnEdit);

                var btnDel = new Button
                {
                    Text = "Delete",
                    Size = new Size(70, 30),
                    Location = new Point(700, 14),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(192, 57, 43),
                    ForeColor = Color.White,
                    Cursor = Cursors.Hand
                };
                btnDel.FlatAppearance.BorderSize = 0;
                btnDel.Click += async (s, e) =>
                {
                    await _deviceManager.RemoveDeviceAsync(session.Config.Id);
                    RenderDevicesList();
                };
                rowPanel.Controls.Add(btnDel);

                listFlow.Controls.Add(rowPanel);
            }

            _devicesListPanel.Controls.Add(listFlow);
        }

        private void ShowAddDeviceDialog()
        {
            using var form = new AddDeviceForm();
            if (form.ShowDialog(this) == DialogResult.OK && form.ResultConfig != null)
            {
                var session = _deviceManager.AddDevice(form.ResultConfig);
                if (form.ConnectImmediately)
                {
                    session.Connect();
                }
                ShowView("Dashboard");
            }
        }

        private async void EditDevice(DeviceConfig config)
        {
            using var form = new AddDeviceForm(config);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                if (form.IsDeleteRequested)
                {
                    await _deviceManager.RemoveDeviceAsync(config.Id);
                }
                else if (form.ResultConfig != null)
                {
                    _deviceManager.UpdateDevice(form.ResultConfig);
                    if (form.ConnectImmediately)
                    {
                        _deviceManager.ConnectDevice(config.Id);
                    }
                }
                RenderDevicesList();
            }
        }

        private void LoadRecentLogs()
        {
            try
            {
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (Directory.Exists(logDir))
                {
                    var latestLog = Directory.GetFiles(logDir, "*.log").OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
                    if (latestLog != null)
                    {
                        using var fs = new FileStream(latestLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var reader = new StreamReader(fs);
                        _txtLogsView.Text = reader.ReadToEnd();
                        _txtLogsView.SelectionStart = _txtLogsView.Text.Length;
                        _txtLogsView.ScrollToCaret();
                        return;
                    }
                }
                _txtLogsView.Text = "No log files found yet.";
            }
            catch (Exception ex)
            {
                _txtLogsView.Text = $"Failed to read logs: {ex.Message}";
            }
        }

        private void OnGlobalStatsChanged(int total, int online, int streaming, int offline)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnGlobalStatsChanged(total, online, streaming, offline)));
                return;
            }

            _lblStatTotal.Text = $"Total: {total}";
            _lblStatOnline.Text = $"Online: {online}";
            _lblStatStreaming.Text = $"Streaming: {streaming}";
            _lblStatOffline.Text = $"Offline: {offline}";
        }

        private void OnGridLayoutChanged(object? sender, EventArgs e)
        {
            int preset = _cboGridLayout.SelectedIndex switch
            {
                1 => 1,  // 1x1
                2 => 2,  // 2x1
                3 => 3,  // 3x1
                4 => 4,  // 4x1
                5 => 5,  // 2x2
                6 => 6,  // 3x2
                7 => 7,  // 4x2
                8 => 8,  // 5x2
                9 => 9,  // 3x3
                10 => 16,// 4x4
                _ => 0   // Auto
            };
            _dashboardControl.SetGridLayout(preset);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            _deviceManager.Dispose();
            LoggingService.CloseAndFlush();
        }
    }
}
