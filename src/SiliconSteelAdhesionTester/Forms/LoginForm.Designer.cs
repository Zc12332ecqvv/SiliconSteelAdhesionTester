namespace SiliconSteelAdhesionTester.Forms
{
    partial class LoginForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Panel pnlHeader;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblSubtitle;
        private System.Windows.Forms.Panel pnlCard;
        private System.Windows.Forms.Label lblUser;
        private System.Windows.Forms.Label lblPassword;
        private System.Windows.Forms.TextBox txtUser;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.Button btnLogin;
        private System.Windows.Forms.Label lblError;
        private System.Windows.Forms.Label lblHint;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.pnlHeader = new System.Windows.Forms.Panel();
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblSubtitle = new System.Windows.Forms.Label();
            this.pnlCard = new System.Windows.Forms.Panel();
            this.lblUser = new System.Windows.Forms.Label();
            this.lblPassword = new System.Windows.Forms.Label();
            this.txtUser = new System.Windows.Forms.TextBox();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.btnLogin = new System.Windows.Forms.Button();
            this.lblError = new System.Windows.Forms.Label();
            this.lblHint = new System.Windows.Forms.Label();
            this.pnlHeader.SuspendLayout();
            this.pnlCard.SuspendLayout();
            this.SuspendLayout();
            // pnlHeader
            this.pnlHeader.BackColor = System.Drawing.Color.FromArgb(25, 48, 72);
            this.pnlHeader.Controls.Add(this.lblSubtitle);
            this.pnlHeader.Controls.Add(this.lblTitle);
            this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlHeader.Location = new System.Drawing.Point(0, 0);
            this.pnlHeader.Name = "pnlHeader";
            this.pnlHeader.Size = new System.Drawing.Size(720, 136);
            // lblTitle
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 22F, System.Drawing.FontStyle.Bold);
            this.lblTitle.ForeColor = System.Drawing.Color.White;
            this.lblTitle.Location = new System.Drawing.Point(74, 34);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Text = "自动涂层附着力测试仪";
            // lblSubtitle
            this.lblSubtitle.AutoSize = true;
            this.lblSubtitle.ForeColor = System.Drawing.Color.FromArgb(188, 211, 231);
            this.lblSubtitle.Location = new System.Drawing.Point(78, 88);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Text = "PRODUCTION CONTROL SYSTEM  ·  上位机操作终端";
            // pnlCard
            this.pnlCard.BackColor = System.Drawing.Color.White;
            this.pnlCard.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlCard.Controls.Add(this.lblHint);
            this.pnlCard.Controls.Add(this.lblError);
            this.pnlCard.Controls.Add(this.btnLogin);
            this.pnlCard.Controls.Add(this.txtPassword);
            this.pnlCard.Controls.Add(this.txtUser);
            this.pnlCard.Controls.Add(this.lblPassword);
            this.pnlCard.Controls.Add(this.lblUser);
            this.pnlCard.Location = new System.Drawing.Point(116, 174);
            this.pnlCard.Name = "pnlCard";
            this.pnlCard.Size = new System.Drawing.Size(488, 304);
            // labels/textboxes
            this.lblUser.AutoSize = true; this.lblUser.Location = new System.Drawing.Point(46, 38); this.lblUser.Text = "登录账号";
            this.lblPassword.AutoSize = true; this.lblPassword.Location = new System.Drawing.Point(46, 102); this.lblPassword.Text = "登录密码";
            this.txtUser.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F); this.txtUser.Location = new System.Drawing.Point(48, 62); this.txtUser.Size = new System.Drawing.Size(390, 31); this.txtUser.Text = "admin";
            this.txtPassword.Font = new System.Drawing.Font("Microsoft YaHei UI", 11F); this.txtPassword.Location = new System.Drawing.Point(48, 126); this.txtPassword.Size = new System.Drawing.Size(390, 31); this.txtPassword.Text = "Admin@123"; this.txtPassword.UseSystemPasswordChar = true;
            // btnLogin
            this.btnLogin.BackColor = System.Drawing.Color.FromArgb(0, 120, 215); this.btnLogin.FlatStyle = System.Windows.Forms.FlatStyle.Flat; this.btnLogin.ForeColor = System.Drawing.Color.White; this.btnLogin.Location = new System.Drawing.Point(48, 186); this.btnLogin.Size = new System.Drawing.Size(390, 42); this.btnLogin.Text = "登  录"; this.btnLogin.UseVisualStyleBackColor = false;
            // error/hint
            this.lblError.AutoSize = true; this.lblError.ForeColor = System.Drawing.Color.Firebrick; this.lblError.Location = new System.Drawing.Point(48, 164); this.lblError.Visible = false;
            this.lblHint.AutoSize = true; this.lblHint.ForeColor = System.Drawing.Color.Gray; this.lblHint.Location = new System.Drawing.Point(48, 251); this.lblHint.Text = "默认管理员账号：admin / Admin@123";
            // LoginForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.Color.FromArgb(241, 245, 249);
            this.ClientSize = new System.Drawing.Size(720, 520);
            this.Controls.Add(this.pnlCard);
            this.Controls.Add(this.pnlHeader);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "LoginForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "自动涂层附着力测试仪 - 登录";
            this.pnlHeader.ResumeLayout(false); this.pnlHeader.PerformLayout();
            this.pnlCard.ResumeLayout(false); this.pnlCard.PerformLayout();
            this.ResumeLayout(false);
        }
    }
}
