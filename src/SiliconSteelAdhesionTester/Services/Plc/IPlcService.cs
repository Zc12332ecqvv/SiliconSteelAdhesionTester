using System;
using System.Threading;
using System.Threading.Tasks;
using SiliconSteelAdhesionTester.Models;

namespace SiliconSteelAdhesionTester.Services.Plc
{
    public interface IPlcService : IDisposable
    {
        event EventHandler<PlcSnapshot> SnapshotChanged;
        event EventHandler<string> CommunicationFault;
        bool IsConnected { get; }
        Task StartAsync(CancellationToken cancellationToken);
        Task PulseAsync(string address, CancellationToken cancellationToken);
        Task WriteAsync(string address, object value, CancellationToken cancellationToken);
        Task<object> ReadAsync(string address, CancellationToken cancellationToken);
    }
}
