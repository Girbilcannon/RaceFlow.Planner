using RaceFlow.Planner.Core;

namespace RaceFlow.Planner.Planner
{
    public sealed class RaceNodeMetadata
    {
        public string SegmentId { get; set; } = string.Empty;

        public int RacePath { get; set; } = 1;

        public int SegmentOrder { get; set; } = 1;

        public string SegmentName { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public bool IsDisabled { get; set; }

        public int BackdropColorArgb { get; set; }

        public RaceFlowNodeType NodeType { get; set; } = RaceFlowNodeType.Checkpoint;

        public RaceFlowSide Side { get; set; } = RaceFlowSide.Left;

        public RaceFlowDirection Direction { get; set; } = RaceFlowDirection.BottomToTop;

        public bool IsEndOfRace { get; set; }

        public RaceFlowFinishMode FinishMode { get; set; } = RaceFlowFinishMode.AutoFinish;

        public int LoopCount { get; set; } = 3;

        public int LoopCheckpointRequirement { get; set; } = 3;

        public double Radius { get; set; } = 5.0;

        public double Angle { get; set; } = 360.0;

        public string MapId { get; set; } = string.Empty;

        public double WorldX { get; set; }

        public double WorldY { get; set; }

        public double WorldZ { get; set; }

        public string RuntimeLabel { get; set; } = string.Empty;
    }
}