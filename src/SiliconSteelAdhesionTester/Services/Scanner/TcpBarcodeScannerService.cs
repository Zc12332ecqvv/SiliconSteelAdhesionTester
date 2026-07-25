using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SiliconSteelAdhesionTester.Services.Scanner
{
    public sealed class TcpBarcodeScannerService : IDisposable
    {
        private readonly BarcodeScannerEndpoint[] _endpoints;
        private readonly int _duplicateSeconds;
        private readonly int _reconnectDelayMs;
        private readonly object _duplicateLock = new object();
        private readonly Dictionary<string, DateTime> _recentBarcodes =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;

        public TcpBarcodeScannerService(
            BarcodeScannerEndpoint oriented,
            BarcodeScannerEndpoint nonOriented,
            int duplicateSeconds,
            int reconnectDelayMs)
        {
            _endpoints = new[] { oriented, nonOriented };
            _duplicateSeconds = Math.Max(0, duplicateSeconds);
            _reconnectDelayMs = Math.Max(500, reconnectDelayMs);
        }

        public event EventHandler<BarcodeScannedEventArgs> BarcodeScanned;
        public event EventHandler<ScannerStatusEventArgs> StatusChanged;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            return Task.WhenAll(
                RunEndpointAsync(_endpoints[0], cancellationToken),
                RunEndpointAsync(_endpoints[1], cancellationToken));
        }

        private async Task RunEndpointAsync(BarcodeScannerEndpoint endpoint, CancellationToken cancellationToken)
        {
            var failureReported = false;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using (var client = new TcpClient())
                    using (cancellationToken.Register(() => CloseQuietly(client)))
                    {
                        var connectTask = client.ConnectAsync(endpoint.IpAddress, endpoint.Port);
                        var timeoutTask = Task.Delay(5000, cancellationToken);
                        if (await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false) != connectTask)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            throw new TimeoutException("连接超时");
                        }

                        await connectTask.ConfigureAwait(false);
                        failureReported = false;
                        RaiseStatus(endpoint, true, endpoint.DisplayName + "已连接");

                        using (var stream = client.GetStream())
                        using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
                        {
                            while (!cancellationToken.IsCancellationRequested)
                            {
                                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                                if (line == null) throw new IOException("扫码枪已断开连接");
                                ProcessBarcode(endpoint, line);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!failureReported)
                    {
                        RaiseStatus(endpoint, false, endpoint.DisplayName + "连接失败：" + ex.Message + "，正在自动重连");
                        failureReported = true;
                    }

                    try
                    {
                        await Task.Delay(_reconnectDelayMs, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private void ProcessBarcode(BarcodeScannerEndpoint endpoint, string rawBarcode)
        {
            var barcode = Normalize(rawBarcode);
            if (string.IsNullOrEmpty(barcode)) return;
            if (barcode.Length > 256)
            {
                RaiseStatus(endpoint, true, endpoint.DisplayName + "收到超长数据，已忽略");
                return;
            }

            var now = DateTime.Now;
            lock (_duplicateLock)
            {
                DateTime previous;
                if (_recentBarcodes.TryGetValue(barcode, out previous) &&
                    (now - previous).TotalSeconds < _duplicateSeconds)
                {
                    RaiseStatus(endpoint, true, "重复扫码已忽略：" + barcode);
                    return;
                }

                _recentBarcodes[barcode] = now;
                RemoveExpiredBarcodes(now);
            }

            BarcodeScanned?.Invoke(this, new BarcodeScannedEventArgs(endpoint.Source, barcode, now));
        }

        private void RemoveExpiredBarcodes(DateTime now)
        {
            var expired = new List<string>();
            foreach (var item in _recentBarcodes)
            {
                if ((now - item.Value).TotalSeconds >= Math.Max(1, _duplicateSeconds))
                    expired.Add(item.Key);
            }
            foreach (var barcode in expired) _recentBarcodes.Remove(barcode);
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim(' ', '\t', '\r', '\n', '\0', '\u0002', '\u0003');
        }

        private void RaiseStatus(BarcodeScannerEndpoint endpoint, bool connected, string message)
        {
            StatusChanged?.Invoke(this, new ScannerStatusEventArgs(endpoint.Source, connected, message));
        }

        private static void CloseQuietly(TcpClient client)
        {
            try { client.Close(); }
            catch { }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TcpBarcodeScannerService));
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
