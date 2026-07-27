using System;

namespace SiliconSteelAdhesionTester.Models
{
    public sealed class InspectionRecord
    {
        public long Id { get; set; }
        public string QrCodeContent { get; set; }
        public string MaterialType { get; set; }
        public double? LossRatePercent { get; set; }
        public int? ParticleCount { get; set; }
        public bool? IsQualified { get; set; }
        public string ImagePath { get; set; }
        public string OperatorName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class SystemLogRecord
    {
        public string Category { get; set; }
        public string CodeOrAction { get; set; }
        public string Node { get; set; }
        public string Message { get; set; }
        public string UserName { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
