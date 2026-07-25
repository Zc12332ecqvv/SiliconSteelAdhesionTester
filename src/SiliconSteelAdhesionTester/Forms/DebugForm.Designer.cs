namespace SiliconSteelAdhesionTester.Forms
{
    partial class DebugForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Panel pnlHeader;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.GroupBox grpRegister;
        private System.Windows.Forms.TextBox txtAddress;
        private System.Windows.Forms.TextBox txtValue;
        private System.Windows.Forms.Button btnRead;
        private System.Windows.Forms.Button btnOn;
        private System.Windows.Forms.Button btnOff;
        private System.Windows.Forms.Button btnS2Scan;
        private System.Windows.Forms.Button btnS2Camera;
        private System.Windows.Forms.Button btnS4Camera;
        private System.Windows.Forms.Label lblResult;
        private System.Windows.Forms.GroupBox grpFlow;
        private System.Windows.Forms.Panel flowTable;

        protected override void Dispose(bool disposing) { if (disposing && components != null) components.Dispose(); base.Dispose(disposing); }

        private void InitializeComponent()
        {
            this.pnlHeader = new System.Windows.Forms.Panel();
            this.lblTitle = new System.Windows.Forms.Label();
            this.grpRegister = new System.Windows.Forms.GroupBox();
            this.txtAddress = new System.Windows.Forms.TextBox();
            this.txtValue = new System.Windows.Forms.TextBox();
            this.btnRead = new System.Windows.Forms.Button();
            this.btnOn = new System.Windows.Forms.Button();
            this.btnOff = new System.Windows.Forms.Button();
            this.btnS2Scan = new System.Windows.Forms.Button();
            this.btnS2Camera = new System.Windows.Forms.Button();
            this.btnS4Camera = new System.Windows.Forms.Button();
            this.lblResult = new System.Windows.Forms.Label();
            this.grpFlow = new System.Windows.Forms.GroupBox();
            this.flowTable = new System.Windows.Forms.Panel();
            this.pnlHeader.SuspendLayout();
            this.grpRegister.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlHeader
            // 
            this.pnlHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(25)))), ((int)(((byte)(48)))), ((int)(((byte)(72)))));
            this.pnlHeader.Controls.Add(this.lblTitle);
            this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlHeader.Location = new System.Drawing.Point(0, 0);
            this.pnlHeader.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.pnlHeader.Name = "pnlHeader";
            this.pnlHeader.Size = new System.Drawing.Size(2168, 144);
            this.pnlHeader.TabIndex = 2;
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 18F, System.Drawing.FontStyle.Bold);
            this.lblTitle.ForeColor = System.Drawing.Color.White;
            this.lblTitle.Location = new System.Drawing.Point(44, 34);
            this.lblTitle.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(315, 64);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "调试诊断中心";
            // 
            // grpRegister
            // 
            this.grpRegister.Controls.Add(this.txtAddress);
            this.grpRegister.Controls.Add(this.txtValue);
            this.grpRegister.Controls.Add(this.btnRead);
            this.grpRegister.Controls.Add(this.btnOn);
            this.grpRegister.Controls.Add(this.btnOff);
            this.grpRegister.Controls.Add(this.btnS2Scan);
            this.grpRegister.Controls.Add(this.btnS2Camera);
            this.grpRegister.Controls.Add(this.btnS4Camera);
            this.grpRegister.Controls.Add(this.lblResult);
            this.grpRegister.Location = new System.Drawing.Point(44, 576);
            this.grpRegister.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.grpRegister.Name = "grpRegister";
            this.grpRegister.Padding = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.grpRegister.Size = new System.Drawing.Size(2076, 504);
            this.grpRegister.TabIndex = 0;
            this.grpRegister.TabStop = false;
            this.grpRegister.Text = "PLC寄存器与PC握手点调试";
            // 
            // txtAddress
            // 
            this.txtAddress.Location = new System.Drawing.Point(56, 96);
            this.txtAddress.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.txtAddress.Name = "txtAddress";
            this.txtAddress.Size = new System.Drawing.Size(536, 40);
            this.txtAddress.TabIndex = 0;
            this.txtAddress.Text = "DB4120.DBX578.3";
            // 
            // txtValue
            // 
            this.txtValue.Location = new System.Drawing.Point(628, 96);
            this.txtValue.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.txtValue.Name = "txtValue";
            this.txtValue.ReadOnly = true;
            this.txtValue.Size = new System.Drawing.Size(256, 40);
            this.txtValue.TabIndex = 1;
            // 
            // btnRead
            // 
            this.btnRead.Location = new System.Drawing.Point(0, 0);
            this.btnRead.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.btnRead.Name = "btnRead";
            this.btnRead.Size = new System.Drawing.Size(150, 46);
            this.btnRead.TabIndex = 2;
            // 
            // btnOn
            // 
            this.btnOn.Location = new System.Drawing.Point(0, 0);
            this.btnOn.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.btnOn.Name = "btnOn";
            this.btnOn.Size = new System.Drawing.Size(150, 46);
            this.btnOn.TabIndex = 3;
            // 
            // btnOff
            // 
            this.btnOff.Location = new System.Drawing.Point(0, 0);
            this.btnOff.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.btnOff.Name = "btnOff";
            this.btnOff.Size = new System.Drawing.Size(150, 46);
            this.btnOff.TabIndex = 4;
            // 
            // btnS2Scan
            // 
            this.btnS2Scan.Location = new System.Drawing.Point(0, 0);
            this.btnS2Scan.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.btnS2Scan.Name = "btnS2Scan";
            this.btnS2Scan.Size = new System.Drawing.Size(150, 46);
            this.btnS2Scan.TabIndex = 5;
            // 
            // btnS2Camera
            // 
            this.btnS2Camera.Location = new System.Drawing.Point(0, 0);
            this.btnS2Camera.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.btnS2Camera.Name = "btnS2Camera";
            this.btnS2Camera.Size = new System.Drawing.Size(150, 46);
            this.btnS2Camera.TabIndex = 6;
            // 
            // btnS4Camera
            // 
            this.btnS4Camera.Location = new System.Drawing.Point(0, 0);
            this.btnS4Camera.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.btnS4Camera.Name = "btnS4Camera";
            this.btnS4Camera.Size = new System.Drawing.Size(150, 46);
            this.btnS4Camera.TabIndex = 7;
            // 
            // lblResult
            // 
            this.lblResult.AutoSize = true;
            this.lblResult.ForeColor = System.Drawing.Color.DimGray;
            this.lblResult.Location = new System.Drawing.Point(56, 352);
            this.lblResult.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblResult.Name = "lblResult";
            this.lblResult.Size = new System.Drawing.Size(581, 35);
            this.lblResult.TabIndex = 8;
            this.lblResult.Text = "警告：实体PLC模式下写点会直接影响设备动作。";
            // 
            // grpFlow
            // 
            this.grpFlow.Location = new System.Drawing.Point(44, 184);
            this.grpFlow.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.grpFlow.Name = "grpFlow";
            this.grpFlow.Padding = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.grpFlow.Size = new System.Drawing.Size(2076, 352);
            this.grpFlow.TabIndex = 1;
            this.grpFlow.TabStop = false;
            this.grpFlow.Text = "全流程进度诊断";
            // 
            // flowTable
            // 
            this.flowTable.Location = new System.Drawing.Point(0, 0);
            this.flowTable.Name = "flowTable";
            this.flowTable.Size = new System.Drawing.Size(200, 100);
            this.flowTable.TabIndex = 0;
            // 
            // DebugForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(192F, 192F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.ClientSize = new System.Drawing.Size(2168, 1132);
            this.Controls.Add(this.grpRegister);
            this.Controls.Add(this.grpFlow);
            this.Controls.Add(this.pnlHeader);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F);
            this.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.MinimumSize = new System.Drawing.Size(2174, 1139);
            this.Name = "DebugForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.pnlHeader.ResumeLayout(false);
            this.pnlHeader.PerformLayout();
            this.grpRegister.ResumeLayout(false);
            this.grpRegister.PerformLayout();
            this.ResumeLayout(false);

        }

    }
}
