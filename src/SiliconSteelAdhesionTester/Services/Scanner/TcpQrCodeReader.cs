using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SiliconSteelAdhesionTester.Configuration;

namespace SiliconSteelAdhesionTester.Services.Scanner
{
    public interface IQrCodeReader
    {
        Task<string> ReadAsync(bool oriented, CancellationToken cancellationToken);
    }

    public sealed class TcpQrCodeReader : IQrCodeReader
    {
        private readonly AppSettings _settings;

        public TcpQrCodeReader(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<string> ReadAsync(bool oriented, CancellationToken cancellationToken)
        {

            string ip = oriented ? _settings.OrientedScannerIp : _settings.NonOrientedScannerIp;
            int port = oriented ? _settings.OrientedScannerPort : _settings.NonOrientedScannerPort;
            using (TcpClient client = new TcpClient())
            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                Task connect = client.ConnectAsync(ip, port);
                Task completed = await Task.WhenAny(connect, Task.Delay(_settings.ScannerConnectTimeoutMs, timeout.Token)).ConfigureAwait(false);
                if (completed != connect)
                {
                    client.Close();
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException("连接SR-1000超时：" + ip + ":" + port);
                }
                await connect.ConfigureAwait(false);

                using (NetworkStream stream = client.GetStream())
                {
                    string terminator = ResolveTerminator(_settings.ScannerTerminator);
                    if (!string.IsNullOrEmpty(_settings.ScannerTriggerCommand))
                        await SendCommandAsync(stream, _settings.ScannerTriggerCommand + terminator, cancellationToken).ConfigureAwait(false);

                    timeout.CancelAfter(Math.Max(100, _settings.ScannerReadTimeoutMs));
                    string content;
                    try
                    {
                        content = await ReadMessageAsync(stream, terminator, timeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        throw new TimeoutException("等待SR-1000二维码结果超时：" + ip + ":" + port);
                    }
                    finally
                    {
                        if (!string.IsNullOrEmpty(_settings.ScannerStopCommand) && stream.CanWrite)
                        {
                            try { await SendCommandAsync(stream, _settings.ScannerStopCommand + terminator, CancellationToken.None).ConfigureAwait(false); }
                            catch { }
                        }
                    }

                    content = Normalize(content);
                    if (content.Length < _settings.QrCodeMinimumLength)
                        throw new InvalidDataException("SR-1000返回的二维码内容长度不足。");
                    if (content.Length > 256)
                        throw new InvalidDataException("SR-1000返回的二维码内容超过256字符。");
                    return content;
                }
            }
        }
       
        private static async Task SendCommandAsync(NetworkStream stream, string command, CancellationToken cancellationToken)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(command);
            await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task<string> ReadMessageAsync(NetworkStream stream, string terminator, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[512];
            using (MemoryStream message = new MemoryStream())
            {
                while (message.Length < 4096)
                {
                    int count = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                    if (count <= 0) throw new IOException("SR-1000已断开连接。");
                    message.Write(buffer, 0, count);
                    string current = Encoding.UTF8.GetString(message.ToArray());
                    if (string.IsNullOrEmpty(terminator) || current.Contains(terminator))
                        return string.IsNullOrEmpty(terminator)
                            ? current
                            : current.Substring(0, current.IndexOf(terminator, StringComparison.Ordinal));
                }
                throw new InvalidDataException("SR-1000返回数据超过4096字节。");
            }
        }

        private static string ResolveTerminator(string value)
        {
            switch ((value ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "CR": return "\r";
                case "LF": return "\n";
                case "CRLF": return "\r\n";
                case "NONE":
                case "无": return string.Empty;
                default: return "\r";
            }
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim(' ', '\t', '\r', '\n', '\0', '\u0002', '\u0003');
        }
    }
}
