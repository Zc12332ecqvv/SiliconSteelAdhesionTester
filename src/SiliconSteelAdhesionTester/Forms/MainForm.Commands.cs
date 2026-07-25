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
            dgvTasks.Rows.Add("-", "-", "等待LIMS任务");
            AppendRuntimeLog("主界面初始化完成");

            BindCommand(btnAutoMode, async () =>
            {
                _automatic = true;
                await _plc.WriteAsync(PlcAddresses.AutoMode, false, _shutdown.Token);
            });
            btnManualMode.Click += async (s, e) =>
            {
                if (!_user.CanDebug)
                {
                    MessageBox.Show("当前账号没有手动调试权限，请使用电气调试员或超级管理员账号。", "权限不足", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                await ExecuteButtonAsync(btnManualMode, async () =>
                {
                    _automatic = false;
                    await _plc.WriteAsync(PlcAddresses.AutoMode, true, _shutdown.Token);
                });
            };
            BindCommand(btnLineStart, () => _plc.PulseAsync(PlcAddresses.LineStart, _shutdown.Token));
            BindCommand(btnLinePause, () => _plc.WriteAsync(PlcAddresses.LinePause, false, _shutdown.Token));
            BindCommand(btnLineStop, () => _plc.WriteAsync(_settings.Simulation ? PlcAddresses.SimulationLineStop : PlcAddresses.LinePause, true, _shutdown.Token));
            BindCommand(btnLineHome, () => _plc.PulseAsync(PlcAddresses.LineHome, _shutdown.Token));
            BindCommand(btnFaultReset, () => _plc.PulseAsync(PlcAddresses.ResetPulse, _shutdown.Token));
            for (var station = 1; station <= 4; station++) BindStation(station);

            btnVision.Click += (s, e) => OpenVisionWindow();
            btnNavVision.Click += (s, e) => OpenVisionWindow();
            btnDebug.Click += (s, e) => new DebugForm(_plc, _user, _shutdown.Token).Show(this);
            btnRecords.Click += (s, e) => MessageBox.Show("生产记录查询将在数据业务阶段接入。", "生产记录");
            btnFaultLogs.Click += (s, e) => MessageBox.Show("故障日志查询将在报表阶段接入。", "故障日志");
            btnNavRecords.Click += (s, e) => btnRecords.PerformClick();
            btnNavLogs.Click += (s, e) => btnFaultLogs.PerformClick();
            btnNavSettings.Click += (s, e) => MessageBox.Show("系统设置页面将在相机、LIMS和正式判定参数确认后接入。", "系统设置");
        }

        private void OpenVisionWindow()
        {
            new VisionInspectionForm(new AdhesionVisionService(_settings)).Show(this);
        }

        private void BindStation(int station)
        {
            BindCommand(_stationStarts[station - 1], () => _plc.PulseAsync(PlcAddresses.StationStart(station), _shutdown.Token));
            BindCommand(_stationContinuous[station - 1], async () =>
            {
                var address = PlcAddresses.StationContinuous(station);
                var current = Convert.ToBoolean(await _plc.ReadAsync(address, _shutdown.Token));
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
