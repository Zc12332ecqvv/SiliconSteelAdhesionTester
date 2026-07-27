using System;

namespace SiliconSteelAdhesionTester.Models
{
    public enum UserRole { Operator = 1, Engineer = 2, SuperAdmin = 3 }
    public enum StationState { Idle, Ready, Running, Completed, Fault }

    public sealed class UserSession
    {
        public long Id { get; set; }
        public string UserName { get; set; }
        public string DisplayName { get; set; }
        public UserRole Role { get; set; }
        public bool CanDebug { get { return Role >= UserRole.Engineer; } }
        public bool IsSuperAdmin { get { return Role == UserRole.SuperAdmin; } }
    }

    public sealed class StationSnapshot
    {
        public int Number { get; set; }
        public bool Ready { get; set; }
        public bool Home { get; set; }
        public bool Done { get; set; }
        public bool Fault { get; set; }
        public bool Running { get; set; }
        public short Step { get; set; }
        public StationState State
        {
            get
            {
                if (Fault) return StationState.Fault;
                if (Done) return StationState.Completed;
                if (Running) return StationState.Running;
                return Ready ? StationState.Ready : StationState.Idle;
            }
        }
    }

    public sealed class PlcSnapshot : EventArgs
    {
        public bool Connected { get; set; }
        public bool Automatic { get; set; }
        public bool EmergencyStop { get; set; }
        public DateTime Timestamp { get; set; }
        public StationSnapshot[] Stations { get; set; }
        public string QrCodeContent { get; set; }
        public int TotalCount { get; set; }
        public int ShiftCount { get; set; }
        public int FlowStepIndex { get; set; }
        public bool FlowPaused { get; set; }
        public bool FlowFault { get; set; }
        public string FlowMessage { get; set; }
        public bool S2ScanAllowed { get; set; }
        public bool S3ScanAllowed { get; set; }
    }
}
