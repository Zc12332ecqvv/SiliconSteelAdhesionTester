using System.Drawing;

namespace SiliconSteelAdhesionTester.Services.Vision
{
    public enum AdhesionTestType
    {
        OrientedBeforeAfter,
        NonOrientedTape
    }

    public sealed class AdhesionVisionResult
    {
        public AdhesionTestType TestType { get; set; }
        public double LossRatePercent { get; set; }
        public bool IsQualified { get; set; }
        public int DefectPixelCount { get; set; }
        public int InspectionPixelCount { get; set; }
        public int ParticleCount { get; set; }
        public string MaskImagePath { get; set; }
        public string AnnotatedImagePath { get; set; }
        public string Message { get; set; }
    }

    public interface IAdhesionVisionService
    {
        AdhesionVisionResult AnalyzeOriented(
            string beforeBendingImagePath,
            string afterBendingImagePath,
            Rectangle? inspectionRegion = null,
            string sampleId = null);

        AdhesionVisionResult AnalyzeNonOrientedTape(
            string tapeImagePath,
            Rectangle? inspectionRegion = null,
            string sampleId = null);
    }
}
