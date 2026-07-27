using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Xml;

namespace SiliconSteelAdhesionTester.Configuration
{
    public sealed class AppSettings
    {
        public string PlcIp { get; set; }
        public int PlcPort { get; set; }
        public short Rack { get; set; }
        public short Slot { get; set; }
        public int PollIntervalMs { get; set; }
        public int CommandPulseMs { get; set; }
        public int DuplicateQrCodeSeconds { get; set; }
        public bool QrCodeScannerEnabled { get; set; }
        public int QrCodeInputTimeoutMs { get; set; }
        public int QrCodeMinimumLength { get; set; }
        public string OrientedScannerIp { get; set; }
        public int OrientedScannerPort { get; set; }
        public string NonOrientedScannerIp { get; set; }
        public int NonOrientedScannerPort { get; set; }
        public int ScannerConnectTimeoutMs { get; set; }
        public int ScannerReadTimeoutMs { get; set; }
        public string ScannerTriggerCommand { get; set; }
        public string ScannerStopCommand { get; set; }
        public string ScannerTerminator { get; set; }
        public double OrientedMaxLossRate { get; set; }
        public double NonOrientedMaxLossRate { get; set; }
        public int VisionDifferenceThreshold { get; set; }
        public int VisionMinimumParticleArea { get; set; }
        public string VisionOutputDirectory { get; set; }
        public string SiteName { get; set; }
        public string DeviceName { get; set; }
        public string DeviceCode { get; set; }
        public string LimsEndpoint { get; set; }
        public bool Simulation { get; set; }

        public static string OverrideFilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "SystemSettings.xml"); }
        }

        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings
            {
                PlcIp = Read("PlcIp", "192.168.3.2"),
                PlcPort = int.Parse(Read("PlcPort", "502")),
                Rack = short.Parse(Read("PlcRack", "0")),
                Slot = short.Parse(Read("PlcSlot", "1")),
                PollIntervalMs = int.Parse(Read("PollIntervalMs", "250")),
                CommandPulseMs = int.Parse(Read("CommandPulseMs", "300")),
                DuplicateQrCodeSeconds = int.Parse(ReadCompatible("DuplicateQrCodeSeconds", "DuplicateBarcodeSeconds", "30")),
                QrCodeScannerEnabled = bool.Parse(ReadCompatible("QrCodeScannerEnabled", "BarcodeScannerEnabled", "true")),
                QrCodeInputTimeoutMs = int.Parse(ReadCompatible("QrCodeInputTimeoutMs", "BarcodeInputTimeoutMs", "120")),
                QrCodeMinimumLength = int.Parse(ReadCompatible("QrCodeMinimumLength", "BarcodeMinimumLength", "4")),
                OrientedScannerIp = Read("OrientedScannerIp", "192.168.3.11"),
                OrientedScannerPort = int.Parse(Read("OrientedScannerPort", "9004")),
                NonOrientedScannerIp = Read("NonOrientedScannerIp", "192.168.3.12"),
                NonOrientedScannerPort = int.Parse(Read("NonOrientedScannerPort", "9004")),
                ScannerConnectTimeoutMs = int.Parse(Read("ScannerConnectTimeoutMs", "3000")),
                ScannerReadTimeoutMs = int.Parse(Read("ScannerReadTimeoutMs", "5000")),
                ScannerTriggerCommand = Read("ScannerTriggerCommand", string.Empty),
                ScannerStopCommand = Read("ScannerStopCommand", string.Empty),
                ScannerTerminator = Read("ScannerTerminator", "CR"),
                OrientedMaxLossRate = double.Parse(Read("OrientedMaxLossRate", "3.0"), CultureInfo.InvariantCulture),
                NonOrientedMaxLossRate = double.Parse(Read("NonOrientedMaxLossRate", "3.0"), CultureInfo.InvariantCulture),
                VisionDifferenceThreshold = int.Parse(Read("VisionDifferenceThreshold", "28")),
                VisionMinimumParticleArea = int.Parse(Read("VisionMinimumParticleArea", "20")),
                VisionOutputDirectory = Read("VisionOutputDirectory", "VisionResults"),
                SiteName = Read("SiteName", string.Empty),
                DeviceName = Read("DeviceName", "硅钢附着力测试仪"),
                DeviceCode = Read("DeviceCode", string.Empty),
                LimsEndpoint = Read("LimsEndpoint", string.Empty),
                Simulation = Read("PlcMode", "Simulation").Equals("Simulation", StringComparison.OrdinalIgnoreCase)
            };
            settings.ApplyOverrides();
            return settings;
        }

        public void SaveOverrides()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OverrideFilePath));
            string temporaryPath = OverrideFilePath + ".tmp";
            XmlWriterSettings writerSettings = new XmlWriterSettings { Indent = true };
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

        private void ApplyOverrides()
        {
            if (!File.Exists(OverrideFilePath)) return;
            XmlDocument document = new XmlDocument();
            try { document.Load(OverrideFilePath); }
            catch (XmlException) { return; }
            catch (IOException) { return; }
            XmlElement root = document.DocumentElement;
            if (root == null || root.Name != "SystemSettings") return;
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
            OrientedMaxLossRate = DoubleValue(root, "OrientedMaxLossRate", OrientedMaxLossRate);
            NonOrientedMaxLossRate = DoubleValue(root, "NonOrientedMaxLossRate", NonOrientedMaxLossRate);
            VisionDifferenceThreshold = IntValue(root, "VisionDifferenceThreshold", VisionDifferenceThreshold);
            VisionMinimumParticleArea = IntValue(root, "VisionMinimumParticleArea", VisionMinimumParticleArea);
            VisionOutputDirectory = Value(root, "VisionOutputDirectory", VisionOutputDirectory);
            SiteName = Value(root, "SiteName", SiteName);
            DeviceName = Value(root, "DeviceName", DeviceName);
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
    }
}
