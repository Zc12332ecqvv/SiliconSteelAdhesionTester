using System;
using System.Collections.Generic;
using System.Text;

namespace SiliconSteelAdhesionTester.Services.Scanner
{
    /// <summary>
    /// 接收配置为 USB-HID 键盘模式、并以回车结尾的工业扫码枪输入。
    /// 字符间隔超时后自动丢弃残留，避免把人工键盘输入拼成条码。
    /// </summary>
    public sealed class KeyboardBarcodeScanner
    {
        private readonly StringBuilder _buffer = new StringBuilder();
        private readonly Dictionary<string, DateTime> _recent =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly int _inputTimeoutMs;
        private readonly int _minimumLength;
        private readonly int _duplicateSeconds;
        private DateTime _lastCharacterAt = DateTime.MinValue;

        public KeyboardBarcodeScanner(int inputTimeoutMs, int minimumLength, int duplicateSeconds)
        {
            _inputTimeoutMs = Math.Max(30, inputTimeoutMs);
            _minimumLength = Math.Max(1, minimumLength);
            _duplicateSeconds = Math.Max(0, duplicateSeconds);
        }

        public BarcodeInputResult Accept(char character, DateTime receivedAt)
        {
            if (character == '\r' || character == '\n' || character == '\t')
            {
                if (_buffer.Length == 0) return BarcodeInputResult.None;
                string barcode = Normalize(_buffer.ToString());
                Reset();
                if (barcode.Length < _minimumLength)
                    return BarcodeInputResult.Rejected("二维码长度不足");
                if (barcode.Length > 256)
                    return BarcodeInputResult.Rejected("二维码长度超过256字符");

                if (_recent.TryGetValue(barcode, out DateTime previous) &&
                    (receivedAt - previous).TotalSeconds < _duplicateSeconds)
                    return BarcodeInputResult.Rejected("重复扫码已忽略：" + barcode);

                _recent[barcode] = receivedAt;
                RemoveExpired(receivedAt);
                return BarcodeInputResult.Completed(barcode);
            }

            if (char.IsControl(character)) return BarcodeInputResult.None;
            if (_lastCharacterAt != DateTime.MinValue &&
                (receivedAt - _lastCharacterAt).TotalMilliseconds > _inputTimeoutMs)
                _buffer.Clear();
            _lastCharacterAt = receivedAt;
            _buffer.Append(character);
            return BarcodeInputResult.None;

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

    public sealed class BarcodeInputResult
    {
        private BarcodeInputResult(bool hasResult, bool accepted, string barcode, string message)
        {
            HasResult = hasResult;
            Accepted = accepted;
            Barcode = barcode;
            Message = message;
        }

        public static readonly BarcodeInputResult None =
            new BarcodeInputResult(false, false, null, null);

        public bool HasResult { get; }
        public bool Accepted { get; }
        public string Barcode { get; }
        public string Message { get; }

        public static BarcodeInputResult Completed(string barcode)
        {
            return new BarcodeInputResult(true, true, barcode, null);
        }

        public static BarcodeInputResult Rejected(string message)
        {
            return new BarcodeInputResult(true, false, null, message);
        }
    }
}
