using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SiliconSteelAdhesionTester.Forms
{
    public partial class MainForm
    {
        private Panel pnlSamplePreview;
        private Label lblSamplePreviewTitle;
        private Label lblSamplePreviewStatus;
        private PictureBox picCurrentSample;
        private Timer samplePreviewTimer;
        private string _previewSampleId;
        private string _previewImagePath;
        private DateTime _previewImageWriteTime;

        private void InitializeSamplePreview()
        {
            pnlSamplePreview = new Panel
            {
                BackColor = Color.FromArgb(243, 246, 250),
                BorderStyle = BorderStyle.FixedSingle
            };
            lblSamplePreviewTitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 42,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                Padding = new Padding(12, 10, 0, 0),
                Text = "当前样品"
            };
            lblSamplePreviewStatus = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 34,
                ForeColor = Color.DimGray,
                Padding = new Padding(12, 7, 8, 0),
                AutoEllipsis = true,
                Text = "等待任务"
            };
            picCurrentSample = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(28, 36, 46),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            pnlSamplePreview.Controls.Add(picCurrentSample);
            pnlSamplePreview.Controls.Add(lblSamplePreviewStatus);
            pnlSamplePreview.Controls.Add(lblSamplePreviewTitle);
            pnlStationHeader.Controls.Add(pnlSamplePreview);
            pnlSamplePreview.BringToFront();

            samplePreviewTimer = new Timer(components) { Interval = 1000 };
            samplePreviewTimer.Tick += (s, e) => RefreshSamplePreview();
            samplePreviewTimer.Start();
        }

        private void LayoutSamplePreviewAndTaskPanels()
        {
            if (pnlSamplePreview == null || pnlStationHeader.ClientSize.Width <= 0) return;

            const int margin = 12;
            const int gap = 14;
            int availableWidth = pnlStationHeader.ClientSize.Width - margin * 2;
            int previewWidth = Math.Max(300, Math.Min(520, (int)(availableWidth * 0.38)));
            int rightLeft = margin + previewWidth + gap;
            int rightWidth = Math.Max(300, pnlStationHeader.ClientSize.Width - rightLeft - margin);
            int availableHeight = Math.Max(160, pnlStationHeader.ClientSize.Height - margin * 2);

            pnlSamplePreview.Bounds = new Rectangle(margin, margin, previewWidth, availableHeight);

            lblQueueTitle.Dock = DockStyle.None;
            dgvTasks.Dock = DockStyle.None;
            lblLogTitle.Dock = DockStyle.None;
            txtRuntimeLog.Dock = DockStyle.None;

            int queueHeight = Math.Max(150, (availableHeight - lblQueueTitle.Height - lblLogTitle.Height) / 2);
            lblQueueTitle.Bounds = new Rectangle(rightLeft, margin, rightWidth, lblQueueTitle.Height);
            dgvTasks.Bounds = new Rectangle(rightLeft, lblQueueTitle.Bottom, rightWidth, queueHeight);
            lblLogTitle.Bounds = new Rectangle(rightLeft, dgvTasks.Bottom, rightWidth, lblLogTitle.Height);
            txtRuntimeLog.Bounds = new Rectangle(
                rightLeft,
                lblLogTitle.Bottom,
                rightWidth,
                Math.Max(40, margin + availableHeight - lblLogTitle.Bottom));
        }

        private void SetPreviewSample(string sampleId)
        {
            sampleId = string.IsNullOrWhiteSpace(sampleId) ? null : sampleId.Trim();
            if (string.Equals(_previewSampleId, sampleId, StringComparison.OrdinalIgnoreCase)) return;

            _previewSampleId = sampleId;
            _previewImagePath = null;
            _previewImageWriteTime = DateTime.MinValue;
            ReplacePreviewImage(null);
            lblSamplePreviewStatus.Text = sampleId == null
                ? "等待任务"
                : "检验号 " + sampleId + " · 等待样品图片";
            RefreshSamplePreview();
        }

        private void RefreshSamplePreview()
        {
            if (string.IsNullOrWhiteSpace(_previewSampleId) || _settings == null) return;

            try
            {
                string root = Path.GetFullPath(_settings.VisionOutputDirectory);
                if (!Directory.Exists(root)) return;

                string safeSampleId = MakeSafeSampleFileName(_previewSampleId);
                FileInfo newest = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Where(IsSupportedImage)
                    .Where(path => Path.GetFileName(path).IndexOf(safeSampleId, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(file => IsMarkedImage(file.Name))
                    .ThenByDescending(file => file.LastWriteTimeUtc)
                    .FirstOrDefault();

                if (newest == null ||
                    (string.Equals(newest.FullName, _previewImagePath, StringComparison.OrdinalIgnoreCase) &&
                     newest.LastWriteTimeUtc == _previewImageWriteTime))
                    return;

                using (FileStream stream = new FileStream(newest.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image source = Image.FromStream(stream))
                    ReplacePreviewImage(new Bitmap(source));

                _previewImagePath = newest.FullName;
                _previewImageWriteTime = newest.LastWriteTimeUtc;
                lblSamplePreviewStatus.Text = "检验号 " + _previewSampleId + " · " +
                    (IsMarkedImage(newest.Name) ? "检测结果" : "实时样品") + " · " +
                    newest.LastWriteTime.ToString("HH:mm:ss");
            }
            catch (IOException)
            {
                // 相机仍在写文件时，下一个刷新周期自动重试。
            }
            catch (UnauthorizedAccessException)
            {
                lblSamplePreviewStatus.Text = "图片目录无访问权限";
            }
            catch (ArgumentException)
            {
                // 图片文件尚未写完整，下一个刷新周期自动重试。
            }
        }

        private void ReplacePreviewImage(Image image)
        {
            if (picCurrentSample == null)
            {
                image?.Dispose();
                return;
            }
            Image old = picCurrentSample.Image;
            picCurrentSample.Image = image;
            old?.Dispose();
        }

        private static bool IsSupportedImage(string path)
        {
            string extension = Path.GetExtension(path);
            return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".tif", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMarkedImage(string name)
        {
            return name.IndexOf("_marked", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("annotated", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string MakeSafeSampleFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value;
        }
    }
}
