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
using SiliconSteelAdhesionTester.Services.Vision;

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
        private readonly KeyboardQrCodeScanner _qrCodeScanner;
        private string _lastScannedQrCode;
        private bool _s2ScanAllowed;
        private bool _s3ScanAllowed;
        private bool _s2ScanResponseActive;
        private bool _s3ScanResponseActive;
        private PlcSnapshot _latestSnapshot;
        private bool _resourcesDisposed;
        private IQrCodeReader _tcpQrCodeReader;
        private IImageAcquisitionService _imageAcquisition;
        private IAdhesionVisionService _automaticVision;
        private string _lastOrientedQrCode;
        private string _lastNonOrientedQrCode;

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
            _tcpQrCodeReader = new TcpQrCodeReader(_settings);
            _imageAcquisition = string.Equals(_settings.CameraProvider, "MVS", StringComparison.OrdinalIgnoreCase)
                ? (IImageAcquisitionService)new MvsImageAcquisitionService(_settings)
                : new FolderImageAcquisitionService(_settings);
            _automaticVision = new AdhesionVisionService(_settings);
            if (_settings.QrCodeScannerEnabled)
            {
                _qrCodeScanner = new KeyboardQrCodeScanner(
                    _settings.QrCodeInputTimeoutMs,
                    _settings.QrCodeMinimumLength,
                    _settings.DuplicateQrCodeSeconds);
                KeyPreview = true;
                KeyPress += QrCodeKeyPress;
            }
            InitializeRuntimeBindings();
            InitializeSamplePreview();
            pnlProcess.Visible = false;
            pnlStationHeader.Dock = DockStyle.None;
            pnlStationHeader.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pnlStationHeader.BringToFront();
            ApplyResponsiveLayout();
            ApplyPermissions();
            _plc.SnapshotChanged += PlcSnapshotChanged;
            _plc.CommunicationFault += PlcCommunicationFault;
            Shown += async (s, e) => await RunPlcAsync();
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

            int clientWidth = ClientSize.Width;
            int clientHeight = ClientSize.Height;
            pnlHeader.Height = Math.Max(72, (int)(clientHeight * 0.085));
            pnlNavigation.Width = Math.Max(176, Math.Min(250, (int)(clientWidth * 0.12)));
            pnlStationHeader.Width = Math.Max(320, Math.Min(460, (int)(clientWidth * 0.24)));
            pnlOverview.Height = Math.Max(100, (int)(clientHeight * 0.115));
            pnlFlow.Height = Math.Max(100, (int)(clientHeight * 0.115));
            pnlBottom.Height = Math.Max(76, (int)(clientHeight * 0.09));
            PerformLayout();

            int contentLeft = pnlNavigation.Right;
            int contentTop = pnlFlow.Bottom;
            int contentBottom = pnlBottom.Top;
            pnlStationHeader.Bounds = new Rectangle(
                contentLeft,
                contentTop,
                Math.Max(320, ClientSize.Width - contentLeft),
                Math.Max(180, contentBottom - contentTop));

            int headerWidth = pnlHeader.ClientSize.Width;
            int horizontalGap = Math.Max(8, lblMode.Width / 10);
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

            int overviewWidth = pnlOverview.ClientSize.Width;
            if (overviewWidth > 0)
            {
                lblShiftCount.Left = overviewWidth - lblShiftCount.Width - 24;
                lblTotalCount.Left = lblShiftCount.Left - lblTotalCount.Width - 34;
                lblQrCodeContent.Top = Math.Max(lblCurrentTask.Bottom + 4, (pnlOverview.ClientSize.Height - lblQrCodeContent.Height) / 2);
                lblTotalCount.Top = Math.Max(0, (pnlOverview.ClientSize.Height - lblTotalCount.Height) / 2);
                lblShiftCount.Top = lblTotalCount.Top;
            }

            int flowWidth = pnlFlow.ClientSize.Width;
            if (flowWidth > 0)
            {
                const int margin = 14;
                const int gap = 6;
                int nodeWidth = Math.Max(80, (flowWidth - margin * 2 - gap * 7) / 8);
                for (int i = 0; i < _flowNodes.Length; i++)
                {
                    _flowNodes[i].Left = margin + i * (nodeWidth + gap);
                    _flowNodes[i].Width = nodeWidth;
                    _flowNodes[i].AutoEllipsis = true;
                }
                int messageTop = _flowNodes[0].Bottom + 6;
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

            int processWidth = pnlProcess.ClientSize.Width;
            int processHeight = pnlProcess.ClientSize.Height;
            if (processWidth > 0 && processHeight > 0)
            {
                Label[] steps = new[] { lblStep1, lblStep2, lblStep3, lblStep4 };
                Label[] statuses = new[] { lblStationStatus1, lblStationStatus2, lblStationStatus3, lblStationStatus4 };
                Panel[] runLamps = new[] { pnlRun1, pnlRun2, pnlRun3, pnlRun4 };
                Panel[] readyLamps = new[] { pnlReady1, pnlReady2, pnlReady3, pnlReady4 };
                Panel[] doneLamps = new[] { pnlDone1, pnlDone2, pnlDone3, pnlDone4 };
                Button[] continuousButtons = new[] { btnS1Continuous, btnS2Continuous, btnS3Continuous, btnS4Continuous };
                Button[] startButtons = new[] { btnS1Start, btnS2Start, btnS3Start, btnS4Start };
                int startLeft = processWidth - startButtons[0].Width - 14;
                int continuousLeft = startLeft - continuousButtons[0].Width - 10;
                int doneLeft = continuousLeft - doneLamps[0].Width - 18;
                int readyLeft = doneLeft - readyLamps[0].Width - 10;
                int runLeft = readyLeft - runLamps[0].Width - 10;
                int statusLeft = steps[0].Right + 14;
                int statusWidth = Math.Max(180, runLeft - statusLeft - 18);
                int rowHeight = Math.Max(64, processHeight / 4);
                for (int i = 0; i < 4; i++)
                {
                    int rowTop = i * rowHeight;
                    int cardTop = rowTop + Math.Max(8, rowHeight / 10);
                    int cardHeight = Math.Max(42, rowHeight - Math.Max(16, rowHeight / 5));
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

            int navigationWidth = pnlNavigation.ClientSize.Width;
            Button[] navButtons = _user != null && _user.CanDebug
                ? new[] { btnNavMonitor, btnNavVision, btnDebug, btnNavRecords, btnNavLogs, btnNavSettings }
                : new[] { btnNavMonitor, btnNavVision, btnNavRecords, btnNavLogs, btnNavSettings };
            int navTop = Math.Max(18, pnlNavigation.ClientSize.Height / 40);
            int navHeight = Math.Max(48, Math.Min(64, pnlNavigation.ClientSize.Height / 13));
            for (int i = 0; i < navButtons.Length; i++)
            {
                navButtons[i].Location = new Point(0, navTop + i * navHeight);
                navButtons[i].Size = new Size(navigationWidth, navHeight);
                navButtons[i].FlatStyle = FlatStyle.Flat;
                navButtons[i].FlatAppearance.BorderSize = 0;
                navButtons[i].BackColor = i == 0
                    ? Color.FromArgb(38, 112, 190)
                    : Color.FromArgb(25, 43, 63);
                navButtons[i].ForeColor = Color.White;
            }
            btnVision.Visible = false;
            btnRecords.Visible = false;
            btnFaultLogs.Visible = false;

            Button[] commandButtons = new[] { btnManualMode, btnAutoMode, btnLineStart, btnLinePause, btnLineStop, btnLineHome, btnFaultReset };
            int commandGap = 12;
            int commandMargin = 20;
            int commandWidth = Math.Max(86, (pnlBottom.ClientSize.Width - commandMargin * 2 - commandGap * 6) / 7);
            int commandHeight = Math.Max(44, pnlBottom.ClientSize.Height - 24);
            for (int i = 0; i < commandButtons.Length; i++)
            {
                commandButtons[i].Location = new Point(commandMargin + i * (commandWidth + commandGap), (pnlBottom.ClientSize.Height - commandHeight) / 2);
                commandButtons[i].Size = new Size(commandWidth, commandHeight);
            }

            LayoutSamplePreviewAndTaskPanels();

            pnlBottom.BringToFront();
            ResumeLayout(true);
        }

        private void ApplyPermissions()
        {
            btnDebug.Visible = _user.CanDebug;
            foreach (Button button in _stationStarts) button.Enabled = _user.CanDebug;
            foreach (Button button in _stationContinuous) button.Enabled = _user.CanDebug;
            lblPermission.Text = _user.CanDebug ? "调试功能已授权" : "操作员模式 · 手动调试受限";
        }

        private async Task RunPlcAsync()
        {
            try { await _plc.StartAsync(_shutdown.Token); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ShowFault("PLC", "通讯", ex.Message); }
        }

        private async void QrCodeKeyPress(object sender, KeyPressEventArgs e)
        {
            if (_qrCodeScanner == null || (!_s2ScanAllowed && !_s3ScanAllowed))
            {
                return;
            }

            e.Handled = true;
            QrCodeInputResult result = _qrCodeScanner.Accept(e.KeyChar, DateTime.Now);
            if (!result.HasResult) return;

            if (_s2ScanAllowed && _s3ScanAllowed)
            {
                AppendRuntimeLog("[SCANNER] S2与S3同时允许扫码，无法判断二维码所属工位，已拒绝");
                return;
            }

            bool oriented = _s2ScanAllowed;
            if ((oriented && _s2ScanResponseActive) ||
                (!oriented && _s3ScanResponseActive))
                return;

            if (!result.Accepted)
            {
                AppendRuntimeLog("[SCANNER] " + result.Message);
                _database.SaveQrCodeScanEvent(result.QrCodeContent, oriented ? "取向" : "无取向", oriented ? "S2" : "S3", false, result.Message, _user.UserName);
                await SendScanResponseAsync(oriented, false);
                return;
            }

            RegisterQrCode(result.QrCodeContent, oriented);
            await SendScanResponseAsync(oriented, true);
        }

        private void RegisterQrCode(string qrCodeContent, bool oriented)
        {
            string type = oriented ? "取向" : "无取向";
            _lastScannedQrCode = qrCodeContent;
            if (oriented) _lastOrientedQrCode = qrCodeContent;
            else _lastNonOrientedQrCode = qrCodeContent;
            EnqueueQrCode(qrCodeContent, oriented);
            lblQrCodeContent.Text = qrCodeContent;
            lblCurrentTask.Text = "当前检验号 · 二维码读取完成，正在通知PLC";
            lblMaterialType.Text = type + "硅钢片";
            SetPreviewSample(qrCodeContent);

            if (dgvTasks.Rows.Count == 1 &&
                Convert.ToString(dgvTasks.Rows[0].Cells[0].Value) == "-")
                dgvTasks.Rows.Clear();
            dgvTasks.Rows.Insert(0, qrCodeContent, type, "二维码读取完成");
            while (dgvTasks.Rows.Count > 100)
                dgvTasks.Rows.RemoveAt(dgvTasks.Rows.Count - 1);

            AppendRuntimeLog("[" + type + "] 二维码读取成功：" + qrCodeContent);
            _database.SaveQrCodeScanEvent(qrCodeContent, type, oriented ? "S2" : "S3", true, "二维码读取成功", _user.UserName);
            _database.LogOperation(_user.UserName, "二维码读取", type + "工位：" + qrCodeContent);
        }

        private async Task SendScanResponseAsync(bool oriented, bool accepted)
        {
            string done = oriented ? PlcAddresses.S2ScanDone : PlcAddresses.S3ScanDone;
            string ok = oriented ? PlcAddresses.S2ScanOk : PlcAddresses.S3ScanOk;
            string ng = oriented ? PlcAddresses.S2ScanNg : PlcAddresses.S3ScanNg;
            try
            {
                if (oriented) _s2ScanResponseActive = true;
                else _s3ScanResponseActive = true;
                await _plc.WriteAsync(ok, accepted, _shutdown.Token);
                await _plc.WriteAsync(ng, !accepted, _shutdown.Token);
                await _plc.WriteAsync(done, true, _shutdown.Token);
                AppendRuntimeLog("[PLC] 已返回扫码完成/" + (accepted ? "OK" : "NG"));
            }
            catch (Exception ex)
            {
                if (oriented) _s2ScanResponseActive = false;
                else _s3ScanResponseActive = false;
                AppendRuntimeLog("[SCANNER_PLC] 扫码结果写入PLC失败：" + ex.Message);
            }
        }

        private async Task ResetScanResponseAsync(bool oriented)
        {
            string done = oriented ? PlcAddresses.S2ScanDone : PlcAddresses.S3ScanDone;
            string ok = oriented ? PlcAddresses.S2ScanOk : PlcAddresses.S3ScanOk;
            string ng = oriented ? PlcAddresses.S2ScanNg : PlcAddresses.S3ScanNg;
            try
            {
                await _plc.WriteAsync(done, false, _shutdown.Token);
                await _plc.WriteAsync(ok, false, _shutdown.Token);
                await _plc.WriteAsync(ng, false, _shutdown.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                AppendRuntimeLog("[SCANNER_PLC] 复位扫码应答失败：" + ex.Message);
            }
        }

        private void PlcSnapshotChanged(object sender, PlcSnapshot snapshot)
        {
            if (InvokeRequired) { BeginInvoke(new Action<object, PlcSnapshot>(PlcSnapshotChanged), sender, snapshot); return; }
            _latestSnapshot = snapshot;
            lblConnection.Text = snapshot.Connected
                ? "● PLC在线  " + (_settings.Simulation
                    ? "仿真模式"
                    : _settings.PlcIp + ":" + _settings.PlcPort)
                : "● PLC离线";
            lblConnection.ForeColor = snapshot.Connected ? Color.ForestGreen : Color.Firebrick;
            lblTotalCount.Text = snapshot.TotalCount.ToString("N0") + "  PCS";
            lblShiftCount.Text = snapshot.ShiftCount.ToString("N0") + "  PCS";
            string currentQrCode = !string.IsNullOrWhiteSpace(snapshot.QrCodeContent)
                ? snapshot.QrCodeContent
                : _lastScannedQrCode;
            lblQrCodeContent.Text = string.IsNullOrWhiteSpace(currentQrCode) ? "-" : currentQrCode;
            lblCurrentTask.Text = string.IsNullOrWhiteSpace(currentQrCode)
                ? "当前检验号 · 等待二维码/总控任务"
                : string.IsNullOrWhiteSpace(snapshot.QrCodeContent)
                    ? "当前检验号 · 已读取二维码，等待总控任务"
                    : "当前检验号 · 正在执行";
            SetPreviewSample(currentQrCode);
            lblMode.Text = snapshot.Automatic ? "自动模式" : "手动模式";
            lblMode.BackColor = snapshot.Automatic ? Color.LimeGreen : Color.Gold;
            lblHome.Text = IsAllHome(snapshot) ? "在原位" : "未回原位";
            lblHome.BackColor = IsAllHome(snapshot) ? Color.LimeGreen : Color.OrangeRed;
            UpdateScanPermission(snapshot);
            UpdateAutomaticInteractions(snapshot);
            _automatic = snapshot.Automatic;
            UpdateFlow(snapshot);
            if (snapshot.Stations != null)
                foreach (StationSnapshot station in snapshot.Stations) UpdateStation(station);
        }

        private void UpdateScanPermission(PlcSnapshot snapshot)
        {
            if (snapshot.S2ScanAllowed && !_s2ScanAllowed)
            {
                _qrCodeScanner?.Reset();
                AppendRuntimeLog("[QR] PLC允许S2取向工位读取二维码");
            }
            if (snapshot.S3ScanAllowed && !_s3ScanAllowed)
            {
                _qrCodeScanner?.Reset();
                AppendRuntimeLog("[QR] PLC允许S3无取向工位读取二维码");
            }

            _s2ScanAllowed = snapshot.S2ScanAllowed;
            _s3ScanAllowed = snapshot.S3ScanAllowed;

            if (!snapshot.S2ScanAllowed && _s2ScanResponseActive)
            {
                _s2ScanResponseActive = false;
                _ = ResetScanResponseAsync(true);
            }
            if (!snapshot.S3ScanAllowed && _s3ScanResponseActive)
            {
                _s3ScanResponseActive = false;
                _ = ResetScanResponseAsync(false);
            }
        }

        private void UpdateFlow(PlcSnapshot snapshot)
        {
            int current = Math.Max(0, Math.Min(_flowNodes.Length - 1, snapshot.FlowStepIndex));
            for (int i = 0; i < _flowNodes.Length; i++)
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
            int index = snapshot.Number - 1;
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
            foreach (StationSnapshot station in snapshot.Stations) if (!station.Home) return false;
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
                case 2: return "等待取向工位二维码读取/拍照";
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
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message;
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
            if (disposing && !_resourcesDisposed)
            {
                _resourcesDisposed = true;
                _shutdown.Cancel();
                _shutdown.Dispose();
                KeyPress -= QrCodeKeyPress;
                _plc?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
