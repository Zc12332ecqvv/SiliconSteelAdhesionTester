using System.Configuration;

namespace SiliconSteelAdhesionTester.Configuration
{
    public sealed class AppSettings
    {
        public string PlcIp { get; private set; }
        public short Rack { get; private set; }
        public short Slot { get; private set; }
        public int PollIntervalMs { get; private set; }
        public int CommandPulseMs { get; private set; }
        public int DuplicateBarcodeSeconds { get; private set; }
        public bool BarcodeScannerEnabled { get; private set; }
        public int BarcodeInputTimeoutMs { get; private set; }
        public int BarcodeMinimumLength { get; private set; }
        public double OrientedMaxLossRate { get; private set; }
        public double NonOrientedMaxLossRate { get; private set; }
        public int VisionDifferenceThreshold { get; private set; }
        public int VisionMinimumParticleArea { get; private set; }
        public string VisionOutputDirectory { get; private set; }
        public bool Simulation { get; private set; }

        public static AppSettings Load()
        {
            return new AppSettings
            {
                PlcIp = Read("PlcIp", "192.168.3.2"),
                Rack = short.Parse(Read("PlcRack", "0")),
                Slot = short.Parse(Read("PlcSlot", "1")),
                PollIntervalMs = int.Parse(Read("PollIntervalMs", "250")),
                CommandPulseMs = int.Parse(Read("CommandPulseMs", "300")),
                DuplicateBarcodeSeconds = int.Parse(Read("DuplicateBarcodeSeconds", "30")),
                BarcodeScannerEnabled = bool.Parse(Read("BarcodeScannerEnabled", "true")),
                BarcodeInputTimeoutMs = int.Parse(Read("BarcodeInputTimeoutMs", "120")),
                BarcodeMinimumLength = int.Parse(Read("BarcodeMinimumLength", "4")),
                OrientedMaxLossRate = double.Parse(Read("OrientedMaxLossRate", "3.0"), System.Globalization.CultureInfo.InvariantCulture),
                NonOrientedMaxLossRate = double.Parse(Read("NonOrientedMaxLossRate", "3.0"), System.Globalization.CultureInfo.InvariantCulture),
                VisionDifferenceThreshold = int.Parse(Read("VisionDifferenceThreshold", "28")),
                VisionMinimumParticleArea = int.Parse(Read("VisionMinimumParticleArea", "20")),
                VisionOutputDirectory = Read("VisionOutputDirectory", "VisionResults"),
                Simulation = Read("PlcMode", "Simulation").Equals("Simulation", System.StringComparison.OrdinalIgnoreCase)
            };
        }

        private static string Read(string key, string fallback)
        {
            return ConfigurationManager.AppSettings[key] ?? fallback;
        }
    }
}
