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
        private bool? _currentMaterialOriented;
        private string _manualTaskId;
        private bool _homeSignalMismatchActive;
        private int? _taskTotalCount;
        private int _taskCompletedCount;
        private int _taskQualifiedCount;
        private int _taskUnqualifiedCount;

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
                BeginInvoke(new Action(ClearPassiveSelections));
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
            pnlOverview.Height = Math.Max(150, (int)(clientHeight * 0.145));
            pnlFlow.Height = Math.Max(122, (int)(clientHeight * 0.13));
            pnlBottom.Height = Math.Max(104, (int)(clientHeight * 0.115));
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
                int margin = 22;
                int statisticsLeft = Math.Max(520, (int)(overviewWidth * 0.45));
                int statisticsWidth = Math.Max(440, overviewWidth - statisticsLeft - margin);
                int statisticGap = 8;
                int statisticWidth = Math.Max(98, (statisticsWidth - statisticGap * 3) / 4);
                Label[] statistics = { lblTotalCount, lblShiftCount, lblQualifiedCount, lblUnqualifiedCount };
                for (int i = 0; i < statistics.Length; i++)
                {
                    statistics[i].Bounds = new Rectangle(
                        statisticsLeft + i * (statisticWidth + statisticGap),
                        12,
                        statisticWidth,
                        40);
                    statistics[i].TextAlign = ContentAlignment.MiddleCenter;
                    statistics[i].BackColor = Color.FromArgb(247, 249, 252);
                }
                lblCurrentTask.Width = Math.Max(300, statisticsLeft - lblCurrentTask.Left - 18);
                lblMaterialType.Left = Math.Max(260, Math.Min(430, statisticsLeft / 2));
                lblMaterialType.Width = Math.Max(180, statisticsLeft - lblMaterialType.Left - 18);
                lblQrCodeContent.Width = Math.Max(300, statisticsLeft - lblQrCodeContent.Left - 18);
                lblQrCodeContent.Top = Math.Max(lblCurrentTask.Bottom + 4, 43);
                lblTaskProgressText.Bounds = new Rectangle(
                    statisticsLeft,
                    58,
                    statisticsWidth,
                    30);
                pnlTaskProgressTrack.Bounds = new Rectangle(
                    statisticsLeft,
                    96,
                    statisticsWidth,
                    12);
                UpdateTaskProgressBarWidth();
            }

            int flowWidth = pnlFlow.ClientSize.Width;
            if (flowWidth > 0)
            {
                const int margin = 18;
                const int gap = 10;
                int nodeWidth = Math.Max(120, (flowWidth - margin * 2 - gap * 4) / 5);
                for (int i = 0; i < _flowNodes.Length; i++)
                {
                    _flowNodes[i].Left = margin + i * (nodeWidth + gap);
                    _flowNodes[i].Width = nodeWidth;
                    _flowNodes[i].Top = 42;
                    _flowNodes[i].Height = 46;
                    _flowNodes[i].AutoEllipsis = true;
                }
                lblFlowLegend.AutoSize = false;
                lblFlowLegend.Location = new Point(margin, 4);
                lblFlowLegend.Size = new Size(flowWidth - margin * 2, 34);
                lblFlowLegend.TextAlign = ContentAlignment.MiddleLeft;
                lblFlowLegend.BringToFront();
                int messageTop = _flowNodes[0].Bottom + 7;
                lblFlowMessage.AutoSize = false;
                lblFlowMessage.Location = new Point(margin, messageTop);
                lblFlowMessage.Size = new Size(flowWidth - margin * 2, lblFlowMessage.PreferredHeight + 4);
                lblFlowMessage.AutoEllipsis = true;
                lblFlowMessage.BringToFront();
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
                ? new[] { btnNavMonitor, btnDebug, btnNavRecords, btnNavLogs, btnNavSettings }
                : new[] { btnNavMonitor, btnNavRecords, btnNavLogs, btnNavSettings };
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

            Button[] commandButtons = new[] { btnManualMode, btnAutoMode, btnLineStart, btnLinePause, btnLineHome, btnFaultReset };
            int commandGap = 10;
            int commandMargin = 20;
            int commandWidth = Math.Max(96, (pnlBottom.ClientSize.Width - commandMargin * 2 - commandGap * 5) / 6);
            int feedbackHeight = 30;
            int commandAreaTop = feedbackHeight + 7;
            int commandHeight = Math.Max(44, pnlBottom.ClientSize.Height - commandAreaTop - 10);
            if (lblCommandFeedback != null)
                lblCommandFeedback.Bounds = new Rectangle(0, 0, pnlBottom.ClientSize.Width, feedbackHeight);
            for (int i = 0; i < commandButtons.Length; i++)
            {
                commandButtons[i].Location = new Point(
                    commandMargin + i * (commandWidth + commandGap),
                    commandAreaTop + Math.Max(0, (pnlBottom.ClientSize.Height - commandAreaTop - commandHeight) / 2));
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
                AppendRuntimeLog("[SCANNER] 取向弯折检测工位与无取向弯折工位同时允许扫码，无法判断二维码所属工位，已拒绝");
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
            _currentMaterialOriented = oriented;
            UpdateFlowPresentation();
            _lastScannedQrCode = qrCodeContent;
            if (oriented) _lastOrientedQrCode = qrCodeContent;
            else _lastNonOrientedQrCode = qrCodeContent;
            EnqueueQrCode(qrCodeContent, oriented);
            lblQrCodeContent.Text = qrCodeContent;
            lblCurrentTask.Text = "当前检验号 · 二维码读取完成，正在通知PLC";
            lblMaterialType.Text = type + "硅钢片";
            SetPreviewSample(qrCodeContent);
            ShowCurrentSamplePending(qrCodeContent, type);
            AddBatchSamplePending(qrCodeContent, type);

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
            _automatic = snapshot.Automatic;
            lblConnection.Text = snapshot.Connected
                ? "● PLC在线  " + (_settings.Simulation
                    ? "仿真模式"
                    : _settings.PlcIp + ":" + _settings.PlcPort)
                : "● PLC离线";
            lblConnection.ForeColor = snapshot.Connected ? Color.ForestGreen : Color.Firebrick;
            UpdateTaskProgressPresentation();
            string currentQrCode = !string.IsNullOrWhiteSpace(snapshot.QrCodeContent)
                ? snapshot.QrCodeContent
                : _lastScannedQrCode;
            lblQrCodeContent.Text = string.IsNullOrWhiteSpace(currentQrCode) ? "-" : currentQrCode;
            lblCurrentTask.Text = string.IsNullOrWhiteSpace(currentQrCode)
                ? (_automatic
                    ? "当前任务 · 等待总控任务（协议待现场确认）"
                    : "当前任务 · 等待手动任务或扫码")
                : string.IsNullOrWhiteSpace(snapshot.QrCodeContent)
                    ? "当前试样 · 已读取二维码"
                    : "当前试样 · 正在执行";
            SetPreviewSample(currentQrCode);
            lblMode.Text = snapshot.Automatic ? "自动模式" : "手动模式";
            lblMode.BackColor = snapshot.Automatic ? Color.LimeGreen : Color.Gold;
            bool stationHomes = AreAllStationHomes(snapshot);
            bool homeSignalsAgree = snapshot.WholeLineHome == stationHomes;
            lblHome.Text = homeSignalsAgree
                ? (snapshot.WholeLineHome ? "在原位" : "未回原位")
                : "零位信号不一致";
            lblHome.BackColor = homeSignalsAgree
                ? (snapshot.WholeLineHome ? Color.LimeGreen : Color.OrangeRed)
                : Color.Gold;
            if (!homeSignalsAgree && !_homeSignalMismatchActive)
                AppendRuntimeLog("[PLC] 整机零位与四工位Home信号不一致，已禁止按“在原位”处理");
            else if (homeSignalsAgree && _homeSignalMismatchActive)
                AppendRuntimeLog("[PLC] 整机零位与四工位Home信号已恢复一致");
            _homeSignalMismatchActive = !homeSignalsAgree;
            UpdateScanPermission(snapshot);
            UpdateAutomaticInteractions(snapshot);
            UpdateStartPauseButtonState(snapshot);
            if (snapshot.S2ScanAllowed && !snapshot.S3ScanAllowed)
                _currentMaterialOriented = true;
            else if (snapshot.S3ScanAllowed && !snapshot.S2ScanAllowed)
                _currentMaterialOriented = false;
            UpdateFlowPresentation();
            UpdateFlow(snapshot);
            if (snapshot.Stations != null)
                foreach (StationSnapshot station in snapshot.Stations) UpdateStation(station);
        }

        private void UpdateScanPermission(PlcSnapshot snapshot)
        {
            if (snapshot.S2ScanAllowed && !_s2ScanAllowed)
            {
                _qrCodeScanner?.Reset();
                AppendRuntimeLog("[QR] PLC允许取向弯折检测工位读取二维码");
            }
            if (snapshot.S3ScanAllowed && !_s3ScanAllowed)
            {
                _qrCodeScanner?.Reset();
                AppendRuntimeLog("[QR] PLC允许无取向弯折工位读取二维码");
            }

            _s2ScanAllowed = snapshot.S2ScanAllowed;
            _s3ScanAllowed = snapshot.S3ScanAllowed;

            if (!snapshot.S2ScanAllowed &&
                (_s2ScanResponseActive || snapshot.S2ScanDone || snapshot.S2ScanOk || snapshot.S2ScanNg))
            {
                _s2ScanResponseActive = false;
                _ = ResetScanResponseAsync(true);
            }
            if (!snapshot.S3ScanAllowed &&
                (_s3ScanResponseActive || snapshot.S3ScanDone || snapshot.S3ScanOk || snapshot.S3ScanNg))
            {
                _s3ScanResponseActive = false;
                _ = ResetScanResponseAsync(false);
            }
        }

        private void UpdateFlow(PlcSnapshot snapshot)
        {
            int current = DetermineInteractionStep(snapshot);
            bool hasActiveSample = _currentMaterialOriented.HasValue;
            bool completed = _currentMaterialOriented == true
                ? (_s2SecondPhotoResponseActive || snapshot.S2SecondPhotoDone)
                : _currentMaterialOriented == false &&
                  (_s4PhotoResponseActive || snapshot.S4PhotoDone);
            bool interactionFailed = _currentMaterialOriented == true
                ? (snapshot.S2ScanDone && snapshot.S2ScanNg) ||
                  (snapshot.S2SecondPhotoDone && snapshot.S2SecondPhotoNg)
                : _currentMaterialOriented == false &&
                  ((snapshot.S3ScanDone && snapshot.S3ScanNg) ||
                   (snapshot.S4PhotoDone && snapshot.S4PhotoNg));
            for (int i = 0; i < _flowNodes.Length; i++)
            {
                if ((snapshot.FlowFault || interactionFailed) &&
                    i == (completed ? _flowNodes.Length - 1 : current))
                {
                    _flowNodes[i].BackColor = Color.Firebrick;
                    _flowNodes[i].ForeColor = Color.White;
                }
                else if (completed || i < current)
                {
                    _flowNodes[i].BackColor = Color.FromArgb(38, 166, 91);
                    _flowNodes[i].ForeColor = Color.White;
                }
                else if (hasActiveSample && i == current)
                {
                    bool waitingForPlc = IsWaitingForPlc(snapshot, current);
                    _flowNodes[i].BackColor = snapshot.FlowPaused || waitingForPlc
                        ? Color.FromArgb(236, 177, 50)
                        : Color.FromArgb(38, 112, 190);
                    _flowNodes[i].ForeColor = snapshot.FlowPaused ? Color.FromArgb(45, 40, 15) : Color.White;
                }
                else
                {
                    _flowNodes[i].BackColor = Color.WhiteSmoke;
                    _flowNodes[i].ForeColor = Color.FromArgb(55, 55, 70);
                }
            }
            lblFlowMessage.Text = BuildInteractionMessage(snapshot, current, completed);
        }

        private void UpdateFlowPresentation()
        {
            if (_flowNodes == null || _flowNodes.Length != 5) return;

            string[] captions;
            if (_currentMaterialOriented == true)
            {
                captions = new[]
                {
                    "1 扫码", "2 压弯前拍照", "3 等待弯折", "4 压弯后拍照", "5 判定回传"
                };
                lblFlowLegend.Text = "当前试样流程 · 取向弯折检测";
                lblMaterialType.Text = "当前试样 · 取向";
            }
            else if (_currentMaterialOriented == false)
            {
                captions = new[]
                {
                    "1 扫码", "2 等待弯折完成", "3 等待进入检测工位", "4 胶带图像采集", "5 判定回传"
                };
                lblFlowLegend.Text = "当前试样流程 · 无取向弯折及检测";
                lblMaterialType.Text = "当前试样 · 无取向";
            }
            else
            {
                captions = new[]
                {
                    "1 扫码", "2 等待试样处理", "3 等待检测条件", "4 图像采集", "5 判定回传"
                };
                lblFlowLegend.Text = "当前试样流程 · 等待识别试样类型";
                lblMaterialType.Text = "当前试样 · 等待扫码";
            }

            for (int i = 0; i < _flowNodes.Length; i++)
                _flowNodes[i].Text = captions[i];
        }

        private int DetermineInteractionStep(PlcSnapshot snapshot)
        {
            if (_currentMaterialOriented == true)
            {
                if (_s2SecondPhotoResponseActive || snapshot.S2SecondPhotoDone) return 4;
                if (snapshot.S2SecondPhotoAllowed) return 3;
                if (_s2FirstPhotoDoneActive || snapshot.S2FirstPhotoDone ||
                    !string.IsNullOrWhiteSpace(_orientedBeforeImagePath)) return 2;
                if ((snapshot.S2ScanDone && snapshot.S2ScanOk) ||
                    snapshot.S2FirstPhotoAllowed || _s2ScanResponseActive ||
                    _orientedQrCodeQueue.Count > 0) return 1;
                return 0;
            }
            if (_currentMaterialOriented == false)
            {
                if (_s4PhotoResponseActive || snapshot.S4PhotoDone) return 4;
                if (snapshot.S4PhotoAllowed || snapshot.S4HasMaterialForTape) return 3;
                if (IsStationDone(snapshot, 3)) return 2;
                if ((snapshot.S3ScanDone && snapshot.S3ScanOk) ||
                    _s3ScanResponseActive || _nonOrientedQrCodeQueue.Count > 0) return 1;
                return 0;
            }
            return 0;
        }

        private static bool IsStationDone(PlcSnapshot snapshot, int stationNumber)
        {
            if (snapshot == null || snapshot.Stations == null) return false;
            foreach (StationSnapshot station in snapshot.Stations)
                if (station.Number == stationNumber) return station.Done;
            return false;
        }

        private bool IsWaitingForPlc(PlcSnapshot snapshot, int current)
        {
            if (snapshot.FlowPaused) return true;
            if (_currentMaterialOriented == true)
                return current == 2 && !snapshot.S2SecondPhotoAllowed;
            if (_currentMaterialOriented == false)
                return (current == 1 && !snapshot.S4HasMaterialForTape) ||
                    (current == 3 && !snapshot.S4PhotoAllowed);
            return false;
        }

        private string BuildInteractionMessage(PlcSnapshot snapshot, int current, bool completed)
        {
            if (snapshot.FlowFault) return "设备流程异常，请查看运行日志和故障信息";
            if (!_currentMaterialOriented.HasValue)
                return "等待取向弯折检测工位或无取向弯折工位发出扫码请求";
            if (snapshot.FlowPaused) return "设备已暂停，当前试样进度保持不变";
            if (completed)
                return "本片检测结果已返回PLC，等待PLC复位交互信号";
            if (_currentMaterialOriented == true)
            {
                if (snapshot.S2ScanDone && snapshot.S2ScanNg)
                    return "二维码读取失败，已返回扫码完成/NG";
                switch (current)
                {
                    case 0: return "取向弯折检测工位允许扫码，正在读取二维码";
                    case 1: return snapshot.S2ScanDone && snapshot.S2ScanOk
                        ? "二维码读取成功，等待PLC复位扫码允许并发出压弯前拍照允许"
                        : snapshot.S2FirstPhotoAllowed
                        ? "取向弯折检测工位允许压弯前拍照，正在采集图像"
                        : "扫码已完成，等待PLC发出压弯前拍照允许";
                    case 2: return "压弯前拍照已完成，等待PLC完成弯折并允许压弯后拍照";
                    case 3: return "正在采集压弯后图像并执行视觉判定";
                }
            }
            else
            {
                if (snapshot.S3ScanDone && snapshot.S3ScanNg)
                    return "二维码读取失败，已返回扫码完成/NG";
                switch (current)
                {
                    case 0: return "无取向弯折工位允许扫码，正在读取二维码";
                    case 1: return snapshot.S3ScanDone && snapshot.S3ScanOk
                        ? "二维码读取成功，等待PLC完成无取向弯折和转运"
                        : "扫码已完成，等待PLC完成无取向弯折和转运";
                    case 3: return snapshot.S4PhotoAllowed
                        ? "无取向检测工位允许拍照，正在采集胶带图像并执行视觉判定"
                        : "试样已到达无取向检测工位，等待PLC发出拍照允许";
                }
            }
            return "等待PLC交互信号";
        }

        private void BeginPlaceholderMasterTask()
        {
            _manualTaskId = null;
            BeginTaskProgress(null);
            lblCurrentTask.Text = "当前任务 · 等待总控任务（协议待现场确认）";
        }

        private void BeginTaskProgress(int? totalCount)
        {
            _taskTotalCount = totalCount;
            _taskCompletedCount = 0;
            _taskQualifiedCount = 0;
            _taskUnqualifiedCount = 0;
            ClearBatchSampleList();
            UpdateTaskProgressPresentation();
        }

        private void RecordTaskResult(bool qualified)
        {
            _taskCompletedCount++;
            if (qualified) _taskQualifiedCount++;
            else _taskUnqualifiedCount++;
            UpdateTaskProgressPresentation();
        }

        private void UpdateTaskProgressPresentation()
        {
            if (lblTotalCount == null) return;
            lblTotalCount.Text = "任务总数  " +
                (_taskTotalCount.HasValue ? _taskTotalCount.Value.ToString("N0") : "--");
            lblShiftCount.Text = "已完成  " + _taskCompletedCount.ToString("N0");
            lblQualifiedCount.Text = "合格  " + _taskQualifiedCount.ToString("N0");
            lblUnqualifiedCount.Text = "不合格  " + _taskUnqualifiedCount.ToString("N0");

            if (_taskTotalCount.HasValue && _taskTotalCount.Value > 0)
            {
                int boundedCompleted = Math.Min(_taskCompletedCount, _taskTotalCount.Value);
                int percent = (int)Math.Round(boundedCompleted * 100D / _taskTotalCount.Value);
                lblTaskProgressText.Text = "任务进度 · " + boundedCompleted + " / " +
                    _taskTotalCount.Value + "（" + percent + "%）";
            }
            else
            {
                lblTaskProgressText.Text = _automatic
                    ? "任务进度 · 等待总控协议确认和任务数据"
                    : "任务进度 · 请先创建手动任务";
            }
            UpdateTaskProgressBarWidth();
        }

        private void UpdateTaskProgressBarWidth()
        {
            if (pnlTaskProgressTrack == null || pnlTaskProgressFill == null) return;
            double ratio = _taskTotalCount.HasValue && _taskTotalCount.Value > 0
                ? Math.Min(1D, _taskCompletedCount / (double)_taskTotalCount.Value)
                : 0D;
            pnlTaskProgressFill.Width = (int)Math.Round(pnlTaskProgressTrack.ClientSize.Width * ratio);
        }

        private void UpdateStation(StationSnapshot snapshot)
        {
            int index = snapshot.Number - 1;
            _stationSteps[index].Text = StationDisplayName(snapshot.Number) + " · " + snapshot.Step + "步";
            _stationStatuses[index].Text = StationDescription(snapshot.Number, snapshot, _latestSnapshot);
            _runLamps[index].BackColor = snapshot.Running ? Color.LimeGreen : Color.White;
            _readyLamps[index].BackColor = snapshot.Ready ? Color.LimeGreen : Color.White;
            _doneLamps[index].BackColor = snapshot.Done ? Color.LimeGreen : Color.White;
            _stationStarts[index].Enabled = _user.CanDebug && !_automatic;
            _stationContinuous[index].Enabled = _user.CanDebug;
        }

        private static bool IsAllHome(PlcSnapshot snapshot)
        {
            return snapshot != null && snapshot.WholeLineHome && AreAllStationHomes(snapshot);
        }

        private static bool AreAllStationHomes(PlcSnapshot snapshot)
        {
            if (snapshot.Stations == null || snapshot.Stations.Length == 0) return false;
            foreach (StationSnapshot station in snapshot.Stations) if (!station.Home) return false;
            return true;
        }

        private static string StationDescription(int station, StationSnapshot snapshot, PlcSnapshot lineSnapshot)
        {
            if (snapshot.Done) return "本工序完成，等待流转";
            switch (station)
            {
                case 1:
                    if (lineSnapshot != null && lineSnapshot.S1AutomaticRunning)
                        return lineSnapshot.Automatic
                            ? "AGV送料完成，S1自动流程已接通"
                            : "S1手动启动已接通";
                    return lineSnapshot != null && lineSnapshot.Automatic
                        ? "等待总控/AGV送料完成"
                        : "等待手动任务启动";
                case 2:
                    return lineSnapshot != null && lineSnapshot.S2HasPendingMaterial
                        ? (snapshot.Running ? "取向有料，正在执行二维码读取/拍照" : "取向待检工位有料")
                        : "取向待检工位无料";
                case 3:
                    return lineSnapshot != null && lineSnapshot.S3HasPendingMaterial
                        ? (snapshot.Running ? "无取向有料，弯折流程运行中" : "无取向弯折待检工位有料")
                        : "无取向弯折待检工位无料";
                case 4:
                    return lineSnapshot != null && lineSnapshot.S4HasMaterialForTape
                        ? (snapshot.Running ? "无取向检测有料，胶带流程运行中" : "无取向检测有料，可进入胶带粘取")
                        : "无取向检测等待来料";
            }
            if (!snapshot.Running && snapshot.Step == 0) return "工位已就绪，等待总控任务";
            if (!snapshot.Running) return "流程已暂停，等待继续";
            return "工位流程运行中……";
        }

        private static string StationDisplayName(int station)
        {
            switch (station)
            {
                case 1: return "取放料移载工位";
                case 2: return "取向弯折检测工位";
                case 3: return "无取向弯折工位";
                case 4: return "无取向检测工位";
                default: return "未知工位";
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
            txtRuntimeLog.SelectionStart = txtRuntimeLog.TextLength;
            txtRuntimeLog.SelectionLength = 0;
        }

        private void ClearPassiveSelections()
        {
            if (dgvTasks != null)
            {
                dgvTasks.ClearSelection();
                dgvTasks.CurrentCell = null;
            }
            if (txtRuntimeLog != null)
            {
                txtRuntimeLog.SelectionStart = txtRuntimeLog.TextLength;
                txtRuntimeLog.SelectionLength = 0;
            }
            ActiveControl = null;
        }

        private void DgvTasksMouseDown(object sender, MouseEventArgs e)
        {
            DataGridView.HitTestInfo hit = dgvTasks.HitTest(e.X, e.Y);
            if (hit.RowIndex >= 0) return;
            ClearTaskSelection();
        }

        private void ClearTaskSelectionOnBlankArea(object sender, MouseEventArgs e)
        {
            ClearTaskSelection();
        }

        private void ClearTaskSelection()
        {
            dgvTasks.ClearSelection();
            dgvTasks.CurrentCell = null;
            ActiveControl = null;
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
