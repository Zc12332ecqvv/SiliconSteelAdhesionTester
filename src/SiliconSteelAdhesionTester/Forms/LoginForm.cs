using System;
using System.Windows.Forms;
using SiliconSteelAdhesionTester.Data;
using SiliconSteelAdhesionTester.Models;

namespace SiliconSteelAdhesionTester.Forms
{
    [System.ComponentModel.DesignerCategory("Form")]
    public partial class LoginForm : Form
    {
        private readonly DatabaseService _database;
        public UserSession CurrentUser { get; private set; }

        public LoginForm()
        {
            InitializeComponent();
        }

        public LoginForm(DatabaseService database)
            : this()
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            btnLogin.Click += LoginClick;
            AcceptButton = btnLogin;
        }

        private void LoginClick(object sender, EventArgs e)
        {
            CurrentUser = _database.Authenticate(txtUser.Text, txtPassword.Text);
            if (CurrentUser == null)
            {
                lblError.Text = "账号或密码错误，请重新输入。";
                lblError.Visible = true;
                txtPassword.SelectAll();
                txtPassword.Focus();
                return;
            }
            DialogResult = DialogResult.OK;
        }
    }
}
