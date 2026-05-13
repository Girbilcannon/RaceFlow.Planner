using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RaceFlow.Planner.Export
{
    public sealed class FlowMapExportDocument
    {
        [JsonPropertyName("format")]
        public string Format { get; set; } = "flowmap";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "2.0.0";

        [JsonPropertyName("layoutName")]
        public string LayoutName { get; set; } = string.Empty;

        [JsonPropertyName("exportedAtUtc")]
        public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("sections")]
        public List<FlowMapExportSection> Sections { get; set; } = new();

        [JsonPropertyName("admin")]
        public FlowMapExportAdmin Admin { get; set; } = new();

        [JsonPropertyName("race")]
        public FlowMapExportRace Race { get; set; } = new();
    }

    public sealed class FlowMapExportAdmin
    {
        [JsonPropertyName("globalTriggerScale")]
        public double GlobalTriggerScale { get; set; } = 1.0;

        [JsonPropertyName("themeFile")]
        public string ThemeFile { get; set; } = string.Empty;

        [JsonPropertyName("playbackDelayMs")]
        public int PlaybackDelayMs { get; set; }

        [JsonPropertyName("outputScale")]
        public float OutputScale { get; set; } = 1.0f;

        [JsonPropertyName("outputOffsetX")]
        public float OutputOffsetX { get; set; } = 0f;

        [JsonPropertyName("outputOffsetY")]
        public float OutputOffsetY { get; set; } = 0f;

        [JsonPropertyName("outputNodeTextScale")]
        public float OutputNodeTextScale { get; set; } = 1.0f;

        [JsonPropertyName("outputRacerTextScale")]
        public float OutputRacerTextScale { get; set; } = 1.0f;
    }

    public sealed class FlowMapExportRace
    {
        [JsonPropertyName("finishMode")]
        public string FinishMode { get; set; } = "auto-finish";

        [JsonPropertyName("totalLoops")]
        public int TotalLoops { get; set; } = 1;

        [JsonPropertyName("loopRetriggerCheckpointCount")]
        public int LoopRetriggerCheckpointCount { get; set; } = 3;
    }

    public sealed class FlowMapExportSection
    {
        [JsonPropertyName("side")]
        public string Side { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("direction")]
        public string Direction { get; set; } = string.Empty;

        [JsonPropertyName("visualScale")]
        public float VisualScale { get; set; } = 1.0f;

        [JsonPropertyName("offsetX")]
        public float OffsetX { get; set; }

        [JsonPropertyName("offsetY")]
        public float OffsetY { get; set; }

        [JsonPropertyName("segmentCount")]
        public int SegmentCount { get; set; }

        [JsonPropertyName("segments")]
        public List<FlowMapExportSegment> Segments { get; set; } = new();
    }

    public sealed class FlowMapExportSegment
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("racePath")]
        public int RacePath { get; set; } = 1;

        [JsonPropertyName("segmentOrder")]
        public int SegmentOrder { get; set; } = 1;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("side")]
        public string Side { get; set; } = string.Empty;

        [JsonPropertyName("direction")]
        public string Direction { get; set; } = string.Empty;

        [JsonPropertyName("visualScale")]
        public float VisualScale { get; set; } = 1.0f;

        [JsonPropertyName("offsetX")]
        public float OffsetX { get; set; }

        [JsonPropertyName("offsetY")]
        public float OffsetY { get; set; }

        [JsonPropertyName("pewFileName")]
        public string PewFileName { get; set; } = string.Empty;

        [JsonPropertyName("checkpointFileName")]
        public string CheckpointFileName { get; set; } = string.Empty;

        [JsonPropertyName("nodeCount")]
        public int NodeCount { get; set; }

        [JsonPropertyName("checkpointRecordCount")]
        public int CheckpointRecordCount { get; set; }

        [JsonPropertyName("nodes")]
        public List<FlowMapExportNode> Nodes { get; set; } = new();
    }

    public sealed class FlowMapExportNode
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("runtimeLabel")]
        public string RuntimeLabel { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("planner")]
        public FlowMapExportPlanner Planner { get; set; } = new();

        [JsonPropertyName("binding")]
        public FlowMapExportBinding Binding { get; set; } = new();

        [JsonPropertyName("telemetry")]
        public FlowMapExportTelemetry Telemetry { get; set; } = new();

        [JsonPropertyName("graph")]
        public FlowMapExportGraph Graph { get; set; } = new();
    }

    public sealed class FlowMapExportPlanner
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }
    }

    public sealed class FlowMapExportBinding
    {
        [JsonPropertyName("isBound")]
        public bool IsBound { get; set; }

        [JsonPropertyName("ignoreNode")]
        public bool IgnoreNode { get; set; }

        [JsonPropertyName("isEndOfRace")]
        public bool IsEndOfRace { get; set; }

        [JsonPropertyName("endOfRaceMode")]
        public string? EndOfRaceMode { get; set; }

        [JsonPropertyName("outputOverrideNodeIds")]
        public List<string> OutputOverrideNodeIds { get; set; } = new();
    }

    public sealed class FlowMapExportTelemetry
    {
        [JsonPropertyName("hasCheckpoint")]
        public bool HasCheckpoint { get; set; }

        [JsonPropertyName("step")]
        public int Step { get; set; }

        [JsonPropertyName("checkpointLabel")]
        public string CheckpointLabel { get; set; } = string.Empty;

        [JsonPropertyName("mapId")]
        public string MapId { get; set; } = string.Empty;

        [JsonPropertyName("position")]
        public FlowMapExportVector3 Position { get; set; } = new();

        [JsonPropertyName("trigger")]
        public FlowMapExportTrigger Trigger { get; set; } = new();

        [JsonPropertyName("note")]
        public string Note { get; set; } = string.Empty;

        [JsonPropertyName("checkpointType")]
        public string CheckpointType { get; set; } = string.Empty;
    }

    public sealed class FlowMapExportVector3
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("z")]
        public double Z { get; set; }
    }

    public sealed class FlowMapExportTrigger
    {
        [JsonPropertyName("radius")]
        public double Radius { get; set; }

        [JsonPropertyName("angle")]
        public double Angle { get; set; }
    }

    public sealed class FlowMapExportGraph
    {
        [JsonPropertyName("inputCount")]
        public int InputCount { get; set; }

        [JsonPropertyName("outputCount")]
        public int OutputCount { get; set; }

        [JsonPropertyName("graphInputs")]
        public List<FlowMapExportGraphInput> GraphInputs { get; set; } = new();

        [JsonPropertyName("graphOutputs")]
        public List<FlowMapExportGraphOutput> GraphOutputs { get; set; } = new();

        [JsonPropertyName("runtimeOutputNodeIds")]
        public List<string> RuntimeOutputNodeIds { get; set; } = new();
    }

    public sealed class FlowMapExportGraphInput
    {
        [JsonPropertyName("fromNodeId")]
        public string FromNodeId { get; set; } = string.Empty;

        [JsonPropertyName("fromNodeTitle")]
        public string FromNodeTitle { get; set; } = string.Empty;

        [JsonPropertyName("fromSocketIndex")]
        public int FromSocketIndex { get; set; }

        [JsonPropertyName("toSocketIndex")]
        public int ToSocketIndex { get; set; }
    }

    public sealed class FlowMapExportGraphOutput
    {
        [JsonPropertyName("toNodeId")]
        public string ToNodeId { get; set; } = string.Empty;

        [JsonPropertyName("toNodeTitle")]
        public string ToNodeTitle { get; set; } = string.Empty;

        [JsonPropertyName("fromSocketIndex")]
        public int FromSocketIndex { get; set; }

        [JsonPropertyName("toSocketIndex")]
        public int ToSocketIndex { get; set; }
    }
}
