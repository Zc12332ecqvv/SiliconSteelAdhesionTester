using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using SiliconSteelAdhesionTester.Services.Vision;
using SiliconSteelAdhesionTester.Models;

namespace SiliconSteelAdhesionTester.Data
{
    public sealed class DatabaseService
    {
        private readonly string _connectionString;
        public string DatabasePath { get; private set; }

        public DatabaseService()
        {
            string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            Directory.CreateDirectory(dataDir);
            DatabasePath = Path.Combine(dataDir, "AdhesionTester.db");
            string legacyDatabasePath = Path.Combine(dataDir, "Sorter.db");
            if (!File.Exists(DatabasePath) && File.Exists(legacyDatabasePath))
                File.Copy(legacyDatabasePath, DatabasePath);
            _connectionString = "Data Source=" + DatabasePath + ";Version=3;foreign keys=true;";
        }

        public void Initialize()
        {
            if (!File.Exists(DatabasePath)) SQLiteConnection.CreateFile(DatabasePath);
            using (SQLiteConnection connection = Open())
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA synchronous=FULL;
PRAGMA foreign_keys=ON;
PRAGMA busy_timeout=5000;
CREATE TABLE IF NOT EXISTS Users(Id INTEGER PRIMARY KEY AUTOINCREMENT, UserName TEXT NOT NULL UNIQUE, DisplayName TEXT NOT NULL, PasswordHash TEXT NOT NULL, Role INTEGER NOT NULL, Enabled INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS QrCodeWhitelist(Id INTEGER PRIMARY KEY AUTOINCREMENT, QrCodeContent TEXT NOT NULL UNIQUE, MaterialType INTEGER NOT NULL, Enabled INTEGER NOT NULL DEFAULT 1, Remark TEXT, CreatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS ProductionRecords(Id INTEGER PRIMARY KEY AUTOINCREMENT, QrCodeContent TEXT, MaterialType INTEGER, VisionResult INTEGER, IsQualified INTEGER NOT NULL, ImagePath TEXT, ProcessData TEXT, OperatorName TEXT, CreatedAt TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS IX_ProductionRecords_CreatedAt ON ProductionRecords(CreatedAt);
CREATE TABLE IF NOT EXISTS FaultLogs(Id INTEGER PRIMARY KEY AUTOINCREMENT, FaultCode TEXT, Node TEXT, Message TEXT NOT NULL, OperatorName TEXT, IsCleared INTEGER NOT NULL DEFAULT 0, CreatedAt TEXT NOT NULL, ClearedAt TEXT);
CREATE TABLE IF NOT EXISTS OperationLogs(Id INTEGER PRIMARY KEY AUTOINCREMENT, UserName TEXT, Action TEXT NOT NULL, Detail TEXT, CreatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS InspectionTasks(Id INTEGER PRIMARY KEY AUTOINCREMENT, TaskNo TEXT NOT NULL UNIQUE, Source TEXT NOT NULL, Status TEXT NOT NULL, OrientedCount INTEGER, NonOrientedCount INTEGER, OperatorName TEXT, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS ScanEvents(Id INTEGER PRIMARY KEY AUTOINCREMENT, QrCodeContent TEXT, MaterialType TEXT, Station TEXT, IsAccepted INTEGER NOT NULL, Message TEXT, OperatorName TEXT, CreatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS VisionResults(Id INTEGER PRIMARY KEY AUTOINCREMENT, QrCodeContent TEXT, TestType TEXT NOT NULL, LossRatePercent REAL NOT NULL, ParticleCount INTEGER NOT NULL, DefectPixelCount INTEGER NOT NULL, InspectionPixelCount INTEGER NOT NULL, IsQualified INTEGER NOT NULL, SourceImagePath TEXT, MaskImagePath TEXT, AnnotatedImagePath TEXT, OperatorName TEXT, CreatedAt TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS IX_VisionResults_CreatedAt ON VisionResults(CreatedAt);
CREATE TABLE IF NOT EXISTS CaptureImages(Id INTEGER PRIMARY KEY AUTOINCREMENT, QrCodeContent TEXT, Station TEXT NOT NULL, CaptureStage TEXT NOT NULL, ImagePath TEXT NOT NULL, OperatorName TEXT, CreatedAt TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS IX_CaptureImages_QrCodeContent ON CaptureImages(QrCodeContent);
CREATE TABLE IF NOT EXISTS SyncOutbox(Id INTEGER PRIMARY KEY AUTOINCREMENT, EntityType TEXT NOT NULL, EntityId INTEGER NOT NULL, Payload TEXT NOT NULL, Status TEXT NOT NULL DEFAULT 'Pending', RetryCount INTEGER NOT NULL DEFAULT 0, NextAttemptAt TEXT, LastError TEXT, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS IX_SyncOutbox_Status ON SyncOutbox(Status,NextAttemptAt);
PRAGMA user_version=1;";
                command.ExecuteNonQuery();
            }
            EnsureQrCodeSchema();
            CreateDailyBackup();
            EnsureUser("operator", "操作员", "123456", UserRole.Operator);
            EnsureUser("engineer", "电气调试员", "123456", UserRole.Engineer);
            EnsureUser("admin", "超级管理员", "Admin@123", UserRole.SuperAdmin);
        }

        public UserSession Authenticate(string userName, string password)
        {
            using (SQLiteConnection connection = Open())
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Id,UserName,DisplayName,Role FROM Users WHERE UserName=@u AND PasswordHash=@p AND Enabled=1";
                command.Parameters.AddWithValue("@u", userName.Trim());
                command.Parameters.AddWithValue("@p", Hash(password));
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    UserSession user = new UserSession { Id = reader.GetInt64(0), UserName = reader.GetString(1), DisplayName = reader.GetString(2), Role = (UserRole)reader.GetInt32(3) };
                    LogOperation(user.UserName, "登录", "登录成功");
                    return user;
                }
            }
        }

        public void LogFault(string code, string node, string message, string userName)
        {
            Execute("INSERT INTO FaultLogs(FaultCode,Node,Message,OperatorName,CreatedAt) VALUES(@a,@b,@c,@d,@e)", code, node, message, userName, DateTime.Now.ToString("s"));
        }

        public void LogOperation(string userName, string action, string detail)
        {
            Execute("INSERT INTO OperationLogs(UserName,Action,Detail,CreatedAt) VALUES(@a,@b,@c,@d)", userName, action, detail, DateTime.Now.ToString("s"));
        }

        public void SaveQrCodeScanEvent(string qrCodeContent, string materialType, string station, bool accepted, string message, string userName)
        {
            Execute("INSERT INTO ScanEvents(QrCodeContent,MaterialType,Station,IsAccepted,Message,OperatorName,CreatedAt) VALUES(@a,@b,@c,@d,@e,@f,@g)",
                qrCodeContent, materialType, station, accepted ? 1 : 0, message, userName, DateTime.Now.ToString("s"));
        }

        public void SaveManualTask(string taskNo, int? orientedCount, int? nonOrientedCount, string userName)
        {
            Execute(@"INSERT OR REPLACE INTO InspectionTasks(TaskNo,Source,Status,OrientedCount,NonOrientedCount,OperatorName,CreatedAt,UpdatedAt)
VALUES(@a,'Manual','Pending',@b,@c,@d,COALESCE((SELECT CreatedAt FROM InspectionTasks WHERE TaskNo=@a),@e),@e)",
                taskNo, orientedCount, nonOrientedCount, userName, DateTime.Now.ToString("s"));
        }


        //视觉检测结果入库
        public long SaveVisionResult(string qrCodeContent, string sourceImagePath, AdhesionVisionResult result, string userName)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            using (SQLiteConnection connection = Open())
            using (SQLiteTransaction transaction = connection.BeginTransaction())
            {
                long id;
                using (SQLiteCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"INSERT INTO VisionResults(QrCodeContent,TestType,LossRatePercent,ParticleCount,DefectPixelCount,InspectionPixelCount,IsQualified,SourceImagePath,MaskImagePath,AnnotatedImagePath,OperatorName,CreatedAt)
VALUES(@qrCode,@type,@loss,@particles,@defects,@pixels,@qualified,@source,@mask,@annotated,@user,@created);
SELECT last_insert_rowid();";
                    command.Parameters.AddWithValue("@qrCode", qrCodeContent);
                    command.Parameters.AddWithValue("@type", result.TestType.ToString());
                    command.Parameters.AddWithValue("@loss", result.LossRatePercent);
                    command.Parameters.AddWithValue("@particles", result.ParticleCount);
                    command.Parameters.AddWithValue("@defects", result.DefectPixelCount);
                    command.Parameters.AddWithValue("@pixels", result.InspectionPixelCount);
                    command.Parameters.AddWithValue("@qualified", result.IsQualified ? 1 : 0);
                    command.Parameters.AddWithValue("@source", sourceImagePath);
                    command.Parameters.AddWithValue("@mask", result.MaskImagePath);
                    command.Parameters.AddWithValue("@annotated", result.AnnotatedImagePath);
                    command.Parameters.AddWithValue("@user", userName);
                    command.Parameters.AddWithValue("@created", DateTime.Now.ToString("s"));
                    id = Convert.ToInt64(command.ExecuteScalar());
                }
                using (SQLiteCommand outbox = connection.CreateCommand())
                {
                    outbox.Transaction = transaction;
                    outbox.CommandText = @"INSERT INTO SyncOutbox(EntityType,EntityId,Payload,Status,CreatedAt,UpdatedAt)
VALUES('VisionResult',@id,@payload,'Pending',@now,@now)";
                    outbox.Parameters.AddWithValue("@id", id);
                    outbox.Parameters.AddWithValue("@payload", "{\"visionResultId\":" + id + ",\"qrCodeContent\":\"" + JsonEscape(qrCodeContent) + "\"}");
                    outbox.Parameters.AddWithValue("@now", DateTime.Now.ToString("s"));
                    outbox.ExecuteNonQuery();
                }
                transaction.Commit();
                return id;
            }
        }

        public void SaveCaptureImage(string qrCodeContent, string station, string captureStage, string imagePath, string userName)
        {
            Execute(@"INSERT INTO CaptureImages(QrCodeContent,Station,CaptureStage,ImagePath,OperatorName,CreatedAt)
VALUES(@a,@b,@c,@d,@e,@f)", qrCodeContent, station, captureStage, imagePath, userName, DateTime.Now.ToString("s"));
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
            List<InspectionRecord> records = new List<InspectionRecord>();
            using (SQLiteConnection connection = Open())
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT Id,QrCodeContent,TestType,LossRatePercent,ParticleCount,IsQualified,AnnotatedImagePath,OperatorName,CreatedAt
FROM VisionResults
WHERE (@keyword='' OR QrCodeContent LIKE @pattern)
  AND (@from='' OR CreatedAt>=@from)
  AND (@to='' OR CreatedAt<=@to)
ORDER BY Id DESC LIMIT @limit";
                command.Parameters.AddWithValue("@keyword", keyword ?? string.Empty);
                command.Parameters.AddWithValue("@pattern", "%" + (keyword ?? string.Empty) + "%");
                command.Parameters.AddWithValue("@from", from.HasValue ? from.Value.ToString("s") : string.Empty);
                command.Parameters.AddWithValue("@to", to.HasValue ? to.Value.ToString("s") : string.Empty);
                command.Parameters.AddWithValue("@limit", Math.Max(1, Math.Min(5000, limit)));
                using (SQLiteDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        records.Add(new InspectionRecord
                        {
                            Id = reader.GetInt64(0), QrCodeContent = reader.IsDBNull(1) ? null : reader.GetString(1), MaterialType = reader.GetString(2),
                            LossRatePercent = reader.GetDouble(3), ParticleCount = reader.GetInt32(4),
                            IsQualified = reader.GetInt32(5) == 1, ImagePath = reader.IsDBNull(6) ? null : reader.GetString(6),
                            OperatorName = reader.IsDBNull(7) ? null : reader.GetString(7), CreatedAt = DateTime.Parse(reader.GetString(8))
                        });
            }
            return records;
        }

        public List<SystemLogRecord> GetSystemLogs(int limit)
        {
            return GetSystemLogs(null, null, limit);
        }

        public List<SystemLogRecord> GetSystemLogs(DateTime? from, DateTime? to, int limit)
        {
            List<SystemLogRecord> records = new List<SystemLogRecord>();
            using (SQLiteConnection connection = Open())
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT Category,CodeOrAction,Node,Message,UserName,CreatedAt FROM (
SELECT '故障' Category,FaultCode CodeOrAction,Node,Message,OperatorName UserName,CreatedAt FROM FaultLogs
UNION ALL SELECT '操作',Action,'',Detail,UserName,CreatedAt FROM OperationLogs)
WHERE (@from='' OR CreatedAt>=@from)
  AND (@to='' OR CreatedAt<=@to)
ORDER BY CreatedAt DESC LIMIT @limit";
                command.Parameters.AddWithValue("@from", from.HasValue ? from.Value.ToString("s") : string.Empty);
                command.Parameters.AddWithValue("@to", to.HasValue ? to.Value.ToString("s") : string.Empty);
                command.Parameters.AddWithValue("@limit", Math.Max(1, Math.Min(5000, limit)));
                using (SQLiteDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        records.Add(new SystemLogRecord
                        {
                            Category = reader.GetString(0), CodeOrAction = reader.IsDBNull(1) ? null : reader.GetString(1),
                            Node = reader.IsDBNull(2) ? null : reader.GetString(2), Message = reader.IsDBNull(3) ? null : reader.GetString(3),
                            UserName = reader.IsDBNull(4) ? null : reader.GetString(4), CreatedAt = DateTime.Parse(reader.GetString(5))
                        });
            }
            return records;
        }

        private void EnsureUser(string userName, string displayName, string password, UserRole role)
        {
            Execute("INSERT OR IGNORE INTO Users(UserName,DisplayName,PasswordHash,Role,Enabled,CreatedAt) VALUES(@a,@b,@c,@d,1,@e)", userName, displayName, Hash(password), (int)role, DateTime.Now.ToString("s"));
        }

        private SQLiteConnection Open() { SQLiteConnection c = new SQLiteConnection(_connectionString); c.Open(); return c; }

        private void EnsureQrCodeSchema()
        {
            using (SQLiteConnection connection = Open())
            {
                EnsureColumn(connection, "ProductionRecords", "QrCodeContent", "TEXT");
                EnsureColumn(connection, "ScanEvents", "QrCodeContent", "TEXT");
                EnsureColumn(connection, "VisionResults", "QrCodeContent", "TEXT");
                CopyLegacyQrCodeValues(connection, "ProductionRecords");
                CopyLegacyQrCodeValues(connection, "ScanEvents");
                CopyLegacyQrCodeValues(connection, "VisionResults");
                using (SQLiteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"CREATE INDEX IF NOT EXISTS IX_ScanEvents_QrCodeContent ON ScanEvents(QrCodeContent);
CREATE INDEX IF NOT EXISTS IX_VisionResults_QrCodeContent ON VisionResults(QrCodeContent);";
                    command.ExecuteNonQuery();
                }
            }
        }

        private static void EnsureColumn(SQLiteConnection connection, string table, string column, string type)
        {
            if (ColumnExists(connection, table, column)) return;
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = "ALTER TABLE [" + table + "] ADD COLUMN [" + column + "] " + type;
                command.ExecuteNonQuery();
            }
        }

        private static void CopyLegacyQrCodeValues(SQLiteConnection connection, string table)
        {
            if (!ColumnExists(connection, table, "Barcode") || !ColumnExists(connection, table, "QrCodeContent")) return;
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE [" + table + "] SET QrCodeContent=Barcode WHERE (QrCodeContent IS NULL OR QrCodeContent='') AND Barcode IS NOT NULL";
                command.ExecuteNonQuery();
            }
        }

        private static bool ColumnExists(SQLiteConnection connection, string table, string column)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info([" + table + "])";
                using (SQLiteDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private void CreateDailyBackup()
        {
            if (!File.Exists(DatabasePath)) return;
            string directory = Path.Combine(Path.GetDirectoryName(DatabasePath), "Backup");
            Directory.CreateDirectory(directory);
            string target = Path.Combine(directory, "AdhesionTester_" + DateTime.Today.ToString("yyyyMMdd") + ".db");
            if (!File.Exists(target))
            {
                string targetConnectionString = "Data Source=" + target + ";Version=3;";
                using (SQLiteConnection source = Open())
                using (SQLiteConnection destination = new SQLiteConnection(targetConnectionString))
                {
                    destination.Open();
                    source.BackupDatabase(destination, "main", "main", -1, null, 0);
                }
            }
            foreach (string file in Directory.GetFiles(directory, "AdhesionTester_*.db"))
                if (File.GetCreationTime(file) < DateTime.Today.AddDays(-30)) File.Delete(file);
        }

        private static string JsonEscape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private void Execute(string sql, params object[] values)
        {
            using (SQLiteConnection connection = Open())
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                for (int i = 0; i < values.Length; i++) command.Parameters.AddWithValue("@" + (char)('a' + i), values[i] ?? DBNull.Value);
                command.ExecuteNonQuery();
            }
        }

        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }
    }
}
