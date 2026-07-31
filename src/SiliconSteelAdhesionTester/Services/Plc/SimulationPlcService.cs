using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using SiliconSteelAdhesionTester.Configuration;
using SiliconSteelAdhesionTester.Models;

namespace SiliconSteelAdhesionTester.Services.Plc
{
    public sealed class SimulationPlcService : IPlcService
    {
        private readonly AppSettings _settings;
        private readonly ConcurrentDictionary<string, object> _memory = new ConcurrentDictionary<string, object>();
        private readonly short[] _steps = new short[4];
        private readonly bool[] _stationRunning = { false, false, false, false };
        private int _completedCount;
        private bool _lineRunning;
        private bool _linePaused;
        private int _flowStep;

        public event EventHandler<PlcSnapshot> SnapshotChanged;
#pragma warning disable 0067
        public event EventHandler<string> CommunicationFault;
#pragma warning restore 0067
        public bool IsConnected { get; private set; }

        public SimulationPlcService(AppSettings settings)
        {
            _settings = settings;
            _memory[PlcAddresses.AutoMode] = false;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            IsConnected = true;
            while (!cancellationToken.IsCancellationRequested)
            {
                StationSnapshot[] stations = new StationSnapshot[4];
                bool wholeLineHome = true;
                for (int i = 0; i < 4; i++)
                {
                    stations[i] = new StationSnapshot
                    {
                        Number = i + 1,
                        Ready = _steps[i] == 0,
                        Home = _steps[i] == 0,
                        Done = _steps[i] == 15,
                        Running = _lineRunning && !_linePaused && _stationRunning[i],
                        Step = _steps[i]
                    };
                    if (!stations[i].Home) wholeLineHome = false;
                }

                SnapshotChanged?.Invoke(this, new PlcSnapshot
                {
                    Connected = true,
                    Automatic = !GetBool(PlcAddresses.AutoMode),
                    Timestamp = DateTime.Now,
                    Stations = stations,
                    QrCodeContent = null,
                    TotalCount = _completedCount,
                    ShiftCount = _completedCount,
                    FlowStepIndex = _flowStep,
                    FlowPaused = !_lineRunning || _linePaused,
                    WholeLineHome = wholeLineHome,
                    S1AutomaticRunning = _lineRunning && !_linePaused && _stationRunning[0],
                    S2HasPendingMaterial = GetBool(PlcAddresses.S2HasPendingMaterial),
                    S3HasPendingMaterial = GetBool(PlcAddresses.S3HasPendingMaterial),
                    S4HasMaterialForTape = GetBool(PlcAddresses.S4HasMaterialForTape),
                    S2ScanAllowed = GetBool(PlcAddresses.S2ScanAllowed),
                    S2ScanDone = GetBool(PlcAddresses.S2ScanDone),
                    S2ScanOk = GetBool(PlcAddresses.S2ScanOk),
                    S2ScanNg = GetBool(PlcAddresses.S2ScanNg),
                    S3ScanAllowed = GetBool(PlcAddresses.S3ScanAllowed),
                    S3ScanDone = GetBool(PlcAddresses.S3ScanDone),
                    S3ScanOk = GetBool(PlcAddresses.S3ScanOk),
                    S3ScanNg = GetBool(PlcAddresses.S3ScanNg),
                    S2FirstPhotoAllowed = GetBool(PlcAddresses.S2FirstPhotoAllowed),
                    S2FirstPhotoDone = GetBool(PlcAddresses.S2FirstPhotoDone),
                    S2SecondPhotoAllowed = GetBool(PlcAddresses.S2SecondPhotoAllowed),
                    S2SecondPhotoDone = GetBool(PlcAddresses.S2SecondPhotoDone),
                    S2SecondPhotoOk = GetBool(PlcAddresses.S2SecondPhotoOk),
                    S2SecondPhotoNg = GetBool(PlcAddresses.S2SecondPhotoNg),
                    S4PhotoAllowed = GetBool(PlcAddresses.S4CameraAllowed),
                    S4PhotoDone = GetBool(PlcAddresses.S4CameraDone),
                    S4PhotoOk = GetBool(PlcAddresses.S4CameraOk),
                    S4PhotoNg = GetBool(PlcAddresses.S4CameraNg),
                    FlowMessage = FlowDescription(_lineRunning, _linePaused)
                });
                await Task.Delay(_settings.PollIntervalMs, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task PulseAsync(string address, CancellationToken cancellationToken)
        {
            await WriteAsync(address, true, cancellationToken).ConfigureAwait(false);
            try { await Task.Delay(_settings.CommandPulseMs, cancellationToken).ConfigureAwait(false); }
            finally { await WriteAsync(address, false, CancellationToken.None).ConfigureAwait(false); }
        }

        public Task WriteAsync(string address, object value, CancellationToken cancellationToken)
        {
            bool wasOn = GetBool(address);
            _memory[address] = value;
            bool isOn = value is bool && (bool)value;
            if (address == PlcAddresses.LineStart && isOn)
            {
                _lineRunning = true;
                _linePaused = false;
            }
            else if (address == PlcAddresses.LinePause) { _linePaused = true; }
            else if (address == PlcAddresses.LineHome && isOn)
            {
                for (int i = 0; i < 4; i++)
                {
                    _steps[i] = 0;
                    _stationRunning[i] = false;
                }
                _flowStep = 0;
            }
            else
            {
                for (int station = 1; station <= 4; station++)
                {
                    if (address == PlcAddresses.StationStart(station) && isOn)
                    {
                        _stationRunning[station - 1] = true;
                        _lineRunning = true;
                        _linePaused = false;
                    }
                    if (address == PlcAddresses.StationHome(station) && isOn) _steps[station - 1] = 0;
                }
            }
            UpdateSignalDrivenState(address, isOn, wasOn);
            return Task.CompletedTask;
        }

        public Task<object> ReadAsync(string address, CancellationToken cancellationToken)
        {
            return Task.FromResult(_memory.TryGetValue(address, out object value) ? value : (object)false);
        }

        private bool GetBool(string address)
        {
            return _memory.TryGetValue(address, out object value) && value is bool && (bool)value;
        }

        private void UpdateSignalDrivenState(string address, bool isOn, bool wasOn)
        {
            if (!isOn) return;
            if (address == PlcAddresses.S2ScanAllowed)
            {
                _stationRunning[1] = true;
                _steps[1] = 1;
                _flowStep = 0;
            }
            else if (address == PlcAddresses.S2FirstPhotoAllowed)
            {
                _stationRunning[1] = true;
                _steps[1] = 2;
                _flowStep = 1;
            }
            else if (address == PlcAddresses.S2SecondPhotoAllowed)
            {
                _stationRunning[1] = true;
                _steps[1] = 3;
                _flowStep = 3;
            }
            else if (address == PlcAddresses.S2SecondPhotoDone && !wasOn)
            {
                _steps[1] = 15;
                _stationRunning[1] = false;
                _flowStep = 4;
                _completedCount++;
            }
            else if (address == PlcAddresses.S3ScanAllowed)
            {
                _stationRunning[2] = true;
                _steps[2] = 1;
                _flowStep = 0;
            }
            else if (address == PlcAddresses.S4HasMaterialForTape)
            {
                _steps[2] = 15;
                _stationRunning[2] = false;
                _stationRunning[3] = true;
                _steps[3] = 1;
                _flowStep = 2;
            }
            else if (address == PlcAddresses.S4CameraAllowed)
            {
                _stationRunning[3] = true;
                _steps[3] = 2;
                _flowStep = 3;
            }
            else if (address == PlcAddresses.S4CameraDone && !wasOn)
            {
                _steps[3] = 15;
                _stationRunning[3] = false;
                _flowStep = 4;
                _completedCount++;
            }
        }

        private string FlowDescription(bool running, bool paused)
        {
            if (!running) return "等待点击启动";
            if (paused) return "仿真流程已暂停，等待再次启动";
            if (GetBool(PlcAddresses.S2SecondPhotoAllowed))
                return "取向弯折检测工位：压弯后拍照允许已接通";
            if (GetBool(PlcAddresses.S2FirstPhotoAllowed))
                return "取向弯折检测工位：压弯前拍照允许已接通";
            if (GetBool(PlcAddresses.S2ScanAllowed))
                return "取向弯折检测工位：扫码允许已接通";
            if (GetBool(PlcAddresses.S4CameraAllowed))
                return "无取向检测工位：拍照允许已接通";
            if (GetBool(PlcAddresses.S4HasMaterialForTape))
                return "无取向试样已到达检测工位";
            if (GetBool(PlcAddresses.S3ScanAllowed))
                return "无取向弯折工位：扫码允许已接通";
            return "仿真已启动，等待工程师调试写入PLC交互信号";
        }

        public void Dispose() { IsConnected = false; }
    }
}
