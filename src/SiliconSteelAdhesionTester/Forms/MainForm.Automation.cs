using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SiliconSteelAdhesionTester.Models;
using SiliconSteelAdhesionTester.Services.Plc;
using SiliconSteelAdhesionTester.Services.Vision;

namespace SiliconSteelAdhesionTester.Forms
{
    public partial class MainForm
    {
        private bool _s2ScanRequestRunning;
        private bool _s3ScanRequestRunning;
        private bool _s2FirstPhotoAttempted;
        private bool _s2FirstPhotoDoneActive;
        private bool _s2SecondPhotoAttempted;
        private bool _s2SecondPhotoRequestRunning;
        private bool _s2SecondPhotoResponseActive;
        private bool _s4PhotoRequestRunning;
        private bool _s4PhotoResponseActive;
        private string _orientedBeforeImagePath;
        private readonly Dictionary<string, DateTime> _recentAutomaticQrCodes =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<string> _orientedQrCodeQueue = new Queue<string>();
        private readonly Queue<string> _nonOrientedQrCodeQueue = new Queue<string>();

        private void UpdateAutomaticInteractions(PlcSnapshot snapshot)
        {
            if (!_settings.AutomaticDeviceInteractionsEnabled) return;

            if (_settings.QrCodeScannerEnabled)
            {
                if (snapshot.S2ScanAllowed && !_s2ScanResponseActive && !_s2ScanRequestRunning)
                    _ = HandleAutomaticQrCodeAsync(true);
                if (snapshot.S3ScanAllowed && !_s3ScanResponseActive && !_s3ScanRequestRunning)
                    _ = HandleAutomaticQrCodeAsync(false);
            }

            if (snapshot.S2FirstPhotoAllowed && !_s2FirstPhotoAttempted)
                _ = HandleS2FirstPhotoAsync();
            if (!snapshot.S2FirstPhotoAllowed)
            {
                _s2FirstPhotoAttempted = false;
                if (_s2FirstPhotoDoneActive)
                {
                    _s2FirstPhotoDoneActive = false;
                    _ = ResetSingleResponseAsync(PlcAddresses.S2FirstPhotoDone, "S2第一次拍照");
                }
            }

            if (snapshot.S2SecondPhotoAllowed &&
                !_s2SecondPhotoAttempted &&
                !_s2SecondPhotoResponseActive &&
                !_s2SecondPhotoRequestRunning)
                _ = HandleS2SecondPhotoAsync();
            if (!snapshot.S2SecondPhotoAllowed)
            {
                _s2SecondPhotoAttempted = false;
                if (_s2SecondPhotoResponseActive)
                {
                    _s2SecondPhotoResponseActive = false;
                    _ = ResetResultResponseAsync(
                        PlcAddresses.S2SecondPhotoDone,
                        PlcAddresses.S2SecondPhotoOk,
                        PlcAddresses.S2SecondPhotoNg,
                        "S2第二次拍照");
                }
            }

            if (snapshot.S4PhotoAllowed && !_s4PhotoResponseActive && !_s4PhotoRequestRunning)
                _ = HandleS4PhotoAsync();
            if (!snapshot.S4PhotoAllowed && _s4PhotoResponseActive)
            {
                _s4PhotoResponseActive = false;
                _ = ResetResultResponseAsync(
                    PlcAddresses.S4CameraDone,
                    PlcAddresses.S4CameraOk,
                    PlcAddresses.S4CameraNg,
                    "S4拍照");
            }
        }

        private async Task HandleAutomaticQrCodeAsync(bool oriented)
        {
            if (oriented) _s2ScanRequestRunning = true;
            else _s3ScanRequestRunning = true;
            string station = oriented ? "S2" : "S3";
            string material = oriented ? "取向" : "无取向";
            try
            {
                AppendRuntimeLog("[QR] " + station + "正在连接SR-1000读取二维码");
                string qrCodeContent = await _tcpQrCodeReader.ReadAsync(oriented, _shutdown.Token);
                if (!IsQrPermissionActive(oriented)) return;
                bool responseAlreadySent = oriented ? _s2ScanResponseActive : _s3ScanResponseActive;
                if (responseAlreadySent) return;
                if (IsAutomaticDuplicate(qrCodeContent))
                    throw new InvalidDataException("重复二维码已拒绝：" + qrCodeContent);

                RegisterQrCode(qrCodeContent, oriented);
                await SendScanResponseAsync(oriented, true);
            }
            catch (OperationCanceledException) when (!_shutdown.IsCancellationRequested)
            {
                await RejectAutomaticQrCodeAsync(oriented, station, material, "二维码读取超时");
            }
            catch (Exception ex)
            {
                await RejectAutomaticQrCodeAsync(oriented, station, material, ex.Message);
            }
            finally
            {
                if (oriented) _s2ScanRequestRunning = false;
                else _s3ScanRequestRunning = false;
            }
        }

        private async Task RejectAutomaticQrCodeAsync(bool oriented, string station, string material, string message)
        {
            if (!IsQrPermissionActive(oriented)) return;
            if (oriented ? _s2ScanResponseActive : _s3ScanResponseActive) return;
            AppendRuntimeLog("[QR] " + station + "读取失败：" + message);
            _database.SaveQrCodeScanEvent(null, material, station, false, message, _user.UserName);
            await SendScanResponseAsync(oriented, false);
        }

        private bool IsAutomaticDuplicate(string qrCodeContent)
        {
            DateTime now = DateTime.Now;
            DateTime previous;
            if (_settings.DuplicateQrCodeSeconds > 0 &&
                _recentAutomaticQrCodes.TryGetValue(qrCodeContent, out previous) &&
                (now - previous).TotalSeconds < _settings.DuplicateQrCodeSeconds)
                return true;
            _recentAutomaticQrCodes[qrCodeContent] = now;
            List<string> expired = new List<string>();
            foreach (KeyValuePair<string, DateTime> item in _recentAutomaticQrCodes)
                if ((now - item.Value).TotalSeconds >= Math.Max(1, _settings.DuplicateQrCodeSeconds))
                    expired.Add(item.Key);
            foreach (string key in expired) _recentAutomaticQrCodes.Remove(key);
            return false;
        }

        private async Task HandleS2FirstPhotoAsync()
        {
            _s2FirstPhotoAttempted = true;
            _orientedBeforeImagePath = null;
            try
            {
                string qrCode = RequireQrCode(true);
                AppendRuntimeLog("[CAMERA] S2第一次拍照请求，等待压弯前图片");
                string image = await _imageAcquisition.AcquireAsync(CaptureStage.OrientedBefore, qrCode, _shutdown.Token);
                if (_latestSnapshot == null || !_latestSnapshot.S2FirstPhotoAllowed) return;
                _orientedBeforeImagePath = image;
                _database.SaveCaptureImage(qrCode, "S2", CaptureStage.OrientedBefore.ToString(), image, _user.UserName);
                await _plc.WriteAsync(PlcAddresses.S2FirstPhotoDone, true, _shutdown.Token);
                _s2FirstPhotoDoneActive = true;
                AppendRuntimeLog("[PLC] 已返回S2第一次拍照完成");
            }
            catch (OperationCanceledException) when (!_shutdown.IsCancellationRequested)
            {
                LogAutomationFault("CAMERA_TIMEOUT", "S2第一次拍照", "等待压弯前图片超时；未返回完成信号");
            }
            catch (Exception ex)
            {
                LogAutomationFault("CAMERA_FIRST", "S2第一次拍照", ex.Message);
            }
        }

        private async Task HandleS2SecondPhotoAsync()
        {
            _s2SecondPhotoAttempted = true;
            _s2SecondPhotoRequestRunning = true;
            try
            {
                string qrCode = RequireQrCode(true);
                if (string.IsNullOrWhiteSpace(_orientedBeforeImagePath) || !File.Exists(_orientedBeforeImagePath))
                    throw new InvalidOperationException("缺少本周期第一次拍照图片，不能执行取向压弯前后对比。");
                AppendRuntimeLog("[CAMERA] S2第二次拍照请求，等待压弯后图片");
                string after = await _imageAcquisition.AcquireAsync(CaptureStage.OrientedAfter, qrCode, _shutdown.Token);
                if (_latestSnapshot == null || !_latestSnapshot.S2SecondPhotoAllowed) return;
                _database.SaveCaptureImage(qrCode, "S2", CaptureStage.OrientedAfter.ToString(), after, _user.UserName);
                AdhesionVisionResult result = _automaticVision.AnalyzeOriented(_orientedBeforeImagePath, after, null, qrCode);
                _database.SaveVisionResult(qrCode, after, result, _user.UserName);
                SetPreviewSample(qrCode);
                await SendResultResponseAsync(
                    PlcAddresses.S2SecondPhotoDone,
                    PlcAddresses.S2SecondPhotoOk,
                    PlcAddresses.S2SecondPhotoNg,
                    result.IsQualified,
                    "S2第二次拍照");
                _s2SecondPhotoResponseActive = true;
                AppendRuntimeLog("[VISION] S2 " + result.Message);
            }
            catch (OperationCanceledException) when (!_shutdown.IsCancellationRequested)
            {
                LogAutomationFault(
                    "CAMERA_TIMEOUT",
                    "S2第二次拍照",
                    "等待压弯后图片超时；未返回完成/NG信号，等待故障复位");
            }
            catch (Exception ex)
            {
                LogAutomationFault(
                    "CAMERA_SECOND",
                    "S2第二次拍照",
                    ex.Message + "；未返回完成/NG信号，等待故障复位");
            }
            finally { _s2SecondPhotoRequestRunning = false; }
        }

        private async Task HandleS4PhotoAsync()
        {
            _s4PhotoRequestRunning = true;
            try
            {
                string qrCode = RequireQrCode(false);
                AppendRuntimeLog("[CAMERA] S4拍照请求，等待无取向胶带图片");
                string tape = await _imageAcquisition.AcquireAsync(CaptureStage.NonOrientedTape, qrCode, _shutdown.Token);
                if (_latestSnapshot == null || !_latestSnapshot.S4PhotoAllowed) return;
                _database.SaveCaptureImage(qrCode, "S4", CaptureStage.NonOrientedTape.ToString(), tape, _user.UserName);
                AdhesionVisionResult result = _automaticVision.AnalyzeNonOrientedTape(tape, null, qrCode);
                _database.SaveVisionResult(qrCode, tape, result, _user.UserName);
                SetPreviewSample(qrCode);
                await SendResultResponseAsync(
                    PlcAddresses.S4CameraDone,
                    PlcAddresses.S4CameraOk,
                    PlcAddresses.S4CameraNg,
                    result.IsQualified,
                    "S4拍照");
                _s4PhotoResponseActive = true;
                AppendRuntimeLog("[VISION] S4 " + result.Message);
            }
            catch (OperationCanceledException) when (!_shutdown.IsCancellationRequested)
            {
                await RejectPhotoAsync(false, "S4拍照", "等待无取向胶带图片超时");
            }
            catch (Exception ex)
            {
                await RejectPhotoAsync(false, "S4拍照", ex.Message);
            }
            finally { _s4PhotoRequestRunning = false; }
        }

        private async Task RejectPhotoAsync(bool s2, string node, string message)
        {
            if (_latestSnapshot == null ||
                (s2 && !_latestSnapshot.S2SecondPhotoAllowed) ||
                (!s2 && !_latestSnapshot.S4PhotoAllowed))
                return;
            LogAutomationFault("VISION_NG", node, message);
            await SendResultResponseAsync(
                s2 ? PlcAddresses.S2SecondPhotoDone : PlcAddresses.S4CameraDone,
                s2 ? PlcAddresses.S2SecondPhotoOk : PlcAddresses.S4CameraOk,
                s2 ? PlcAddresses.S2SecondPhotoNg : PlcAddresses.S4CameraNg,
                false,
                node);
            if (s2) _s2SecondPhotoResponseActive = true;
            else _s4PhotoResponseActive = true;
        }

        private async Task SendResultResponseAsync(string done, string ok, string ng, bool accepted, string node)
        {
            await _plc.WriteAsync(ok, accepted, _shutdown.Token);
            await _plc.WriteAsync(ng, !accepted, _shutdown.Token);
            await _plc.WriteAsync(done, true, _shutdown.Token);
            AppendRuntimeLog("[PLC] 已返回" + node + "完成/" + (accepted ? "OK" : "NG"));
        }

        private async Task ResetResultResponseAsync(string done, string ok, string ng, string node)
        {
            try
            {
                await _plc.WriteAsync(done, false, _shutdown.Token);
                await _plc.WriteAsync(ok, false, _shutdown.Token);
                await _plc.WriteAsync(ng, false, _shutdown.Token);
                if (node == "S2第二次拍照")
                {
                    _orientedBeforeImagePath = null;
                    _s2FirstPhotoAttempted = false;
                    CompleteQrCode(true);
                }
                else if (node == "S4拍照") CompleteQrCode(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { AppendRuntimeLog("[PLC] 复位" + node + "应答失败：" + ex.Message); }
        }

        private async Task ResetSingleResponseAsync(string done, string node)
        {
            try { await _plc.WriteAsync(done, false, _shutdown.Token); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { AppendRuntimeLog("[PLC] 复位" + node + "应答失败：" + ex.Message); }
        }

        private string RequireQrCode(bool oriented)
        {
            Queue<string> queue = oriented ? _orientedQrCodeQueue : _nonOrientedQrCodeQueue;
            if (queue.Count == 0)
                throw new InvalidOperationException((oriented ? "S2" : "S4") + "没有关联到有效二维码，禁止生成无追溯检测记录。");
            return queue.Peek();
        }

        private void LogAutomationFault(string code, string node, string message)
        {
            AppendRuntimeLog("[" + code + "] " + node + "：" + message);
            _database.LogFault(code, node, message, _user.UserName);
        }

        private bool IsQrPermissionActive(bool oriented)
        {
            return _latestSnapshot != null &&
                   (oriented ? _latestSnapshot.S2ScanAllowed : _latestSnapshot.S3ScanAllowed);
        }

        private void EnqueueQrCode(string qrCodeContent, bool oriented)
        {
            Queue<string> queue = oriented ? _orientedQrCodeQueue : _nonOrientedQrCodeQueue;
            if (queue.Count == 0 || !string.Equals(queue.ToArray()[queue.Count - 1], qrCodeContent, StringComparison.OrdinalIgnoreCase))
                queue.Enqueue(qrCodeContent);
        }

        private void CompleteQrCode(bool oriented)
        {
            Queue<string> queue = oriented ? _orientedQrCodeQueue : _nonOrientedQrCodeQueue;
            if (queue.Count > 0) queue.Dequeue();
        }
    }
}
