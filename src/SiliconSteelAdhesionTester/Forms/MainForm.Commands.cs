using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using SiliconSteelAdhesionTester.Services.Plc;
using SiliconSteelAdhesionTester.Services.Vision;

namespace SiliconSteelAdhesionTester.Forms
{
    public partial class MainForm
    {
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
            AppendRuntimeLog("主界面初始化完成");

            btnAutoMode.Click += async (s, e) =>
            {
                if (_automatic) return;
                if (!CanSwitchOperatingMode()) return;
                await ExecuteButtonAsync(btnAutoMode, async () =>
                {
                    await _plc.WriteAsync(PlcAddresses.AutoMode, false, _shutdown.Token);
                    _automatic = true;
                    AppendRuntimeLog("已切换为自动模式");
                });
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
                    });
                }
                if (!_automatic) OpenManualTaskDialog();
            };
            BindCommand(btnLineStart, () => _plc.PulseAsync(PlcAddresses.LineStart, _shutdown.Token));
            BindCommand(btnLinePause, () => _plc.WriteAsync(PlcAddresses.LinePause, false, _shutdown.Token));
            BindCommand(btnLineStop, () => _plc.WriteAsync(_settings.Simulation ? PlcAddresses.SimulationLineStop : PlcAddresses.LinePause, true, _shutdown.Token));
            BindCommand(btnLineHome, () => _plc.PulseAsync(PlcAddresses.LineHome, _shutdown.Token));
            BindCommand(btnFaultReset, () => _plc.PulseAsync(PlcAddresses.ResetPulse, _shutdown.Token));
            for (int station = 1; station <= 4; station++) BindStation(station);

            btnVision.Click += (s, e) => OpenVisionWindow();
            btnNavVision.Click += (s, e) => OpenVisionWindow();
            btnDebug.Click += (s, e) => new DebugForm(_plc, _user, _shutdown.Token).Show(this);
            btnRecords.Click += (s, e) => new DataRecordsForm(_database, false).Show(this);
            btnFaultLogs.Click += (s, e) => new DataRecordsForm(_database, true).Show(this);
            btnNavRecords.Click += (s, e) => new DataRecordsForm(_database, false).Show(this);
            btnNavLogs.Click += (s, e) => new DataRecordsForm(_database, true).Show(this);
            btnNavSettings.Click += (s, e) => OpenSettingsWindow();
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

        private void BindStation(int station)
        {
            BindCommand(_stationStarts[station - 1], () => _plc.PulseAsync(PlcAddresses.StationStart(station), _shutdown.Token));
            BindCommand(_stationContinuous[station - 1], async () =>
            {
                string address = PlcAddresses.StationContinuous(station);
                bool current = Convert.ToBoolean(await _plc.ReadAsync(address, _shutdown.Token));
                await _plc.WriteAsync(address, !current, _shutdown.Token);
                _stationContinuous[station - 1].BackColor = !current ? Color.SeaGreen : Color.FromArgb(74, 78, 105);
            });
        }

        private void BindCommand(Button button, Func<Task> action)
        {
            button.Click += async (s, e) => await ExecuteButtonAsync(button, action);
        }

        private async Task ExecuteButtonAsync(Button button, Func<Task> action)
        {
            try { button.Enabled = false; await action(); }
            catch (Exception ex) { ShowFault("COMMAND", button.Text, ex.Message); }
            finally { button.Enabled = true; }
        }
    }
}
