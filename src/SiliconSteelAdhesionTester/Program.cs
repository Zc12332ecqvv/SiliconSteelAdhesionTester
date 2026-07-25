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

            var database = new DatabaseService();
            database.Initialize();
            var settings = AppSettings.Load();
            var plc = PlcServiceFactory.Create(settings);

            using (var login = new LoginForm(database))
            {
                if (login.ShowDialog() != DialogResult.OK) return;
                Application.Run(new MainForm(login.CurrentUser, database, plc, settings));
            }
        }
    }
}
