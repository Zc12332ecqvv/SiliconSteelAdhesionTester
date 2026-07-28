#if SIMULATION_ONLY
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SiliconSteelAdhesionTester.Models;
using SiliconSteelAdhesionTester.Services.Vision;

namespace SiliconSteelAdhesionTester.Data
{
    // 零依赖仿真数据服务，保证首次下载即可生成、登录和演示界面。
    // 实体发布构建会换用 DatabaseService.cs 中的 SQLite 实现。
    public sealed class DatabaseService
    {
        private readonly Dictionary<string, Account> _accounts = new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase)
        {
            { "operator", new Account("123456", "操作员", UserRole.Operator) },
            { "engineer", new Account("123456", "电气调试员", UserRole.Engineer) },
            { "admin", new Account("Admin@123", "超级管理员", UserRole.SuperAdmin) }
        };

        private string _dataDirectory;
        public string DatabasePath { get { return Path.Combine(_dataDirectory, "AdhesionTester-Simulation.log"); } }

        public void Initialize()
        {
            _dataDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Data"));
            Directory.CreateDirectory(_dataDirectory);
            string legacyLogPath = Path.Combine(_dataDirectory, "Sorter-Simulation.log");
            if (!File.Exists(DatabasePath) && File.Exists(legacyLogPath))
                File.Copy(legacyLogPath, DatabasePath);
            if (!File.Exists(DatabasePath)) File.WriteAllText(DatabasePath, "Time\tType\tUser\tCode\tNode\tMessage" + Environment.NewLine, Encoding.UTF8);
        }

        public UserSession Authenticate(string userName, string password)
        {
            if (string.IsNullOrWhiteSpace(userName) || !_accounts.TryGetValue(userName.Trim(), out Account account) || account.Password != password) return null;
            UserSession user = new UserSession { Id = (long)account.Role, UserName = userName.Trim(), DisplayName = account.DisplayName, Role = account.Role };
            LogOperation(user.UserName, "登录", "仿真模式登录成功");
            return user;
        }

        public void LogFault(string code, string node, string message, string userName)
        {
            Append("FAULT", userName, code, node, message);
        }

        public void LogOperation(string userName, string action, string detail)
        {
            Append("OPERATION", userName, action, string.Empty, detail);
        }

        public void SaveQrCodeScanEvent(string qrCodeContent, string materialType, string station, bool accepted, string message, string userName)
        {
            Append("QR", userName, accepted ? "OK" : "NG", station, materialType + " " + qrCodeContent + " " + message);
        }

        public void SaveManualTask(string taskNo, int? orientedCount, int? nonOrientedCount, string userName)
        {
            Append("TASK", userName, taskNo, "Manual", "取向=" + orientedCount + " 无取向=" + nonOrientedCount);
        }

        public long SaveVisionResult(string qrCodeContent, string sourceImagePath, AdhesionVisionResult result, string userName)
        {
            Append("VISION", userName, result.IsQualified ? "OK" : "NG", qrCodeContent,
                result.TestType + " 脱落率=" + result.LossRatePercent.ToString("F3") + "% 颗粒=" + result.ParticleCount + " 图片=" + result.AnnotatedImagePath);
            return DateTime.Now.Ticks;
        }

        public void SaveCaptureImage(string qrCodeContent, string station, string captureStage, string imagePath, string userName)
        {
            Append("CAPTURE", userName, captureStage, station, qrCodeContent + " " + imagePath);
        }

        public List<InspectionRecord> GetInspectionRecords(string keyword, int limit) { return new List<InspectionRecord>(); }
        public List<SystemLogRecord> GetSystemLogs(int limit) { return new List<SystemLogRecord>(); }

        private void Append(string type, string user, string code, string node, string message)
        {
            if (string.IsNullOrEmpty(_dataDirectory)) Initialize();
            string safe = (message ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            File.AppendAllText(DatabasePath, DateTime.Now.ToString("s") + "\t" + type + "\t" + user + "\t" + code + "\t" + node + "\t" + safe + Environment.NewLine, Encoding.UTF8);
        }

        private sealed class Account
        {
            public Account(string password, string displayName, UserRole role) { Password = password; DisplayName = displayName; Role = role; }
            public string Password { get; private set; }
            public string DisplayName { get; private set; }
            public UserRole Role { get; private set; }
        }
    }
}
#endif
