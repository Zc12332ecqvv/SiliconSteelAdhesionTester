using System;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using SiliconSteelAdhesionTester.Models;
using SiliconSteelAdhesionTester.Services.Plc;

namespace SiliconSteelAdhesionTester.Forms
{
    [System.ComponentModel.DesignerCategory("Form")]
    public partial class DebugForm : Form
    {
        private readonly IPlcService _plc;
        private readonly CancellationToken _token;
        private readonly GroupBox _grpStations = new GroupBox();
        private readonly Label[] _stationStepLabels = new Label[4];
        private readonly Label[] _stationStateLabels = new Label[4];
        private readonly Panel[] _stationRunLamps = new Panel[4];
        private readonly Panel[] _stationReadyLamps = new Panel[4];
        private readonly Panel[] _stationDoneLamps = new Panel[4];

        public DebugForm()
        {
            InitializeComponent();
        }

        public DebugForm(IPlcService plc, UserSession user, CancellationToken token)
            : this()
        {
            _plc = plc ?? throw new ArgumentNullException(nameof(plc));
            _token = token;
            if (user == null) throw new ArgumentNullException(nameof(user));
            ConfigureRuntimeUi();
            Text = "调试诊断 - " + user.DisplayName;
            btnRead.Click += async (s, e) => await ExecuteSafe(ReadValueAsync);
            btnOn.Click += async (s, e) => await ExecuteSafe(() => WriteValueAsync(true));
            btnOff.Click += async (s, e) => await ExecuteSafe(() => WriteValueAsync(false));
            btnS2Scan.Click += (s, e) => txtAddress.Text = PlcAddresses.S2ScanAllowed;
            btnS2Camera.Click += (s, e) => txtAddress.Text = PlcAddresses.S2SecondPhotoAllowed;
            btnS4Camera.Click += (s, e) => txtAddress.Text = PlcAddresses.S4CameraAllowed;
            _plc.SnapshotChanged += PlcSnapshotChanged;
            FormClosed += (s, e) => _plc.SnapshotChanged -= PlcSnapshotChanged;
        }

        private void ConfigureRuntimeUi()
        {
            MinimumSize = new Size(900, 650);
            AutoScroll = true;
            string[] steps = { "AGV送料", "S1来料", "读取二维码", "二维码校验", "相机拍照", "视觉分类", "工位加工", "等待来料" };
            for (int i = 0; i < steps.Length; i++)
            {
                Label label = new Label
                {
                    Location = new System.Drawing.Point(16 + i * 126, 42),
                    Size = new System.Drawing.Size(112, 88),
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                    BackColor = i == 0 ? System.Drawing.Color.DodgerBlue : System.Drawing.Color.WhiteSmoke,
                    ForeColor = i == 0 ? System.Drawing.Color.White : System.Drawing.Color.FromArgb(45, 55, 65),
                    Text = (i + 1) + "\r\n" + steps[i]
                };
                grpFlow.Controls.Add(label);
            }

            SetupButton(btnRead, "读取", 462, 45, 90, System.Drawing.Color.SteelBlue);
            SetupButton(btnOn, "置 ON", 565, 45, 90, System.Drawing.Color.SeaGreen);
            SetupButton(btnOff, "置 OFF", 668, 45, 90, System.Drawing.Color.Firebrick);
            SetupButton(btnS2Scan, "S2 扫码允许", 28, 104, 160, System.Drawing.Color.FromArgb(73, 94, 116));
            SetupButton(btnS2Camera, "S2 拍照允许", 202, 104, 160, System.Drawing.Color.FromArgb(73, 94, 116));
            SetupButton(btnS4Camera, "S4 拍照允许", 376, 104, 160, System.Drawing.Color.FromArgb(73, 94, 116));
            BuildStationDebugPanel();
            Controls.Add(_grpStations);
            Resize += (s, e) => ApplyResponsiveLayout();
            Shown += (s, e) => ApplyResponsiveLayout();
        }

        private void BuildStationDebugPanel()
        {
            _grpStations.Text = "四工位运行与单步调试";
            _grpStations.BackColor = Color.White;

            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 8,
                RowCount = 5
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 8));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 7));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 7));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 7));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            for (int i = 1; i < 5; i++)
                table.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

            string[] headers = { "工位", "步骤", "状态", "运行", "就绪", "完成", "连续/单步", "单步启动" };
            for (int column = 0; column < headers.Length; column++)
                table.Controls.Add(NewCellLabel(headers[column], true), column, 0);

            for (int index = 0; index < 4; index++)
            {
                int station = index + 1;
                table.Controls.Add(NewCellLabel("S" + station, true), 0, station);
                _stationStepLabels[index] = NewCellLabel("0 步", false);
                _stationStateLabels[index] = NewCellLabel("等待PLC状态", false);
                _stationRunLamps[index] = NewLamp();
                _stationReadyLamps[index] = NewLamp();
                _stationDoneLamps[index] = NewLamp();
                table.Controls.Add(_stationStepLabels[index], 1, station);
                table.Controls.Add(_stationStateLabels[index], 2, station);
                table.Controls.Add(WrapLamp(_stationRunLamps[index]), 3, station);
                table.Controls.Add(WrapLamp(_stationReadyLamps[index]), 4, station);
                table.Controls.Add(WrapLamp(_stationDoneLamps[index]), 5, station);

                Button continuous = NewStationButton("S" + station + " 连续");
                Button start = NewStationButton("S" + station + " 启动");
                continuous.Click += async (s, e) => await ExecuteSafe(async () =>
                {
                    string address = PlcAddresses.StationContinuous(station);
                    bool current = Convert.ToBoolean(await _plc.ReadAsync(address, _token));
                    await _plc.WriteAsync(address, !current, _token);
                });
                start.Click += async (s, e) =>
                    await ExecuteSafe(() => _plc.PulseAsync(PlcAddresses.StationStart(station), _token));
                table.Controls.Add(continuous, 6, station);
                table.Controls.Add(start, 7, station);
            }
            _grpStations.Controls.Add(table);
        }

        private void ApplyResponsiveLayout()
        {
            int width = Math.Max(760, ClientSize.Width - 44);
            grpFlow.Location = new Point(22, pnlHeader.Bottom + 18);
            grpFlow.Size = new Size(width, 180);
            _grpStations.Location = new Point(22, grpFlow.Bottom + 14);
            _grpStations.Size = new Size(width, 330);
            grpRegister.Location = new Point(22, _grpStations.Bottom + 14);
            grpRegister.Size = new Size(width, Math.Max(230, ClientSize.Height - grpRegister.Top - 22));
        }

        private void PlcSnapshotChanged(object sender, PlcSnapshot snapshot)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<object, PlcSnapshot>(PlcSnapshotChanged), sender, snapshot);
                return;
            }
            if (snapshot.Stations == null) return;
            foreach (StationSnapshot station in snapshot.Stations)
            {
                int index = station.Number - 1;
                if (index < 0 || index >= 4) continue;
                _stationStepLabels[index].Text = station.Step + " 步";
                _stationStateLabels[index].Text = station.Fault ? "故障" :
                    station.Done ? "完成" :
                    station.Running ? "运行中" :
                    station.Ready ? "已就绪" : "等待";
                _stationRunLamps[index].BackColor = station.Running ? Color.LimeGreen : Color.WhiteSmoke;
                _stationReadyLamps[index].BackColor = station.Ready ? Color.LimeGreen : Color.WhiteSmoke;
                _stationDoneLamps[index].BackColor = station.Done ? Color.LimeGreen : Color.WhiteSmoke;
            }
        }

        private static Label NewCellLabel(string text, bool bold)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                BackColor = bold ? Color.FromArgb(232, 239, 247) : Color.FromArgb(247, 249, 252),
                Font = new Font("Microsoft YaHei UI", 9.5F, bold ? FontStyle.Bold : FontStyle.Regular),
                Margin = new Padding(2)
            };
        }

        private static Panel NewLamp()
        {
            return new Panel { Size = new Size(22, 22), BackColor = Color.WhiteSmoke };
        }

        private static Control WrapLamp(Panel lamp)
        {
            Panel holder = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            lamp.Anchor = AnchorStyles.None;
            holder.Controls.Add(lamp);
            holder.Resize += (s, e) =>
                lamp.Location = new Point((holder.Width - lamp.Width) / 2, (holder.Height - lamp.Height) / 2);
            return holder;
        }

        private static Button NewStationButton(string text)
        {
            return new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 12, 6, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White
            };
        }

        private static void SetupButton(Button button, string text, int x, int y, int width, System.Drawing.Color color)
        {
            button.Text = text;
            button.Location = new System.Drawing.Point(x, y);
            button.Size = new System.Drawing.Size(width, 36);
            button.BackColor = color;
            button.ForeColor = System.Drawing.Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
        }

        private async Task ExecuteSafe(Func<Task> action)
        {
            try { await action(); lblResult.Text = "操作成功  " + DateTime.Now.ToString("HH:mm:ss"); }
            catch (Exception ex) { lblResult.Text = "操作失败：" + ex.Message; MessageBox.Show(ex.Message, "PLC操作失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private async Task ReadValueAsync() { txtValue.Text = Convert.ToString(await _plc.ReadAsync(txtAddress.Text.Trim(), _token)); }
        private async Task WriteValueAsync(bool value) { await _plc.WriteAsync(txtAddress.Text.Trim(), value, _token); await ReadValueAsync(); }
    }
}
