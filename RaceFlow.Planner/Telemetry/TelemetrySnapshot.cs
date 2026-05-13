using System;

namespace RaceFlow.Planner.Telemetry
{
    public sealed class TelemetrySnapshot
    {
        public bool Available { get; set; }
        public bool GameRunning { get; set; }
        public bool TelemetryReady { get; set; }
        public string FailureReason { get; set; } = string.Empty;
        public DateTime LastUpdateUtc { get; set; }
        public int? MapId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public bool HasUsableTelemetry =>
            Available &&
            GameRunning &&
            TelemetryReady &&
            MapId.HasValue;

        public bool IsLive =>
            HasUsableTelemetry &&
            LastUpdateUtc != default &&
            DateTime.UtcNow - LastUpdateUtc <= TimeSpan.FromSeconds(3);
    }
}
