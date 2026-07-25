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
        private int _processTick;
        private int _completedCount;
        private int _barcodeSequence = 1;
        private string _currentBarcode;
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
                if (_lineRunning && !_linePaused) _processTick++;
                if (_lineRunning && !_linePaused && _processTick % 8 == 0)
                {
                    for (var i = 0; i < 4; i++)
                    {
                        if (!_stationRunning[i]) continue;
                        _steps[i]++;
                        if (_steps[i] > 15) _steps[i] = 0;
                    }
                }
                if (_lineRunning && !_linePaused && _processTick % 12 == 0)
                {
                    _flowStep++;
                    if (_flowStep >= 8)
                    {
                        _flowStep = 0;
                        _completedCount++;
                        _barcodeSequence++;
                        _currentBarcode = "SIM-1-" + _barcodeSequence.ToString("D6");
                    }
                }

                var stations = new StationSnapshot[4];
                for (var i = 0; i < 4; i++)
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
                }

                var handler = SnapshotChanged;
                if (handler != null) handler(this, new PlcSnapshot
                {
                    Connected = true,
                    Automatic = !GetBool(PlcAddresses.AutoMode),
                    Timestamp = DateTime.Now,
                    Stations = stations,
                    Barcode = _currentBarcode,
                    TotalCount = _completedCount,
                    ShiftCount = _completedCount,
                    FlowStepIndex = _flowStep,
                    FlowPaused = !_lineRunning || _linePaused,
                    S2ScanAllowed = GetBool(PlcAddresses.S2ScanAllowed),
                    S3ScanAllowed = GetBool(PlcAddresses.S3ScanAllowed),
                    FlowMessage = FlowDescription(_flowStep, _lineRunning, _linePaused)
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
            _memory[address] = value;
            var isOn = value is bool && (bool)value;
            if (address == PlcAddresses.LineStart && isOn)
            {
                if (string.IsNullOrEmpty(_currentBarcode))
                    _currentBarcode = "SIM-1-" + _barcodeSequence.ToString("D6");
                _lineRunning = true;
                _linePaused = false;
                for (var i = 0; i < 4; i++) _stationRunning[i] = true;
            }
            else if (address == PlcAddresses.LinePause) { _linePaused = true; }
            else if (address == PlcAddresses.SimulationLineStop) { _lineRunning = false; _linePaused = false; for (var i = 0; i < 4; i++) _stationRunning[i] = false; }
            else if (address == PlcAddresses.LineHome && isOn) { for (var i = 0; i < 4; i++) _steps[i] = 0; _flowStep = 0; }
            else
            {
                for (var station = 1; station <= 4; station++)
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
            return Task.CompletedTask;
        }

        public Task<object> ReadAsync(string address, CancellationToken cancellationToken)
        {
            object value;
            return Task.FromResult(_memory.TryGetValue(address, out value) ? value : (object)false);
        }

        private bool GetBool(string address)
        {
            object value;
            return _memory.TryGetValue(address, out value) && value is bool && (bool)value;
        }

        private static string FlowDescription(int step, bool running, bool paused)
        {
            string[] descriptions =
            {
                "AGV正在配送物料到S1上料位",
                "S1传感器检测来料到位",
                "扫码设备读取物料条码",
                "上位机执行白名单、品类与重复扫码校验",
                "工业相机采集物料图像",
                "视觉系统返回有取向、无取向或不良品分类",
                "PLC按检测结果执行对应工位流程",
                "本件流程完成，通知AGV并等待下一次来料"
            };
            if (!running) return "等待总控下达任务";
            return paused ? "流程已暂停：" + descriptions[step] : descriptions[step];
        }

        public void Dispose() { IsConnected = false; }
    }
}
