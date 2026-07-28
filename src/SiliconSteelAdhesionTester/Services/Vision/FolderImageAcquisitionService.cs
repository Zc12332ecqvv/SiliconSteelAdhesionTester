using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SiliconSteelAdhesionTester.Configuration;

namespace SiliconSteelAdhesionTester.Services.Vision
{
    public enum CaptureStage
    {
        OrientedBefore,
        OrientedAfter,
        NonOrientedTape
    }

    public interface IImageAcquisitionService
    {
        Task<string> AcquireAsync(CaptureStage stage, string qrCodeContent, CancellationToken cancellationToken);
    }

    public sealed class FolderImageAcquisitionService : IImageAcquisitionService
    {
        private static readonly string[] Extensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };
        private readonly AppSettings _settings;

        public FolderImageAcquisitionService(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<string> AcquireAsync(CaptureStage stage, string qrCodeContent, CancellationToken cancellationToken)
        {
            string inputDirectory = Path.Combine(Path.GetFullPath(_settings.CameraInputDirectory), RelativeStageDirectory(stage));
            Directory.CreateDirectory(inputDirectory);
            DateTime requestedAt = DateTime.Now;
            WriteTriggerRequest(stage, qrCodeContent, requestedAt);

            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(Math.Max(500, _settings.CameraCaptureTimeoutMs));
                while (true)
                {
                    timeout.Token.ThrowIfCancellationRequested();
                    FileInfo candidate = new DirectoryInfo(inputDirectory)
                        .EnumerateFiles()
                        .Where(f => Extensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase))
                        .Where(f => f.LastWriteTime >= requestedAt)
                        .OrderByDescending(f => f.LastWriteTime)
                        .FirstOrDefault();
                    if (candidate != null && await IsStableAsync(candidate, timeout.Token).ConfigureAwait(false))
                        return Archive(candidate.FullName, stage, qrCodeContent);
                    await Task.Delay(100, timeout.Token).ConfigureAwait(false);
                }
            }
        }

        private async Task<bool> IsStableAsync(FileInfo file, CancellationToken cancellationToken)
        {
            long length = file.Length;
            await Task.Delay(Math.Max(50, _settings.CameraFileStableMs), cancellationToken).ConfigureAwait(false);
            file.Refresh();
            return file.Exists && file.Length > 0 && file.Length == length;
        }

        private string Archive(string source, CaptureStage stage, string qrCodeContent)
        {
            string directory = Path.Combine(
                Path.GetFullPath(_settings.VisionOutputDirectory),
                "raw",
                DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                RelativeStageDirectory(stage));
            Directory.CreateDirectory(directory);
            string safeCode = SafeName(string.IsNullOrWhiteSpace(qrCodeContent) ? "UNKNOWN" : qrCodeContent);
            string target = Path.Combine(directory,
                DateTime.Now.ToString("HHmmss_fff", CultureInfo.InvariantCulture) + "_" + safeCode + Path.GetExtension(source));
            File.Copy(source, target, false);
            return target;
        }

        private void WriteTriggerRequest(CaptureStage stage, string qrCodeContent, DateTime requestedAt)
        {
            string directory = Path.Combine(Path.GetFullPath(_settings.CameraInputDirectory), "requests");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, requestedAt.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + "_" + stage + ".trigger");
            File.WriteAllText(path, "Stage=" + stage + Environment.NewLine + "QrCodeContent=" + (qrCodeContent ?? string.Empty), System.Text.Encoding.UTF8);
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
    }
}
