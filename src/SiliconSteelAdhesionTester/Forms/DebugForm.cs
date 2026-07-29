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
            _plc.SnapshotChanged += PlcSnapshotChanged;
            FormClosed += (s, e) => _plc.SnapshotChanged -= PlcSnapshotChanged;
        }

        private void ConfigureRuntimeUi(UserSession user)
        {
            SuspendLayout();
            Controls.Clear();
            AutoScroll = false;
            MinimumSize = new Size(1080, 720);
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
            _tabs.TabPages.Add(BuildStationPage());
            _tabs.TabPages.Add(BuildRegisterPage());
            Controls.Add(_tabs);
            Controls.Add(pnlHeader);
            ResumeLayout(true);
        }

        private void BuildHeader(UserSession user)
        {
            pnlHeader.Controls.Clear();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 88;
            pnlHeader.Padding = new Padding(30, 14, 24, 12);
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
                Location = new Point(33, 54),
                Text = "视觉流程仿真、工位状态和PLC寄存器工具"
            };
            Label identity = new Label
            {
                AutoSize = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(400, 34),
                Location = new Point(Math.Max(640, ClientSize.Width - 430), 27),
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
            TabPage page = NewTabPage("S2视觉仿真");
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 24),
                ColumnCount = 1,
                RowCount = 4
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 218));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _lblModeNotice.Dock = DockStyle.Fill;
            _lblModeNotice.Padding = new Padding(18, 0, 18, 0);
            _lblModeNotice.TextAlign = ContentAlignment.MiddleLeft;
            _lblModeNotice.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
            _lblModeNotice.BackColor = _settings.Simulation
                ? Color.FromArgb(224, 245, 235)
                : Color.FromArgb(255, 235, 235);
            _lblModeNotice.ForeColor = _settings.Simulation
                ? Color.FromArgb(25, 112, 72)
                : Color.Firebrick;
            _lblModeNotice.Text = _settings.Simulation
                ? "安全仿真：不会连接SR-1000，也不会驱动实体相机。选择两张图片即可完整模拟S2扫码、两次拍照和结果返回。"
                : "当前是实体PLC模式，一键视觉仿真已禁用；请切换到PLC仿真模式后重启程序。";
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
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            for (int row = 0; row < 3; row++) form.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));

            _txtSimulationQr.Text = "SIM-S2-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            ConfigureInput(_txtSimulationQr);
            ConfigureInput(_txtBeforeImage);
            ConfigureInput(_txtAfterImage);
            AddInputRow(form, 0, "测试二维码", _txtSimulationQr, null);
            AddInputRow(form, 1, "压弯前图片", _txtBeforeImage, CreateBrowseButton(_txtBeforeImage, _picBeforePreview));
            AddInputRow(form, 2, "压弯后图片", _txtAfterImage, CreateBrowseButton(_txtAfterImage, _picAfterPreview));
            inputs.Controls.Add(form);
            layout.Controls.Add(inputs, 0, 1);

            Panel actions = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 14, 0, 10) };
            _btnGenerateDemoImages.Text = "▣  生成示例图片";
            _btnGenerateDemoImages.Size = new Size(180, 50);
            _btnGenerateDemoImages.Location = new Point(0, 14);
            StyleActionButton(_btnGenerateDemoImages, Color.FromArgb(82, 103, 126));
            _btnGenerateDemoImages.Click += (s, e) => GenerateDemoImages();
            _btnRunS2Simulation.Text = "▶  一键执行S2视觉流程";
            _btnRunS2Simulation.Size = new Size(260, 50);
            _btnRunS2Simulation.Location = new Point(194, 14);
            StyleActionButton(_btnRunS2Simulation, Color.FromArgb(35, 112, 190));
            _btnRunS2Simulation.Enabled = _settings.Simulation;
            _btnRunS2Simulation.Click += async (s, e) => await RunS2SimulationAsync();
            _simulationProgress.Size = new Size(260, 10);
            _simulationProgress.Location = new Point(470, 34);
            _simulationProgress.Style = ProgressBarStyle.Marquee;
            _simulationProgress.Visible = false;
            _lblSimulationResult.AutoSize = false;
            _lblSimulationResult.Location = new Point(750, 14);
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

            GroupBox logGroup = NewGroup("仿真过程与结果");
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

        private TabPage BuildRegisterPage()
        {
            TabPage page = NewTabPage("PLC寄存器工具");
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 300,
                Padding = new Padding(24, 20, 24, 24),
                ColumnCount = 1,
                RowCount = 2
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));

            Label warning = new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 0, 18, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = _settings.Simulation ? Color.FromArgb(231, 239, 247) : Color.FromArgb(255, 235, 235),
                ForeColor = _settings.Simulation ? Color.FromArgb(35, 83, 125) : Color.Firebrick,
                Text = _settings.Simulation
                    ? "当前为PLC仿真模式。寄存器操作只影响仿真内存。"
                    : "警告：当前为实体PLC模式，写点可能直接触发设备动作。"
            };
            layout.Controls.Add(warning, 0, 0);

            grpRegister.Controls.Clear();
            grpRegister.Text = "地址读写";
            grpRegister.Dock = DockStyle.Fill;
            grpRegister.Padding = new Padding(18, 26, 18, 14);
            FlowLayoutPanel toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(6, 12, 6, 6)
            };
            txtAddress.Width = 330;
            txtAddress.Font = new Font("Consolas", 10.5F);
            txtValue.Width = 150;
            SetupButton(btnRead, "读取", 0, 0, 92, Color.FromArgb(58, 126, 174));
            SetupButton(btnOn, "置 ON", 0, 0, 92, Color.FromArgb(39, 145, 91));
            SetupButton(btnOff, "置 OFF", 0, 0, 92, Color.FromArgb(196, 68, 68));
            toolbar.Controls.Add(NewToolbarLabel("PLC地址"));
            toolbar.Controls.Add(txtAddress);
            toolbar.Controls.Add(NewToolbarLabel("当前值"));
            toolbar.Controls.Add(txtValue);
            toolbar.Controls.Add(btnRead);
            toolbar.Controls.Add(btnOn);
            toolbar.Controls.Add(btnOff);

            string[] quickNames = { "S2扫码允许", "S2第一次拍照", "S2第二次拍照", "S3扫码允许", "S4拍照允许" };
            string[] quickAddresses =
            {
                PlcAddresses.S2ScanAllowed,
                PlcAddresses.S2FirstPhotoAllowed,
                PlcAddresses.S2SecondPhotoAllowed,
                PlcAddresses.S3ScanAllowed,
                PlcAddresses.S4CameraAllowed
            };
            for (int i = 0; i < quickNames.Length; i++)
            {
                string address = quickAddresses[i];
                Button quick = new Button
                {
                    Text = quickNames[i],
                    AutoSize = true,
                    Height = 38,
                    Margin = new Padding(8, 12, 0, 0),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.White
                };
                quick.Click += (s, e) => txtAddress.Text = address;
                toolbar.Controls.Add(quick);
            }
            lblResult.AutoSize = true;
            lblResult.Margin = new Padding(16, 20, 0, 0);
            lblResult.Text = "等待操作";
            toolbar.Controls.Add(lblResult);
            grpRegister.Controls.Add(toolbar);
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
                ShowSimulationValidation("请输入测试二维码。");
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
            _lblSimulationResult.Text = "正在执行扫码、第一次拍照、第二次拍照和视觉分析…";
            AppendSimulationLog("START", "开始S2视觉仿真：" + qrCode);
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
                MessageBox.Show(this, ex.Message, "S2视觉仿真失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _simulationProgress.Visible = false;
                _btnRunS2Simulation.Enabled = _settings.Simulation;
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
                AutoSize = true,
                Margin = new Padding(8, 20, 6, 0)
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
