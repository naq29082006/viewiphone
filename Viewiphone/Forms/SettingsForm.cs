using Viewiphone.Models;
using Viewiphone.Services;

namespace Viewiphone.Forms
{
    public class SettingsForm : Form
    {
        private readonly DeviceSession? _targetSession;
        public StreamConfig ResultConfig { get; private set; }

        private ComboBox _cboResolution = null!;
        private ComboBox _cboFps = null!;
        private ComboBox _cboBitrate = null!;
        private ComboBox _cboScale = null!;
        private CheckBox _chkHwAccel = null!;
        private Button _btnApplyToAgent = null!;
        private Button _btnForceKeyframe = null!;
        private Button _btnSave = null!;
        private Button _btnCancel = null!;

        public SettingsForm(DeviceSession? session = null)
        {
            _targetSession = session;
            ResultConfig = session?.Config.StreamSettings ?? new StreamConfig();
            InitializeComponent();
            PopulateData();
        }

        private void InitializeComponent()
        {
            Text = _targetSession == null ? "Global Stream Settings" : $"Settings - {_targetSession.Config.Name}";
            Size = new Size(460, 400);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(28, 28, 34);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            int labelX = 24;
            int inputX = 170;
            int inputW = 230;
            int y = 24;
            int spacing = 44;

            // Resolution Preset
            var lblRes = new Label { Text = "Resolution Preset:", Location = new Point(labelX, y + 4), AutoSize = true };
            _cboResolution = new ComboBox
            {
                Location = new Point(inputX, y),
                Width = inputW,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(40, 40, 50),
                ForeColor = Color.White
            };
            _cboResolution.Items.AddRange(new object[] { "480p (Fast / Low Bandwidth)", "720p (Recommended)", "1080p (High Quality)" });
            Controls.Add(lblRes);
            Controls.Add(_cboResolution);

            y += spacing;

            // Target FPS
            var lblFps = new Label { Text = "Target Frame Rate:", Location = new Point(labelX, y + 4), AutoSize = true };
            _cboFps = new ComboBox
            {
                Location = new Point(inputX, y),
                Width = inputW,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(40, 40, 50),
                ForeColor = Color.White
            };
            _cboFps.Items.AddRange(new object[] { "10 FPS (Low CPU / Farm)", "15 FPS (Default Smooth)", "20 FPS", "30 FPS (Full Realtime)" });
            Controls.Add(lblFps);
            Controls.Add(_cboFps);

            y += spacing;

            // Target Bitrate
            var lblBitrate = new Label { Text = "Target Bitrate:", Location = new Point(labelX, y + 4), AutoSize = true };
            _cboBitrate = new ComboBox
            {
                Location = new Point(inputX, y),
                Width = inputW,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(40, 40, 50),
                ForeColor = Color.White
            };
            _cboBitrate.Items.AddRange(new object[] { "500 Kbps (0.5 Mbps)", "1000 Kbps (1.0 Mbps)", "1500 Kbps (1.5 Mbps)", "2500 Kbps (2.5 Mbps)", "4000 Kbps (4.0 Mbps)" });
            Controls.Add(lblBitrate);
            Controls.Add(_cboBitrate);

            y += spacing;

            // Scale
            var lblScale = new Label { Text = "Display Scale:", Location = new Point(labelX, y + 4), AutoSize = true };
            _cboScale = new ComboBox
            {
                Location = new Point(inputX, y),
                Width = inputW,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(40, 40, 50),
                ForeColor = Color.White
            };
            _cboScale.Items.AddRange(new object[] { "30% (Compact)", "40% (Medium)", "50% (Recommended)", "75% (Large)", "100% (Native)" });
            Controls.Add(lblScale);
            Controls.Add(_cboScale);

            y += spacing;

            // Hardware Acceleration
            _chkHwAccel = new CheckBox
            {
                Text = "Hardware Accelerated Decoding (VideoToolbox / DXVA)",
                Location = new Point(labelX, y),
                AutoSize = true,
                Checked = true
            };
            Controls.Add(_chkHwAccel);

            y += 44;

            // Realtime agent action buttons
            if (_targetSession != null)
            {
                _btnApplyToAgent = new Button
                {
                    Text = "⚡ Apply to Agent Now",
                    Location = new Point(labelX, y),
                    Size = new Size(180, 32),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(41, 128, 185),
                    ForeColor = Color.White,
                    Cursor = Cursors.Hand
                };
                _btnApplyToAgent.FlatAppearance.BorderSize = 0;
                _btnApplyToAgent.Click += OnApplyToAgentClicked;
                Controls.Add(_btnApplyToAgent);

                _btnForceKeyframe = new Button
                {
                    Text = "🔄 Request Keyframe",
                    Location = new Point(labelX + 190, y),
                    Size = new Size(180, 32),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(52, 73, 94),
                    ForeColor = Color.White,
                    Cursor = Cursors.Hand
                };
                _btnForceKeyframe.FlatAppearance.BorderSize = 0;
                _btnForceKeyframe.Click += OnForceKeyframeClicked;
                Controls.Add(_btnForceKeyframe);

                y += 44;
            }

            // Save & Cancel buttons
            _btnSave = new Button
            {
                Text = "Save",
                Location = new Point(220, y),
                Size = new Size(100, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(39, 174, 96),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += OnSaveClicked;
            Controls.Add(_btnSave);

            _btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(330, y),
                Size = new Size(80, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 70),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
            Controls.Add(_btnCancel);
        }

        private void PopulateData()
        {
            _cboResolution.SelectedIndex = ResultConfig.ResolutionPreset switch
            {
                "480p" => 0,
                "1080p" => 2,
                _ => 1 // 720p
            };

            _cboFps.SelectedIndex = ResultConfig.TargetFps switch
            {
                10 => 0,
                20 => 2,
                30 => 3,
                _ => 1 // 15
            };

            _cboBitrate.SelectedIndex = ResultConfig.TargetBitrate switch
            {
                500000 => 0,
                1000000 => 1,
                2500000 => 3,
                4000000 => 4,
                _ => 2 // 1500000
            };

            _cboScale.SelectedIndex = ResultConfig.ScalePercentage switch
            {
                30 => 0,
                40 => 1,
                75 => 3,
                100 => 4,
                _ => 2 // 50%
            };

            _chkHwAccel.Checked = ResultConfig.HardwareAcceleration;
        }

        private async void OnApplyToAgentClicked(object? sender, EventArgs e)
        {
            if (_targetSession == null) return;

            ushort fps = GetSelectedFps();
            ushort scale = GetSelectedScale();
            uint bitrate = GetSelectedBitrate();

            await _targetSession.TcpClient.SendConfigAsync(fps, scale, bitrate, requestKeyframe: true);
            MessageBox.Show("Configuration dispatched to iPhone Agent!", "Applied", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async void OnForceKeyframeClicked(object? sender, EventArgs e)
        {
            if (_targetSession == null) return;

            await _targetSession.TcpClient.SendConfigAsync(0, 0, 0, requestKeyframe: true);
        }

        private void OnSaveClicked(object? sender, EventArgs e)
        {
            ResultConfig.TargetFps = GetSelectedFps();
            ResultConfig.ScalePercentage = GetSelectedScale();
            ResultConfig.TargetBitrate = (int)GetSelectedBitrate();
            ResultConfig.ResolutionPreset = _cboResolution.SelectedIndex switch
            {
                0 => "480p",
                2 => "1080p",
                _ => "720p"
            };
            ResultConfig.HardwareAcceleration = _chkHwAccel.Checked;

            DialogResult = DialogResult.OK;
        }

        private ushort GetSelectedFps() => _cboFps.SelectedIndex switch
        {
            0 => 10,
            2 => 20,
            3 => 30,
            _ => 15
        };

        private ushort GetSelectedScale() => _cboScale.SelectedIndex switch
        {
            0 => 30,
            1 => 40,
            3 => 75,
            4 => 100,
            _ => 50
        };

        private uint GetSelectedBitrate() => _cboBitrate.SelectedIndex switch
        {
            0 => 500000,
            1 => 1000000,
            3 => 2500000,
            4 => 4000000,
            _ => 1500000
        };
    }
}
