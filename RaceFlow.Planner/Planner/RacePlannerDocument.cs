using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using RaceFlow.Planner.Models;
using RaceFlow.Planner.ThemeBuilder;

namespace RaceFlow.Planner.Planner
{
    public sealed class RacePlannerDocument
    {
        [JsonPropertyName("format")]
        public string Format { get; set; } = "raceflow-planner";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonPropertyName("savedAtUtc")]
        public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("graph")]
        public GraphDocument Graph { get; set; } = new();

        [JsonPropertyName("nodeMetadata")]
        public Dictionary<string, RaceNodeMetadata> NodeMetadata { get; set; } = new();

        [JsonPropertyName("tuning")]
        public FlowMapTuningData Tuning { get; set; } = new();

        [JsonPropertyName("theme")]
        public ThemeProject Theme { get; set; } = new();
    }
}