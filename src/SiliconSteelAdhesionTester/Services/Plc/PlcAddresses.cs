namespace SiliconSteelAdhesionTester.Services.Plc
{
    // PLC 地址来自《PC交互表20260708.xlsx》，采用 S7.NET 的 DBX/DBW/DBD 表达法。
    public static class PlcAddresses
    {
        public const string AutoMode = "DB5120.DBX62.1";       // 表中注明“取反”
        public const string EmergencyStop = "DB5120.DBX62.3"; // 表中注明“取反”
        public const string ResetPulse = "DB5120.DBX62.4";
        public const string LineStart = "DB5120.DBX64.0";
        public const string LinePause = "DB5120.DBX68.0";
        public const string LineHome = "DB5120.DBX72.0";
        public const string SimulationLineStop = "SIM.LINE.STOP";

        public static string StationStart(int station) { return "DB5120.DBX64." + station; }
        public static string StationPause(int station) { return "DB5120.DBX68." + station; }
        public static string StationHome(int station) { return "DB5120.DBX72." + station; }
        public static string StationContinuous(int station) { return "DB4120.DBX" + StationBase[station - 1] + ".5"; }

        public static readonly int[] StationBase = { 306, 572, 838, 1104 };
        public static string StationReady(int station) { return "DB4120.DBX" + StationBase[station - 1] + ".0"; }
        public static string StationHomeState(int station) { return "DB4120.DBX" + StationBase[station - 1] + ".1"; }
        public static string StationDone(int station) { return "DB4120.DBX" + StationBase[station - 1] + ".2"; }
        public static string StationStep(int station) { return "DB4120.DBW" + (StationBase[station - 1] + 2); }

        public const string S2ScanAllowed = "DB4120.DBX578.3";
        public const string S2ScanDone = "DB4120.DBX576.3";
        public const string S2ScanOk = "DB4120.DBX576.4";
        public const string S2ScanNg = "DB4120.DBX576.5";
        public const string S2CameraAllowed = "DB4120.DBX578.0";
        public const string S2CameraDone = "DB4120.DBX576.0";
        public const string S2CameraOk = "DB4120.DBX576.1";
        public const string S2CameraNg = "DB4120.DBX576.2";
        public const string S3ScanAllowed = "DB4120.DBX844.3";
        public const string S4CameraAllowed = "DB4120.DBX1110.0";
    }
}
