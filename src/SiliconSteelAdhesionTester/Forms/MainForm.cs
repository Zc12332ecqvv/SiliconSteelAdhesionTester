using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SiliconSteelAdhesionTester.Configuration;
using SiliconSteelAdhesionTester.Data;
using SiliconSteelAdhesionTester.Models;
using SiliconSteelAdhesionTester.Services.Plc;
using SiliconSteelAdhesionTester.Services.Scanner;

namespace SiliconSteelAdhesionTester.Forms
{
    [System.ComponentModel.DesignerCategory("Form")]
    public partial class MainForm : Form
    {
        private readonly UserSession _user;
        private readonly DatabaseService _database;
        private readonly IPlcService _plc;
        private readonly AppSettings _settings;
        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();
        private Panel[] _runLamps;
        private Panel[] _readyLamps;
        private Panel[] _doneLamps;
        private Label[] _stationStatuses;
        private Label[] _stationSteps;
        private Button[] _stationStarts;
        private Button[] _stationContinuous;
        private bool _automatic;
        private Label[] _flowNodes;
        private TcpBarcodeScannerService _barcodeScanners;
        private string _lastScannedBarcode;

        public MainForm()
        {
            InitializeComponent();
        }

        public MainForm(UserSession user, DatabaseService database, IPlcService plc, AppSettings settings)
            : this()
        {
            _user = user ?? throw new ArgumentNullException(nameof(user));
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _plc = plc ?? throw new ArgumentNullException(nameof(plc));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (_settings.BarcodeScannerEnabled)
            {
                _barcodeScanners = new TcpBarcodeScannerService(
                    new BarcodeScannerEndpoint(
                        BarcodeScannerSource.Oriented,
                        _settings.OrientedScannerIp,
                        _settings.OrientedScannerPort),
                    new BarcodeScannerEndpoint(
                        BarcodeScannerSource.NonOriented,
                        _settings.NonOrientedScannerIp,
                        _settings.NonOrientedScannerPort),
                    _settings.DuplicateBarcodeSeconds,
                    _settings.ScannerReconnectDelayMs);
                _barcodeScanners.BarcodeScanned += BarcodeScanned;
                _barcodeScanners.StatusChanged += ScannerStatusChanged;
            }
            InitializeRuntimeBindings();
            ApplyResponsiveLayout();
            ApplyPermissions();
            _plc.SnapshotChanged += PlcSnapshotChanged;
            _plc.CommunicationFault += PlcCommunicationFault;
            Shown += async (s, e) => await RunPlcAsync();
            Shown += async (s, e) => await RunBarcodeScannersAsync();
            Shown += (s, e) =>
            {
                MaximizedBounds = Screen.FromHandle(Handle).WorkingArea;
                ApplyResponsiveLayout();
            };
            Resize += (s, e) => ApplyResponsiveLayout();
            FormClosing += (s, e) => _shutdown.Cancel();
        }

        private void ApplyResponsiveLayout()
        {
            if (ClientSize.Width <= 0) return;
            SuspendLayout();

            var clientWidth = ClientSize.Width;
            var clientHeight = ClientSize.Height;
            pnlHeader.Height = Math.Max(72, (int)(clientHeight * 0.085));
            pnlNavigation.Width = Math.Max(176, Math.Min(250, (int)(clientWidth * 0.12)));
            pnlStationHeader.Width = Math.Max(320, Math.Min(460, (int)(clientWidth * 0.24)));
            pnlOverview.Height = Math.Max(100, (int)(clientHeight * 0.115));
            pnlFlow.Height = Math.Max(100, (int)(clientHeight * 0.115));
            pnlBottom.Height = Math.Max(76, (int)(clientHeight * 0.09));
            PerformLayout();

            var headerWidth = pnlHeader.ClientSize.Width;
            var horizontalGap = Math.Max(8, lblMode.Width / 10);
            lblTitle.Top = Math.Max(0, (pnlHeader.ClientSize.Height - lblTitle.Height) / 2);
            lblConnection.Top = Math.Max(0, (pnlHeader.ClientSize.Height - lblConnection.Height) / 2);
            lblMode.Left = headerWidth - lblMode.Width - horizontalGap;
            lblMode.Top = Math.Max(0, (pnlHeader.ClientSize.Height - lblMode.Height) / 2);
            lblHome.Left = lblMode.Left - lblHome.Width - horizontalGap;
            lblHome.Top = Math.Max(0, (pnlHeader.ClientSize.Height - lblHome.Height) / 2);
            lblUser.AutoSize = false;
            lblUser.AutoEllipsis = true;
            lblUser.TextAlign = ContentAlignment.MiddleRight;
            lblUser.Width = Math.Max(220, Math.Min(420, lblHome.Left - 540));
            lblUser.Left = lblHome.Left - lblUser.Width - horizontalGap;
            lblUser.Top = Math.Max(4, pnlHeader.ClientSize.Height / 2 - lblUser.Height);
            lblPermission.AutoSize = false;
            lblPermission.AutoEllipsis = true;
            lblPermission.TextAlign = ContentAlignment.MiddleRight;
            lblPermission.Width = lblUser.Width;
            lblPermission.Left = lblUser.Left;
            lblPermission.Top = pnlHeader.ClientSize.Height / 2;

            var overviewWidth = pnlOverview.ClientSize.Width;
            if (overviewWidth > 0)
            {
                lblShiftCount.Left = overviewWidth - lblShiftCount.Width - 24;
                lblTotalCount.Left = lblShiftCount.Left - lblTotalCount.Width - 34;
                lblBarcode.Top = Math.Max(lblCurrentTask.Bottom + 4, (pnlOverview.ClientSize.Height - lblBarcode.Height) / 2);
                lblTotalCount.Top = Math.Max(0, (pnlOverview.ClientSize.Height - lblTotalCount.Height) / 2);
                lblShiftCount.Top = lblTotalCount.Top;
            }

            var flowWidth = pnlFlow.ClientSize.Width;
            if (flowWidth > 0)
            {
                const int margin = 14;
                const int gap = 6;
                var nodeWidth = Math.Max(80, (flowWidth - margin * 2 - gap * 7) / 8);
                for (var i = 0; i < _flowNodes.Length; i++)
                {
                    _flowNodes[i].Left = margin + i * (nodeWidth + gap);
                    _flowNodes[i].Width = nodeWidth;
                    _flowNodes[i].AutoEllipsis = true;
                }
                var messageTop = _flowNodes[0].Bottom + 6;
                lblFlowMessage.AutoSize = false;
                lblFlowMessage.Location = new Point(margin, messageTop);
                lblFlowMessage.Size = new Size(Math.Max(180, flowWidth / 2), lblFlowMessage.PreferredHeight + 4);
                lblFlowMessage.AutoEllipsis = true;
                lblFlowMessage.BringToFront();
                lblFlowLegend.AutoSize = false;
                lblFlowLegend.Size = new Size(Math.Min(430, flowWidth / 2 - margin), lblFlowLegend.PreferredHeight + 4);
                lblFlowLegend.Top = messageTop;
                lblFlowLegend.Left = Math.Max(margin, flowWidth - lblFlowLegend.Width - margin);
                lblFlowLegend.TextAlign = ContentAlignment.MiddleRight;
                lblFlowLegend.BringToFront();
                pnlFlow.Height = Math.Max(pnlFlow.Height, messageTop + Math.Max(lblFlowMessage.Height, lblFlowLegend.Height) + 8);
            }

            var processWidth = pnlProcess.ClientSize.Width;
            var processHeight = pnlProcess.ClientSize.Height;
            if (processWidth > 0 && processHeight > 0)
            {
                var steps = new[] { lblStep1, lblStep2, lblStep3, lblStep4 };
                var statuses = new[] { lblStationStatus1, lblStationStatus2, lblStationStatus3, lblStationStatus4 };
                var runLamps = new[] { pnlRun1, pnlRun2, pnlRun3, pnlRun4 };
                var readyLamps = new[] { pnlReady1, pnlReady2, pnlReady3, pnlReady4 };
                var doneLamps = new[] { pnlDone1, pnlDone2, pnlDone3, pnlDone4 };
                var continuousButtons = new[] { btnS1Continuous, btnS2Continuous, btnS3Continuous, btnS4Continuous };
                var startButtons = new[] { btnS1Start, btnS2Start, btnS3Start, btnS4Start };
                var startLeft = processWidth - startButtons[0].Width - 14;
                var continuousLeft = startLeft - continuousButtons[0].Width - 10;
                var doneLeft = continuousLeft - doneLamps[0].Width - 18;
                var readyLeft = doneLeft - readyLamps[0].Width - 10;
                var runLeft = readyLeft - runLamps[0].Width - 10;
                var statusLeft = steps[0].Right + 14;
                var statusWidth = Math.Max(180, runLeft - statusLeft - 18);
                var rowHeight = Math.Max(64, processHeight / 4);
                for (var i = 0; i < 4; i++)
                {
                    var rowTop = i * rowHeight;
                    var cardTop = rowTop + Math.Max(8, rowHeight / 10);
                    var cardHeight = Math.Max(42, rowHeight - Math.Max(16, rowHeight / 5));
                    steps[i].Top = cardTop;
                    steps[i].Height = cardHeight;
                    statuses[i].Left = statusLeft;
                    statuses[i].Width = statusWidth;
                    statuses[i].Top = cardTop;
                    statuses[i].Height = cardHeight;
                    statuses[i].Padding = new Padding(12, 0, 8, 0);
                    statuses[i].AutoEllipsis = true;
                    runLamps[i].Left = runLeft;
                    readyLamps[i].Left = readyLeft;
                    doneLamps[i].Left = doneLeft;
                    runLamps[i].Top = rowTop + (rowHeight - runLamps[i].Height) / 2;
                    readyLamps[i].Top = rowTop + (rowHeight - readyLamps[i].Height) / 2;
                    doneLamps[i].Top = rowTop + (rowHeight - doneLamps[i].Height) / 2;
                    continuousButtons[i].Left = continuousLeft;
                    startButtons[i].Left = startLeft;
                    continuousButtons[i].Top = rowTop + (rowHeight - continuousButtons[i].Height) / 2;
                    startButtons[i].Top = rowTop + (rowHeight - startButtons[i].Height) / 2;
                }
            }

            var navigationWidth = pnlNavigation.ClientSize.Width;
            var navButtons = new[] { btnNavMonitor, btnNavVision, btnNavRecords, btnNavLogs, btnNavSettings };
            var navTop = Math.Max(18, pnlNavigation.ClientSize.Height / 40);
            var navHeight = Math.Max(48, Math.Min(64, pnlNavigation.ClientSize.Height / 13));
            for (var i = 0; i < navButtons.Length; i++)
            {
                navButtons[i].Location = new Point(0, navTop + i * navHeight);
                navButtons[i].Size = new Size(navigationWidth, navHeight);
            }
            var utilityWidth = Math.Max(120, navigationWidth - 28);
            btnVision.Location = new Point(14, navTop + navHeight * 6);
            btnDebug.Location = new Point(14, navTop + navHeight * 7);
            btnRecords.Location = new Point(14, navTop + navHeight * 8);
            btnFaultLogs.Location = new Point(14, navTop + navHeight * 9);
            btnVision.Width = utilityWidth;
            btnDebug.Width = utilityWidth;
            btnRecords.Width = utilityWidth;
            btnFaultLogs.Width = utilityWidth;

            var commandButtons = new[] { btnManualMode, btnAutoMode, btnLineStart, btnLinePause, btnLineStop, btnLineHome, btnFaultReset };
            var commandGap = 12;
            var commandMargin = 20;
            var commandWidth = Math.Max(86, (pnlBottom.ClientSize.Width - commandMargin * 2 - commandGap * 6) / 7);
            var commandHeight = Math.Max(44, pnlBottom.ClientSize.Height - 24);
            for (var i = 0; i < commandButtons.Length; i++)
            {
                commandButtons[i].Location = new Point(commandMargin + i * (commandWidth + commandGap), (pnlBottom.ClientSize.Height - commandHeight) / 2);
                commandButtons[i].Size = new Size(commandWidth, commandHeight);
            }

            var rightHeight = pnlStationHeader.ClientSize.Height;
            var sectionHeight = Math.Max(160, (rightHeight - lblQueueTitle.Height - lblLogTitle.Height) / 2);
            dgvTasks.Height = sectionHeight;

            pnlBottom.BringToFront();
            btnVision.ForeColor = Color.White;
            btnDebug.ForeColor = Color.White;
            btnRecords.ForeColor = Color.White;
            btnFaultLogs.ForeColor = Color.White;
            btnVision.BackColor = Color.FromArgb(38, 65, 91);
            btnDebug.BackColor = Color.FromArgb(38, 65, 91);
            btnRecords.BackColor = Color.FromArgb(38, 65, 91);
            btnFaultLogs.BackColor = Color.FromArgb(38, 65, 91);
            ResumeLayout(true);
        }

        private void ApplyPermissions()
        {
            btnDebug.Visible = _user.CanDebug;
            foreach (var button in _stationStarts) button.Enabled = _user.CanDebug;
            foreach (var button in _stationContinuous) button.Enabled = _user.CanDebug;
            lblPermission.Text = _user.CanDebug ? "调试功能已授权" : "操作员模式 · 手动调试受限";
        }

        private async Task RunPlcAsync()
        {
            try { await _plc.StartAsync(_shutdown.Token); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ShowFault("PLC", "通讯", ex.Message); }
        }

        private async Task RunBarcodeScannersAsync()
        {
            if (_barcodeScanners == null)
            {
                AppendRuntimeLog("二维码扫码功能已在配置中禁用");
                return;
            }

            AppendRuntimeLog("正在连接取向、无取向二维码扫码枪");
            try
            {
                await _barcodeScanners.StartAsync(_shutdown.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                AppendRuntimeLog("[SCANNER] 扫码服务异常：" + ex.Message);
            }
        }

        private void BarcodeScanned(object sender, BarcodeScannedEventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<object, BarcodeScannedEventArgs>(BarcodeScanned), sender, e);
                return;
            }

            var type = e.Source == BarcodeScannerSource.Oriented ? "取向" : "无取向";
            _lastScannedBarcode = e.Barcode;
            lblBarcode.Text = e.Barcode;
            lblCurrentTask.Text = "当前检验号 · 已扫码，等待总控任务";
            lblMaterialType.Text = type + "硅钢片";

            if (dgvTasks.Rows.Count == 1 &&
                Convert.ToString(dgvTasks.Rows[0].Cells[0].Value) == "-")
                dgvTasks.Rows.Clear();
            dgvTasks.Rows.Insert(0, e.Barcode, type, "已扫码，等待总控");
            while (dgvTasks.Rows.Count > 100)
                dgvTasks.Rows.RemoveAt(dgvTasks.Rows.Count - 1);

            AppendRuntimeLog("[" + type + "] 二维码扫描成功：" + e.Barcode);
            _database.LogOperation(_user.UserName, "二维码扫描", type + "扫码枪：" + e.Barcode);
        }

        private void ScannerStatusChanged(object sender, ScannerStatusEventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<object, ScannerStatusEventArgs>(ScannerStatusChanged), sender, e);
                return;
            }
            AppendRuntimeLog("[SCANNER] " + e.Message);
        }

        private void PlcSnapshotChanged(object sender, PlcSnapshot snapshot)
        {
            if (InvokeRequired) { BeginInvoke(new Action<object, PlcSnapshot>(PlcSnapshotChanged), sender, snapshot); return; }
            lblConnection.Text = snapshot.Connected ? "● PLC在线  " + (_settings.Simulation ? "仿真模式" : _settings.PlcIp) : "● PLC离线";
            lblConnection.ForeColor = snapshot.Connected ? Color.ForestGreen : Color.Firebrick;
            lblTotalCount.Text = snapshot.TotalCount.ToString("N0") + "  PCS";
            lblShiftCount.Text = snapshot.ShiftCount.ToString("N0") + "  PCS";
            var currentBarcode = !string.IsNullOrWhiteSpace(snapshot.Barcode)
                ? snapshot.Barcode
                : _lastScannedBarcode;
            lblBarcode.Text = string.IsNullOrWhiteSpace(currentBarcode) ? "-" : currentBarcode;
            lblCurrentTask.Text = string.IsNullOrWhiteSpace(currentBarcode)
                ? "当前检验号 · 等待扫码/总控任务"
                : string.IsNullOrWhiteSpace(snapshot.Barcode)
                    ? "当前检验号 · 已扫码，等待总控任务"
                    : "当前检验号 · 正在执行";
            lblMode.Text = snapshot.Automatic ? "自动模式" : "手动模式";
            lblMode.BackColor = snapshot.Automatic ? Color.LimeGreen : Color.Gold;
            lblHome.Text = IsAllHome(snapshot) ? "在原位" : "未回原位";
            lblHome.BackColor = IsAllHome(snapshot) ? Color.LimeGreen : Color.OrangeRed;
            _automatic = snapshot.Automatic;
            UpdateFlow(snapshot);
            if (snapshot.Stations != null)
                foreach (var station in snapshot.Stations) UpdateStation(station);
        }

        private void UpdateFlow(PlcSnapshot snapshot)
        {
            var current = Math.Max(0, Math.Min(_flowNodes.Length - 1, snapshot.FlowStepIndex));
            for (var i = 0; i < _flowNodes.Length; i++)
            {
                if (snapshot.FlowFault && i == current)
                {
                    _flowNodes[i].BackColor = Color.Firebrick;
                    _flowNodes[i].ForeColor = Color.White;
                }
                else if (i < current)
                {
                    _flowNodes[i].BackColor = Color.LimeGreen;
                    _flowNodes[i].ForeColor = Color.FromArgb(25, 45, 25);
                }
                else if (i == current)
                {
                    _flowNodes[i].BackColor = snapshot.FlowPaused ? Color.Gold : Color.DodgerBlue;
                    _flowNodes[i].ForeColor = snapshot.FlowPaused ? Color.FromArgb(45, 40, 15) : Color.White;
                }
                else
                {
                    _flowNodes[i].BackColor = Color.WhiteSmoke;
                    _flowNodes[i].ForeColor = Color.FromArgb(55, 55, 70);
                }
            }
            lblFlowMessage.Text = snapshot.FlowMessage ?? "等待流程状态";
            lblFlowLegend.Text = snapshot.FlowFault ? "红色：故障" : snapshot.FlowPaused ? "黄色：暂停" : "绿色：完成  蓝色：执行中  白色：未执行";
        }

        private void UpdateStation(StationSnapshot snapshot)
        {
            var index = snapshot.Number - 1;
            _stationSteps[index].Text = snapshot.Step + " 步";
            _stationStatuses[index].Text = StationDescription(snapshot.Number, snapshot);
            _runLamps[index].BackColor = snapshot.Running ? Color.LimeGreen : Color.White;
            _readyLamps[index].BackColor = snapshot.Ready ? Color.LimeGreen : Color.White;
            _doneLamps[index].BackColor = snapshot.Done ? Color.LimeGreen : Color.White;
            _stationStarts[index].Enabled = _user.CanDebug && !_automatic;
            _stationContinuous[index].Enabled = _user.CanDebug;
        }

        private static bool IsAllHome(PlcSnapshot snapshot)
        {
            if (snapshot.Stations == null || snapshot.Stations.Length == 0) return false;
            foreach (var station in snapshot.Stations) if (!station.Home) return false;
            return true;
        }

        private static string StationDescription(int station, StationSnapshot snapshot)
        {
            if (!snapshot.Running && snapshot.Step == 0) return "工位已就绪，等待总控任务";
            if (!snapshot.Running) return "流程已暂停，等待继续";
            if (snapshot.Done) return "本工序完成，等待流转";
            switch (station)
            {
                case 1: return "RBT分配任务中……";
                case 2: return "等待取向工位扫码/拍照";
                case 3: return "无取向折弯流程运行中";
                default: return "磁性能检测流程运行中";
            }
        }

        private void PlcCommunicationFault(object sender, string message)
        {
            if (InvokeRequired) { BeginInvoke(new Action<object, string>(PlcCommunicationFault), sender, message); return; }
            ShowFault("PLC_OFFLINE", "PLC通讯", message);
        }

        private void ShowFault(string code, string node, string message)
        {
            _database.LogFault(code, node, message, _user.UserName);
            AppendRuntimeLog("[" + code + "] " + node + "：" + message);
            MessageBox.Show(message, "故障 - " + node, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void AppendRuntimeLog(string message)
        {
            if (txtRuntimeLog == null) return;
            var line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message;
            if (string.IsNullOrWhiteSpace(txtRuntimeLog.Text) || txtRuntimeLog.Text.StartsWith("系统启动"))
                txtRuntimeLog.Text = line;
            else
                txtRuntimeLog.AppendText(Environment.NewLine + line);
        }

        private static string RoleText(UserRole role)
        {
            switch (role)
            {
                case UserRole.SuperAdmin: return "超级管理员";
                case UserRole.Engineer: return "电气调试员";
                default: return "操作员";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _shutdown.Cancel();
                _shutdown.Dispose();
                if (_barcodeScanners != null)
                {
                    _barcodeScanners.BarcodeScanned -= BarcodeScanned;
                    _barcodeScanners.StatusChanged -= ScannerStatusChanged;
                    _barcodeScanners.Dispose();
                }
                if (_plc != null) _plc.Dispose();
                if (components != null) components.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
