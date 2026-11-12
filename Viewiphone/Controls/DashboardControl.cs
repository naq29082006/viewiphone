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
            _gridPanel.Resize += (s, e) => UpdateCardSizes();

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
                Dock = DockStyle.None,
                Anchor = AnchorStyles.None,
                Margin = new Padding(2)
            };

            card.MaximizeRequested += OnCardMaximizeRequested;
            card.SettingsRequested += (c) => SettingsRequested?.Invoke(c);
            card.AspectRatioChanged += (c) =>
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    try { BeginInvoke(new Action(UpdateCardSizes)); } catch { }
                }
            };

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
                UpdateCardSizes();
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
                    3 => (3, 1),
                    4 => (4, 1),
                    5 => (2, 2),
                    6 => (3, 2),
                    7 => (4, 2),
                    8 => (5, 2),
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

            UpdateCardSizes();
            _gridPanel.ResumeLayout(true);
        }

        private void UpdateCardSizes()
        {
            if (_cards.Count == 0 || _gridPanel.ColumnCount == 0 || _gridPanel.RowCount == 0) return;

            int cols = _gridPanel.ColumnCount;
            int rows = _gridPanel.RowCount;

            int cellW = Math.Max(50, (_gridPanel.ClientSize.Width / cols) - 8);
            int cellH = Math.Max(80, (_gridPanel.ClientSize.Height / rows) - 8);

            const int extraH = 62; // Header (32) + Footer (26) + Margins/Border (4)

            foreach (var card in _cards.Values)
            {
                if (_maximizedCard != null && card != _maximizedCard) continue;

                float ratio = card.AspectRatio; // e.g. 9/16 = 0.5625

                // Fit by height first
                int targetH = cellH;
                int targetVideoH = Math.Max(20, targetH - extraH);
                int targetW = (int)(targetVideoH * ratio) + 2;

                // If wider than cell, fit by cell width
                if (targetW > cellW)
                {
                    targetW = cellW;
                    int targetVideoW = Math.Max(20, targetW - 2);
                    targetH = Math.Min(cellH, (int)(targetVideoW / ratio) + extraH);
                }

                card.Size = new Size(Math.Max(120, targetW), Math.Max(100, targetH));
            }
        }

        private static (int cols, int rows) CalculateAutoGrid(int count)
        {
            if (count <= 1) return (1, 1);
            if (count == 2) return (2, 1);
            if (count == 3) return (3, 1);
            if (count == 4) return (4, 1);
            if (count <= 6) return (3, 2);
            if (count <= 8) return (4, 2);
            if (count <= 10) return (5, 2);
            if (count <= 12) return (4, 3);
            return (4, 4); // Up to 16
        }
    }
}
