using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            lblSamplePreviewTitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 38,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(27, 52, 76),
                BackColor = Color.FromArgb(240, 245, 250),
                Padding = new Padding(14, 8, 0, 0),
                Text = "当前试样 · 长条图像预览"
            };
            lblSamplePreviewStatus = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                ForeColor = Color.DimGray,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(14, 5, 8, 0),
                AutoEllipsis = true,
                Text = "等待任务"
            };
            picCurrentSample = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 34, 45),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            picCurrentSample.Paint += DrawSamplePlaceholder;
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
            const int gap = 12;
            int availableWidth = pnlStationHeader.ClientSize.Width - margin * 2;
            int availableHeight = Math.Max(160, pnlStationHeader.ClientSize.Height - margin * 2);
            int previewHeight = Math.Max(150, Math.Min(230, (int)(availableHeight * 0.42)));
            int lowerTop = margin + previewHeight + gap;
            int lowerHeight = Math.Max(100, margin + availableHeight - lowerTop);
            int queueWidth = Math.Max(300, (int)((availableWidth - gap) * 0.43));
            int logLeft = margin + queueWidth + gap;
            int logWidth = Math.Max(300, margin + availableWidth - logLeft);

            pnlSamplePreview.Bounds = new Rectangle(margin, margin, availableWidth, previewHeight);

            lblQueueTitle.Dock = DockStyle.None;
            dgvTasks.Dock = DockStyle.None;
            lblLogTitle.Dock = DockStyle.None;
            txtRuntimeLog.Dock = DockStyle.None;

            lblQueueTitle.Bounds = new Rectangle(margin, lowerTop, queueWidth, lblQueueTitle.Height);
            dgvTasks.Bounds = new Rectangle(
                margin,
                lblQueueTitle.Bottom,
                queueWidth,
                Math.Max(40, lowerHeight - lblQueueTitle.Height));
            lblLogTitle.Bounds = new Rectangle(logLeft, lowerTop, logWidth, lblLogTitle.Height);
            txtRuntimeLog.Bounds = new Rectangle(
                logLeft,
                lblLogTitle.Bottom,
                logWidth,
                Math.Max(40, lowerHeight - lblLogTitle.Height));
        }

        private void DrawSamplePlaceholder(object sender, PaintEventArgs e)
        {
            if (picCurrentSample.Image != null) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int stripWidth = Math.Max(180, Math.Min(picCurrentSample.ClientSize.Width - 100, 820));
            int stripHeight = Math.Max(34, Math.Min(68, stripWidth / 9));
            Rectangle strip = new Rectangle(
                (picCurrentSample.ClientSize.Width - stripWidth) / 2,
                (picCurrentSample.ClientSize.Height - stripHeight) / 2 - 8,
                stripWidth,
                stripHeight);

            using (GraphicsPath path = RoundedRectangle(strip, 8))
            using (LinearGradientBrush brush = new LinearGradientBrush(
                strip,
                Color.FromArgb(175, 188, 200),
                Color.FromArgb(104, 121, 137),
                LinearGradientMode.Vertical))
            using (Pen border = new Pen(Color.FromArgb(210, 220, 229), 1.5F))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
            }

            string text = string.IsNullOrWhiteSpace(_previewSampleId)
                ? "等待长条试样图像"
                : "检验号 " + _previewSampleId + " · 等待图像";
            using (Font font = new Font("Microsoft YaHei UI", 9F))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(190, 202, 213)))
            {
                SizeF size = e.Graphics.MeasureString(text, font);
                e.Graphics.DrawString(
                    text,
                    font,
                    textBrush,
                    (picCurrentSample.ClientSize.Width - size.Width) / 2,
                    strip.Bottom + 10);
            }
        }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            Rectangle arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.X;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
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
            picCurrentSample.Invalidate();
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
