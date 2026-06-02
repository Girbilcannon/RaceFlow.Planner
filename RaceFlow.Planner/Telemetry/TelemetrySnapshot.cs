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

        public bool HasCameraData { get; set; }

        public double CameraX { get; set; }
        public double CameraY { get; set; }
        public double CameraZ { get; set; }

        public double CameraFrontX { get; set; }
        public double CameraFrontY { get; set; }
        public double CameraFrontZ { get; set; }

        public double CameraTopX { get; set; }
        public double CameraTopY { get; set; }
        public double CameraTopZ { get; set; }

        public double Fov { get; set; }

        public bool HasUsableCamera =>
            HasUsableTelemetry &&
            HasCameraData &&
            Fov > 0 &&
            IsFinite(CameraX) &&
            IsFinite(CameraY) &&
            IsFinite(CameraZ) &&
            IsFinite(CameraFrontX) &&
            IsFinite(CameraFrontY) &&
            IsFinite(CameraFrontZ);

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

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
