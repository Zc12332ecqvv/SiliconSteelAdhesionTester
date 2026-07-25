using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
CREATE TABLE IF NOT EXISTS Users(Id INTEGER PRIMARY KEY AUTOINCREMENT, UserName TEXT NOT NULL UNIQUE, DisplayName TEXT NOT NULL, PasswordHash TEXT NOT NULL, Role INTEGER NOT NULL, Enabled INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS BarcodeWhitelist(Id INTEGER PRIMARY KEY AUTOINCREMENT, Barcode TEXT NOT NULL UNIQUE, MaterialType INTEGER NOT NULL, Enabled INTEGER NOT NULL DEFAULT 1, Remark TEXT, CreatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS ProductionRecords(Id INTEGER PRIMARY KEY AUTOINCREMENT, Barcode TEXT NOT NULL, MaterialType INTEGER, VisionResult INTEGER, IsQualified INTEGER NOT NULL, ImagePath TEXT, ProcessData TEXT, OperatorName TEXT, CreatedAt TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS IX_ProductionRecords_CreatedAt ON ProductionRecords(CreatedAt);
CREATE TABLE IF NOT EXISTS FaultLogs(Id INTEGER PRIMARY KEY AUTOINCREMENT, FaultCode TEXT, Node TEXT, Message TEXT NOT NULL, OperatorName TEXT, IsCleared INTEGER NOT NULL DEFAULT 0, CreatedAt TEXT NOT NULL, ClearedAt TEXT);
CREATE TABLE IF NOT EXISTS OperationLogs(Id INTEGER PRIMARY KEY AUTOINCREMENT, UserName TEXT, Action TEXT NOT NULL, Detail TEXT, CreatedAt TEXT NOT NULL);";
                command.ExecuteNonQuery();
            }
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

        private void EnsureUser(string userName, string displayName, string password, UserRole role)
        {
            Execute("INSERT OR IGNORE INTO Users(UserName,DisplayName,PasswordHash,Role,Enabled,CreatedAt) VALUES(@a,@b,@c,@d,1,@e)", userName, displayName, Hash(password), (int)role, DateTime.Now.ToString("s"));
        }

        private SQLiteConnection Open() { SQLiteConnection c = new SQLiteConnection(_connectionString); c.Open(); return c; }

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
