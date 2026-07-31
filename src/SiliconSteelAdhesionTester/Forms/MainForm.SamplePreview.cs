using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SiliconSteelAdhesionTester.Services.Vision;

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
        private Panel pnlCurrentResult;
        private PictureBox picCurrentResult;
        private Label lblCurrentResultId;
        private Label lblCurrentResultType;
        private Label lblCurrentResultValue;
        private Label lblCurrentResultDecision;
        private Label lblCurrentResultTime;
        private Label lblBatchListTitle;

        private void InitializeSamplePreview()
        {
            // 主监控页只展示任务与运行状态；检测图片统一放在“视觉检测”页面。
            dgvTasks.EnableHeadersVisualStyles = false;
            dgvTasks.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvTasks.ColumnHeadersHeight = 46;
            dgvTasks.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(222, 232, 242);
            dgvTasks.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(31, 52, 72);
            dgvTasks.ColumnHeadersDefaultCellStyle.Font =
                new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            dgvTasks.ColumnHeadersDefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleLeft;
            dgvTasks.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 6, 0);
            dgvTasks.RowTemplate.Height = 42;
            dgvTasks.DefaultCellStyle.Font = new Font("Microsoft YaHei UI", 10F);
            dgvTasks.DefaultCellStyle.Padding = new Padding(10, 4, 6, 4);
            dgvTasks.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 238, 252);
            dgvTasks.DefaultCellStyle.SelectionForeColor = Color.FromArgb(25, 48, 72);
            dgvTasks.GridColor = Color.FromArgb(224, 229, 235);
            dgvTasks.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvTasks.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            colTaskId.FillWeight = 30;
            colMaterial.FillWeight = 24;
            colTaskState.FillWeight = 46;
            foreach (DataGridViewRow row in dgvTasks.Rows) row.Height = 42;

            dgvTasks.Columns[2].HeaderText = "检测结果";
            dgvTasks.Columns[0].Width = 220;
            dgvTasks.Columns[1].Width = 90;
            dgvTasks.Columns[2].Width = 120;
            dgvTasks.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "LossRate",
                HeaderText = "脱落率",
                Width = 120,
                ReadOnly = true
            });
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CompletedAt",
                HeaderText = "完成时间",
                Width = 165,
                ReadOnly = true
            });
            dgvTasks.Rows.Clear();
            dgvTasks.Visible = true;
            lblQueueTitle.Text = "当前试样检测结果";
            pnlCurrentResult = new Panel
            {
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            picCurrentResult = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(238, 242, 246),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            TableLayoutPanel details = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 206,
                ColumnCount = 2,
                RowCount = 5,
                BackColor = Color.White,
                Padding = new Padding(14, 10, 14, 10)
            };
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            for (int row = 0; row < 5; row++)
                details.RowStyles.Add(new RowStyle(SizeType.Percent, 20));

            lblCurrentResultId = NewResultValue("检验号：--");
            lblCurrentResultType = NewResultValue("类型：--");
            lblCurrentResultValue = NewResultValue("检测值：--");
            lblCurrentResultDecision = NewResultValue("等待检测");
            lblCurrentResultDecision.Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold);
            lblCurrentResultDecision.TextAlign = ContentAlignment.MiddleCenter;
            lblCurrentResultDecision.BackColor = Color.FromArgb(235, 239, 244);
            lblCurrentResultTime = NewResultValue("完成时间：--");
            Label hint = NewResultValue("每片检测完成后自动覆盖显示最新结果");
            hint.ForeColor = Color.DimGray;

            details.Controls.Add(lblCurrentResultId, 0, 0);
            details.SetColumnSpan(lblCurrentResultId, 2);
            details.Controls.Add(lblCurrentResultType, 0, 1);
            details.Controls.Add(lblCurrentResultValue, 0, 2);
            details.Controls.Add(lblCurrentResultTime, 0, 3);
            details.Controls.Add(hint, 0, 4);
            details.Controls.Add(lblCurrentResultDecision, 1, 1);
            details.SetRowSpan(lblCurrentResultDecision, 4);
            pnlCurrentResult.Controls.Add(picCurrentResult);
            pnlCurrentResult.Controls.Add(details);
            pnlStationHeader.Controls.Add(pnlCurrentResult);
            pnlCurrentResult.BringToFront();

            lblBatchListTitle = new Label
            {
                Text = "当前批次试样明细 · 0片",
                Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(27, 52, 76),
                BackColor = Color.FromArgb(240, 245, 250),
                Padding = new Padding(14, 10, 0, 0)
            };
            pnlStationHeader.Controls.Add(lblBatchListTitle);
            lblBatchListTitle.BringToFront();
            dgvTasks.BringToFront();
        }

        private void LayoutSamplePreviewAndTaskPanels()
        {
            if (pnlStationHeader.ClientSize.Width <= 0) return;

            const int margin = 16;
            const int gap = 18;
            int availableWidth = pnlStationHeader.ClientSize.Width - margin * 2;
            int availableHeight = Math.Max(180, pnlStationHeader.ClientSize.Height - margin * 2);
            int queueWidth = Math.Max(420, (int)((availableWidth - gap) * 0.48));
            int logLeft = margin + queueWidth + gap;
            int logWidth = Math.Max(300, margin + availableWidth - logLeft);

            lblQueueTitle.Dock = DockStyle.None;
            dgvTasks.Dock = DockStyle.None;
            lblLogTitle.Dock = DockStyle.None;
            txtRuntimeLog.Dock = DockStyle.None;

            lblQueueTitle.Bounds = new Rectangle(margin, margin, queueWidth, 52);
            int resultHeight = Math.Max(250, Math.Min(340, (int)(availableHeight * 0.53)));
            pnlCurrentResult.Bounds = new Rectangle(
                margin,
                lblQueueTitle.Bottom,
                queueWidth,
                resultHeight);
            lblBatchListTitle.Bounds = new Rectangle(
                margin,
                pnlCurrentResult.Bottom + 10,
                queueWidth,
                44);
            dgvTasks.Bounds = new Rectangle(
                margin,
                lblBatchListTitle.Bottom,
                queueWidth,
                Math.Max(90, margin + availableHeight - lblBatchListTitle.Bottom));
            lblLogTitle.Bounds = new Rectangle(logLeft, margin, logWidth, 52);
            txtRuntimeLog.Bounds = new Rectangle(
                logLeft,
                lblLogTitle.Bottom,
                logWidth,
                Math.Max(80, availableHeight - lblLogTitle.Height));

            lblQueueTitle.Padding = new Padding(14, 14, 0, 0);
            lblLogTitle.Padding = new Padding(14, 14, 0, 0);
            lblQueueTitle.BackColor = Color.FromArgb(240, 245, 250);
            lblLogTitle.BackColor = Color.FromArgb(240, 245, 250);
            txtRuntimeLog.BorderStyle = BorderStyle.FixedSingle;
        }

        private void AddBatchSamplePending(string sampleId, string materialType)
        {
            if (dgvTasks == null) return;
            dgvTasks.Rows.Add(sampleId, materialType, "检测中", "--", "--");
            DataGridViewRow row = dgvTasks.Rows[dgvTasks.Rows.Count - 1];
            row.Height = 42;
            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 250, 226);
            UpdateBatchListTitle();
            dgvTasks.FirstDisplayedScrollingRowIndex = Math.Max(0, dgvTasks.Rows.Count - 1);
        }

        private void UpdateBatchSampleResult(string sampleId, AdhesionVisionResult result)
        {
            if (dgvTasks == null || result == null) return;
            for (int index = dgvTasks.Rows.Count - 1; index >= 0; index--)
            {
                DataGridViewRow row = dgvTasks.Rows[index];
                if (!string.Equals(
                    Convert.ToString(row.Cells[0].Value),
                    sampleId,
                    StringComparison.OrdinalIgnoreCase)) continue;
                row.Cells[2].Value = result.IsQualified ? "合格" : "不合格";
                row.Cells[3].Value = result.LossRatePercent.ToString("F3") + "%";
                row.Cells[4].Value = DateTime.Now.ToString("HH:mm:ss");
                row.DefaultCellStyle.BackColor = result.IsQualified
                    ? Color.FromArgb(232, 247, 238)
                    : Color.FromArgb(255, 235, 235);
                break;
            }
            UpdateBatchListTitle();
        }

        private void UpdateBatchSampleFailure(string sampleId, string message)
        {
            if (dgvTasks == null || string.IsNullOrWhiteSpace(sampleId)) return;
            for (int index = dgvTasks.Rows.Count - 1; index >= 0; index--)
            {
                DataGridViewRow row = dgvTasks.Rows[index];
                if (!string.Equals(
                    Convert.ToString(row.Cells[0].Value),
                    sampleId,
                    StringComparison.OrdinalIgnoreCase)) continue;
                row.Cells[2].Value = "检测失败";
                row.Cells[3].Value = "--";
                row.Cells[4].Value = DateTime.Now.ToString("HH:mm:ss");
                row.Cells[2].ToolTipText = message;
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 235);
                break;
            }
        }

        private void ClearBatchSampleList()
        {
            if (dgvTasks == null) return;
            dgvTasks.Rows.Clear();
            UpdateBatchListTitle();
        }

        private void UpdateBatchListTitle()
        {
            if (lblBatchListTitle != null)
                lblBatchListTitle.Text = "当前批次试样明细 · " + dgvTasks.Rows.Count + "片";
        }

        private static Label NewResultValue(string text)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Padding = new Padding(4, 0, 4, 0),
                Font = new Font("Microsoft YaHei UI", 10.5F),
                ForeColor = Color.FromArgb(35, 52, 68)
            };
        }

        private void ShowCurrentSamplePending(string sampleId, string materialType)
        {
            if (lblCurrentResultId == null) return;
            lblCurrentResultId.Text = "检验号：" + (string.IsNullOrWhiteSpace(sampleId) ? "--" : sampleId);
            lblCurrentResultType.Text = "类型：" + (string.IsNullOrWhiteSpace(materialType) ? "--" : materialType);
            lblCurrentResultValue.Text = "检测值：等待视觉判定";
            lblCurrentResultTime.Text = "完成时间：--";
            lblCurrentResultDecision.Text = "检测中";
            lblCurrentResultDecision.BackColor = Color.FromArgb(255, 244, 204);
            lblCurrentResultDecision.ForeColor = Color.FromArgb(128, 88, 0);
            ReplaceCurrentResultImage(null);
        }

        private void ShowCurrentSampleResult(
            string sampleId,
            string materialType,
            AdhesionVisionResult result)
        {
            if (result == null || lblCurrentResultId == null) return;
            lblCurrentResultId.Text = "检验号：" + (string.IsNullOrWhiteSpace(sampleId) ? "--" : sampleId);
            lblCurrentResultType.Text = "类型：" + materialType;
            lblCurrentResultValue.Text =
                "脱落率：" + result.LossRatePercent.ToString("F3") + "%    缺陷区域：" + result.ParticleCount;
            lblCurrentResultTime.Text = "完成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            lblCurrentResultDecision.Text = result.IsQualified ? "合格" : "不合格";
            lblCurrentResultDecision.BackColor = result.IsQualified
                ? Color.FromArgb(218, 242, 229)
                : Color.FromArgb(255, 226, 226);
            lblCurrentResultDecision.ForeColor = result.IsQualified ? Color.SeaGreen : Color.Firebrick;
            ReplaceCurrentResultImage(result.AnnotatedImagePath);
            UpdateBatchSampleResult(sampleId, result);
        }

        private void ReplaceCurrentResultImage(string imagePath)
        {
            if (picCurrentResult == null) return;
            Image next = null;
            if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
            {
                using (FileStream stream = new FileStream(
                    imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image source = Image.FromStream(stream))
                    next = new Bitmap(source);
            }
            Image previous = picCurrentResult.Image;
            picCurrentResult.Image = next;
            previous?.Dispose();
            picCurrentResult.Invalidate();
        }

        private void DrawSamplePlaceholder(object sender, PaintEventArgs e)
        {
            if (picCurrentSample.Image != null) return;

            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            Rectangle canvas = new Rectangle(
                4,
                4,
                Math.Max(1, picCurrentSample.ClientSize.Width - 8),
                Math.Max(1, picCurrentSample.ClientSize.Height - 8));
            using (Brush background = new SolidBrush(Color.FromArgb(235, 238, 242)))
            using (Pen border = new Pen(Color.FromArgb(145, 154, 164)))
            {
                e.Graphics.FillRectangle(background, canvas);
                e.Graphics.DrawRectangle(border, canvas);
            }

            int top = canvas.Top + 6;
            int availableHeight = Math.Max(36, canvas.Height - 12);
            int gap = Math.Max(3, availableHeight / 18);
            int stripHeight = Math.Max(10, (availableHeight - gap * 2) / 3);
            Color[] stripColors =
            {
                Color.FromArgb(104, 113, 121),
                Color.FromArgb(145, 153, 158),
                Color.FromArgb(38, 43, 50)
            };
            for (int row = 0; row < stripColors.Length; row++)
            {
                Rectangle strip = new Rectangle(
                    canvas.Left + 3,
                    top + row * (stripHeight + gap),
                    Math.Max(1, canvas.Width - 6),
                    stripHeight);
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    strip,
                    ControlPaint.Light(stripColors[row], 0.08F),
                    ControlPaint.Dark(stripColors[row], 0.08F),
                    LinearGradientMode.Vertical))
                {
                    e.Graphics.FillRectangle(brush, strip);
                }

                if (row == 0)
                {
                    using (Pen marker = new Pen(Color.FromArgb(225, 61, 64), Math.Max(2, stripHeight / 10F)))
                    {
                        for (int segment = 1; segment <= 6; segment++)
                        {
                            int x = strip.Left + strip.Width * segment / 7;
                            e.Graphics.DrawLine(marker, x, strip.Top + 2, x, strip.Bottom - 2);
                        }
                    }
                }
            }

            string text = string.IsNullOrWhiteSpace(_previewSampleId)
                ? "等待试样检测图像"
                : "检验号 " + _previewSampleId + " · 等待图像";
            using (Font font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold))
            using (Brush shadow = new SolidBrush(Color.FromArgb(185, 0, 0, 0)))
            using (Brush textBrush = new SolidBrush(Color.White))
            {
                SizeF size = e.Graphics.MeasureString(text, font);
                float x = canvas.Left + 12;
                float y = canvas.Bottom - size.Height - 8;
                e.Graphics.DrawString(text, font, shadow, x + 1, y + 1);
                e.Graphics.DrawString(text, font, textBrush, x, y);
            }
        }

        private void SetPreviewSample(string sampleId)
        {
            sampleId = string.IsNullOrWhiteSpace(sampleId) ? null : sampleId.Trim();
            if (string.Equals(_previewSampleId, sampleId, StringComparison.OrdinalIgnoreCase)) return;
            _previewSampleId = sampleId;
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
