using System;
using System.Drawing;
using System.Windows.Forms;

namespace SiliconSteelAdhesionTester.Forms
{
    [System.ComponentModel.DesignerCategory("Form")]
    public sealed class ManualTaskForm : Form
    {
        private readonly TextBox _trayNumber = new TextBox();
        private readonly TextBox _orientedCount = new TextBox();
        private readonly TextBox _nonOrientedCount = new TextBox();

        public ManualTaskForm()
        {
            Text = "创建手动任务";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 10F);
            BackColor = Color.White;
            ClientSize = new Size(520, 350);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            Label title = new Label
            {
                Text = "手动任务",
                Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold),
                ForeColor = Color.FromArgb(18, 50, 80),
                Location = new Point(34, 26),
                AutoSize = true
            };
            Label hint = new Label
            {
                Text = "以下内容均为选填；创建任务不会自动启动设备。",
                ForeColor = Color.DimGray,
                Location = new Point(38, 72),
                AutoSize = true
            };

            AddField("料盘编号", _trayNumber, 112);
            AddField("取向数量", _orientedCount, 166);
            AddField("无取向数量", _nonOrientedCount, 220);

            Button cancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(280, 286),
                Size = new Size(96, 42)
            };
            Button create = new Button
            {
                Text = "创建任务",
                BackColor = Color.SeaGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(390, 286),
                Size = new Size(96, 42)
            };
            create.Click += CreateClicked;

            Controls.Add(title);
            Controls.Add(hint);
            Controls.Add(cancel);
            Controls.Add(create);
            AcceptButton = create;
            CancelButton = cancel;
        }

        public string TrayNumber => _trayNumber.Text.Trim();
        public int? OrientedCount => ParseOptionalCount(_orientedCount.Text);
        public int? NonOrientedCount => ParseOptionalCount(_nonOrientedCount.Text);

        private void AddField(string labelText, TextBox input, int top)
        {
            Label label = new Label
            {
                Text = labelText,
                Location = new Point(40, top + 7),
                Size = new Size(100, 30)
            };
            input.Location = new Point(152, top);
            input.Size = new Size(334, 34);
            Controls.Add(label);
            Controls.Add(input);
        }

        private void CreateClicked(object sender, EventArgs e)
        {
            if (!ValidateCount(_orientedCount.Text, out _))
            {
                ShowCountError("取向数量");
                return;
            }
            if (!ValidateCount(_nonOrientedCount.Text, out _))
            {
                ShowCountError("无取向数量");
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private static bool ValidateCount(string value, out int count)
        {
            count = 0;
            return string.IsNullOrWhiteSpace(value) ||
                   (int.TryParse(value.Trim(), out count) && count >= 0);
        }

        private static int? ParseOptionalCount(string value)
        {
            return int.TryParse((value ?? string.Empty).Trim(), out int count) ? count : (int?)null;
        }

        private static void ShowCountError(string field)
        {
            MessageBox.Show(field + "必须是大于或等于0的整数，也可以留空。",
                "输入不正确", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
