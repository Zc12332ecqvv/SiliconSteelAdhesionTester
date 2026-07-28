using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MvCamCtrl.NET;
using SiliconSteelAdhesionTester.Configuration;

namespace SiliconSteelAdhesionTester.Services.Vision
{
    public sealed class MvsImageAcquisitionService : IImageAcquisitionService
    {
        private readonly AppSettings _settings;
        private readonly SemaphoreSlim _captureLock = new SemaphoreSlim(1, 1);

        public MvsImageAcquisitionService(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<string> AcquireAsync(CaptureStage stage, string qrCodeContent, CancellationToken cancellationToken)
        {
            await _captureLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await Task.Run(
                    () => Capture(stage, qrCodeContent, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _captureLock.Release();
            }
        }

        private string Capture(CaptureStage stage, string qrCodeContent, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureMvsRuntimePath();
            MyCamera.MV_CC_DEVICE_INFO_LIST devices = new MyCamera.MV_CC_DEVICE_INFO_LIST();
            Check(MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE, ref devices), "枚举GigE相机");
            if (devices.nDeviceNum == 0)
                throw new InvalidOperationException("MVS没有发现GigE相机，请检查相机供电、网线和网卡IP。");

            string cameraIp = stage == CaptureStage.NonOrientedTape
                ? _settings.NonOrientedCameraIp
                : _settings.OrientedCameraIp;
            if (string.IsNullOrWhiteSpace(cameraIp))
                cameraIp = _settings.CameraIp;
            MyCamera.MV_CC_DEVICE_INFO selected = FindCamera(devices, cameraIp);
            MyCamera camera = new MyCamera();
            bool opened = false;
            bool grabbing = false;
            try
            {
                Check(camera.MV_CC_CreateDevice_NET(ref selected), "创建相机");
                Check(camera.MV_CC_OpenDevice_NET(), "打开相机");
                opened = true;
                int packetSize = camera.MV_CC_GetOptimalPacketSize_NET();
                if (packetSize > 0)
                    camera.MV_CC_SetIntValueEx_NET("GevSCPSPacketSize", packetSize);
                Check(camera.MV_CC_SetEnumValueByString_NET("TriggerMode", "On"), "设置触发模式");
                Check(camera.MV_CC_SetEnumValueByString_NET("TriggerSource", "Software"), "设置软件触发");
                Check(camera.MV_CC_SetGrabStrategy_NET(MyCamera.MV_GRAB_STRATEGY.MV_GrabStrategy_UpcomingImage), "设置取图策略");
                Check(camera.MV_CC_StartGrabbing_NET(), "开始取图");
                grabbing = true;
                cancellationToken.ThrowIfCancellationRequested();
                Check(camera.MV_CC_SetCommandValue_NET("TriggerSoftware"), "发送软件触发");

                MyCamera.MV_FRAME_OUT frame = new MyCamera.MV_FRAME_OUT();
                Check(camera.MV_CC_GetImageBuffer_NET(ref frame, Math.Max(500, _settings.CameraCaptureTimeoutMs)), "等待相机图像");
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string outputPath = BuildOutputPath(stage, qrCodeContent);
                    MyCamera.MV_SAVE_IMG_TO_FILE_PARAM save = new MyCamera.MV_SAVE_IMG_TO_FILE_PARAM
                    {
                        nWidth = frame.stFrameInfo.nWidth,
                        nHeight = frame.stFrameInfo.nHeight,
                        enPixelType = frame.stFrameInfo.enPixelType,
                        pData = frame.pBufAddr,
                        nDataLen = frame.stFrameInfo.nFrameLen,
                        enImageType = MyCamera.MV_SAVE_IAMGE_TYPE.MV_Image_Bmp,
                        iMethodValue = 2,
                        pImagePath = outputPath
                    };
                    Check(camera.MV_CC_SaveImageToFile_NET(ref save), "保存相机原图");
                    return outputPath;
                }
                finally
                {
                    camera.MV_CC_FreeImageBuffer_NET(ref frame);
                }
            }
            finally
            {
                if (grabbing) camera.MV_CC_StopGrabbing_NET();
                if (opened) camera.MV_CC_CloseDevice_NET();
                camera.MV_CC_DestroyDevice_NET();
            }
        }

        private static MyCamera.MV_CC_DEVICE_INFO FindCamera(MyCamera.MV_CC_DEVICE_INFO_LIST devices, string configuredIp)
        {
            uint wantedIp = 0;
            bool selectByIp = !string.IsNullOrWhiteSpace(configuredIp);
            if (selectByIp)
            {
                IPAddress address;
                if (!IPAddress.TryParse(configuredIp.Trim(), out address))
                    throw new InvalidOperationException("相机IP格式不正确：" + configuredIp);
                byte[] bytes = address.GetAddressBytes();
                wantedIp = (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
            }
            for (int index = 0; index < devices.nDeviceNum; index++)
            {
                MyCamera.MV_CC_DEVICE_INFO device = (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(
                    devices.pDeviceInfo[index], typeof(MyCamera.MV_CC_DEVICE_INFO));
                if (!selectByIp) return device;
                MyCamera.MV_GIGE_DEVICE_INFO_EX info = (MyCamera.MV_GIGE_DEVICE_INFO_EX)MyCamera.ByteToStruct(
                    device.SpecialInfo.stGigEInfo, typeof(MyCamera.MV_GIGE_DEVICE_INFO_EX));
                if (info.nCurrentIp == wantedIp) return device;
            }
            throw new InvalidOperationException(
                "MVS未发现配置的相机 " + configuredIp + "，请用MVS客户端确认相机IP和电脑网卡网段。");
        }

        private string BuildOutputPath(CaptureStage stage, string qrCodeContent)
        {
            string directory = Path.Combine(
                Path.GetFullPath(_settings.VisionOutputDirectory), "raw",
                DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture), RelativeStageDirectory(stage));
            Directory.CreateDirectory(directory);
            string safeCode = SafeName(string.IsNullOrWhiteSpace(qrCodeContent) ? "UNKNOWN" : qrCodeContent);
            return Path.Combine(directory,
                DateTime.Now.ToString("HHmmss_fff", CultureInfo.InvariantCulture) + "_" + safeCode + ".bmp");
        }

        private static string RelativeStageDirectory(CaptureStage stage)
        {
            switch (stage)
            {
                case CaptureStage.OrientedBefore: return Path.Combine("oriented", "before");
                case CaptureStage.OrientedAfter: return Path.Combine("oriented", "after");
                default: return Path.Combine("non-oriented", "tape");
            }
        }

        private static string SafeName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value;
        }

        private static void Check(int result, string action)
        {
            if (result != MyCamera.MV_OK)
                throw new InvalidOperationException(action + "失败，MVS错误码：0x" + result.ToString("X8"));
        }

        private static void EnsureMvsRuntimePath()
        {
            string runtime = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Common Files", "MVS", "Runtime", "Win64_x64");
            if (!Directory.Exists(runtime)) return;
            string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (currentPath.IndexOf(runtime, StringComparison.OrdinalIgnoreCase) < 0)
                Environment.SetEnvironmentVariable("PATH", runtime + ";" + currentPath);
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GENICAM_GENTL64_PATH")))
                Environment.SetEnvironmentVariable("GENICAM_GENTL64_PATH", runtime);
        }
    }
}
