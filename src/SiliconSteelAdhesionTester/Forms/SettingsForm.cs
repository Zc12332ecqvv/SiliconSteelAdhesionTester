using System;
using System.Drawing;
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
        private readonly CheckBox chkScannerEnabled = new CheckBox { Text = "启用SR-1000网络扫码", AutoSize = true };
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
        private readonly TextBox txtSiteName = NewText();
        private readonly TextBox txtDeviceName = NewText();
        private readonly TextBox txtDeviceCode = NewText();
        private readonly TextBox txtLimsEndpoint = NewText();

        public SettingsForm(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Text = "系统设置";
            Font = new Font("Microsoft YaHei UI", 9.5F);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 620);
            Size = new Size(900, 700);
            BackColor = Color.FromArgb(238, 242, 247);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = Color.FromArgb(18, 32, 49) };
            header.Controls.Add(new Label
            {
                Text = "系统设置",
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(24, 19)
            });
            header.Controls.Add(new Label
            {
                Text = "现场参数可配置 · 保存后重启程序生效",
                ForeColor = Color.Silver,
                AutoSize = true,
                Location = new Point(170, 32)
            });

            TabControl tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(18, 7) };
            tabs.TabPages.Add(BuildNetworkPage());
            tabs.TabPages.Add(BuildScannerPage());
            tabs.TabPages.Add(BuildVisionPage());
            tabs.TabPages.Add(BuildSystemPage());

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 72, BackColor = Color.White };
            Button btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Size = new Size(110, 42), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            Button btnSave = new Button { Text = "保存设置", Size = new Size(130, 42), BackColor = Color.FromArgb(35, 156, 96), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += SaveClicked;
            footer.Resize += (s, e) =>
            {
                btnSave.Location = new Point(footer.ClientSize.Width - btnSave.Width - 22, 15);
                btnCancel.Location = new Point(btnSave.Left - btnCancel.Width - 12, 15);
            };
            footer.Controls.Add(btnCancel);
            footer.Controls.Add(btnSave);

            Controls.Add(tabs);
            Controls.Add(footer);
            Controls.Add(header);
            AcceptButton = btnSave;
            CancelButton = btnCancel;
            LoadValues();
        }

        private TabPage BuildNetworkPage()
        {
            TabPage page = NewPage("网络与PLC");
            TableLayoutPanel table = NewTable();
            cboPlcMode.Items.AddRange(new object[] { "仿真模式", "S7实体PLC" });
            AddRow(table, "运行模式", cboPlcMode, "修改PLC模式、IP或端口后必须重启程序");
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
            TabPage page = NewPage("SR-1000扫码枪");
            TableLayoutPanel table = NewTable();
            AddRow(table, "扫码功能", chkScannerEnabled, "取向与无取向工位可分别配置");
            AddRow(table, "取向扫码枪IP", txtOrientedIp, "S2取向工位");
            AddRow(table, "取向扫码枪端口", numOrientedPort, "SR-1000 TCP端口");
            AddRow(table, "无取向扫码枪IP", txtNonOrientedIp, "S3无取向工位");
            AddRow(table, "无取向扫码枪端口", numNonOrientedPort, "SR-1000 TCP端口");
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
            Label saveLocation = new Label
            {
                AutoSize = false,
                Height = 54,
                ForeColor = Color.DimGray,
                Text = "设置文件：" + AppSettings.OverrideFilePath + Environment.NewLine +
                       "数据库密码不在本页面明文保存；正式数据库接口确定后使用受保护凭据。"
            };
            AddRow(table, "保存说明", saveLocation, string.Empty);
            page.Controls.Add(Wrap(table));
            return page;
        }

        private void LoadValues()
        {
            cboPlcMode.SelectedIndex = _settings.Simulation ? 0 : 1;
            txtPlcIp.Text = _settings.PlcIp;
            numPlcPort.Value = Clamp(numPlcPort, _settings.PlcPort);
            numRack.Value = Clamp(numRack, _settings.Rack);
            numSlot.Value = Clamp(numSlot, _settings.Slot);
            numPoll.Value = Clamp(numPoll, _settings.PollIntervalMs);
            numPulse.Value = Clamp(numPulse, _settings.CommandPulseMs);
            chkScannerEnabled.Checked = _settings.BarcodeScannerEnabled;
            txtOrientedIp.Text = _settings.OrientedScannerIp;
            numOrientedPort.Value = Clamp(numOrientedPort, _settings.OrientedScannerPort);
            txtNonOrientedIp.Text = _settings.NonOrientedScannerIp;
            numNonOrientedPort.Value = Clamp(numNonOrientedPort, _settings.NonOrientedScannerPort);
            numConnectTimeout.Value = Clamp(numConnectTimeout, _settings.ScannerConnectTimeoutMs);
            numInputTimeout.Value = Clamp(numInputTimeout, _settings.ScannerReadTimeoutMs);
            numMinLength.Value = Clamp(numMinLength, _settings.BarcodeMinimumLength);
            numDuplicateSeconds.Value = Clamp(numDuplicateSeconds, _settings.DuplicateBarcodeSeconds);
            txtTriggerCommand.Text = _settings.ScannerTriggerCommand;
            txtStopCommand.Text = _settings.ScannerStopCommand;
            cboTerminator.SelectedItem = _settings.ScannerTerminator;
            if (cboTerminator.SelectedIndex < 0) cboTerminator.SelectedIndex = 0;
            numOrientedLoss.Value = Clamp(numOrientedLoss, (decimal)_settings.OrientedMaxLossRate);
            numNonOrientedLoss.Value = Clamp(numNonOrientedLoss, (decimal)_settings.NonOrientedMaxLossRate);
            numDifference.Value = Clamp(numDifference, _settings.VisionDifferenceThreshold);
            numParticleArea.Value = Clamp(numParticleArea, _settings.VisionMinimumParticleArea);
            txtVisionDirectory.Text = _settings.VisionOutputDirectory;
            txtSiteName.Text = _settings.SiteName;
            txtDeviceName.Text = _settings.DeviceName;
            txtDeviceCode.Text = _settings.DeviceCode;
            txtLimsEndpoint.Text = _settings.LimsEndpoint;
        }

        private void SaveClicked(object sender, EventArgs e)
        {
            string error;
            if (!ValidateIp(txtPlcIp.Text, "PLC IP", out error) ||
                !ValidateIp(txtOrientedIp.Text, "取向扫码枪IP", out error) ||
                !ValidateIp(txtNonOrientedIp.Text, "无取向扫码枪IP", out error))
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

            _settings.Simulation = cboPlcMode.SelectedIndex == 0;
            _settings.PlcIp = txtPlcIp.Text.Trim();
            _settings.PlcPort = (int)numPlcPort.Value;
            _settings.Rack = (short)numRack.Value;
            _settings.Slot = (short)numSlot.Value;
            _settings.PollIntervalMs = (int)numPoll.Value;
            _settings.CommandPulseMs = (int)numPulse.Value;
            _settings.BarcodeScannerEnabled = chkScannerEnabled.Checked;
            _settings.OrientedScannerIp = txtOrientedIp.Text.Trim();
            _settings.OrientedScannerPort = (int)numOrientedPort.Value;
            _settings.NonOrientedScannerIp = txtNonOrientedIp.Text.Trim();
            _settings.NonOrientedScannerPort = (int)numNonOrientedPort.Value;
            _settings.ScannerConnectTimeoutMs = (int)numConnectTimeout.Value;
            _settings.ScannerReadTimeoutMs = (int)numInputTimeout.Value;
            _settings.BarcodeMinimumLength = (int)numMinLength.Value;
            _settings.DuplicateBarcodeSeconds = (int)numDuplicateSeconds.Value;
            _settings.ScannerTriggerCommand = txtTriggerCommand.Text.Trim();
            _settings.ScannerStopCommand = txtStopCommand.Text.Trim();
            _settings.ScannerTerminator = Convert.ToString(cboTerminator.SelectedItem);
            _settings.OrientedMaxLossRate = (double)numOrientedLoss.Value;
            _settings.NonOrientedMaxLossRate = (double)numNonOrientedLoss.Value;
            _settings.VisionDifferenceThreshold = (int)numDifference.Value;
            _settings.VisionMinimumParticleArea = (int)numParticleArea.Value;
            _settings.VisionOutputDirectory = txtVisionDirectory.Text.Trim();
            _settings.SiteName = txtSiteName.Text.Trim();
            _settings.DeviceName = txtDeviceName.Text.Trim();
            _settings.DeviceCode = txtDeviceCode.Text.Trim();
            _settings.LimsEndpoint = txtLimsEndpoint.Text.Trim();

            try
            {
                _settings.SaveOverrides();
                MessageBox.Show(this, "设置已保存。请重启程序，使PLC和扫码通讯参数完整生效。",
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
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
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
        private static decimal Clamp(NumericUpDown control, decimal value) { return Math.Max(control.Minimum, Math.Min(control.Maximum, value)); }
    }
}
