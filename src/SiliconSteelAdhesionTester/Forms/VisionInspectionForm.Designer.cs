namespace SiliconSteelAdhesionTester.Forms
{
    partial class VisionInspectionForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.ComboBox cboMode;
        private System.Windows.Forms.TextBox txtSampleId;
        private System.Windows.Forms.TextBox txtBefore;
        private System.Windows.Forms.TextBox txtAfter;
        private System.Windows.Forms.Button btnBefore;
        private System.Windows.Forms.Button btnAfter;
        private System.Windows.Forms.Button btnAnalyze;
        private System.Windows.Forms.Label lblBefore;
        private System.Windows.Forms.Label lblAfter;
        private System.Windows.Forms.Label lblInstruction;
        private System.Windows.Forms.Label lblLossRate;
        private System.Windows.Forms.Label lblParticleCount;
        private System.Windows.Forms.Label lblDecision;
        private System.Windows.Forms.Label lblOutput;
        private System.Windows.Forms.Label lblModeCaption;
        private System.Windows.Forms.Label lblSampleCaption;
        private System.Windows.Forms.Label lblSourceTitle;
        private System.Windows.Forms.Label lblResultTitle;
        private System.Windows.Forms.Label lblLossCaption;
        private System.Windows.Forms.Label lblParticleCaption;
        private System.Windows.Forms.PictureBox picSource;
        private System.Windows.Forms.PictureBox picResult;
        private System.Windows.Forms.TextBox txtLog;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (picSource != null && picSource.Image != null) picSource.Image.Dispose();
                if (picResult != null && picResult.Image != null) picResult.Image.Dispose();
                if (components != null) components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.cboMode = new System.Windows.Forms.ComboBox();
            this.txtSampleId = new System.Windows.Forms.TextBox();
            this.txtBefore = new System.Windows.Forms.TextBox();
            this.txtAfter = new System.Windows.Forms.TextBox();
            this.btnBefore = new System.Windows.Forms.Button();
            this.btnAfter = new System.Windows.Forms.Button();
            this.btnAnalyze = new System.Windows.Forms.Button();
            this.lblBefore = new System.Windows.Forms.Label();
            this.lblAfter = new System.Windows.Forms.Label();
            this.lblInstruction = new System.Windows.Forms.Label();
            this.lblLossRate = new System.Windows.Forms.Label();
            this.lblParticleCount = new System.Windows.Forms.Label();
            this.lblDecision = new System.Windows.Forms.Label();
            this.lblOutput = new System.Windows.Forms.Label();
            this.lblModeCaption = new System.Windows.Forms.Label();
            this.lblSampleCaption = new System.Windows.Forms.Label();
            this.lblSourceTitle = new System.Windows.Forms.Label();
            this.lblResultTitle = new System.Windows.Forms.Label();
            this.lblLossCaption = new System.Windows.Forms.Label();
            this.lblParticleCaption = new System.Windows.Forms.Label();
            this.picSource = new System.Windows.Forms.PictureBox();
            this.picResult = new System.Windows.Forms.PictureBox();
            this.txtLog = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.picSource)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picResult)).BeginInit();
            this.SuspendLayout();
            this.lblModeCaption.AutoSize = true; this.lblModeCaption.Location = new System.Drawing.Point(24, 22); this.lblModeCaption.Text = "检测方式";
            this.lblSampleCaption.AutoSize = true; this.lblSampleCaption.Location = new System.Drawing.Point(420, 22); this.lblSampleCaption.Text = "检验号";
            this.cboMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboMode.Items.AddRange(new object[] { "取向测试 · 压弯前后对比", "非取向测试 · 胶带颗粒" });
            this.cboMode.Location = new System.Drawing.Point(126, 18); this.cboMode.Size = new System.Drawing.Size(265, 28); this.cboMode.SelectedIndexChanged += new System.EventHandler(this.cboMode_SelectedIndexChanged);
            this.txtSampleId.Location = new System.Drawing.Point(505, 18); this.txtSampleId.Size = new System.Drawing.Size(210, 28);
            this.lblBefore.AutoSize = true; this.lblBefore.Location = new System.Drawing.Point(24, 68); this.lblBefore.Text = "压弯前照片";
            this.txtBefore.Location = new System.Drawing.Point(126, 64); this.txtBefore.Size = new System.Drawing.Size(589, 28);
            this.btnBefore.Location = new System.Drawing.Point(728, 62); this.btnBefore.Size = new System.Drawing.Size(88, 32); this.btnBefore.Text = "选择"; this.btnBefore.Click += new System.EventHandler(this.btnBefore_Click);
            this.lblAfter.AutoSize = true; this.lblAfter.Location = new System.Drawing.Point(24, 108); this.lblAfter.Text = "压弯后照片";
            this.txtAfter.Location = new System.Drawing.Point(126, 104); this.txtAfter.Size = new System.Drawing.Size(589, 28);
            this.btnAfter.Location = new System.Drawing.Point(728, 102); this.btnAfter.Size = new System.Drawing.Size(88, 32); this.btnAfter.Text = "选择"; this.btnAfter.Click += new System.EventHandler(this.btnAfter_Click);
            this.btnAnalyze.BackColor = System.Drawing.Color.DodgerBlue; this.btnAnalyze.ForeColor = System.Drawing.Color.White; this.btnAnalyze.FlatStyle = System.Windows.Forms.FlatStyle.Flat; this.btnAnalyze.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Bold); this.btnAnalyze.Location = new System.Drawing.Point(840, 62); this.btnAnalyze.Size = new System.Drawing.Size(155, 72); this.btnAnalyze.Text = "开始 OpenCV 检测"; this.btnAnalyze.Click += new System.EventHandler(this.btnAnalyze_Click);
            this.lblInstruction.BackColor = System.Drawing.Color.FromArgb(235, 242, 250); this.lblInstruction.Location = new System.Drawing.Point(24, 148); this.lblInstruction.Size = new System.Drawing.Size(971, 38); this.lblInstruction.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblSourceTitle.AutoSize = true; this.lblSourceTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Bold); this.lblSourceTitle.Location = new System.Drawing.Point(24, 198); this.lblSourceTitle.Text = "采集图像";
            this.lblResultTitle.AutoSize = true; this.lblResultTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F, System.Drawing.FontStyle.Bold); this.lblResultTitle.Location = new System.Drawing.Point(530, 198); this.lblResultTitle.Text = "OpenCV缺陷标记";
            this.picSource.BackColor = System.Drawing.Color.FromArgb(30, 34, 42); this.picSource.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle; this.picSource.Location = new System.Drawing.Point(24, 226); this.picSource.Size = new System.Drawing.Size(465, 288); this.picSource.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picResult.BackColor = System.Drawing.Color.FromArgb(30, 34, 42); this.picResult.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle; this.picResult.Location = new System.Drawing.Point(530, 226); this.picResult.Size = new System.Drawing.Size(465, 288); this.picResult.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.lblLossCaption.AutoSize = true; this.lblLossCaption.ForeColor = System.Drawing.Color.DimGray; this.lblLossCaption.Location = new System.Drawing.Point(24, 526); this.lblLossCaption.Text = "脱落率";
            this.lblParticleCaption.AutoSize = true; this.lblParticleCaption.ForeColor = System.Drawing.Color.DimGray; this.lblParticleCaption.Location = new System.Drawing.Point(260, 526); this.lblParticleCaption.Text = "缺陷区域数";
            this.lblLossRate.BackColor = System.Drawing.Color.WhiteSmoke; this.lblLossRate.Font = new System.Drawing.Font("Microsoft YaHei UI", 18F, System.Drawing.FontStyle.Bold); this.lblLossRate.Location = new System.Drawing.Point(24, 550); this.lblLossRate.Size = new System.Drawing.Size(220, 58); this.lblLossRate.Text = "0.000 %"; this.lblLossRate.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblParticleCount.BackColor = System.Drawing.Color.WhiteSmoke; this.lblParticleCount.Font = new System.Drawing.Font("Microsoft YaHei UI", 18F, System.Drawing.FontStyle.Bold); this.lblParticleCount.Location = new System.Drawing.Point(260, 550); this.lblParticleCount.Size = new System.Drawing.Size(160, 58); this.lblParticleCount.Text = "0"; this.lblParticleCount.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblDecision.BackColor = System.Drawing.Color.DimGray; this.lblDecision.ForeColor = System.Drawing.Color.White; this.lblDecision.Font = new System.Drawing.Font("Microsoft YaHei UI", 18F, System.Drawing.FontStyle.Bold); this.lblDecision.Location = new System.Drawing.Point(436, 550); this.lblDecision.Size = new System.Drawing.Size(230, 58); this.lblDecision.Text = "等待检测"; this.lblDecision.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblOutput.AutoEllipsis = true; this.lblOutput.Location = new System.Drawing.Point(682, 550); this.lblOutput.Size = new System.Drawing.Size(313, 58); this.lblOutput.Text = "结果图路径";
            this.txtLog.Location = new System.Drawing.Point(24, 624); this.txtLog.Multiline = true; this.txtLog.ReadOnly = true; this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical; this.txtLog.Size = new System.Drawing.Size(971, 86);
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F); this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi; this.BackColor = System.Drawing.Color.White; this.ClientSize = new System.Drawing.Size(1024, 735);
            this.Controls.Add(this.lblModeCaption); this.Controls.Add(this.lblSampleCaption); this.Controls.Add(this.cboMode); this.Controls.Add(this.txtSampleId); this.Controls.Add(this.lblBefore); this.Controls.Add(this.txtBefore); this.Controls.Add(this.btnBefore); this.Controls.Add(this.lblAfter); this.Controls.Add(this.txtAfter); this.Controls.Add(this.btnAfter); this.Controls.Add(this.btnAnalyze); this.Controls.Add(this.lblInstruction); this.Controls.Add(this.lblSourceTitle); this.Controls.Add(this.lblResultTitle); this.Controls.Add(this.picSource); this.Controls.Add(this.picResult); this.Controls.Add(this.lblLossCaption); this.Controls.Add(this.lblParticleCaption); this.Controls.Add(this.lblLossRate); this.Controls.Add(this.lblParticleCount); this.Controls.Add(this.lblDecision); this.Controls.Add(this.lblOutput); this.Controls.Add(this.txtLog);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F); this.MinimumSize = new System.Drawing.Size(1040, 774); this.Name = "VisionInspectionForm"; this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent; this.Text = "硅钢附着力测试仪 · OpenCV视觉检测";
            ((System.ComponentModel.ISupportInitialize)(this.picSource)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picResult)).EndInit();
            this.ResumeLayout(false); this.PerformLayout();
        }
    }
}
