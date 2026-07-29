using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SiliconSteelAdhesionTester.Services.Plc;
using SiliconSteelAdhesionTester.Services.Vision;

namespace SiliconSteelAdhesionTester.Forms
{
    public partial class MainForm
    {
        private Label lblCommandFeedback;
        private ToolTip commandToolTip;
        private bool _pauseCommandIssued;

        private void InitializeRuntimeBindings()
        {
            lblUser.Text = "用户：" + _user.DisplayName + "  |  " + RoleText(_user.Role);
            _runLamps = new[] { pnlRun1, pnlRun2, pnlRun3, pnlRun4 };
            _readyLamps = new[] { pnlReady1, pnlReady2, pnlReady3, pnlReady4 };
            _doneLamps = new[] { pnlDone1, pnlDone2, pnlDone3, pnlDone4 };
            _stationStatuses = new[] { lblStationStatus1, lblStationStatus2, lblStationStatus3, lblStationStatus4 };
            _stationSteps = new[] { lblStep1, lblStep2, lblStep3, lblStep4 };
            _stationStarts = new[] { btnS1Start, btnS2Start, btnS3Start, btnS4Start };
            _stationContinuous = new[] { btnS1Continuous, btnS2Continuous, btnS3Continuous, btnS4Continuous };
            _flowNodes = new[] { lblFlow1, lblFlow2, lblFlow3, lblFlow4, lblFlow5, lblFlow6, lblFlow7, lblFlow8 };
            lblMaterialType.Text = "等待任务数据";
            UpdateFlowPresentation();
            AppendRuntimeLog("主界面初始化完成");
            InitializeCommandBar();

            btnAutoMode.Click += async (s, e) =>
            {
                if (_automatic) return;
                if (!CanSwitchOperatingMode()) return;
                await ExecuteButtonAsync(btnAutoMode, async () =>
                {
                    await _plc.WriteAsync(PlcAddresses.AutoMode, false, _shutdown.Token);
                    _automatic = true;
                    AppendRuntimeLog("已切换为自动模式");
                }, "自动模式", "已发送自动模式切换指令");
            };
            btnManualMode.Click += async (s, e) =>
            {
                if (!_user.CanDebug)
                {
                    MessageBox.Show("当前账号没有手动调试权限，请使用电气调试员或超级管理员账号。", "权限不足", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (_automatic)
                {
                    if (!CanSwitchOperatingMode()) return;
                    await ExecuteButtonAsync(btnManualMode, async () =>
                    {
                        await _plc.WriteAsync(PlcAddresses.AutoMode, true, _shutdown.Token);
                        _automatic = false;
                        AppendRuntimeLog("已切换为手动模式");
                    }, "手动模式", "已发送手动模式切换指令");
                }
                if (!_automatic) OpenManualTaskDialog();
            };
            btnLineStart.Click += async (s, e) => await StartLineAsync();
            btnLinePause.Click += async (s, e) => await PauseLineAsync();
            btnLineHome.Click += async (s, e) => await HomeLineAsync();
            btnFaultReset.Click += async (s, e) => await ResetFaultAsync();
            for (int station = 1; station <= 4; station++) BindStation(station);

            btnVision.Click += (s, e) => OpenVisionWindow();
            btnNavVision.Click += (s, e) => OpenVisionWindow();
            btnDebug.Click += (s, e) =>
                new DebugForm(
                    _plc,
                    _user,
                    _settings,
                    RunS2VisionSimulationAsync,
                    _shutdown.Token).Show(this);
            btnRecords.Click += (s, e) => new DataRecordsForm(_database, false).Show(this);
            btnFaultLogs.Click += (s, e) => new DataRecordsForm(_database, true).Show(this);
            btnNavRecords.Click += (s, e) => new DataRecordsForm(_database, false).Show(this);
            btnNavLogs.Click += (s, e) => new DataRecordsForm(_database, true).Show(this);
            btnNavSettings.Click += (s, e) => OpenSettingsWindow();
        }

        private void InitializeCommandBar()
        {
            lblCommandFeedback = new Label
            {
                AutoSize = false,
                BackColor = Color.FromArgb(240, 244, 248),
                ForeColor = Color.FromArgb(55, 72, 88),
                Font = new Font("Microsoft YaHei UI", 9F),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(18, 0, 10, 0),
                Text = "操作提示：启动、回原位等动作会先检查设备状态；实体设备动作请确认现场安全。"
            };
            pnlBottom.Controls.Add(lblCommandFeedback);
            lblCommandFeedback.BringToFront();

            commandToolTip = new ToolTip(components)
            {
                AutoPopDelay = 10000,
                InitialDelay = 350,
                ReshowDelay = 100,
                ShowAlways = true
            };

            StyleCommandButton(btnManualMode, "⚙  手动", Color.FromArgb(73, 92, 112), Color.FromArgb(88, 108, 130));
            StyleCommandButton(btnAutoMode, "◉  自动", Color.FromArgb(35, 102, 170), Color.FromArgb(45, 119, 194));
            StyleCommandButton(btnLineStart, "▶  启动", Color.FromArgb(28, 145, 93), Color.FromArgb(35, 164, 106));
            StyleCommandButton(btnLinePause, "Ⅱ  暂停", Color.FromArgb(213, 145, 32), Color.FromArgb(230, 159, 40));
            StyleCommandButton(btnLineHome, "⌂  回原位", Color.FromArgb(96, 76, 160), Color.FromArgb(112, 89, 183));
            StyleCommandButton(btnFaultReset, "↻  故障复位", Color.FromArgb(188, 91, 35), Color.FromArgb(207, 105, 44));

            commandToolTip.SetToolTip(btnManualMode, "切换为手动模式并添加任务；任务仍通过下方“启动”按钮执行。");
            commandToolTip.SetToolTip(btnAutoMode, "切换为自动模式；切换前要求流程暂停且所有工位在原位。");
            commandToolTip.SetToolTip(btnLineStart, "自动模式执行主控任务；手动模式执行已添加的手动任务，或继续暂停流程。");
            commandToolTip.SetToolTip(btnLinePause, "暂停当前流程；暂停后“启动”按钮会重新可用。");
            commandToolTip.SetToolTip(btnLineHome, "仅在流程已经停止/暂停时，经确认后发送整机回原位脉冲。");
            commandToolTip.SetToolTip(btnFaultReset, "发送HMI复位脉冲；不会绕过急停，也不会消除仍然存在的故障原因。");
        }

        private static void StyleCommandButton(Button button, string text, Color baseColor, Color hoverColor)
        {
            button.Text = text;
            button.BackColor = baseColor;
            button.ForeColor = Color.White;
            button.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = hoverColor;
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(baseColor, 0.08F);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            button.Resize += (s, e) => ApplyRoundedRegion(button, 10);
            ApplyRoundedRegion(button, 10);
        }

        private static void ApplyRoundedRegion(Control control, int radius)
        {
            if (control.Width <= 0 || control.Height <= 0) return;
            using (GraphicsPath path = new GraphicsPath())
            {
                int diameter = radius * 2;
                Rectangle arc = new Rectangle(0, 0, diameter, diameter);
                path.AddArc(arc, 180, 90);
                arc.X = control.Width - diameter - 1;
                path.AddArc(arc, 270, 90);
                arc.Y = control.Height - diameter - 1;
                path.AddArc(arc, 0, 90);
                arc.X = 0;
                path.AddArc(arc, 90, 90);
                path.CloseFigure();
                Region oldRegion = control.Region;
                control.Region = new Region(path);
                if (oldRegion != null) oldRegion.Dispose();
            }
        }

        private async Task StartLineAsync()
        {
            if (!CanSendMotionCommand("启动")) return;
            if (_latestSnapshot.EmergencyStop)
            {
                ShowCommandBlocked("急停处于触发状态，禁止启动。请先确认现场安全并释放急停。");
                return;
            }
            if (_latestSnapshot.FlowFault || HasStationFault(_latestSnapshot))
            {
                ShowCommandBlocked("当前仍有流程或工位故障，禁止启动。请先排除故障原因，再执行故障复位。");
                return;
            }
            bool manualMode = !_latestSnapshot.Automatic;
            if (manualMode && string.IsNullOrWhiteSpace(_manualTaskId))
            {
                ShowCommandBlocked("手动模式下尚未添加任务。请先点击“手动”添加任务，再点击“启动”执行。");
                return;
            }
            if (!_latestSnapshot.FlowPaused)
            {
                SetCommandFeedback("整机已处于运行状态，没有重复发送启动脉冲。", Color.FromArgb(35, 102, 170));
                return;
            }

            bool resumePausedFlow = _pauseCommandIssued;
            string action = resumePausedFlow
                ? "继续当前流程"
                : manualMode
                    ? "执行手动任务 " + _manualTaskId
                    : "配合主控执行下发任务";
            DialogResult result = MessageBox.Show(
                "即将" + action + "。" + Environment.NewLine + Environment.NewLine +
                "请确认：" + Environment.NewLine +
                "1. 防护门、急停和安全区域状态正常；" + Environment.NewLine +
                "2. 设备运动范围内无人、无工具和障碍物；" + Environment.NewLine +
                "3. 当前任务及试样信息正确。" + Environment.NewLine + Environment.NewLine +
                "点击“是”后发送启动脉冲。",
                "确认启动设备",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
            {
                SetCommandFeedback("已取消启动，未向PLC发送指令。", Color.DimGray);
                return;
            }

            bool sent = await ExecuteButtonAsync(
                btnLineStart,
                () => _plc.PulseAsync(
                    resumePausedFlow || !manualMode
                        ? PlcAddresses.LineStart
                        : PlcAddresses.StationStart(1),
                    _shutdown.Token),
                "启动",
                resumePausedFlow
                    ? "整机继续运行脉冲已发送，请观察流程恢复状态"
                    : manualMode
                    ? "S1手动启动脉冲已发送，请观察取放料机构状态"
                    : "整机启动脉冲已发送，等待总控/AGV任务");
            if (sent)
            {
                _pauseCommandIssued = false;
                SetStartPauseVisualState(false, true);
                if (manualMode)
                {
                    UpdateManualTaskRow("执行中");
                    lblCurrentTask.Text = "当前任务 · 手动任务执行中";
                }
                _database.LogOperation(_user.UserName, "整机启动", action);
            }
        }

        private async Task PauseLineAsync()
        {
            if (!CanSendMotionCommand("暂停")) return;
            if (_latestSnapshot.FlowPaused)
            {
                SetCommandFeedback("流程已经处于暂停/停止状态。", Color.FromArgb(170, 112, 25));
                return;
            }
            bool sent = await ExecuteButtonAsync(
                btnLinePause,
                () => _plc.WriteAsync(PlcAddresses.LinePause, false, _shutdown.Token),
                "暂停",
                "暂停指令已发送，请等待设备在PLC定义的安全位置停下");
            if (sent)
            {
                _pauseCommandIssued = true;
                SetStartPauseVisualState(true, true);
                if (!_latestSnapshot.Automatic && !string.IsNullOrWhiteSpace(_manualTaskId))
                    UpdateManualTaskRow("已暂停，等待启动");
                _database.LogOperation(_user.UserName, "整机暂停", "向取反暂停位写入0");
            }
        }

        private void UpdateStartPauseButtonState(SiliconSteelAdhesionTester.Models.PlcSnapshot snapshot)
        {
            if (snapshot == null)
            {
                SetStartPauseVisualState(true, false);
                return;
            }
            SetStartPauseVisualState(snapshot.FlowPaused, snapshot.Connected);
        }

        private void SetStartPauseVisualState(bool paused, bool connected)
        {
            if (btnLineStart == null || btnLinePause == null) return;
            btnLineStart.Enabled = connected && paused;
            btnLinePause.Enabled = connected && !paused;
            btnLineStart.BackColor = btnLineStart.Enabled
                ? Color.FromArgb(28, 145, 93)
                : Color.FromArgb(170, 176, 182);
            btnLinePause.BackColor = btnLinePause.Enabled
                ? Color.FromArgb(213, 145, 32)
                : Color.FromArgb(170, 176, 182);
            btnLineStart.Cursor = btnLineStart.Enabled ? Cursors.Hand : Cursors.Default;
            btnLinePause.Cursor = btnLinePause.Enabled ? Cursors.Hand : Cursors.Default;
        }

        private async Task HomeLineAsync()
        {
            if (!CanSendMotionCommand("回原位")) return;
            if (_latestSnapshot.EmergencyStop)
            {
                ShowCommandBlocked("急停处于触发状态，禁止回原位。请先排除急停原因并确认现场安全。");
                return;
            }
            if (!_latestSnapshot.FlowPaused)
            {
                ShowCommandBlocked("流程仍在运行，禁止回原位。请先点击“暂停”，等待PLC运行标识停止后再操作。");
                return;
            }
            if (IsAllHome(_latestSnapshot))
            {
                SetCommandFeedback("所有工位已经在原位，没有重复发送回原位指令。", Color.FromArgb(96, 76, 160));
                return;
            }

            DialogResult result = MessageBox.Show(
                "整机回原位会驱动机械爪、轴和气缸执行回零动作。" + Environment.NewLine + Environment.NewLine +
                "当前已确认流程处于暂停/停止状态。请再次确认运动区域无人、无障碍物，试样不会因回零动作造成夹伤或碰撞。" +
                Environment.NewLine + Environment.NewLine + "确认发送整机回原位脉冲吗？",
                "确认整机回原位",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
            {
                SetCommandFeedback("已取消回原位，未向PLC发送指令。", Color.DimGray);
                return;
            }

            bool sent = await ExecuteButtonAsync(
                btnLineHome,
                () => _plc.PulseAsync(PlcAddresses.LineHome, _shutdown.Token),
                "回原位",
                "回原位脉冲已发送，请持续观察各工位原位状态");
            if (sent) _database.LogOperation(_user.UserName, "整机回原位", "安全条件检查及人工确认通过");
        }

        private async Task ResetFaultAsync()
        {
            if (!CanSendMotionCommand("故障复位")) return;
            if (_latestSnapshot.EmergencyStop)
            {
                ShowCommandBlocked("急停仍处于触发状态，复位脉冲不会解除急停。请先排除急停原因并释放急停按钮。");
                return;
            }

            bool hasKnownFault = _latestSnapshot.FlowFault || HasStationFault(_latestSnapshot);
            if (!hasKnownFault)
            {
                DialogResult result = MessageBox.Show(
                    "当前PC快照没有检测到可识别的流程/工位故障。" + Environment.NewLine +
                    "“故障复位”只会向PLC发送一次HMI复位脉冲，不会启动设备，也不会清除仍未排除的故障原因。" +
                    Environment.NewLine + Environment.NewLine + "仍要发送复位脉冲吗？",
                    "未检测到可复位故障",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information,
                    MessageBoxDefaultButton.Button2);
                if (result != DialogResult.Yes)
                {
                    SetCommandFeedback("已取消故障复位，未向PLC发送指令。", Color.DimGray);
                    return;
                }
            }

            bool sent = await ExecuteButtonAsync(
                btnFaultReset,
                () => _plc.PulseAsync(PlcAddresses.ResetPulse, _shutdown.Token),
                "故障复位",
                "复位脉冲已发送；若报警仍存在，请检查急停、传感器、气压、伺服和PLC报警");
            if (sent)
                _database.LogOperation(_user.UserName, "故障复位",
                    hasKnownFault ? "检测到故障后发送复位脉冲" : "未检测到故障，用户确认后发送复位脉冲");
        }

        private bool CanSendMotionCommand(string commandName)
        {
            if (_latestSnapshot == null)
            {
                ShowCommandBlocked("尚未取得PLC状态，不能执行“" + commandName + "”。");
                return false;
            }
            if (!_latestSnapshot.Connected || !_plc.IsConnected)
            {
                ShowCommandBlocked("PLC尚未连接，不能执行“" + commandName + "”。");
                return false;
            }
            return true;
        }

        private void ShowCommandBlocked(string message)
        {
            SetCommandFeedback("操作被安全条件拦截：" + message, Color.Firebrick);
            MessageBox.Show(this, message, "操作已拦截", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static bool HasStationFault(SiliconSteelAdhesionTester.Models.PlcSnapshot snapshot)
        {
            return snapshot.Stations != null && snapshot.Stations.Any(station => station.Fault);
        }

        private void SetCommandFeedback(string message, Color color)
        {
            if (lblCommandFeedback == null) return;
            lblCommandFeedback.ForeColor = color;
            lblCommandFeedback.Text = DateTime.Now.ToString("HH:mm:ss") + "  " + message;
        }

        private bool CanSwitchOperatingMode()
        {
            if (_latestSnapshot == null)
            {
                MessageBox.Show("尚未取得PLC状态，暂时不能切换手动/自动模式。",
                    "无法切换模式", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            bool allHome = IsAllHome(_latestSnapshot);
            bool paused = _latestSnapshot.FlowPaused;
            if (allHome && paused) return true;

            string missing = !allHome && !paused
                ? "设备未全部回原位，并且流程尚未暂停。"
                : !allHome
                    ? "设备未全部回原位。"
                    : "流程尚未暂停。";
            MessageBox.Show(
                missing + Environment.NewLine + "请先暂停流程并确认所有工位均在原位，再切换手动/自动模式。",
                "不满足模式切换条件",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        private void OpenManualTaskDialog()
        {
            using (ManualTaskForm dialog = new ManualTaskForm())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                string taskId = string.IsNullOrWhiteSpace(dialog.TrayNumber)
                    ? "MAN-" + DateTime.Now.ToString("yyyyMMddHHmmss")
                    : dialog.TrayNumber;
                string oriented = dialog.OrientedCount.HasValue
                    ? "取向 " + dialog.OrientedCount.Value
                    : "取向未填";
                string nonOriented = dialog.NonOrientedCount.HasValue
                    ? "无取向 " + dialog.NonOrientedCount.Value
                    : "无取向未填";

                if (dgvTasks.Rows.Count == 1 &&
                    Convert.ToString(dgvTasks.Rows[0].Cells[0].Value) == "-")
                    dgvTasks.Rows.Clear();
                dgvTasks.Rows.Insert(0, taskId, oriented + " / " + nonOriented, "手动任务待执行");
                _manualTaskId = taskId;
                lblCurrentTask.Text = "当前任务 · 手动创建，等待启动";
                lblQrCodeContent.Text = taskId;
                SetPreviewSample(taskId);
                lblMaterialType.Text = oriented + "，" + nonOriented;
                AppendRuntimeLog("[MANUAL] 已创建手动任务：" + taskId);
                _database.SaveManualTask(taskId, dialog.OrientedCount, dialog.NonOrientedCount, _user.UserName);
                _database.LogOperation(_user.UserName, "创建手动任务",
                    "料盘=" + (string.IsNullOrWhiteSpace(dialog.TrayNumber) ? "未填" : dialog.TrayNumber) +
                    "；" + oriented + "；" + nonOriented);
            }
        }

        private void OpenVisionWindow()
        {
            new VisionInspectionForm(new AdhesionVisionService(_settings), _database, _user, _lastScannedQrCode).Show(this);
        }

        private void OpenSettingsWindow()
        {
            if (!_user.CanDebug)
            {
                MessageBox.Show("系统设置仅允许电气调试员或超级管理员修改。",
                    "权限不足", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            using (SettingsForm dialog = new SettingsForm(_settings))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    AppendRuntimeLog("[SETTINGS] 系统参数已保存，重启后完整生效");
                    _database.LogOperation(_user.UserName, "修改系统设置", "设置已保存，等待重启生效");
                }
            }
        }

        private void UpdateManualTaskRow(string status)
        {
            if (string.IsNullOrWhiteSpace(_manualTaskId)) return;
            foreach (DataGridViewRow row in dgvTasks.Rows)
            {
                if (string.Equals(Convert.ToString(row.Cells[0].Value), _manualTaskId, StringComparison.Ordinal))
                {
                    row.Cells[2].Value = status;
                    break;
                }
            }
        }

        private void BindStation(int station)
        {
            BindCommand(
                _stationStarts[station - 1],
                () => _plc.PulseAsync(PlcAddresses.StationStart(station), _shutdown.Token),
                "S" + station + "启动",
                "S" + station + "单步启动脉冲已发送");
            BindCommand(_stationContinuous[station - 1], async () =>
            {
                string address = PlcAddresses.StationContinuous(station);
                bool current = Convert.ToBoolean(await _plc.ReadAsync(address, _shutdown.Token));
                await _plc.WriteAsync(address, !current, _shutdown.Token);
                _stationContinuous[station - 1].BackColor = !current ? Color.SeaGreen : Color.FromArgb(74, 78, 105);
            }, "S" + station + "连续模式", "S" + station + "连续/单步模式已切换");
        }

        private void BindCommand(Button button, Func<Task> action, string actionName = null, string successMessage = null)
        {
            button.Click += async (s, e) => await ExecuteButtonAsync(button, action, actionName, successMessage);
        }

        private async Task<bool> ExecuteButtonAsync(Button button, Func<Task> action, string actionName = null, string successMessage = null)
        {
            string originalText = button.Text;
            string displayName = string.IsNullOrWhiteSpace(actionName) ? originalText : actionName;
            bool succeeded = false;
            try
            {
                button.Enabled = false;
                button.Text = "…  " + displayName;
                SetCommandFeedback("正在执行：" + displayName, Color.FromArgb(35, 102, 170));
                await action();
                string completed = string.IsNullOrWhiteSpace(successMessage) ? displayName + "指令已执行" : successMessage;
                SetCommandFeedback("✓ " + completed, Color.FromArgb(28, 128, 82));
                AppendRuntimeLog("[COMMAND] " + completed);
                succeeded = true;
            }
            catch (Exception ex)
            {
                SetCommandFeedback("✕ " + displayName + "失败：" + ex.Message, Color.Firebrick);
                ShowFault("COMMAND", displayName, ex.Message);
            }
            finally
            {
                button.Text = originalText;
                if (button == btnLineStart || button == btnLinePause)
                    UpdateStartPauseButtonState(_latestSnapshot);
                else
                    button.Enabled = true;
            }
            return succeeded;
        }
    }
}
