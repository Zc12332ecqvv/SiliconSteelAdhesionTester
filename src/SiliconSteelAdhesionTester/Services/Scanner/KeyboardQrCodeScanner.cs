using System;
using System.Collections.Generic;
using System.Text;

namespace SiliconSteelAdhesionTester.Services.Scanner
{
    /// <summary>
    /// 接收配置为 USB-HID 键盘模式、并以回车结尾的工业二维码读取器输入。
    /// 字符间隔超时后自动丢弃残留，避免把人工键盘输入拼成二维码内容。
    /// </summary>
    public sealed class KeyboardQrCodeScanner
    {
        private readonly StringBuilder _buffer = new StringBuilder();
        private readonly Dictionary<string, DateTime> _recent =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly int _inputTimeoutMs;
        private readonly int _minimumLength;
        private readonly int _duplicateSeconds;
        private DateTime _lastCharacterAt = DateTime.MinValue;

        public KeyboardQrCodeScanner(int inputTimeoutMs, int minimumLength, int duplicateSeconds)
        {
            _inputTimeoutMs = Math.Max(30, inputTimeoutMs);
            _minimumLength = Math.Max(1, minimumLength);
            _duplicateSeconds = Math.Max(0, duplicateSeconds);
        }

        public QrCodeInputResult Accept(char character, DateTime receivedAt)
        {
            if (character == '\r' || character == '\n' || character == '\t')
            {
                if (_buffer.Length == 0) return QrCodeInputResult.None;
                string qrCodeContent = Normalize(_buffer.ToString());
                Reset();
                if (qrCodeContent.Length < _minimumLength)
                    return QrCodeInputResult.Rejected("二维码内容长度不足");
                if (qrCodeContent.Length > 256)
                    return QrCodeInputResult.Rejected("二维码内容长度超过256字符");

                if (_recent.TryGetValue(qrCodeContent, out DateTime previous) &&
                    (receivedAt - previous).TotalSeconds < _duplicateSeconds)
                    return QrCodeInputResult.Rejected("重复二维码已忽略：" + qrCodeContent);

                _recent[qrCodeContent] = receivedAt;
                RemoveExpired(receivedAt);
                return QrCodeInputResult.Completed(qrCodeContent);
            }

            if (char.IsControl(character)) return QrCodeInputResult.None;
            if (_lastCharacterAt != DateTime.MinValue &&
                (receivedAt - _lastCharacterAt).TotalMilliseconds > _inputTimeoutMs)
                _buffer.Clear();
            _lastCharacterAt = receivedAt;
            _buffer.Append(character);
            return QrCodeInputResult.None;

        }

        public void Reset()
        {
            _buffer.Clear();
            _lastCharacterAt = DateTime.MinValue;
        }

        private void RemoveExpired(DateTime now)
        {
            List<string> expired = new List<string>();
            foreach (KeyValuePair<string, DateTime> item in _recent)
                if ((now - item.Value).TotalSeconds >= Math.Max(1, _duplicateSeconds))
                    expired.Add(item.Key);
            foreach (string key in expired) _recent.Remove(key);
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim(' ', '\t', '\r', '\n', '\0', '\u0002', '\u0003');
        }
    }
    //封装二维码扫码结果状态
    public sealed class QrCodeInputResult
    {
        private QrCodeInputResult(bool hasResult, bool accepted, string qrCodeContent, string message)
        {
            HasResult = hasResult;
            Accepted = accepted;
            QrCodeContent = qrCodeContent;
            Message = message;
        }

        public static readonly QrCodeInputResult None =
            new QrCodeInputResult(false, false, null, null);

        public bool HasResult { get; }
        public bool Accepted { get; }
        public string QrCodeContent { get; }
        public string Message { get; }

        public static QrCodeInputResult Completed(string qrCodeContent)
        {
            return new QrCodeInputResult(true, true, qrCodeContent, null);
        }

        public static QrCodeInputResult Rejected(string message)
        {
            return new QrCodeInputResult(true, false, null, message);
        }
    }
}
