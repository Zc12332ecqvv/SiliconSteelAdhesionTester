using System;
using System.Threading;
using System.Threading.Tasks;
using S7.Net;
using SiliconSteelAdhesionTester.Configuration;
using SiliconSteelAdhesionTester.Models;

namespace SiliconSteelAdhesionTester.Services.Plc
{
    public sealed class S7PlcService : IPlcService
    {
        private readonly AppSettings _settings;
        private readonly S7.Net.Plc _plc;
        public event EventHandler<PlcSnapshot> SnapshotChanged;
        public event EventHandler<string> CommunicationFault;
        public bool IsConnected { get { return _plc.IsConnected; } }

        public S7PlcService(AppSettings settings)
        {
            _settings = settings;
            _plc = new S7.Net.Plc(CpuType.S71500, settings.PlcIp, settings.Rack, settings.Slot);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!_plc.IsConnected) await _plc.OpenAsync(cancellationToken).ConfigureAwait(false);
                    var stations = new StationSnapshot[4];
                    for (var i = 1; i <= 4; i++)
                    {
                        stations[i - 1] = new StationSnapshot
                        {
                            Number = i,
                            Ready = Convert.ToBoolean(await _plc.ReadAsync(PlcAddresses.StationReady(i), cancellationToken).ConfigureAwait(false)),
                            Home = Convert.ToBoolean(await _plc.ReadAsync(PlcAddresses.StationHomeState(i), cancellationToken).ConfigureAwait(false)),
                            Done = Convert.ToBoolean(await _plc.ReadAsync(PlcAddresses.StationDone(i), cancellationToken).ConfigureAwait(false)),
                            Step = Convert.ToInt16(await _plc.ReadAsync(PlcAddresses.StationStep(i), cancellationToken).ConfigureAwait(false))
                        };
                        stations[i - 1].Running = stations[i - 1].Step > 0 && !stations[i - 1].Done;
                    }
                    var autoM = Convert.ToBoolean(await _plc.ReadAsync(PlcAddresses.AutoMode, cancellationToken).ConfigureAwait(false));
                    var eStop = Convert.ToBoolean(await _plc.ReadAsync(PlcAddresses.EmergencyStop, cancellationToken).ConfigureAwait(false));
                    var handler = SnapshotChanged;
                    if (handler != null) handler(this, new PlcSnapshot
                    {
                        Connected = true,
                        Timestamp = DateTime.Now,
                        Automatic = !autoM,
                        EmergencyStop = !eStop,
                        Stations = stations,
                        FlowStepIndex = InferFlowStep(stations),
                        FlowPaused = false,
                        FlowMessage = "实体PLC流程状态（根据四工位自动步骤推算）"
                    });
                }
                catch (Exception ex)
                {
                    var handler = CommunicationFault;
                    if (handler != null) handler(this, ex.Message);
                    try { _plc.Close(); } catch { }
                }
                await Task.Delay(_settings.PollIntervalMs, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task PulseAsync(string address, CancellationToken cancellationToken)
        {
            await WriteAsync(address, true, cancellationToken).ConfigureAwait(false);
            try { await Task.Delay(_settings.CommandPulseMs, cancellationToken).ConfigureAwait(false); }
            finally { await WriteAsync(address, false, CancellationToken.None).ConfigureAwait(false); }
        }

        public async Task WriteAsync(string address, object value, CancellationToken cancellationToken)
        {
            if (!_plc.IsConnected) throw new InvalidOperationException("PLC 未连接");
            await _plc.WriteAsync(address, value, cancellationToken).ConfigureAwait(false);
        }

        public async Task<object> ReadAsync(string address, CancellationToken cancellationToken)
        {
            if (!_plc.IsConnected) throw new InvalidOperationException("PLC 未连接");
            return await _plc.ReadAsync(address, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_plc.IsConnected) _plc.Close();
        }

        private static int InferFlowStep(StationSnapshot[] stations)
        {
            var maxStep = 0;
            foreach (var station in stations) if (station.Step > maxStep) maxStep = station.Step;
            return maxStep % 8;
        }
    }
}
