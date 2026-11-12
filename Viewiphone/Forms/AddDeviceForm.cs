using Viewiphone.Models;
using Viewiphone.Services;

namespace Viewiphone.Forms
{
    public class AddDeviceForm : Form
    {
        public DeviceConfig? ResultConfig { get; private set; }
        public bool ConnectImmediately { get; private set; }
        public bool IsDeleteRequested { get; private set; }

        private TextBox _txtName = null!;
        private TextBox _txtIp = null!;
        private NumericUpDown _numPort = null!;
        private TextBox _txtToken = null!;
        private CheckBox _chkAutoConnect = null!;
        private Label _lblTestResult = null!;
        private Button _btnTest = null!;
        private Button _btnSave = null!;
        private Button _btnConnect = null!;
        private Button _btnDelete = null!;
        private Button _btnCancel = null!;

        private readonly DeviceConfig? _existingConfig;

        public AddDeviceForm(DeviceConfig? existingConfig = null)
        {
            _existingConfig = existingConfig;
            InitializeComponent();
            PopulateData();
        }

        private void InitializeComponent()
        {
            Text = _existingConfig == null ? "Add iPhone Device" : "Edit iPhone Device";
            Size = new Size(520, 480);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(28, 28, 34);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            int labelX = 24;
            int inputX = 160;
            int inputW = 310;
            int y = 24;
            int spacing = 44;

            // Device Name
            var lblName = new Label { Text = "Device Name:", Location = new Point(labelX, y + 4), AutoSize = true };
            _txtName = new TextBox { Location = new Point(inputX, y), Width = inputW, BackColor = Color.FromArgb(40, 40, 50), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            Controls.Add(lblName);
            Controls.Add(_txtName);

            y += spacing;

            // IP Address
            var lblIp = new Label { Text = "IP Address:", Location = new Point(labelX, y + 4), AutoSize = true };
            _txtIp = new TextBox { Location = new Point(inputX, y), Width = inputW, BackColor = Color.FromArgb(40, 40, 50), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            Controls.Add(lblIp);
            Controls.Add(_txtIp);

            y += spacing;

            // Port
            var lblPort = new Label { Text = "Port:", Location = new Point(labelX, y + 4), AutoSize = true };
            _numPort = new NumericUpDown
            {
                Location = new Point(inputX, y),
                Width = 120,
                Minimum = 1,
                Maximum = 65535,
                Value = 5000,
                BackColor = Color.FromArgb(40, 40, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(lblPort);
            Controls.Add(_numPort);

            y += spacing;

            // Token
            var lblToken = new Label { Text = "Auth Token:", Location = new Point(labelX, y + 4), AutoSize = true };
            _txtToken = new TextBox
            {
                Location = new Point(inputX, y),
                Width = inputW,
                PasswordChar = '●',
                BackColor = Color.FromArgb(40, 40, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(lblToken);
            Controls.Add(_txtToken);

            y += spacing;

            // Auto Connect Checkbox
            _chkAutoConnect = new CheckBox
            {
                Text = "Auto-connect on application launch",
                Location = new Point(inputX, y),
                AutoSize = true,
                Checked = true
            };
            Controls.Add(_chkAutoConnect);

            y += spacing;

            // Test Connection Button
            _btnTest = new Button
            {
                Text = "🔍 Test Connection",
                Location = new Point(inputX, y),
                Size = new Size(160, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 73, 94),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            _btnTest.FlatAppearance.BorderSize = 0;
            _btnTest.Click += OnTestClicked;
            Controls.Add(_btnTest);

            y += 40;

            // Test Result Box
            _lblTestResult = new Label
            {
                Text = "Click 'Test Connection' to verify network and agent token.",
                Location = new Point(labelX, y),
                Size = new Size(inputW + inputX - labelX, 48),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9f, FontStyle.Italic)
            };
            Controls.Add(_lblTestResult);

            y += 60;

            // Bottom Buttons
            _btnSave = new Button
            {
                Text = "Save Device",
                Location = new Point(230, y),
                Size = new Size(110, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(39, 174, 96),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += OnSaveClicked;
            Controls.Add(_btnSave);

            _btnConnect = new Button
            {
                Text = "Connect Now",
                Location = new Point(350, y),
                Size = new Size(120, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(41, 128, 185),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnConnect.FlatAppearance.BorderSize = 0;
            _btnConnect.Click += OnConnectClicked;
            Controls.Add(_btnConnect);

            if (_existingConfig != null)
            {
                _btnDelete = new Button
                {
                    Text = "Delete",
                    Location = new Point(labelX, y),
                    Size = new Size(80, 36),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(192, 57, 43),
                    ForeColor = Color.White,
                    Cursor = Cursors.Hand
                };
                _btnDelete.FlatAppearance.BorderSize = 0;
                _btnDelete.Click += OnDeleteClicked;
                Controls.Add(_btnDelete);
            }

            _btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(_existingConfig != null ? 114 : labelX, y),
                Size = new Size(80, 36),
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
            if (_existingConfig != null)
            {
                _txtName.Text = _existingConfig.Name;
                _txtIp.Text = _existingConfig.IpAddress;
                _numPort.Value = _existingConfig.Port;
                _txtToken.Text = _existingConfig.Token;
                _chkAutoConnect.Checked = _existingConfig.AutoConnect;
            }
            else
            {
                _txtName.Text = "iPhone 01";
                _txtIp.Text = "192.168.1.100";
                _numPort.Value = 5000;
                _txtToken.Text = "viewiphone_secret";
                _chkAutoConnect.Checked = true;
            }
        }

        private async void OnTestClicked(object? sender, EventArgs e)
        {
            string ip = _txtIp.Text.Trim();
            int port = (int)_numPort.Value;
            string token = _txtToken.Text.Trim();

            if (string.IsNullOrEmpty(ip))
            {
                _lblTestResult.Text = "⚠️ Please enter an IP address.";
                _lblTestResult.ForeColor = Color.FromArgb(231, 76, 60);
                return;
            }

            _btnTest.Enabled = false;
            _lblTestResult.Text = "⏳ Testing TCP connection and validating token with Agent...";
            _lblTestResult.ForeColor = Color.FromArgb(241, 196, 15);

            var (success, info, message) = await TcpClientService.TestConnectionAsync(ip, port, token);

            _btnTest.Enabled = true;
            if (success)
            {
                _lblTestResult.Text = $"✅ {message}";
                _lblTestResult.ForeColor = Color.FromArgb(46, 204, 113);
                if (info != null && _txtName.Text == "iPhone 01")
                {
                    _txtName.Text = info.DeviceName;
                }
            }
            else
            {
                _lblTestResult.Text = $"❌ {message}";
                _lblTestResult.ForeColor = Color.FromArgb(231, 76, 60);
            }
        }

        private void OnSaveClicked(object? sender, EventArgs e)
        {
            if (!ValidateInputs()) return;

            ResultConfig = CreateConfigFromInputs();
            ConnectImmediately = false;
            DialogResult = DialogResult.OK;
        }

        private void OnConnectClicked(object? sender, EventArgs e)
        {
            if (!ValidateInputs()) return;

            ResultConfig = CreateConfigFromInputs();
            ConnectImmediately = true;
            DialogResult = DialogResult.OK;
        }

        private void OnDeleteClicked(object? sender, EventArgs e)
        {
            var res = MessageBox.Show(
                $"Are you sure you want to delete '{_existingConfig?.Name}'?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (res == DialogResult.Yes)
            {
                IsDeleteRequested = true;
                DialogResult = DialogResult.OK;
            }
        }

        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text))
            {
                MessageBox.Show("Please enter a device name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_txtIp.Text))
            {
                MessageBox.Show("Please enter a valid IP address.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private DeviceConfig CreateConfigFromInputs()
        {
            return new DeviceConfig
            {
                Id = _existingConfig?.Id ?? Guid.NewGuid().ToString("N"),
                Name = _txtName.Text.Trim(),
                IpAddress = _txtIp.Text.Trim(),
                Port = (int)_numPort.Value,
                Token = _txtToken.Text.Trim(),
                AutoConnect = _chkAutoConnect.Checked,
                StreamSettings = _existingConfig?.StreamSettings ?? new StreamConfig()
            };
        }
    }
}
