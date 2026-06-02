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

        public AlternateRaceSystemMetadata? AlternateSystem { get; set; }
    }

    public sealed class AlternateRaceSystemMetadata
    {
        public string SystemKey { get; set; } = string.Empty;

        public SplitWars2NodeMetadata? SplitWars2 { get; set; }
    }

    public sealed class SplitWars2NodeMetadata
    {
        public double DotCenter { get; set; } = 0.0;

        public int DotDensity { get; set; } = 200;

        public double DotDown { get; set; } = 0.0;

        public double DotUp { get; set; } = 10.0;

        public int HyperbolaC { get; set; } = 12;

        public bool IsGoal { get; set; }

        public bool IsStart { get; set; }

        public int TriggerType { get; set; } = SplitWars2TriggerTypes.Circle;

        public double PlaneAngle { get; set; } = 0.0;

        public double RadiusWidth { get; set; } = 10.0;
    }

    public static class AlternateRaceSystemKeys
    {
        public const string SplitWars2 = "split-wars-2";
    }

    public static class SplitWars2TriggerTypes
    {
        public const int Circle = 0;
        public const int Square = 1;
        public const int MapChange = 2;
        public const int Interact = 3;
        public const int Combat = 4;
    }
}
