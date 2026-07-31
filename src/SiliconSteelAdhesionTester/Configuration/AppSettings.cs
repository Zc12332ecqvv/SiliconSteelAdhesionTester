using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Xml;

namespace SiliconSteelAdhesionTester.Configuration
{
    public sealed class AppSettings
    {
        //plc通讯（s7西门子）
        public string PlcIp { get; set; }
        public int PlcPort { get; set; }
        public short Rack { get; set; }
        public short Slot { get; set; }
        public int PollIntervalMs { get; set; }
        public int CommandPulseMs { get; set; }
        public bool AutomaticDeviceInteractionsEnabled { get; set; }
        public bool Simulation { get; set; }
        //二维码扫码模块
        public bool QrCodeScannerEnabled { get; set; }
        public int QrCodeInputTimeoutMs { get; set; }
        public int QrCodeMinimumLength { get; set; }
        public int DuplicateQrCodeSeconds { get; set; }
        public string OrientedScannerIp { get; set; }
        public string NonOrientedScannerIp { get; set; }
        public int OrientedScannerPort { get; set; }
        public int NonOrientedScannerPort { get; set; }
        public int ScannerConnectTimeoutMs { get; set; }
        public int ScannerReadTimeoutMs { get; set; }
        public string ScannerTriggerCommand { get; set; }
        public string ScannerStopCommand { get; set; }
        public string ScannerTerminator { get; set; }
        //相机采集配置
        public string CameraProvider { get; set; }
        public string CameraIp { get; set; }
        public string OrientedCameraIp { get; set; }
        public string NonOrientedCameraIp { get; set; }
        public int CameraCaptureTimeoutMs { get; set; }
        public int CameraFileStableMs { get; set; }
        public string CameraInputDirectory { get; set; }
        //视觉算法参数
        public double OrientedMaxLossRate { get; set; }
        public double NonOrientedMaxLossRate { get; set; }
        public int VisionDifferenceThreshold { get; set; }
        public int VisionMinimumParticleArea { get; set; }
        public string VisionOutputDirectory { get; set; }
        //工厂信息
        public string SiteName { get; set; }
        public string DeviceName { get; set; }
        public string DeviceCode { get; set; }
        public string LimsEndpoint { get; set; }

        public static string OverrideDirectoryPath
        {
            get { return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Data")); }
        }

        public static string OverrideFilePath
        {
            get { return Path.Combine(OverrideDirectoryPath, "SystemSettings.xml"); }
        }

        public static string LastLoadWarning { get; private set; }

        public static AppSettings Load()
        {
            LastLoadWarning = null;
            AppSettings settings = new AppSettings
            {
                PlcIp = Read("PlcIp", "192.168.3.2"),
                PlcPort = ReadInt("PlcPort", 502),
                Rack = ReadShort("PlcRack", 0),
                Slot = ReadShort("PlcSlot", 1),
                PollIntervalMs = ReadInt("PollIntervalMs", 250),
                CommandPulseMs = ReadInt("CommandPulseMs", 300),
                DuplicateQrCodeSeconds = ReadCompatibleInt("DuplicateQrCodeSeconds", "DuplicateBarcodeSeconds", 30),
                QrCodeScannerEnabled = ReadCompatibleBool("QrCodeScannerEnabled", "BarcodeScannerEnabled", true),
                QrCodeInputTimeoutMs = ReadCompatibleInt("QrCodeInputTimeoutMs", "BarcodeInputTimeoutMs", 120),
                QrCodeMinimumLength = ReadCompatibleInt("QrCodeMinimumLength", "BarcodeMinimumLength", 4),
                OrientedScannerIp = Read("OrientedScannerIp", "192.168.3.11"),
                OrientedScannerPort = ReadInt("OrientedScannerPort", 9004),
                NonOrientedScannerIp = Read("NonOrientedScannerIp", "192.168.3.12"),
                NonOrientedScannerPort = ReadInt("NonOrientedScannerPort", 9004),
                ScannerConnectTimeoutMs = ReadInt("ScannerConnectTimeoutMs", 3000),
                ScannerReadTimeoutMs = ReadInt("ScannerReadTimeoutMs", 5000),
                ScannerTriggerCommand = Read("ScannerTriggerCommand", string.Empty),
                ScannerStopCommand = Read("ScannerStopCommand", string.Empty),
                ScannerTerminator = Read("ScannerTerminator", "CR"),
                AutomaticDeviceInteractionsEnabled = ReadBool("AutomaticDeviceInteractionsEnabled", true),
                CameraInputDirectory = Read("CameraInputDirectory", "CameraInput"),
                CameraProvider = Read("CameraProvider", "Folder"),
                CameraIp = Read("CameraIp", string.Empty),
                OrientedCameraIp = Read("OrientedCameraIp", Read("CameraIp", string.Empty)),
                NonOrientedCameraIp = Read("NonOrientedCameraIp", Read("CameraIp", string.Empty)),
                CameraCaptureTimeoutMs = ReadInt("CameraCaptureTimeoutMs", 10000),
                CameraFileStableMs = ReadInt("CameraFileStableMs", 300),
                OrientedMaxLossRate = ReadDouble("OrientedMaxLossRate", 3.0),
                NonOrientedMaxLossRate = ReadDouble("NonOrientedMaxLossRate", 3.0),
                VisionDifferenceThreshold = ReadInt("VisionDifferenceThreshold", 28),
                VisionMinimumParticleArea = ReadInt("VisionMinimumParticleArea", 20),
                VisionOutputDirectory = Read("VisionOutputDirectory", "VisionResults"),
                SiteName = Read("SiteName", string.Empty),
                DeviceName = Read("DeviceName", "自动涂层附着力测试仪"),
                DeviceCode = Read("DeviceCode", string.Empty),
                LimsEndpoint = Read("LimsEndpoint", string.Empty),
                Simulation = Read("PlcMode", "Simulation").Equals("Simulation", StringComparison.OrdinalIgnoreCase)
            };
            settings.ApplyOverrides();
#if SIMULATION_ONLY
            settings.Simulation = true;
#endif
            if (!File.Exists(OverrideFilePath))
            {
                try
                {
                    settings.SaveOverrides();
                }
                catch (IOException ex)
                {
                    LastLoadWarning = "首次创建设置文件失败：" + ex.Message;
                }
                catch (UnauthorizedAccessException ex)
                {
                    LastLoadWarning = "没有权限创建设置文件：" + ex.Message;
                }
            }
            return settings;
        }

        // 保存可由现场人员修改的覆盖配置；App.config仍作为缺省值来源。
        public void SaveOverrides()
        {
            Directory.CreateDirectory(OverrideDirectoryPath);
            string temporaryPath = OverrideFilePath + ".tmp";
            XmlWriterSettings writerSettings = new XmlWriterSettings { Indent = true };
            try
            {
                using (XmlWriter writer = XmlWriter.Create(temporaryPath, writerSettings))
                {
                    writer.WriteStartElement("SystemSettings");
                    Write(writer, "PlcMode", Simulation ? "Simulation" : "S7");
                    Write(writer, "PlcIp", PlcIp);
                    Write(writer, "PlcPort", PlcPort);
                    Write(writer, "PlcRack", Rack);
                    Write(writer, "PlcSlot", Slot);
                    Write(writer, "PollIntervalMs", PollIntervalMs);
                    Write(writer, "CommandPulseMs", CommandPulseMs);
                    Write(writer, "QrCodeScannerEnabled", QrCodeScannerEnabled);
                    Write(writer, "QrCodeInputTimeoutMs", QrCodeInputTimeoutMs);
                    Write(writer, "QrCodeMinimumLength", QrCodeMinimumLength);
                    Write(writer, "DuplicateQrCodeSeconds", DuplicateQrCodeSeconds);
                    Write(writer, "OrientedScannerIp", OrientedScannerIp);
                    Write(writer, "OrientedScannerPort", OrientedScannerPort);
                    Write(writer, "NonOrientedScannerIp", NonOrientedScannerIp);
                    Write(writer, "NonOrientedScannerPort", NonOrientedScannerPort);
                    Write(writer, "ScannerConnectTimeoutMs", ScannerConnectTimeoutMs);
                    Write(writer, "ScannerReadTimeoutMs", ScannerReadTimeoutMs);
                    Write(writer, "ScannerTriggerCommand", ScannerTriggerCommand);
                    Write(writer, "ScannerStopCommand", ScannerStopCommand);
                    Write(writer, "ScannerTerminator", ScannerTerminator);
                    Write(writer, "AutomaticDeviceInteractionsEnabled", AutomaticDeviceInteractionsEnabled);
                    Write(writer, "CameraInputDirectory", CameraInputDirectory);
                    Write(writer, "CameraProvider", CameraProvider);
                    Write(writer, "OrientedCameraIp", OrientedCameraIp);
                    Write(writer, "NonOrientedCameraIp", NonOrientedCameraIp);
                    Write(writer, "CameraCaptureTimeoutMs", CameraCaptureTimeoutMs);
                    Write(writer, "CameraFileStableMs", CameraFileStableMs);
                    Write(writer, "OrientedMaxLossRate", OrientedMaxLossRate.ToString(CultureInfo.InvariantCulture));
                    Write(writer, "NonOrientedMaxLossRate", NonOrientedMaxLossRate.ToString(CultureInfo.InvariantCulture));
                    Write(writer, "VisionDifferenceThreshold", VisionDifferenceThreshold);
                    Write(writer, "VisionMinimumParticleArea", VisionMinimumParticleArea);
                    Write(writer, "VisionOutputDirectory", VisionOutputDirectory);
                    Write(writer, "SiteName", SiteName);
                    Write(writer, "DeviceName", DeviceName);
                    Write(writer, "DeviceCode", DeviceCode);
                    Write(writer, "LimsEndpoint", LimsEndpoint);
                    writer.WriteEndElement();
                }
                if (File.Exists(OverrideFilePath))
                    File.Replace(temporaryPath, OverrideFilePath, OverrideFilePath + ".bak", true);
                else
                    File.Move(temporaryPath, OverrideFilePath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private void ApplyOverrides()
        {
            if (!File.Exists(OverrideFilePath)) return;
            XmlDocument document = new XmlDocument();
            try { document.Load(OverrideFilePath); }
            catch (XmlException ex)
            {
                LastLoadWarning = "设置文件XML格式无效，已使用默认值：" + ex.Message;
                return;
            }
            catch (IOException ex)
            {
                LastLoadWarning = "设置文件读取失败，已使用默认值：" + ex.Message;
                return;
            }
            XmlElement root = document.DocumentElement;
            if (root == null || root.Name != "SystemSettings")
            {
                LastLoadWarning = "设置文件根节点必须是SystemSettings，已使用默认值。";
                return;
            }
            PlcIp = Value(root, "PlcIp", PlcIp);
            PlcPort = IntValue(root, "PlcPort", PlcPort);
            Rack = (short)IntValue(root, "PlcRack", Rack);
            Slot = (short)IntValue(root, "PlcSlot", Slot);
            PollIntervalMs = IntValue(root, "PollIntervalMs", PollIntervalMs);
            CommandPulseMs = IntValue(root, "CommandPulseMs", CommandPulseMs);
            QrCodeScannerEnabled = BoolValue(root, "QrCodeScannerEnabled", BoolValue(root, "BarcodeScannerEnabled", QrCodeScannerEnabled));
            QrCodeInputTimeoutMs = IntValue(root, "QrCodeInputTimeoutMs", IntValue(root, "BarcodeInputTimeoutMs", QrCodeInputTimeoutMs));
            QrCodeMinimumLength = IntValue(root, "QrCodeMinimumLength", IntValue(root, "BarcodeMinimumLength", QrCodeMinimumLength));
            DuplicateQrCodeSeconds = IntValue(root, "DuplicateQrCodeSeconds", IntValue(root, "DuplicateBarcodeSeconds", DuplicateQrCodeSeconds));
            OrientedScannerIp = Value(root, "OrientedScannerIp", OrientedScannerIp);
            OrientedScannerPort = IntValue(root, "OrientedScannerPort", OrientedScannerPort);
            NonOrientedScannerIp = Value(root, "NonOrientedScannerIp", NonOrientedScannerIp);
            NonOrientedScannerPort = IntValue(root, "NonOrientedScannerPort", NonOrientedScannerPort);
            ScannerConnectTimeoutMs = IntValue(root, "ScannerConnectTimeoutMs", ScannerConnectTimeoutMs);
            ScannerReadTimeoutMs = IntValue(root, "ScannerReadTimeoutMs", ScannerReadTimeoutMs);
            ScannerTriggerCommand = Value(root, "ScannerTriggerCommand", ScannerTriggerCommand);
            ScannerStopCommand = Value(root, "ScannerStopCommand", ScannerStopCommand);
            ScannerTerminator = Value(root, "ScannerTerminator", ScannerTerminator);
            AutomaticDeviceInteractionsEnabled = BoolValue(root, "AutomaticDeviceInteractionsEnabled", AutomaticDeviceInteractionsEnabled);
            CameraInputDirectory = Value(root, "CameraInputDirectory", CameraInputDirectory);
            CameraProvider = Value(root, "CameraProvider", CameraProvider);
            CameraIp = Value(root, "CameraIp", CameraIp);
            OrientedCameraIp = Value(root, "OrientedCameraIp", string.IsNullOrWhiteSpace(OrientedCameraIp) ? CameraIp : OrientedCameraIp);
            NonOrientedCameraIp = Value(root, "NonOrientedCameraIp", string.IsNullOrWhiteSpace(NonOrientedCameraIp) ? CameraIp : NonOrientedCameraIp);
            CameraCaptureTimeoutMs = IntValue(root, "CameraCaptureTimeoutMs", CameraCaptureTimeoutMs);
            CameraFileStableMs = IntValue(root, "CameraFileStableMs", CameraFileStableMs);
            OrientedMaxLossRate = DoubleValue(root, "OrientedMaxLossRate", OrientedMaxLossRate);
            NonOrientedMaxLossRate = DoubleValue(root, "NonOrientedMaxLossRate", NonOrientedMaxLossRate);
            VisionDifferenceThreshold = IntValue(root, "VisionDifferenceThreshold", VisionDifferenceThreshold);
            VisionMinimumParticleArea = IntValue(root, "VisionMinimumParticleArea", VisionMinimumParticleArea);
            VisionOutputDirectory = Value(root, "VisionOutputDirectory", VisionOutputDirectory);
            SiteName = Value(root, "SiteName", SiteName);
            DeviceName = Value(root, "DeviceName", DeviceName);
            if (DeviceName == "硅钢附着力测试仪")
                DeviceName = "自动涂层附着力测试仪";
            DeviceCode = Value(root, "DeviceCode", DeviceCode);
            LimsEndpoint = Value(root, "LimsEndpoint", LimsEndpoint);
            Simulation = Value(root, "PlcMode", Simulation ? "Simulation" : "S7")
                .Equals("Simulation", StringComparison.OrdinalIgnoreCase);
        }

        private static void Write(XmlWriter writer, string name, object value)
        {
            writer.WriteElementString(name, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
        }

        private static string Value(XmlElement root, string name, string fallback)
        {
            XmlNode node = root.SelectSingleNode(name);
            return node == null ? fallback : node.InnerText;
        }

        private static int IntValue(XmlElement root, string name, int fallback)
        {
            int value;
            return int.TryParse(Value(root, name, string.Empty), out value) ? value : fallback;
        }

        private static bool BoolValue(XmlElement root, string name, bool fallback)
        {
            bool value;
            return bool.TryParse(Value(root, name, string.Empty), out value) ? value : fallback;
        }

        private static double DoubleValue(XmlElement root, string name, double fallback)
        {
            double value;
            return double.TryParse(Value(root, name, string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? value
                : fallback;
        }

        private static string Read(string key, string fallback)
        {
            return ConfigurationManager.AppSettings[key] ?? fallback;
        }

        private static string ReadCompatible(string key, string legacyKey, string fallback)
        {
            return ConfigurationManager.AppSettings[key] ?? ConfigurationManager.AppSettings[legacyKey] ?? fallback;
        }

        private static int ReadInt(string key, int fallback)
        {
            int value;
            return int.TryParse(Read(key, string.Empty), out value) ? value : fallback;
        }

        private static int ReadCompatibleInt(string key, string legacyKey, int fallback)
        {
            int value;
            return int.TryParse(ReadCompatible(key, legacyKey, string.Empty), out value) ? value : fallback;
        }

        private static short ReadShort(string key, short fallback)
        {
            short value;
            return short.TryParse(Read(key, string.Empty), out value) ? value : fallback;
        }

        private static bool ReadBool(string key, bool fallback)
        {
            bool value;
            return bool.TryParse(Read(key, string.Empty), out value) ? value : fallback;
        }

        private static bool ReadCompatibleBool(string key, string legacyKey, bool fallback)
        {
            bool value;
            return bool.TryParse(ReadCompatible(key, legacyKey, string.Empty), out value) ? value : fallback;
        }

        private static double ReadDouble(string key, double fallback)
        {
            double value;
            return double.TryParse(Read(key, string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? value
                : fallback;
        }
    }
}
