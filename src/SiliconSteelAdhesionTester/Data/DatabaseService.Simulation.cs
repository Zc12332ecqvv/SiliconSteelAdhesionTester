#if SIMULATION_ONLY
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            EnsureTestData();
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

        public List<InspectionRecord> GetInspectionRecords(string keyword, int limit)
        {
            return GetInspectionRecords(keyword, null, null, limit);
        }

        public List<InspectionRecord> GetInspectionRecords(
            string keyword,
            DateTime? from,
            DateTime? to,
            int limit)
        {
            string search = (keyword ?? string.Empty).Trim();
            List<InspectionRecord> records = new List<InspectionRecord>();
            if (!File.Exists(DatabasePath)) return records;
            long id = 0;
            foreach (string line in File.ReadLines(DatabasePath, Encoding.UTF8).Skip(1))
            {
                string[] fields = line.Split('\t');
                if (fields.Length < 6 || !string.Equals(fields[1], "VISION", StringComparison.OrdinalIgnoreCase))
                    continue;
                string qrCode = fields[4];
                if (search.Length > 0 && (qrCode ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                double lossRate;
                int particles;
                DateTime createdAt = ParseTime(fields[0]);
                if (from.HasValue && createdAt < from.Value) continue;
                if (to.HasValue && createdAt > to.Value) continue;
                records.Add(new InspectionRecord
                {
                    Id = ++id,
                    QrCodeContent = qrCode,
                    MaterialType = ReadLeadingText(fields[5]),
                    LossRatePercent = TryReadNumber(fields[5], "脱落率=", "%", out lossRate) ? (double?)lossRate : null,
                    ParticleCount = TryReadInteger(fields[5], "颗粒=", out particles) ? (int?)particles : null,
                    IsQualified = string.Equals(fields[3], "OK", StringComparison.OrdinalIgnoreCase),
                    ImagePath = ReadTrailingText(fields[5], "图片="),
                    OperatorName = fields[2],
                    CreatedAt = createdAt
                });
            }
            return records.OrderByDescending(item => item.CreatedAt)
                .Take(Math.Max(1, Math.Min(5000, limit))).ToList();
        }

        public List<SystemLogRecord> GetSystemLogs(int limit)
        {
            return GetSystemLogs(null, null, limit);
        }

        public List<SystemLogRecord> GetSystemLogs(DateTime? from, DateTime? to, int limit)
        {
            List<SystemLogRecord> records = new List<SystemLogRecord>();
            if (!File.Exists(DatabasePath)) return records;
            foreach (string line in File.ReadLines(DatabasePath, Encoding.UTF8).Skip(1))
            {
                string[] fields = line.Split('\t');
                if (fields.Length < 6 ||
                    (!string.Equals(fields[1], "FAULT", StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(fields[1], "OPERATION", StringComparison.OrdinalIgnoreCase)))
                    continue;
                bool fault = string.Equals(fields[1], "FAULT", StringComparison.OrdinalIgnoreCase);
                DateTime createdAt = ParseTime(fields[0]);
                if (from.HasValue && createdAt < from.Value) continue;
                if (to.HasValue && createdAt > to.Value) continue;
                records.Add(new SystemLogRecord
                {
                    Category = fault ? "故障" : "操作",
                    CodeOrAction = fields[3],
                    Node = fields[4],
                    Message = fields[5],
                    UserName = fields[2],
                    CreatedAt = createdAt
                });
            }
            return records.OrderByDescending(item => item.CreatedAt)
                .Take(Math.Max(1, Math.Min(5000, limit))).ToList();
        }

        private void EnsureTestData()
        {
            const string marker = "TEST_DATA_V1";
            if (File.ReadAllText(DatabasePath, Encoding.UTF8).Contains(marker)) return;

            DateTime start = DateTime.Now.AddMinutes(-35);
            string[] types =
            {
                "Oriented", "Oriented", "NonOrientedTape", "Oriented",
                "NonOrientedTape", "Oriented", "NonOrientedTape", "Oriented",
                "Oriented", "NonOrientedTape", "Oriented", "NonOrientedTape"
            };
            double[] lossRates = { 0.42, 0.68, 1.12, 0.55, 2.36, 0.91, 0.37, 1.88, 0.73, 0.64, 2.14, 0.49 };
            int[] particleCounts = { 2, 3, 6, 2, 14, 4, 1, 11, 3, 2, 12, 2 };
            for (int i = 0; i < types.Length; i++)
            {
                bool qualified = lossRates[i] <= 1.50;
                AppendAt(
                    start.AddMinutes(i * 2),
                    "VISION",
                    "tester",
                    qualified ? "OK" : "NG",
                    "TEST-" + DateTime.Today.ToString("yyyyMMdd") + "-" + (i + 1).ToString("D3"),
                    types[i] + " 脱落率=" + lossRates[i].ToString("F3") +
                    "% 颗粒=" + particleCounts[i] + " 图片=");
            }

            AppendAt(start.AddMinutes(1), "OPERATION", "tester", marker, "", "已生成仿真检测记录与运行日志测试数据");
            AppendAt(start.AddMinutes(6), "OPERATION", "tester", "整机启动", "", "测试任务启动，任务总数12片");
            AppendAt(start.AddMinutes(13), "FAULT", "tester", "CAMERA_TIMEOUT", "取向压弯前拍照", "测试故障：等待相机图片超时");
            AppendAt(start.AddMinutes(14), "OPERATION", "tester", "故障复位", "", "测试故障复位成功，流程继续");
            AppendAt(start.AddMinutes(21), "FAULT", "tester", "QR_READ_NG", "无取向弯折工位", "测试故障：二维码读取失败后重新触发");
            AppendAt(start.AddMinutes(22), "OPERATION", "tester", "二维码重读", "无取向弯折工位", "测试二维码重读成功");
            AppendAt(start.AddMinutes(30), "OPERATION", "tester", "任务完成", "", "测试批次完成：合格9片，不合格3片");
        }

        private void AppendAt(DateTime time, string type, string user, string code, string node, string message)
        {
            string safe = (message ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            File.AppendAllText(
                DatabasePath,
                time.ToString("s") + "\t" + type + "\t" + user + "\t" + code + "\t" + node + "\t" + safe + Environment.NewLine,
                Encoding.UTF8);
        }

        private static DateTime ParseTime(string value)
        {
            DateTime parsed;
            return DateTime.TryParse(value, out parsed) ? parsed : DateTime.MinValue;
        }

        private static string ReadLeadingText(string value)
        {
            int index = (value ?? string.Empty).IndexOf(' ');
            return index > 0 ? value.Substring(0, index) : value;
        }

        private static string ReadTrailingText(string value, string marker)
        {
            int index = (value ?? string.Empty).IndexOf(marker, StringComparison.Ordinal);
            return index < 0 ? null : value.Substring(index + marker.Length).Trim();
        }

        private static bool TryReadNumber(string value, string startMarker, string endMarker, out double result)
        {
            result = 0;
            int start = (value ?? string.Empty).IndexOf(startMarker, StringComparison.Ordinal);
            if (start < 0) return false;
            start += startMarker.Length;
            int end = value.IndexOf(endMarker, start, StringComparison.Ordinal);
            if (end < 0) return false;
            return double.TryParse(
                value.Substring(start, end - start),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out result);
        }

        private static bool TryReadInteger(string value, string marker, out int result)
        {
            result = 0;
            string text = ReadTrailingText(value, marker);
            if (string.IsNullOrWhiteSpace(text)) return false;
            int separator = text.IndexOf(' ');
            if (separator >= 0) text = text.Substring(0, separator);
            return int.TryParse(text, out result);
        }

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
