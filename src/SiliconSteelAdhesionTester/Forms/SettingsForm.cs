using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Windows.Forms;
using SiliconSteelAdhesionTester.Configuration;

namespace SiliconSteelAdhesionTester.Forms
{
    [System.ComponentModel.DesignerCategory("Form")]
    public sealed class SettingsForm : Form
    {
        private readonly AppSettings _settings;
        private readonly ComboBox cboPlcMode = NewCombo();
        private readonly TextBox txtPlcIp = NewText();
        private readonly NumericUpDown numPlcPort = NewNumber(1, 65535);
        private readonly NumericUpDown numRack = NewNumber(0, 255);
        private readonly NumericUpDown numSlot = NewNumber(0, 255);
        private readonly NumericUpDown numPoll = NewNumber(50, 60000);
        private readonly NumericUpDown numPulse = NewNumber(50, 10000);
        private readonly CheckBox chkScannerEnabled = new CheckBox { Text = "启用SR-1000二维码读取", AutoSize = true };
        private readonly TextBox txtOrientedIp = NewText();
        private readonly NumericUpDown numOrientedPort = NewNumber(1, 65535);
        private readonly TextBox txtNonOrientedIp = NewText();
        private readonly NumericUpDown numNonOrientedPort = NewNumber(1, 65535);
        private readonly NumericUpDown numConnectTimeout = NewNumber(100, 60000);
        private readonly NumericUpDown numInputTimeout = NewNumber(20, 60000);
        private readonly NumericUpDown numMinLength = NewNumber(1, 256);
        private readonly NumericUpDown numDuplicateSeconds = NewNumber(0, 3600);
        private readonly TextBox txtTriggerCommand = NewText();
        private readonly TextBox txtStopCommand = NewText();
        private readonly ComboBox cboTerminator = NewCombo();
        private readonly NumericUpDown numOrientedLoss = NewDecimal(0, 100);
        private readonly NumericUpDown numNonOrientedLoss = NewDecimal(0, 100);
        private readonly NumericUpDown numDifference = NewNumber(0, 255);
        private readonly NumericUpDown numParticleArea = NewNumber(1, 1000000);
        private readonly TextBox txtVisionDirectory = NewText();
        private readonly CheckBox chkAutomaticInteractions = new CheckBox { Text = "启用PLC自动设备交互", AutoSize = true };
        private readonly TextBox txtCameraInputDirectory = NewText();
        private readonly ComboBox cboCameraProvider = NewCombo();
        private readonly TextBox txtOrientedCameraIp = NewText();
        private readonly TextBox txtNonOrientedCameraIp = NewText();
        private readonly NumericUpDown numCameraTimeout = NewNumber(500, 120000);
        private readonly NumericUpDown numCameraStable = NewNumber(50, 10000);
        private readonly TextBox txtSiteName = NewText();
        private readonly TextBox txtDeviceName = NewText();
        private readonly TextBox txtDeviceCode = NewText();
        private readonly TextBox txtLimsEndpoint = NewText();
        private readonly Label lblFileState = new Label();

        public SettingsForm(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Text = "系统设置";
            Font = new Font("Microsoft YaHei UI", 9.5F);
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(900, 680);
            Size = new Size(1080, 780);
            BackColor = Color.FromArgb(238, 242, 247);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = Color.FromArgb(18, 32, 49) };
            header.Controls.Add(new Label
            {
                Text = "系统设置",
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(26, 18)
            });
            header.Controls.Add(new Label
            {
                Text = "现场参数配置中心 · 通讯类参数保存后需重启程序生效",
                ForeColor = Color.Silver,
                AutoSize = true,
                Location = new Point(172, 33)
            });

            TabControl tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(18, 7),
                HotTrack = true
            };
            tabs.TabPages.Add(BuildNetworkPage());
            tabs.TabPages.Add(BuildScannerPage());
            tabs.TabPages.Add(BuildVisionPage());
            tabs.TabPages.Add(BuildSystemPage());

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 76, BackColor = Color.White };
            Button btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Size = new Size(110, 42), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            Button btnSave = new Button { Text = "保存设置", Size = new Size(130, 42), BackColor = Color.FromArgb(35, 156, 96), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += SaveClicked;
            lblFileState.AutoSize = false;
            lblFileState.ForeColor = Color.DimGray;
            lblFileState.TextAlign = ContentAlignment.MiddleLeft;
            lblFileState.AutoEllipsis = true;
            lblFileState.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            footer.Resize += (s, e) =>
            {
                btnSave.Location = new Point(footer.ClientSize.Width - btnSave.Width - 24, 17);
                btnCancel.Location = new Point(btnSave.Left - btnCancel.Width - 12, 17);
                lblFileState.Location = new Point(24, 17);
                lblFileState.Size = new Size(Math.Max(120, btnCancel.Left - 48), 42);
            };
            footer.Controls.Add(lblFileState);
            footer.Controls.Add(btnCancel);
            footer.Controls.Add(btnSave);

            Controls.Add(tabs);
            Controls.Add(footer);
            Controls.Add(header);
            AcceptButton = btnSave;
            CancelButton = btnCancel;
            LoadValues();
            UpdateFileState();
        }

        private TabPage BuildNetworkPage()
        {
            TabPage page = NewPage("网络与PLC");
            TableLayoutPanel table = NewTable();
#if SIMULATION_ONLY
            cboPlcMode.Items.Add("仿真模式（当前构建）");
            AddRow(table, "运行模式", cboPlcMode, "当前为安全仿真构建，不会连接或写入实体PLC");
#else
            cboPlcMode.Items.AddRange(new object[] { "仿真模式", "S7实体PLC" });
            AddRow(table, "运行模式", cboPlcMode, "切换运行模式后必须重启程序");
#endif
            AddRow(table, "PLC IP", txtPlcIp, "西门子S7-1200");
            AddRow(table, "PLC端口", numPlcPort, "现场确认使用502");
            AddRow(table, "Rack / Slot", Pair(numRack, numSlot), "默认0 / 1");
            AddRow(table, "PLC轮询周期(ms)", numPoll, "建议不低于100ms");
            AddRow(table, "命令脉冲(ms)", numPulse, "复位、启动等瞬时命令");
            page.Controls.Add(Wrap(table));
            return page;
        }

        private TabPage BuildScannerPage()
        {
            TabPage page = NewPage("SR-1000二维码读取器");
            TableLayoutPanel table = NewTable();
            AddRow(table, "二维码读取功能", chkScannerEnabled, "取向与无取向工位可分别配置");
            AddRow(table, "取向读取器IP", txtOrientedIp, "S2取向工位");
            AddRow(table, "取向读取器端口", numOrientedPort, "SR-1000 TCP端口");
            AddRow(table, "无取向读取器IP", txtNonOrientedIp, "S3无取向工位");
            AddRow(table, "无取向读取器端口", numNonOrientedPort, "SR-1000 TCP端口");
            AddRow(table, "连接超时(ms)", numConnectTimeout, "断线后由通讯服务自动重连");
            AddRow(table, "收码超时(ms)", numInputTimeout, "触发后等待二维码的最长时间");
            AddRow(table, "最小码长", numMinLength, "少于该长度判定为无效码");
            AddRow(table, "重复码间隔(s)", numDuplicateSeconds, "0表示不拦截重复码");
            AddRow(table, "触发命令", txtTriggerCommand, "以SR-1000现场配置/手册为准");
            AddRow(table, "停止命令", txtStopCommand, "以SR-1000现场配置/手册为准");
            cboTerminator.Items.AddRange(new object[] { "CR", "LF", "CRLF", "无" });
            AddRow(table, "命令结束符", cboTerminator, "发送命令时附加");
            page.Controls.Add(Wrap(table));
            return page;
        }

        private TabPage BuildVisionPage()
        {
            TabPage page = NewPage("视觉与判定");
            TableLayoutPanel table = NewTable();
            AddRow(table, "取向最大脱落率(%)", numOrientedLoss, "需用标准样品最终标定");
            AddRow(table, "无取向最大脱落率(%)", numNonOrientedLoss, "需用标准样品最终标定");
            AddRow(table, "图像差异阈值", numDifference, "OpenCV 0～255");
            AddRow(table, "最小颗粒面积(px)", numParticleArea, "过滤微小噪点");
            AddRow(table, "图片保存目录", txtVisionDirectory, "可填写相对或绝对路径");
            AddRow(table, "自动交互", chkAutomaticInteractions, "PLC允许后自动读二维码、取图、判定并返回结果");
            cboCameraProvider.Items.AddRange(new object[] { "MVS海康相机", "文件夹落图" });
            AddRow(table, "相机取图方式", cboCameraProvider, "正式运行选择MVS；无相机调试可选择文件夹落图");
            AddRow(table, "有取向相机IP", txtOrientedCameraIp, "S2第一次、第二次拍照共用的MV-CT120R-9GC01-PRO");
            AddRow(table, "无取向相机IP", txtNonOrientedCameraIp, "S4无取向拍照使用的MV-CT120R-9GC01-PRO");
            AddRow(table, "相机落图目录", txtCameraInputDirectory, "仅文件夹落图模式使用");
            AddRow(table, "相机取图超时(ms)", numCameraTimeout, "超时后第一次拍照不返回完成，判定拍照返回NG");
            AddRow(table, "文件稳定时间(ms)", numCameraStable, "防止读取仍在写入的图片");
            page.Controls.Add(Wrap(table));
            return page;
        }

        private TabPage BuildSystemPage()
        {
            TabPage page = NewPage("站点与接口");
            TableLayoutPanel table = NewTable();
            AddRow(table, "站点名称", txtSiteName, "例如E19");
            AddRow(table, "设备名称", txtDeviceName, "显示及数据追溯使用");
            AddRow(table, "设备编号", txtDeviceCode, "厂内唯一编号");
            AddRow(table, "LIMS接口地址", txtLimsEndpoint, "接口确认后由业务服务使用");

            TextBox path = NewText();
            path.ReadOnly = true;
            path.BackColor = Color.FromArgb(248, 250, 252);
            path.Text = AppSettings.OverrideFilePath;
            AddRow(table, "设置文件", path, "首次启动自动创建，保存时生成.bak备份");

            FlowLayoutPanel fileActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty
            };
            Button btnOpenDirectory = NewSecondaryButton("打开目录", 128);
            Button btnCopyPath = NewSecondaryButton("复制路径", 128);
            btnOpenDirectory.Click += (s, e) => OpenSettingsDirectory();
            btnCopyPath.Click += (s, e) => CopySettingsPath();
            fileActions.Controls.Add(btnOpenDirectory);
            fileActions.Controls.Add(btnCopyPath);
            AddRow(table, "快捷操作", fileActions, "便于现场备份、查看或替换配置");

            Label securityNote = new Label
            {
                AutoSize = false,
                ForeColor = Color.DimGray,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "此文件仅保存设备和算法参数，不保存数据库密码。"
            };
            AddRow(table, "安全说明", securityNote, "正式接口凭据应使用受保护存储");
            page.Controls.Add(Wrap(table));
            return page;
        }

        private void LoadValues()
        {
#if SIMULATION_ONLY
            cboPlcMode.SelectedIndex = 0;
#else
            cboPlcMode.SelectedIndex = _settings.Simulation ? 0 : 1;
#endif
            txtPlcIp.Text = _settings.PlcIp;
            numPlcPort.Value = Clamp(numPlcPort, _settings.PlcPort);
            numRack.Value = Clamp(numRack, _settings.Rack);
            numSlot.Value = Clamp(numSlot, _settings.Slot);
            numPoll.Value = Clamp(numPoll, _settings.PollIntervalMs);
            numPulse.Value = Clamp(numPulse, _settings.CommandPulseMs);
            chkScannerEnabled.Checked = _settings.QrCodeScannerEnabled;
            txtOrientedIp.Text = _settings.OrientedScannerIp;
            numOrientedPort.Value = Clamp(numOrientedPort, _settings.OrientedScannerPort);
            txtNonOrientedIp.Text = _settings.NonOrientedScannerIp;
            numNonOrientedPort.Value = Clamp(numNonOrientedPort, _settings.NonOrientedScannerPort);
            numConnectTimeout.Value = Clamp(numConnectTimeout, _settings.ScannerConnectTimeoutMs);
            numInputTimeout.Value = Clamp(numInputTimeout, _settings.ScannerReadTimeoutMs);
            numMinLength.Value = Clamp(numMinLength, _settings.QrCodeMinimumLength);
            numDuplicateSeconds.Value = Clamp(numDuplicateSeconds, _settings.DuplicateQrCodeSeconds);
            txtTriggerCommand.Text = _settings.ScannerTriggerCommand;
            txtStopCommand.Text = _settings.ScannerStopCommand;
            cboTerminator.SelectedItem = _settings.ScannerTerminator;
            if (cboTerminator.SelectedIndex < 0) cboTerminator.SelectedIndex = 0;
            numOrientedLoss.Value = Clamp(numOrientedLoss, (decimal)_settings.OrientedMaxLossRate);
            numNonOrientedLoss.Value = Clamp(numNonOrientedLoss, (decimal)_settings.NonOrientedMaxLossRate);
            numDifference.Value = Clamp(numDifference, _settings.VisionDifferenceThreshold);
            numParticleArea.Value = Clamp(numParticleArea, _settings.VisionMinimumParticleArea);
            txtVisionDirectory.Text = _settings.VisionOutputDirectory;
            chkAutomaticInteractions.Checked = _settings.AutomaticDeviceInteractionsEnabled;
            cboCameraProvider.SelectedIndex = string.Equals(_settings.CameraProvider, "MVS", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            txtOrientedCameraIp.Text = string.IsNullOrWhiteSpace(_settings.OrientedCameraIp) ? _settings.CameraIp : _settings.OrientedCameraIp;
            txtNonOrientedCameraIp.Text = string.IsNullOrWhiteSpace(_settings.NonOrientedCameraIp) ? _settings.CameraIp : _settings.NonOrientedCameraIp;
            txtCameraInputDirectory.Text = _settings.CameraInputDirectory;
            numCameraTimeout.Value = Clamp(numCameraTimeout, _settings.CameraCaptureTimeoutMs);
            numCameraStable.Value = Clamp(numCameraStable, _settings.CameraFileStableMs);
            txtSiteName.Text = _settings.SiteName;
            txtDeviceName.Text = _settings.DeviceName;
            txtDeviceCode.Text = _settings.DeviceCode;
            txtLimsEndpoint.Text = _settings.LimsEndpoint;
        }

        private void SaveClicked(object sender, EventArgs e)
        {
            string error;
#if SIMULATION_ONLY
            bool useEntityPlc = false;
#else
            bool useEntityPlc = cboPlcMode.SelectedIndex == 1;
#endif
            bool useTcpScanner = chkScannerEnabled.Checked;
            bool useMvsCamera = cboCameraProvider.SelectedIndex == 0;
            if ((useEntityPlc && !ValidateIp(txtPlcIp.Text, "PLC IP", out error)) ||
                (useTcpScanner && !ValidateIp(txtOrientedIp.Text, "取向二维码读取器IP", out error)) ||
                (useTcpScanner && !ValidateIp(txtNonOrientedIp.Text, "无取向二维码读取器IP", out error)) ||
                (useMvsCamera && !ValidateIp(txtOrientedCameraIp.Text, "有取向相机IP", out error)) ||
                (useMvsCamera && !ValidateIp(txtNonOrientedCameraIp.Text, "无取向相机IP", out error)))
            {
                MessageBox.Show(this, error, "参数错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Uri endpoint;
            if (!string.IsNullOrWhiteSpace(txtLimsEndpoint.Text) &&
                (!Uri.TryCreate(txtLimsEndpoint.Text.Trim(), UriKind.Absolute, out endpoint) ||
                 (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps)))
            {
                MessageBox.Show(this, "LIMS接口地址必须是完整的HTTP或HTTPS地址。", "参数错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtVisionDirectory.Text))
            {
                MessageBox.Show(this, "图片保存目录不能为空。", "参数错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!useMvsCamera && string.IsNullOrWhiteSpace(txtCameraInputDirectory.Text))
            {
                MessageBox.Show(this, "文件夹落图模式下，相机落图目录不能为空。", "参数错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

#if SIMULATION_ONLY
            _settings.Simulation = true;
#else
            _settings.Simulation = cboPlcMode.SelectedIndex == 0;
#endif
            _settings.PlcIp = txtPlcIp.Text.Trim();
            _settings.PlcPort = (int)numPlcPort.Value;
            _settings.Rack = (short)numRack.Value;
            _settings.Slot = (short)numSlot.Value;
            _settings.PollIntervalMs = (int)numPoll.Value;
            _settings.CommandPulseMs = (int)numPulse.Value;
            _settings.QrCodeScannerEnabled = chkScannerEnabled.Checked;
            _settings.OrientedScannerIp = txtOrientedIp.Text.Trim();
            _settings.OrientedScannerPort = (int)numOrientedPort.Value;
            _settings.NonOrientedScannerIp = txtNonOrientedIp.Text.Trim();
            _settings.NonOrientedScannerPort = (int)numNonOrientedPort.Value;
            _settings.ScannerConnectTimeoutMs = (int)numConnectTimeout.Value;
            _settings.ScannerReadTimeoutMs = (int)numInputTimeout.Value;
            _settings.QrCodeMinimumLength = (int)numMinLength.Value;
            _settings.DuplicateQrCodeSeconds = (int)numDuplicateSeconds.Value;
            _settings.ScannerTriggerCommand = txtTriggerCommand.Text.Trim();
            _settings.ScannerStopCommand = txtStopCommand.Text.Trim();
            _settings.ScannerTerminator = Convert.ToString(cboTerminator.SelectedItem);
            _settings.OrientedMaxLossRate = (double)numOrientedLoss.Value;
            _settings.NonOrientedMaxLossRate = (double)numNonOrientedLoss.Value;
            _settings.VisionDifferenceThreshold = (int)numDifference.Value;
            _settings.VisionMinimumParticleArea = (int)numParticleArea.Value;
            _settings.VisionOutputDirectory = txtVisionDirectory.Text.Trim();
            _settings.AutomaticDeviceInteractionsEnabled = chkAutomaticInteractions.Checked;
            _settings.CameraProvider = cboCameraProvider.SelectedIndex == 0 ? "MVS" : "Folder";
            _settings.OrientedCameraIp = txtOrientedCameraIp.Text.Trim();
            _settings.NonOrientedCameraIp = txtNonOrientedCameraIp.Text.Trim();
            _settings.CameraInputDirectory = txtCameraInputDirectory.Text.Trim();
            _settings.CameraCaptureTimeoutMs = (int)numCameraTimeout.Value;
            _settings.CameraFileStableMs = (int)numCameraStable.Value;
            _settings.SiteName = txtSiteName.Text.Trim();
            _settings.DeviceName = txtDeviceName.Text.Trim();
            _settings.DeviceCode = txtDeviceCode.Text.Trim();
            _settings.LimsEndpoint = txtLimsEndpoint.Text.Trim();

            try
            {
                _settings.SaveOverrides();
                UpdateFileState();
                MessageBox.Show(this, "设置已保存到：" + Environment.NewLine + AppSettings.OverrideFilePath +
                    Environment.NewLine + Environment.NewLine + "请重启程序，使通讯类参数完整生效。",
                    "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "设置保存失败：" + ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool ValidateIp(string value, string field, out string error)
        {
            IPAddress address;
            if (!IPAddress.TryParse((value ?? string.Empty).Trim(), out address) ||
                address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                error = field + "不是有效的IPv4地址。";
                return false;
            }
            error = null;
            return true;
        }

        private void UpdateFileState()
        {
            bool exists = File.Exists(AppSettings.OverrideFilePath);
            lblFileState.Text = exists
                ? "配置文件已就绪：" + AppSettings.OverrideFilePath
                : "配置文件尚未创建，点击“保存设置”后生成。";
            lblFileState.ForeColor = exists ? Color.FromArgb(38, 119, 78) : Color.DarkOrange;
            if (!string.IsNullOrWhiteSpace(AppSettings.LastLoadWarning))
            {
                lblFileState.Text = AppSettings.LastLoadWarning;
                lblFileState.ForeColor = Color.Firebrick;
            }
        }

        private void OpenSettingsDirectory()
        {
            try
            {
                Directory.CreateDirectory(AppSettings.OverrideDirectoryPath);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = File.Exists(AppSettings.OverrideFilePath)
                        ? "/select,\"" + AppSettings.OverrideFilePath + "\""
                        : "\"" + AppSettings.OverrideDirectoryPath + "\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法打开设置目录：" + ex.Message,
                    "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CopySettingsPath()
        {
            try
            {
                Clipboard.SetText(AppSettings.OverrideFilePath);
                MessageBox.Show(this, "设置文件路径已复制到剪贴板。",
                    "复制成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "复制路径失败：" + ex.Message,
                    "复制失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static TabPage NewPage(string text)
        {
            return new TabPage { Text = text, BackColor = Color.White, Padding = new Padding(18) };
        }

        private static TableLayoutPanel NewTable()
        {
            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                Padding = new Padding(8),
                BackColor = Color.White
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            return table;
        }

        private static Control Wrap(Control content)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            panel.Controls.Add(content);
            return panel;
        }

        private static void AddRow(TableLayoutPanel table, string label, Control input, string hint)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            Label title = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 10, 0) };
            input.Dock = DockStyle.Fill;
            input.Margin = new Padding(4, 7, 4, 7);
            Label note = new Label { Text = hint, Dock = DockStyle.Fill, ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            table.Controls.Add(title, 0, row);
            table.Controls.Add(input, 1, row);
            table.Controls.Add(note, 2, row);
        }

        private static Control Pair(Control first, Control second)
        {
            TableLayoutPanel pair = new TableLayoutPanel { ColumnCount = 3, Dock = DockStyle.Fill, Margin = Padding.Empty };
            pair.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            pair.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            pair.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            first.Dock = DockStyle.Fill;
            second.Dock = DockStyle.Fill;
            pair.Controls.Add(first, 0, 0);
            pair.Controls.Add(new Label { Text = "/", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 1, 0);
            pair.Controls.Add(second, 2, 0);
            return pair;
        }

        private static TextBox NewText() { return new TextBox { BorderStyle = BorderStyle.FixedSingle }; }
        private static ComboBox NewCombo() { return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }; }
        private static NumericUpDown NewNumber(decimal min, decimal max) { return new NumericUpDown { Minimum = min, Maximum = max, ThousandsSeparator = true }; }
        private static NumericUpDown NewDecimal(decimal min, decimal max) { return new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = 3, Increment = 0.1M }; }
        private static Button NewSecondaryButton(string text, int width)
        {
            Button button = new Button
            {
                Text = text,
                Width = width,
                Height = 34,
                BackColor = Color.FromArgb(232, 239, 247),
                ForeColor = Color.FromArgb(25, 67, 105),
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(4, 3, 8, 3)
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(188, 205, 222);
            return button;
        }
        private static decimal Clamp(NumericUpDown control, decimal value) { return Math.Max(control.Minimum, Math.Min(control.Maximum, value)); }
    }
}
