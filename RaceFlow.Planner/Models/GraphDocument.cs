using System.Collections.Generic;
using System.Text.Json.Serialization;
using RaceFlow.Planner.Core;
using RaceFlow.Planner.ThemeBuilder;

namespace RaceFlow.Planner.Models
{
    public class GraphDocument
    {
        public List<GraphNode> Nodes { get; set; } = new();
        public List<NodeConnection> Connections { get; set; } = new();

        [JsonIgnore]
        public FlowMapTuningData Tuning { get; set; } = new();

        [JsonIgnore]
        public ThemeProject Theme { get; set; } = new();
    }

    public sealed class FlowMapTuningData
    {
        public FlowMapAdminTuning Admin { get; set; } = new();
        public Dictionary<string, FlowMapLayoutTuning> Sections { get; set; } = new();
        public Dictionary<string, FlowMapLayoutTuning> Segments { get; set; } = new();

        public FlowMapLayoutTuning GetSection(RaceFlowSide side)
        {
            string key = side.ToString().ToLowerInvariant();
            if (!Sections.TryGetValue(key, out FlowMapLayoutTuning? tuning))
            {
                tuning = new FlowMapLayoutTuning();
                Sections[key] = tuning;
            }
            return tuning;
        }

        public FlowMapLayoutTuning GetSegment(string segmentId)
        {
            if (string.IsNullOrWhiteSpace(segmentId))
                segmentId = "unknown";

            if (!Segments.TryGetValue(segmentId, out FlowMapLayoutTuning? tuning))
            {
                tuning = new FlowMapLayoutTuning();
                Segments[segmentId] = tuning;
            }
            return tuning;
        }
    }

    public sealed class FlowMapAdminTuning
    {
        public float OutputScale { get; set; } = 1.0f;
        public float OutputOffsetX { get; set; } = 0f;
        public float OutputOffsetY { get; set; } = 0f;
        public float OutputNodeTextScale { get; set; } = 1.0f;
        public float OutputRacerTextScale { get; set; } = 1.0f;
    }

    public sealed class FlowMapLayoutTuning
    {
        public float VisualScale { get; set; } = 1.0f;
        public float OffsetX { get; set; } = 0f;
        public float OffsetY { get; set; } = 0f;
    }
}
