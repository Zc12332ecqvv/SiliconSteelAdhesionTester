using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SiliconSteelAdhesionTester.Services.Vision;
using SiliconSteelAdhesionTester.Data;
using SiliconSteelAdhesionTester.Models;

namespace SiliconSteelAdhesionTester.Forms
{
    [System.ComponentModel.DesignerCategory("Form")]
    public partial class VisionInspectionForm : Form
    {
        private readonly IAdhesionVisionService _vision;
        private readonly DatabaseService _database;
        private readonly UserSession _user;

        public VisionInspectionForm()
        {
            InitializeComponent();
        }

        public VisionInspectionForm(IAdhesionVisionService vision)
            : this(vision, null, null, null)
        {
        }

        public VisionInspectionForm(IAdhesionVisionService vision, DatabaseService database, UserSession user, string sampleId)
            : this()
        {
            _vision = vision ?? throw new ArgumentNullException(nameof(vision));
            _database = database;
            _user = user;
            cboMode.SelectedIndex = 0;
            if (!string.IsNullOrWhiteSpace(sampleId)) txtSampleId.Text = sampleId;
            UpdateMode();
        }

        private void cboMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateMode();
        }

        private void UpdateMode()
        {
            bool oriented = cboMode.SelectedIndex == 0;
            lblBefore.Visible = txtBefore.Visible = btnBefore.Visible = oriented;
            lblAfter.Text = oriented ? "压弯后照片" : "胶带照片";
            lblInstruction.Text = oriented
                ? "固定相机和光源；压弯前后试样位置必须一致。算法仅统计固定检测区域内新增脱落。"
                : "胶带应平整放入固定拍照治具，避免手持背景、反光和褶皱进入检测区域。";
        }

        private void btnBefore_Click(object sender, EventArgs e)
        {
            SelectImage(txtBefore);
        }

        private void btnAfter_Click(object sender, EventArgs e)
        {
            SelectImage(txtAfter);
        }

        private void SelectImage(TextBox target)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "图像文件|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff|所有文件|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    target.Text = dialog.FileName;
            }
        }

        private void btnAnalyze_Click(object sender, EventArgs e)
        {
            try
            {
                btnAnalyze.Enabled = false;
                string sampleId = string.IsNullOrWhiteSpace(txtSampleId.Text)
                    ? DateTime.Now.ToString("yyyyMMddHHmmss")
                    : txtSampleId.Text.Trim();
                AdhesionVisionResult result;

                if (cboMode.SelectedIndex == 0)
                {
                    result = _vision.AnalyzeOriented(txtBefore.Text.Trim(), txtAfter.Text.Trim(), null, sampleId);
                    ShowImage(picSource, txtAfter.Text.Trim());
                }
                else
                {
                    result = _vision.AnalyzeNonOrientedTape(txtAfter.Text.Trim(), null, sampleId);
                    ShowImage(picSource, txtAfter.Text.Trim());
                }

                ShowImage(picResult, result.AnnotatedImagePath);
                lblLossRate.Text = result.LossRatePercent.ToString("F3") + " %";
                lblParticleCount.Text = result.ParticleCount.ToString();
                lblDecision.Text = result.IsQualified ? "OK · 合格" : "NG · 不合格";
                lblDecision.BackColor = result.IsQualified ? Color.SeaGreen : Color.Firebrick;
                lblOutput.Text = result.AnnotatedImagePath;
                if (_database != null)
                {
                    string sourcePath = cboMode.SelectedIndex == 0 ? txtAfter.Text.Trim() : txtAfter.Text.Trim();
                    long recordId = _database.SaveVisionResult(sampleId, sourcePath, result, _user == null ? string.Empty : _user.UserName);
                    txtLog.AppendText("已保存数据库，记录编号：" + recordId + Environment.NewLine);
                }
                txtLog.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + sampleId + "  " + result.Message + Environment.NewLine);
            }
            catch (Exception ex)
            {
                lblDecision.Text = "检测失败";
                lblDecision.BackColor = Color.Firebrick;
                txtLog.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  ERROR  " + ex.Message + Environment.NewLine);
                MessageBox.Show(this, ex.Message, "视觉检测失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnAnalyze.Enabled = true;
            }
        }

        private static void ShowImage(PictureBox target, string path)
        {
            if (!File.Exists(path)) return;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (Image source = Image.FromStream(stream))
            {
                Bitmap copy = new Bitmap(source);
                Image old = target.Image;
                target.Image = copy;
                old?.Dispose();
            }
        }
    }
}
