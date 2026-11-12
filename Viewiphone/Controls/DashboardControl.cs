using Viewiphone.Services;

namespace Viewiphone.Controls
{
    public class DashboardControl : UserControl
    {
        private readonly DeviceManager _deviceManager;
        private readonly TableLayoutPanel _gridPanel;
        private readonly Dictionary<string, DeviceCard> _cards = new Dictionary<string, DeviceCard>();
        private DeviceCard? _maximizedCard = null;

        public int GridPreset { get; set; } = 0; // 0 = Auto, 1, 2, 4, 6, 9, 16

        public event Action<DeviceCard>? SettingsRequested;

        public DashboardControl(DeviceManager deviceManager)
        {
            _deviceManager = deviceManager;

            BackColor = Color.FromArgb(18, 18, 22);
            Dock = DockStyle.Fill;

            _gridPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 18, 22),
                Padding = new Padding(4),
                Margin = new Padding(0)
            };

            Controls.Add(_gridPanel);

            WireDeviceManagerEvents();
            ReloadAllDevices();
        }

        private void WireDeviceManagerEvents()
        {
            _deviceManager.DeviceAdded += OnDeviceAdded;
            _deviceManager.DeviceRemoved += OnDeviceRemoved;
        }

        public void ReloadAllDevices()
        {
            _gridPanel.SuspendLayout();
            _gridPanel.Controls.Clear();
            _cards.Clear();
            _maximizedCard = null;

            foreach (var session in _deviceManager.Sessions)
            {
                var card = CreateDeviceCard(session);
                _cards[session.Config.Id] = card;
            }

            RecalculateGridLayout();
            _gridPanel.ResumeLayout(true);
        }

        private void OnDeviceAdded(DeviceSession session)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnDeviceAdded(session)));
                return;
            }

            if (!_cards.ContainsKey(session.Config.Id))
            {
                var card = CreateDeviceCard(session);
                _cards[session.Config.Id] = card;
                RecalculateGridLayout();
            }
        }

        private void OnDeviceRemoved(string deviceId)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnDeviceRemoved(deviceId)));
                return;
            }

            if (_cards.TryGetValue(deviceId, out var card))
            {
                if (_maximizedCard == card)
                {
                    _maximizedCard = null;
                }

                _cards.Remove(deviceId);
                _gridPanel.Controls.Remove(card);
                card.Dispose();
                RecalculateGridLayout();
            }
        }

        private DeviceCard CreateDeviceCard(DeviceSession session)
        {
            var card = new DeviceCard(session)
            {
                Dock = DockStyle.Fill
            };

            card.MaximizeRequested += OnCardMaximizeRequested;
            card.SettingsRequested += (c) => SettingsRequested?.Invoke(c);

            return card;
        }

        private void OnCardMaximizeRequested(DeviceCard card)
        {
            if (_maximizedCard == card)
            {
                // Restore grid
                _maximizedCard = null;
                card.IsMaximized = false;
            }
            else
            {
                // Maximize this card
                _maximizedCard = card;
                card.IsMaximized = true;
            }

            RecalculateGridLayout();
        }

        public void SetGridLayout(int preset)
        {
            GridPreset = preset;
            _maximizedCard = null;
            RecalculateGridLayout();
        }

        private void RecalculateGridLayout()
        {
            _gridPanel.SuspendLayout();
            _gridPanel.Controls.Clear();
            _gridPanel.RowStyles.Clear();
            _gridPanel.ColumnStyles.Clear();

            if (_maximizedCard != null)
            {
                // Single maximized device
                _gridPanel.ColumnCount = 1;
                _gridPanel.RowCount = 1;
                _gridPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                _gridPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                _gridPanel.Controls.Add(_maximizedCard, 0, 0);
                _gridPanel.ResumeLayout(true);
                return;
            }

            int count = _cards.Count;
            if (count == 0)
            {
                _gridPanel.ColumnCount = 1;
                _gridPanel.RowCount = 1;
                _gridPanel.ResumeLayout(true);
                return;
            }

            int cols, rows;
            if (GridPreset > 0)
            {
                (cols, rows) = GridPreset switch
                {
                    1 => (1, 1),
                    2 => (2, 1),
                    4 => (2, 2),
                    6 => (3, 2),
                    9 => (3, 3),
                    16 => (4, 4),
                    _ => CalculateAutoGrid(count)
                };
            }
            else
            {
                (cols, rows) = CalculateAutoGrid(count);
            }

            _gridPanel.ColumnCount = cols;
            _gridPanel.RowCount = rows;

            for (int i = 0; i < cols; i++)
            {
                _gridPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cols));
            }

            for (int j = 0; j < rows; j++)
            {
                _gridPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
            }

            int cardIdx = 0;
            foreach (var card in _cards.Values)
            {
                int c = cardIdx % cols;
                int r = cardIdx / cols;
                if (r >= rows) break; // Exceeds grid capacity for chosen preset

                _gridPanel.Controls.Add(card, c, r);
                cardIdx++;
            }

            _gridPanel.ResumeLayout(true);
        }

        private static (int cols, int rows) CalculateAutoGrid(int count)
        {
            if (count <= 1) return (1, 1);
            if (count == 2) return (2, 1);
            if (count <= 4) return (2, 2);
            if (count <= 6) return (3, 2);
            if (count <= 9) return (3, 3);
            return (4, 4); // Up to 16
        }
    }
}
