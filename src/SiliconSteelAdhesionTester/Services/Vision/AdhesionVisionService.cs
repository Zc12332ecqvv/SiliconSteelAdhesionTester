using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using OpenCvSharp;
using SiliconSteelAdhesionTester.Configuration;

namespace SiliconSteelAdhesionTester.Services.Vision
{
    /// <summary>
    /// 固定相机、固定光源条件下的硅钢片涂层脱落率检测。
    /// 取向材料比较压弯前后图像；非取向材料统计胶带上的脱落颗粒。
    /// </summary>
    public sealed class AdhesionVisionService : IAdhesionVisionService
    {
        private readonly AppSettings _settings;

        public AdhesionVisionService(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public AdhesionVisionResult AnalyzeOriented(
            string beforeBendingImagePath,
            string afterBendingImagePath,
            Rectangle? inspectionRegion = null,
            string sampleId = null)
        {
            using (Mat before = LoadColor(beforeBendingImagePath))
            using (Mat afterOriginal = LoadColor(afterBendingImagePath))
            using (Mat after = new Mat())
            {
                Cv2.Resize(afterOriginal, after, before.Size());
                Rect roi = ResolveRegion(before.Size(), inspectionRegion);

                using (Mat beforeRoi = new Mat(before, roi))
                using (Mat afterRoi = new Mat(after, roi))
                using (Mat beforeLab = new Mat())
                using (Mat afterLab = new Mat())
                using (Mat difference = new Mat())
                using (Mat mask = new Mat())
                {
                    Cv2.CvtColor(beforeRoi, beforeLab, ColorConversionCodes.BGR2Lab);
                    Cv2.CvtColor(afterRoi, afterLab, ColorConversionCodes.BGR2Lab);
                    Cv2.GaussianBlur(beforeLab, beforeLab, new OpenCvSharp.Size(5, 5), 0);
                    Cv2.GaussianBlur(afterLab, afterLab, new OpenCvSharp.Size(5, 5), 0);
                    Cv2.Absdiff(beforeLab, afterLab, difference);
                    Mat[] channels = Cv2.Split(difference);
                    try
                    {
                        channels[0].CopyTo(mask);
                        Cv2.Max(mask, channels[1], mask);
                        Cv2.Max(mask, channels[2], mask);
                        Cv2.Threshold(mask, mask, _settings.VisionDifferenceThreshold, 255, ThresholdTypes.Binary);
                    }
                    finally
                    {
                        foreach (Mat channel in channels) channel.Dispose();
                    }

                    int particleCount = CleanAndCount(mask);
                    return SaveResult(
                        AdhesionTestType.OrientedBeforeAfter,
                        after,
                        roi,
                        mask,
                        particleCount,
                        _settings.OrientedMaxLossRate,
                        sampleId,
                        "压弯前后图像差异");
                }
            }
        }

        public AdhesionVisionResult AnalyzeNonOrientedTape(
            string tapeImagePath,
            Rectangle? inspectionRegion = null,
            string sampleId = null)
        {
            using (Mat tape = LoadColor(tapeImagePath))
            {
                Rect roi = ResolveRegion(tape.Size(), inspectionRegion);
                using (Mat tapeRoi = new Mat(tape, roi))
                using (Mat gray = new Mat())
                using (Mat background = new Mat())
                using (Mat difference = new Mat())
                using (Mat mask = new Mat())
                using (Mat backgroundKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(51, 51)))
                {
                    Cv2.CvtColor(tapeRoi, gray, ColorConversionCodes.BGR2GRAY);
                    Cv2.GaussianBlur(gray, gray, new OpenCvSharp.Size(5, 5), 0);

                    // 大尺度闭运算填平深色颗粒以估计胶带背景。
                    Cv2.MorphologyEx(gray, background, MorphTypes.Close, backgroundKernel);
                    Cv2.Absdiff(gray, background, difference);
                    Cv2.Threshold(difference, mask, _settings.VisionDifferenceThreshold, 255, ThresholdTypes.Binary);

                    int particleCount = CleanAndCount(mask);
                    return SaveResult(
                        AdhesionTestType.NonOrientedTape,
                        tape,
                        roi,
                        mask,
                        particleCount,
                        _settings.NonOrientedMaxLossRate,
                        sampleId,
                        "胶带脱落颗粒面积");
                }
            }
        }

        private int CleanAndCount(Mat mask)
        {
            using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(3, 3)))
            {
                Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);
                Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);
            }

            Cv2.FindContours(mask.Clone(), out OpenCvSharp.Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            mask.SetTo(Scalar.Black);
            List<OpenCvSharp.Point[]> accepted = new List<OpenCvSharp.Point[]>();
            foreach (OpenCvSharp.Point[] contour in contours)
            {
                if (Cv2.ContourArea(contour) >= _settings.VisionMinimumParticleArea)
                    accepted.Add(contour);
            }

            if (accepted.Count > 0)
                Cv2.DrawContours(mask, accepted, -1, Scalar.White, -1);
            return accepted.Count;
        }

        private AdhesionVisionResult SaveResult(
            AdhesionTestType testType,
            Mat source,
            Rect roi,
            Mat roiMask,
            int particleCount,
            double maximumLossRate,
            string sampleId,
            string method)
        {
            int defectPixels = Cv2.CountNonZero(roiMask);
            int inspectionPixels = roi.Width * roi.Height;
            double lossRate = inspectionPixels == 0 ? 0 : defectPixels * 100.0 / inspectionPixels;
            bool qualified = lossRate <= maximumLossRate;

            string safeId = MakeSafeFileName(string.IsNullOrWhiteSpace(sampleId)
                ? DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture)
                : sampleId);
            string outputDirectory = Path.GetFullPath(_settings.VisionOutputDirectory);
            string prefix = testType == AdhesionTestType.OrientedBeforeAfter ? "oriented" : "nonoriented_tape";
            string categoryDirectory = Path.Combine(
                outputDirectory,
                testType == AdhesionTestType.OrientedBeforeAfter ? "oriented" : "non-oriented");
            string maskDirectory = Path.Combine(categoryDirectory, "mask");
            string markedDirectory = Path.Combine(categoryDirectory, "marked");
            Directory.CreateDirectory(maskDirectory);
            Directory.CreateDirectory(markedDirectory);
            string maskPath = Path.Combine(maskDirectory, prefix + "_" + safeId + "_mask.png");
            string annotatedPath = Path.Combine(markedDirectory, prefix + "_" + safeId + "_marked.jpg");

            using (Mat fullMask = Mat.Zeros(source.Size(), MatType.CV_8UC1).ToMat())
            using (Mat target = new Mat(fullMask, roi))
            using (Mat annotated = source.Clone())
            {
                roiMask.CopyTo(target);
                Cv2.FindContours(fullMask.Clone(), out OpenCvSharp.Point[][] contours, out HierarchyIndex[] hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                Cv2.DrawContours(annotated, contours, -1, Scalar.Red, 2);
                Cv2.Rectangle(annotated, roi, qualified ? Scalar.LimeGreen : Scalar.Red, 2);
                Cv2.PutText(
                    annotated,
                    "Loss: " + lossRate.ToString("F3", CultureInfo.InvariantCulture) + "%  " + (qualified ? "OK" : "NG"),
                    new OpenCvSharp.Point(roi.X + 10, Math.Max(30, roi.Y + 30)),
                    HersheyFonts.HersheySimplex,
                    0.8,
                    qualified ? Scalar.LimeGreen : Scalar.Red,
                    2);
                Cv2.ImWrite(maskPath, fullMask);
                Cv2.ImWrite(annotatedPath, annotated);
            }

            return new AdhesionVisionResult
            {
                TestType = testType,
                LossRatePercent = lossRate,
                IsQualified = qualified,
                DefectPixelCount = defectPixels,
                InspectionPixelCount = inspectionPixels,
                ParticleCount = particleCount,
                MaskImagePath = maskPath,
                AnnotatedImagePath = annotatedPath,
                Message = method + "脱落率 " + lossRate.ToString("F3", CultureInfo.InvariantCulture) +
                          "%，判定 " + (qualified ? "合格" : "不合格")
            };
        }

        private static Mat LoadColor(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("检测图片不存在。", path);
            Mat image = Cv2.ImRead(path, ImreadModes.Color);
            if (image.Empty())
            {
                image.Dispose();
                throw new InvalidDataException("OpenCV 无法读取图片：" + path);
            }
            return image;
        }

        private static Rect ResolveRegion(OpenCvSharp.Size imageSize, Rectangle? requested)
        {
            if (!requested.HasValue)
            {
                int marginX = Math.Max(1, imageSize.Width / 20);
                int marginY = Math.Max(1, imageSize.Height / 20);
                return new Rect(marginX, marginY, imageSize.Width - marginX * 2, imageSize.Height - marginY * 2);
            }

            Rectangle value = requested.Value;
            int x = Math.Max(0, value.X);
            int y = Math.Max(0, value.Y);
            int right = Math.Min(imageSize.Width, value.Right);
            int bottom = Math.Min(imageSize.Height, value.Bottom);
            if (right <= x || bottom <= y)
                throw new ArgumentOutOfRangeException(nameof(requested), "检测区域不在图片范围内。");
            return new Rect(x, y, right - x, bottom - y);
        }

        private static string MakeSafeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value;
        }
    }
}
