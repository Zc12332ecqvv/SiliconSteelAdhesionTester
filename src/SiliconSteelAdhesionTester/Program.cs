using System;
using System.Windows.Forms;
using SiliconSteelAdhesionTester.Configuration;
using SiliconSteelAdhesionTester.Data;
using SiliconSteelAdhesionTester.Forms;
using SiliconSteelAdhesionTester.Services.Plc;

namespace SiliconSteelAdhesionTester
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            DatabaseService database = new DatabaseService();
            database.Initialize();
            AppSettings settings = AppSettings.Load();
            IPlcService plc = PlcServiceFactory.Create(settings);

            using (LoginForm login = new LoginForm(database))
            {
                if (login.ShowDialog() != DialogResult.OK) return;
                Application.Run(new MainForm(login.CurrentUser, database, plc, settings));
            }
        }
    }
}
