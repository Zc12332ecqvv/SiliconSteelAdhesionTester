using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SiliconSteelAdhesionTester.Models;
using SiliconSteelAdhesionTester.Services.Plc;

namespace SiliconSteelAdhesionTester.Forms
{
    [System.ComponentModel.DesignerCategory("Form")]
    public partial class DebugForm : Form
    {
        private readonly IPlcService _plc;
        private readonly CancellationToken _token;

        public DebugForm()
        {
            InitializeComponent();
            ConfigureRuntimeUi();
        }

        public DebugForm(IPlcService plc, UserSession user, CancellationToken token)
            : this()
        {
            _plc = plc ?? throw new ArgumentNullException(nameof(plc));
            _token = token;
            if (user == null) throw new ArgumentNullException(nameof(user));
            Text = "调试诊断 - " + user.DisplayName;
            btnRead.Click += async (s, e) => await ExecuteSafe(ReadValueAsync);
            btnOn.Click += async (s, e) => await ExecuteSafe(() => WriteValueAsync(true));
            btnOff.Click += async (s, e) => await ExecuteSafe(() => WriteValueAsync(false));
            btnS2Scan.Click += (s, e) => txtAddress.Text = PlcAddresses.S2ScanAllowed;
            btnS2Camera.Click += (s, e) => txtAddress.Text = PlcAddresses.S2CameraAllowed;
            btnS4Camera.Click += (s, e) => txtAddress.Text = PlcAddresses.S4CameraAllowed;
        }

        private void ConfigureRuntimeUi()
        {
            string[] steps = { "AGV送料", "S1来料", "扫码", "条码校验", "相机拍照", "视觉分类", "工位加工", "等待来料" };
            for (int i = 0; i < steps.Length; i++)
            {
                var label = new Label
                {
                    Location = new System.Drawing.Point(16 + i * 126, 42),
                    Size = new System.Drawing.Size(112, 88),
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                    BackColor = i == 0 ? System.Drawing.Color.DodgerBlue : System.Drawing.Color.WhiteSmoke,
                    ForeColor = i == 0 ? System.Drawing.Color.White : System.Drawing.Color.FromArgb(45, 55, 65),
                    Text = (i + 1) + "\r\n" + steps[i]
                };
                grpFlow.Controls.Add(label);
            }

            SetupButton(btnRead, "读取", 462, 45, 90, System.Drawing.Color.SteelBlue);
            SetupButton(btnOn, "置 ON", 565, 45, 90, System.Drawing.Color.SeaGreen);
            SetupButton(btnOff, "置 OFF", 668, 45, 90, System.Drawing.Color.Firebrick);
            SetupButton(btnS2Scan, "S2 扫码允许", 28, 104, 160, System.Drawing.Color.FromArgb(73, 94, 116));
            SetupButton(btnS2Camera, "S2 拍照允许", 202, 104, 160, System.Drawing.Color.FromArgb(73, 94, 116));
            SetupButton(btnS4Camera, "S4 拍照允许", 376, 104, 160, System.Drawing.Color.FromArgb(73, 94, 116));
        }

        private static void SetupButton(Button button, string text, int x, int y, int width, System.Drawing.Color color)
        {
            button.Text = text;
            button.Location = new System.Drawing.Point(x, y);
            button.Size = new System.Drawing.Size(width, 36);
            button.BackColor = color;
            button.ForeColor = System.Drawing.Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
        }

        private async Task ExecuteSafe(Func<Task> action)
        {
            try { await action(); lblResult.Text = "操作成功  " + DateTime.Now.ToString("HH:mm:ss"); }
            catch (Exception ex) { lblResult.Text = "操作失败：" + ex.Message; MessageBox.Show(ex.Message, "PLC操作失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private async Task ReadValueAsync() { txtValue.Text = Convert.ToString(await _plc.ReadAsync(txtAddress.Text.Trim(), _token)); }
        private async Task WriteValueAsync(bool value) { await _plc.WriteAsync(txtAddress.Text.Trim(), value, _token); await ReadValueAsync(); }
    }
}
