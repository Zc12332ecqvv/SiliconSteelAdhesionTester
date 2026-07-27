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




//确认协议modbus还是S7  映射
//二维码信息（扫描后的信息） 连接方式usb还是tcp/ip
//视觉 样品图 ROI 检测区域如何确定 合格阈值如何计算：面积百分比、颗粒数量、最大缺陷尺寸，还是分级判定？ A/B/C 等级标准及临界样品
//数据库要求