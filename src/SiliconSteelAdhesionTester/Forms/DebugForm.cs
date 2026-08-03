using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SiliconSteelAdhesionTester.Configuration;
using SiliconSteelAdhesionTester.Models;
using SiliconSteelAdhesionTester.Services.Plc;
using SiliconSteelAdhesionTester.Services.Vision;

namespace SiliconSteelAdhesionTester.Forms
{
    [System.ComponentModel.DesignerCategory("Form")]
    public partial class DebugForm : Form
    {
        private readonly IPlcService _plc;
        private readonly AppSettings _settings;
        private readonly Func<string, string, string, Task<AdhesionVisionResult>> _runS2VisionSimulation;
        private readonly CancellationToken _token;
        private readonly GroupBox _grpStations = new GroupBox();
        private readonly Label[] _stationStepLabels = new Label[4];
        private readonly Label[] _stationStateLabels = new Label[4];
        private readonly Panel[] _stationRunLamps = new Panel[4];
        private readonly Panel[] _stationReadyLamps = new Panel[4];
        private readonly Panel[] _stationDoneLamps = new Panel[4];
        private readonly Label[] _flowLabels = new Label[8];
        private readonly TextBox _txtSimulationQr = new TextBox();
        private readonly TextBox _txtBeforeImage = new TextBox();
        private readonly TextBox _txtAfterImage = new TextBox();
        private readonly TextBox _txtSimulationLog = new TextBox();
        private readonly Label _lblSimulationResult = new Label();
        private readonly Label _lblModeNotice = new Label();
        private readonly Button _btnGenerateDemoImages = new Button();
        private readonly Button _btnRunS2Simulation = new Button();
        private readonly ProgressBar _simulationProgress = new ProgressBar();
        private readonly PictureBox _picBeforePreview = new PictureBox();
        private readonly PictureBox _picAfterPreview = new PictureBox();
        private readonly PictureBox _picResultPreview = new PictureBox();
        private TabControl _tabs;

        private sealed class PlcSignalShortcut
        {
            public PlcSignalShortcut(string name, string address, bool pulse = false)
            {
                Name = name;
                Address = address;
                Pulse = pulse;
            }

            public string Name { get; private set; }
            public string Address { get; private set; }
            public bool Pulse { get; private set; }
        }

        public DebugForm()
        {
            InitializeComponent();
        }

        public DebugForm(
            IPlcService plc,
            UserSession user,
            AppSettings settings,
            Func<string, string, string, Task<AdhesionVisionResult>> runS2VisionSimulation,
            CancellationToken token)
            : this()
        {
            _plc = plc ?? throw new ArgumentNullException(nameof(plc));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _runS2VisionSimulation = runS2VisionSimulation ??
                throw new ArgumentNullException(nameof(runS2VisionSimulation));
            _token = token;
            if (user == null) throw new ArgumentNullException(nameof(user));

            ConfigureRuntimeUi(user);
            Text = "调试诊断 - " + user.DisplayName;
            btnRead.Click += async (s, e) => await ExecuteSafe(ReadValueAsync);
            btnOn.Click += async (s, e) => await ExecuteSafe(() => WriteValueAsync(true));
            btnOff.Click += async (s, e) => await ExecuteSafe(() => WriteValueAsync(false));
        }

        private void ConfigureRuntimeUi(UserSession user)
        {
            SuspendLayout();
            Controls.Clear();
            AutoScroll = false;
            MinimumSize = new Size(1280, 760);
            WindowState = FormWindowState.Maximized;
            BackColor = Color.FromArgb(240, 244, 248);

            BuildHeader(user);
            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 10F),
                Padding = new Point(22, 8)
            };
            _tabs.TabPages.Add(BuildVisionSimulationPage());
            _tabs.TabPages.Add(BuildSignalControlPage());
            Controls.Add(_tabs);
            Controls.Add(pnlHeader);
            ResumeLayout(true);
        }

        private void BuildHeader(UserSession user)
        {
            pnlHeader.Controls.Clear();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 126;
            pnlHeader.Padding = new Padding(30, 14, 24, 18);
            pnlHeader.BackColor = Color.FromArgb(25, 48, 72);

            Label title = new Label
            {
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(30, 14),
                Text = "调试诊断中心"
            };
            Label subtitle = new Label
            {
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 9.5F),
                ForeColor = Color.FromArgb(190, 210, 229),
                Location = new Point(33, 72),
                Text = "视觉流程仿真与PLC自动流程信号调试"
            };
            Label identity = new Label
            {
                AutoSize = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(400, 34),
                Location = new Point(Math.Max(640, ClientSize.Width - 430), 40),
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.White,
                Text = (_settings.Simulation ? "● PLC仿真模式" : "⚠ 实体PLC模式") + "  |  " + user.DisplayName
            };
            pnlHeader.Resize += (s, e) => identity.Left = Math.Max(640, pnlHeader.ClientSize.Width - identity.Width - 30);
            pnlHeader.Controls.Add(title);
            pnlHeader.Controls.Add(subtitle);
            pnlHeader.Controls.Add(identity);
        }

        private TabPage BuildVisionSimulationPage()
        {
            TabPage page = NewTabPage("手动附着性测试");
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 24),
                ColumnCount = 1,
                RowCount = 4
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 218));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _lblModeNotice.Dock = DockStyle.Fill;
            _lblModeNotice.Padding = new Padding(18, 0, 18, 0);
            _lblModeNotice.TextAlign = ContentAlignment.MiddleLeft;
            _lblModeNotice.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
            _lblModeNotice.BackColor = Color.FromArgb(224, 245, 235);
            _lblModeNotice.ForeColor = Color.FromArgb(25, 112, 72);
            _lblModeNotice.Text = _settings.Simulation
                ? "安全仿真：不会连接SR-1000，也不会驱动实体相机。选择两张图片即可完整模拟S2扫码、两次拍照和结果返回。"
                : "离线测试：手动选择压弯前、压弯后两张已有图片，只分析附着性；不会写PLC、不会触发相机，也不会改变生产任务计数。";
            layout.Controls.Add(_lblModeNotice, 0, 0);

            GroupBox inputs = NewGroup("测试数据");
            TableLayoutPanel form = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 12, 18, 14),
                ColumnCount = 3,
                RowCount = 3
            };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            for (int row = 0; row < 3; row++) form.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));

            _txtSimulationQr.Text = (_settings.Simulation ? "SIM-S2-" : "MANUAL-") + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            ConfigureInput(_txtSimulationQr);
            ConfigureInput(_txtBeforeImage);
            ConfigureInput(_txtAfterImage);
            AddInputRow(form, 0, "试样编号", _txtSimulationQr, null);
            AddInputRow(form, 1, "压弯前图片", _txtBeforeImage, CreateBrowseButton(_txtBeforeImage, _picBeforePreview));
            AddInputRow(form, 2, "压弯后图片", _txtAfterImage, CreateBrowseButton(_txtAfterImage, _picAfterPreview));
            inputs.Controls.Add(form);
            layout.Controls.Add(inputs, 0, 1);

            Panel actions = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 14, 0, 10) };
            _btnGenerateDemoImages.Text = "▣  生成示例图片";
            _btnGenerateDemoImages.Size = new Size(220, 54);
            _btnGenerateDemoImages.Location = new Point(0, 14);
            StyleActionButton(_btnGenerateDemoImages, Color.FromArgb(82, 103, 126));
            _btnGenerateDemoImages.Click += (s, e) => GenerateDemoImages();
            _btnRunS2Simulation.Text = "▶  分析附着性";
            _btnRunS2Simulation.Size = new Size(310, 54);
            _btnRunS2Simulation.Location = new Point(236, 14);
            StyleActionButton(_btnRunS2Simulation, Color.FromArgb(35, 112, 190));
            _btnRunS2Simulation.Enabled = true;
            _btnRunS2Simulation.Click += async (s, e) => await RunS2SimulationAsync();
            _simulationProgress.Size = new Size(260, 10);
            _simulationProgress.Location = new Point(562, 36);
            _simulationProgress.Style = ProgressBarStyle.Marquee;
            _simulationProgress.Visible = false;
            _lblSimulationResult.AutoSize = false;
            _lblSimulationResult.Location = new Point(838, 14);
            _lblSimulationResult.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _lblSimulationResult.Size = new Size(440, 50);
            _lblSimulationResult.TextAlign = ContentAlignment.MiddleLeft;
            _lblSimulationResult.ForeColor = Color.DimGray;
            _lblSimulationResult.Text = "等待执行";
            actions.Controls.Add(_btnGenerateDemoImages);
            actions.Controls.Add(_btnRunS2Simulation);
            actions.Controls.Add(_simulationProgress);
            actions.Controls.Add(_lblSimulationResult);
            actions.Resize += (s, e) =>
                _lblSimulationResult.Width = Math.Max(240, actions.ClientSize.Width - _lblSimulationResult.Left);
            layout.Controls.Add(actions, 0, 2);

            SplitContainer resultSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 8
            };
            resultSplit.SizeChanged += (s, e) =>
            {
                if (resultSplit.ClientSize.Width < 600) return;
                int desired = (int)(resultSplit.ClientSize.Width * 0.68);
                int maximum = resultSplit.ClientSize.Width - resultSplit.SplitterWidth - 240;
                resultSplit.SplitterDistance = Math.Max(320, Math.Min(desired, maximum));
            };
            GroupBox previewGroup = NewGroup("图片预览");
            previewGroup.Padding = new Padding(12, 28, 12, 12);
            TableLayoutPanel previews = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2
            };
            for (int i = 0; i < 3; i++) previews.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
            previews.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            previews.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            AddPreviewColumn(previews, 0, "压弯前", _picBeforePreview);
            AddPreviewColumn(previews, 1, "压弯后", _picAfterPreview);
            AddPreviewColumn(previews, 2, "分析标记结果", _picResultPreview);
            previewGroup.Controls.Add(previews);
            resultSplit.Panel1.Controls.Add(previewGroup);

            GroupBox logGroup = NewGroup("测试过程与结果");
            _txtSimulationLog.Dock = DockStyle.Fill;
            _txtSimulationLog.Multiline = true;
            _txtSimulationLog.ReadOnly = true;
            _txtSimulationLog.ScrollBars = ScrollBars.Vertical;
            _txtSimulationLog.BackColor = Color.FromArgb(248, 250, 252);
            _txtSimulationLog.BorderStyle = BorderStyle.None;
            _txtSimulationLog.Font = new Font("Consolas", 10F);
            _txtSimulationLog.Margin = new Padding(12);
            logGroup.Padding = new Padding(16, 28, 16, 14);
            logGroup.Controls.Add(_txtSimulationLog);
            resultSplit.Panel2.Controls.Add(logGroup);
            layout.Controls.Add(resultSplit, 0, 3);

            page.Controls.Add(layout);
            return page;
        }

        private TabPage BuildStationPage()
        {
            TabPage page = NewTabPage("工位状态与单步");
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 24),
                ColumnCount = 1,
                RowCount = 2
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 154));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            GroupBox flow = NewGroup("全流程进度诊断");
            TableLayoutPanel flowLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 10, 12, 12),
                ColumnCount = 8,
                RowCount = 1
            };
            string[] steps = { "AGV送料", "S1来料", "读取二维码", "二维码校验", "相机拍照", "视觉判定", "工位加工", "等待来料" };
            for (int i = 0; i < steps.Length; i++)
            {
                flowLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
                _flowLabels[i] = new Label
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(5),
                    TextAlign = ContentAlignment.MiddleCenter,
                    AutoEllipsis = true,
                    BackColor = i == 0 ? Color.FromArgb(35, 112, 190) : Color.FromArgb(235, 239, 244),
                    ForeColor = i == 0 ? Color.White : Color.FromArgb(45, 55, 65),
                    Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
                    Text = (i + 1) + Environment.NewLine + steps[i]
                };
                flowLayout.Controls.Add(_flowLabels[i], i, 0);
            }
            flow.Controls.Add(flowLayout);
            layout.Controls.Add(flow, 0, 0);

            BuildStationDebugPanel();
            _grpStations.Dock = DockStyle.Fill;
            layout.Controls.Add(_grpStations, 0, 1);
            page.Controls.Add(layout);
            return page;
        }

        private TabPage BuildSignalControlPage()
        {
            TabPage page = NewTabPage("PLC流程信号调试");
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(22, 16, 22, 20),
                ColumnCount = 1,
                RowCount = 3
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));

            Label warning = new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 0, 18, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                BackColor = _settings.Simulation ? Color.FromArgb(231, 239, 247) : Color.FromArgb(255, 235, 235),
                ForeColor = _settings.Simulation ? Color.FromArgb(35, 83, 125) : Color.Firebrick,
                Text = _settings.Simulation
                    ? "PLC信号驱动仿真：按流程置位“允许/到料”，观察主界面是否进入下一步；测试后请及时复位。"
                    : "实体PLC模式：下方按钮会直接读写PLC并可能触发设备动作，操作前必须确认现场安全。"
            };
            layout.Controls.Add(warning, 0, 0);

            TabControl signalTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 10F),
                Padding = new Point(18, 6)
            };
            signalTabs.TabPages.Add(BuildSignalCategory("流程条件", new[]
            {
                new PlcSignalShortcut("整机自动运行条件", PlcAddresses.LineRunningCondition),
                new PlcSignalShortcut("上料工位自动运行", PlcAddresses.S1AutomaticRunning),
                new PlcSignalShortcut("取向待检测工位有料", PlcAddresses.S2HasPendingMaterial),
                new PlcSignalShortcut("无取向弯折工位有料", PlcAddresses.S3HasPendingMaterial),
                new PlcSignalShortcut("无取向样品到达检测工位", PlcAddresses.S4HasMaterialForTape),
                new PlcSignalShortcut("整机在零位（只读状态）", PlcAddresses.WholeLineHome)
            }));
            signalTabs.TabPages.Add(BuildSignalCategory("取向检测", new[]
            {
                new PlcSignalShortcut("允许读取二维码", PlcAddresses.S2ScanAllowed),
                new PlcSignalShortcut("二维码读取完成", PlcAddresses.S2ScanDone),
                new PlcSignalShortcut("二维码读取成功", PlcAddresses.S2ScanOk),
                new PlcSignalShortcut("二维码读取失败", PlcAddresses.S2ScanNg),
                new PlcSignalShortcut("允许压弯前拍照", PlcAddresses.S2FirstPhotoAllowed),
                new PlcSignalShortcut("压弯前拍照完成", PlcAddresses.S2FirstPhotoDone),
                new PlcSignalShortcut("允许压弯后拍照", PlcAddresses.S2SecondPhotoAllowed),
                new PlcSignalShortcut("压弯后拍照完成", PlcAddresses.S2SecondPhotoDone),
                new PlcSignalShortcut("压弯后检测合格", PlcAddresses.S2SecondPhotoOk),
                new PlcSignalShortcut("压弯后检测不合格", PlcAddresses.S2SecondPhotoNg)
            }));
            signalTabs.TabPages.Add(BuildSignalCategory("无取向检测", new[]
            {
                new PlcSignalShortcut("允许读取二维码", PlcAddresses.S3ScanAllowed),
                new PlcSignalShortcut("二维码读取完成", PlcAddresses.S3ScanDone),
                new PlcSignalShortcut("二维码读取成功", PlcAddresses.S3ScanOk),
                new PlcSignalShortcut("二维码读取失败", PlcAddresses.S3ScanNg),
                new PlcSignalShortcut("样品到达检测工位", PlcAddresses.S4HasMaterialForTape),
                new PlcSignalShortcut("允许相机拍照", PlcAddresses.S4CameraAllowed),
                new PlcSignalShortcut("相机拍照完成", PlcAddresses.S4CameraDone),
                new PlcSignalShortcut("检测合格", PlcAddresses.S4CameraOk),
                new PlcSignalShortcut("检测不合格", PlcAddresses.S4CameraNg)
            }));
            signalTabs.TabPages.Add(BuildSignalCategory("整机控制", new[]
            {
                new PlcSignalShortcut("自动/手动模式（取反）", PlcAddresses.AutoMode),
                new PlcSignalShortcut("整机启动/继续", PlcAddresses.LineStart, true),
                new PlcSignalShortcut("整机暂停", PlcAddresses.LinePause, true),
                new PlcSignalShortcut("整机回原位", PlcAddresses.LineHome, true),
                new PlcSignalShortcut("故障复位", PlcAddresses.ResetPulse, true)
            }));
            layout.Controls.Add(signalTabs, 0, 1);

            layout.Controls.Add(BuildAdvancedAddressTool(), 0, 2);
            page.Controls.Add(layout);
            return page;
        }

        private TabPage BuildSignalCategory(string title, PlcSignalShortcut[] signals)
        {
            TabPage page = NewTabPage(title);
            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 5,
                RowCount = signals.Length + 1,
                Padding = new Padding(12, 12, 12, 16)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 184));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            string[] headers = { "信号名称", "PLC地址", "置 ON", "置 OFF", "读取当前值" };
            for (int column = 0; column < headers.Length; column++)
                table.Controls.Add(NewSignalCell(headers[column], true), column, 0);

            for (int row = 0; row < signals.Length; row++)
            {
                PlcSignalShortcut signal = signals[row];
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
                table.Controls.Add(NewSignalCell(signal.Name, false), 0, row + 1);
                table.Controls.Add(NewSignalCell(signal.Address, false, true), 1, row + 1);

                Button on = NewSignalButton(signal.Pulse ? "发送脉冲" : "置 ON", Color.FromArgb(39, 145, 91));
                Button off = NewSignalButton("置 OFF", Color.FromArgb(196, 68, 68));
                Button read = NewSignalButton("读取", Color.FromArgb(58, 126, 174));
                Label value = NewSignalCell("--", false);
                value.TextAlign = ContentAlignment.MiddleCenter;
                value.ForeColor = Color.FromArgb(35, 83, 125);
                value.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);

                if (signal.Pulse)
                {
                    on.Click += async (s, e) =>
                    {
                        if (!ConfirmEntitySignalAction(signal.Name + "脉冲")) return;
                        await ExecuteSignalAction(
                            () => _plc.PulseAsync(signal.Address, _token),
                            signal.Name + "脉冲已发送");
                    };
                    off.Enabled = false;
                    off.Text = "脉冲信号";
                }
                else
                {
                    on.Click += async (s, e) => await ExecuteSignalWrite(signal, true, value);
                    off.Click += async (s, e) => await ExecuteSignalWrite(signal, false, value);
                }
                read.Click += async (s, e) => await ExecuteSignalRead(signal, value);

                table.Controls.Add(on, 2, row + 1);
                table.Controls.Add(off, 3, row + 1);
                Panel readPanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(4) };
                read.Dock = DockStyle.Left;
                read.Width = 82;
                value.Dock = DockStyle.Fill;
                readPanel.Controls.Add(value);
                readPanel.Controls.Add(read);
                table.Controls.Add(readPanel, 4, row + 1);
            }

            Panel scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            scroll.Controls.Add(table);
            page.Controls.Add(scroll);
            return page;
        }

        private Control BuildAdvancedAddressTool()
        {
            GroupBox group = NewGroup("高级地址读写（用于交互表中未预置的点位）");
            group.Padding = new Padding(14, 26, 14, 10);
            TableLayoutPanel row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 1
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            txtAddress.Dock = DockStyle.Fill;
            txtAddress.Font = new Font("Consolas", 10F);
            txtAddress.Margin = new Padding(4, 8, 8, 8);
            txtValue.Dock = DockStyle.Fill;
            txtValue.Margin = new Padding(4, 8, 8, 8);
            SetupButton(btnRead, "读取", 0, 0, 80, Color.FromArgb(58, 126, 174));
            SetupButton(btnOn, "置 ON", 0, 0, 80, Color.FromArgb(39, 145, 91));
            SetupButton(btnOff, "置 OFF", 0, 0, 80, Color.FromArgb(196, 68, 68));
            foreach (Button button in new[] { btnRead, btnOn, btnOff })
            {
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(4, 7, 4, 7);
            }
            row.Controls.Add(NewToolbarLabel("PLC地址"), 0, 0);
            row.Controls.Add(txtAddress, 1, 0);
            row.Controls.Add(NewToolbarLabel("当前值"), 2, 0);
            row.Controls.Add(txtValue, 3, 0);
            row.Controls.Add(btnRead, 4, 0);
            row.Controls.Add(btnOn, 5, 0);
            row.Controls.Add(btnOff, 6, 0);
            group.Controls.Add(row);
            return group;
        }

        private async Task ExecuteSignalWrite(PlcSignalShortcut signal, bool value, Label valueLabel)
        {
            if (!ConfirmEntitySignalAction(signal.Name + " 置为 " + (value ? "ON" : "OFF"))) return;
            await ExecuteSignalAction(
                () => _plc.WriteAsync(signal.Address, value, _token),
                signal.Name + " 已置为 " + (value ? "ON" : "OFF"));
            valueLabel.Text = value ? "ON" : "OFF";
            valueLabel.ForeColor = value ? Color.SeaGreen : Color.DimGray;
        }

        private async Task ExecuteSignalRead(PlcSignalShortcut signal, Label valueLabel)
        {
            object value = null;
            await ExecuteSignalAction(async () =>
            {
                value = await _plc.ReadAsync(signal.Address, _token);
            }, signal.Name + " 读取完成");
            bool isOn;
            if (value != null && bool.TryParse(Convert.ToString(value), out isOn))
            {
                valueLabel.Text = isOn ? "ON" : "OFF";
                valueLabel.ForeColor = isOn ? Color.SeaGreen : Color.DimGray;
            }
            else
            {
                valueLabel.Text = value == null ? "--" : Convert.ToString(value);
            }
        }

        private bool ConfirmEntitySignalAction(string actionName)
        {
            if (_settings.Simulation) return true;
            return MessageBox.Show(
                "即将向实体PLC发送“" + actionName + "”。\r\n\r\n请确认设备周围无人、机构动作安全后再继续。",
                "实体PLC操作确认",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private async Task ExecuteSignalAction(Func<Task> action, string successText)
        {
            await ExecuteSafe(async () =>
            {
                await action();
                lblResult.Text = successText;
            });
        }

        private static Label NewSignalCell(string text, bool header, bool address = false)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(1),
                Padding = new Padding(10, 0, 8, 0),
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                BackColor = header ? Color.FromArgb(222, 232, 242) : Color.FromArgb(248, 250, 252),
                ForeColor = Color.FromArgb(38, 48, 58),
                Font = address
                    ? new Font("Consolas", 9.5F)
                    : new Font("Microsoft YaHei UI", 9.5F, header ? FontStyle.Bold : FontStyle.Regular)
            };
        }

        private static Button NewSignalButton(string text, Color color)
        {
            Button button = new Button
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(5, 7, 5, 7),
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = color,
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private TabPage BuildRegisterPage()
        {
            TabPage page = NewTabPage("PLC寄存器工具");
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 24),
                ColumnCount = 1,
                RowCount = 2
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Label warning = new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 0, 18, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = _settings.Simulation ? Color.FromArgb(231, 239, 247) : Color.FromArgb(255, 235, 235),
                ForeColor = _settings.Simulation ? Color.FromArgb(35, 83, 125) : Color.Firebrick,
                Text = _settings.Simulation
                    ? "当前为PLC信号驱动仿真。点击整机启动后不会自动推进；请在此写入扫码、拍照等允许信号，寄存器操作只影响仿真内存。"
                    : "警告：当前为实体PLC模式，写点可能直接触发设备动作。"
            };
            layout.Controls.Add(warning, 0, 0);

            grpRegister.Controls.Clear();
            grpRegister.Text = "地址读写";
            grpRegister.Dock = DockStyle.Fill;
            grpRegister.Padding = new Padding(18, 30, 18, 18);

            TableLayoutPanel registerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(6, 4, 6, 4)
            };
            registerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            registerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            registerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            registerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            TableLayoutPanel addressRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 1,
                Margin = new Padding(0)
            };
            addressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            addressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            addressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            addressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            addressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            addressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            addressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

            txtAddress.Font = new Font("Consolas", 10.5F);
            txtAddress.Dock = DockStyle.Fill;
            txtAddress.Margin = new Padding(6, 12, 12, 10);
            txtValue.Dock = DockStyle.Fill;
            txtValue.Margin = new Padding(6, 12, 12, 10);
            SetupButton(btnRead, "读取", 0, 0, 92, Color.FromArgb(58, 126, 174));
            SetupButton(btnOn, "置 ON", 0, 0, 92, Color.FromArgb(39, 145, 91));
            SetupButton(btnOff, "置 OFF", 0, 0, 92, Color.FromArgb(196, 68, 68));
            Button[] registerButtons = { btnRead, btnOn, btnOff };
            foreach (Button button in registerButtons)
            {
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(6, 10, 6, 10);
            }
            Label addressLabel = NewToolbarLabel("PLC地址");
            addressLabel.Dock = DockStyle.Fill;
            addressLabel.TextAlign = ContentAlignment.MiddleRight;
            addressLabel.Margin = new Padding(0);
            Label valueLabel = NewToolbarLabel("当前值");
            valueLabel.Dock = DockStyle.Fill;
            valueLabel.TextAlign = ContentAlignment.MiddleRight;
            valueLabel.Margin = new Padding(0);
            addressRow.Controls.Add(addressLabel, 0, 0);
            addressRow.Controls.Add(txtAddress, 1, 0);
            addressRow.Controls.Add(valueLabel, 2, 0);
            addressRow.Controls.Add(txtValue, 3, 0);
            addressRow.Controls.Add(btnRead, 4, 0);
            addressRow.Controls.Add(btnOn, 5, 0);
            addressRow.Controls.Add(btnOff, 6, 0);
            registerLayout.Controls.Add(addressRow, 0, 0);

            Label quickTitle = new Label
            {
                Dock = DockStyle.Fill,
                Text = "常用自动流程信号（点击后仅填入地址，再选择置ON或置OFF）",
                ForeColor = Color.FromArgb(70, 82, 94),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };
            registerLayout.Controls.Add(quickTitle, 0, 1);

            TableLayoutPanel quickGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Margin = new Padding(0)
            };
            for (int column = 0; column < 3; column++)
                quickGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
            quickGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            quickGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            string[] quickNames =
            {
                "取向扫码允许（S2）",
                "取向压弯前拍照（S2）",
                "取向压弯后拍照（S2）",
                "无取向扫码允许（S3）",
                "无取向检测到料（.4）",
                "无取向拍照允许（S4）"
            };
            string[] quickAddresses =
            {
                PlcAddresses.S2ScanAllowed,
                PlcAddresses.S2FirstPhotoAllowed,
                PlcAddresses.S2SecondPhotoAllowed,
                PlcAddresses.S3ScanAllowed,
                PlcAddresses.S4HasMaterialForTape,
                PlcAddresses.S4CameraAllowed
            };
            for (int i = 0; i < quickNames.Length; i++)
            {
                string address = quickAddresses[i];
                Button quick = new Button
                {
                    Text = quickNames[i],
                    Dock = DockStyle.Fill,
                    Margin = new Padding(6),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.White,
                    Font = new Font("Microsoft YaHei UI", 9.5F)
                };
                quick.Click += (s, e) => txtAddress.Text = address;
                quickGrid.Controls.Add(quick, i % 3, i / 3);
            }
            registerLayout.Controls.Add(quickGrid, 0, 2);

            lblResult.AutoSize = false;
            lblResult.Dock = DockStyle.Top;
            lblResult.Height = 42;
            lblResult.Margin = new Padding(8, 12, 8, 0);
            lblResult.Padding = new Padding(12, 0, 12, 0);
            lblResult.TextAlign = ContentAlignment.MiddleLeft;
            lblResult.BackColor = Color.FromArgb(247, 249, 252);
            lblResult.Text = "等待操作";
            registerLayout.Controls.Add(lblResult, 0, 3);
            grpRegister.Controls.Add(registerLayout);
            layout.Controls.Add(grpRegister, 0, 1);
            page.Controls.Add(layout);
            return page;
        }

        private void BuildStationDebugPanel()
        {
            _grpStations.Controls.Clear();
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
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            for (int i = 1; i < 5; i++) table.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

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
                    continuous.BackColor = !current ? Color.FromArgb(224, 245, 235) : Color.White;
                });
                start.Click += async (s, e) =>
                    await ExecuteSafe(() => _plc.PulseAsync(PlcAddresses.StationStart(station), _token));
                table.Controls.Add(continuous, 6, station);
                table.Controls.Add(start, 7, station);
            }
            _grpStations.Controls.Add(table);
        }

        private async Task RunS2SimulationAsync()
        {
            string qrCode = _txtSimulationQr.Text.Trim();
            string before = _txtBeforeImage.Text.Trim();
            string after = _txtAfterImage.Text.Trim();
            if (string.IsNullOrWhiteSpace(qrCode))
            {
                ShowSimulationValidation("请输入试样编号。");
                return;
            }
            if (!File.Exists(before))
            {
                ShowSimulationValidation("请选择有效的压弯前图片。");
                return;
            }
            if (!File.Exists(after))
            {
                ShowSimulationValidation("请选择有效的压弯后图片。");
                return;
            }

            _btnRunS2Simulation.Enabled = false;
            _simulationProgress.Visible = true;
            ShowPreview(_picBeforePreview, before);
            ShowPreview(_picAfterPreview, after);
            ShowPreview(_picResultPreview, null);
            _lblSimulationResult.ForeColor = Color.FromArgb(35, 83, 125);
            _lblSimulationResult.Text = _settings.Simulation
                ? "正在执行S2视觉仿真与分析…"
                : "正在分析压弯前后图片的附着性…";
            AppendSimulationLog("START", (_settings.Simulation ? "开始S2视觉仿真：" : "开始手动附着性测试：") + qrCode);
            try
            {
                AdhesionVisionResult result = await _runS2VisionSimulation(qrCode, before, after);
                ShowPreview(_picResultPreview, result.AnnotatedImagePath);
                _lblSimulationResult.ForeColor = result.IsQualified ? Color.SeaGreen : Color.Firebrick;
                _lblSimulationResult.Text =
                    (result.IsQualified ? "OK 合格" : "NG 不合格") +
                    "  脱落率 " + result.LossRatePercent.ToString("F3") +
                    "%  颗粒 " + result.ParticleCount;
                AppendSimulationLog(
                    result.IsQualified ? "OK" : "NG",
                    result.Message + "；标记图：" + result.AnnotatedImagePath);
            }
            catch (Exception ex)
            {
                _lblSimulationResult.ForeColor = Color.Firebrick;
                _lblSimulationResult.Text = "执行失败：" + ex.Message;
                AppendSimulationLog("ERROR", ex.Message);
                MessageBox.Show(this, ex.Message, "附着性测试失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _simulationProgress.Visible = false;
                _btnRunS2Simulation.Enabled = true;
            }
        }

        private void ShowSimulationValidation(string message)
        {
            _lblSimulationResult.ForeColor = Color.Firebrick;
            _lblSimulationResult.Text = message;
            AppendSimulationLog("WARN", message);
        }

        private void AppendSimulationLog(string level, string message)
        {
            _txtSimulationLog.AppendText(
                DateTime.Now.ToString("HH:mm:ss.fff") + "  [" + level + "] " +
                message + Environment.NewLine);
        }

        private void GenerateDemoImages()
        {
            try
            {
                string directory = Path.Combine(
                    Path.GetTempPath(),
                    "SiliconSteelAdhesionTester",
                    "VisionSimulation");
                Directory.CreateDirectory(directory);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                string beforePath = Path.Combine(directory, stamp + "_before.png");
                string afterPath = Path.Combine(directory, stamp + "_after.png");
                const int width = 1600;
                const int height = 300;
                Rectangle sample = new Rectangle(40, 75, 1520, 150);

                using (Bitmap before = new Bitmap(width, height))
                using (Graphics graphics = Graphics.FromImage(before))
                {
                    graphics.Clear(Color.FromArgb(235, 238, 241));
                    using (Brush steel = new SolidBrush(Color.FromArgb(82, 88, 94)))
                        graphics.FillRectangle(steel, sample);
                    using (Pen edge = new Pen(Color.FromArgb(42, 46, 50), 3))
                        graphics.DrawRectangle(edge, sample);
                    before.Save(beforePath, System.Drawing.Imaging.ImageFormat.Png);
                }

                using (Bitmap after = new Bitmap(beforePath))
                using (Graphics graphics = Graphics.FromImage(after))
                {
                    using (Brush defect = new SolidBrush(Color.FromArgb(198, 205, 212)))
                    {
                        graphics.FillEllipse(defect, 380, 112, 34, 22);
                        graphics.FillEllipse(defect, 785, 126, 46, 25);
                        graphics.FillEllipse(defect, 1180, 105, 28, 34);
                        graphics.FillRectangle(defect, 1320, 154, 52, 15);
                    }
                    after.Save(afterPath, System.Drawing.Imaging.ImageFormat.Png);
                }

                _txtBeforeImage.Text = beforePath;
                _txtAfterImage.Text = afterPath;
                ShowPreview(_picBeforePreview, beforePath);
                ShowPreview(_picAfterPreview, afterPath);
                ShowPreview(_picResultPreview, null);
                _lblSimulationResult.ForeColor = Color.FromArgb(35, 83, 125);
                _lblSimulationResult.Text = "示例图片已生成，可以直接点击“一键执行”";
                AppendSimulationLog("DEMO", "已生成320×30比例的压弯前/后示例图片。");
            }
            catch (Exception ex)
            {
                ShowSimulationValidation("生成示例图片失败：" + ex.Message);
            }
        }

        private static void AddPreviewColumn(
            TableLayoutPanel table,
            int column,
            string title,
            PictureBox picture)
        {
            Label label = new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold)
            };
            picture.Dock = DockStyle.Fill;
            picture.Margin = new Padding(5);
            picture.BackColor = Color.FromArgb(31, 38, 46);
            picture.SizeMode = PictureBoxSizeMode.Zoom;
            table.Controls.Add(label, column, 0);
            table.Controls.Add(picture, column, 1);
        }

        private static void ShowPreview(PictureBox target, string path)
        {
            Image previous = target.Image;
            target.Image = null;
            if (previous != null) previous.Dispose();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (Image image = Image.FromStream(stream))
                target.Image = new Bitmap(image);
        }

        private static Button CreateBrowseButton(TextBox target, PictureBox preview)
        {
            Button button = new Button
            {
                Text = "选择图片…",
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 8, 0, 8),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White
            };
            button.Click += (s, e) =>
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Filter = "图像文件|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff|所有文件|*.*";
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        target.Text = dialog.FileName;
                        ShowPreview(preview, dialog.FileName);
                    }
                }
            };
            return button;
        }

        private static void AddInputRow(
            TableLayoutPanel table,
            int row,
            string labelText,
            TextBox input,
            Button browse)
        {
            Label label = new Label
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold)
            };
            input.Dock = DockStyle.Fill;
            input.Margin = new Padding(0, 9, 0, 9);
            table.Controls.Add(label, 0, row);
            table.Controls.Add(input, 1, row);
            if (browse != null) table.Controls.Add(browse, 2, row);
        }

        private static void ConfigureInput(TextBox input)
        {
            input.Font = new Font("Microsoft YaHei UI", 10.5F);
            input.BorderStyle = BorderStyle.FixedSingle;
        }

        private static TabPage NewTabPage(string text)
        {
            return new TabPage
            {
                Text = text,
                BackColor = Color.FromArgb(240, 244, 248),
                Padding = new Padding(0)
            };
        }

        private static GroupBox NewGroup(string text)
        {
            return new GroupBox
            {
                Text = text,
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold)
            };
        }

        private static Label NewToolbarLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(6, 8, 6, 8)
            };
        }

        private void PlcSnapshotChanged(object sender, PlcSnapshot snapshot)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<object, PlcSnapshot>(PlcSnapshotChanged), sender, snapshot);
                return;
            }

            int current = Math.Max(0, Math.Min(_flowLabels.Length - 1, snapshot.FlowStepIndex));
            for (int i = 0; i < _flowLabels.Length; i++)
            {
                if (_flowLabels[i] == null) continue;
                _flowLabels[i].BackColor = i == current
                    ? Color.FromArgb(35, 112, 190)
                    : i < current
                        ? Color.FromArgb(218, 242, 229)
                        : Color.FromArgb(235, 239, 244);
                _flowLabels[i].ForeColor = i == current ? Color.White : Color.FromArgb(45, 55, 65);
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
                BackColor = bold ? Color.FromArgb(226, 235, 244) : Color.FromArgb(247, 249, 252),
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
            Button button = new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 12, 6, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(183, 194, 205);
            return button;
        }

        private static void StyleActionButton(Button button, Color color)
        {
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
            button.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
        }

        private static void SetupButton(Button button, string text, int x, int y, int width, Color color)
        {
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, 38);
            button.Margin = new Padding(8, 12, 0, 0);
            StyleActionButton(button, color);
        }

        private async Task ExecuteSafe(Func<Task> action)
        {
            try
            {
                await action();
                lblResult.ForeColor = Color.SeaGreen;
                lblResult.Text = "操作成功  " + DateTime.Now.ToString("HH:mm:ss");
            }
            catch (Exception ex)
            {
                lblResult.ForeColor = Color.Firebrick;
                lblResult.Text = "操作失败：" + ex.Message;
                MessageBox.Show(ex.Message, "PLC操作失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ReadValueAsync()
        {
            txtValue.Text = Convert.ToString(await _plc.ReadAsync(txtAddress.Text.Trim(), _token));
        }

        private async Task WriteValueAsync(bool value)
        {
            await _plc.WriteAsync(txtAddress.Text.Trim(), value, _token);
            await ReadValueAsync();
        }
    }
}
