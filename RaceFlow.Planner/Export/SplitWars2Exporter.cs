using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using RaceFlow.Planner.Core;
using RaceFlow.Planner.Models;
using RaceFlow.Planner.Planner;

namespace RaceFlow.Planner.Export
{
    public static class SplitWars2Exporter
    {
        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true
        };

        public static int ExportToFile(GraphDocument document, string filePath)
        {
            ArgumentNullException.ThrowIfNull(document);

            List<SplitWars2CheckpointExport> route = Build(document);
            string json = JsonSerializer.Serialize(route, WriteOptions);
            File.WriteAllText(filePath, json);
            return route.Count;
        }

        public static List<SplitWars2CheckpointExport> Build(GraphDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            return document.Nodes
                .Where(ShouldExport)
                .OrderBy(n => n.Metadata.RacePath)
                .ThenBy(n => n.Metadata.SegmentOrder)
                .ThenBy(n => n.X)
                .ThenBy(n => n.Y)
                .Select(BuildCheckpoint)
                .ToList();
        }

        private static bool ShouldExport(GraphNode node)
        {
            if (node.Metadata.IsDisabled)
                return false;

            if (!string.Equals(node.Metadata.AlternateSystem?.SystemKey, AlternateRaceSystemKeys.SplitWars2, StringComparison.OrdinalIgnoreCase))
                return false;

            if (node.Metadata.AlternateSystem?.SplitWars2 == null)
                return false;

            // Split/converge are Planner-only flow helpers for SW2 authoring.
            return node.Metadata.NodeType != RaceFlowNodeType.Split &&
                   node.Metadata.NodeType != RaceFlowNodeType.Converge;
        }

        private static SplitWars2CheckpointExport BuildCheckpoint(GraphNode node)
        {
            SplitWars2NodeMetadata sw2 = node.Metadata.AlternateSystem!.SplitWars2!;

            return new SplitWars2CheckpointExport
            {
                DotCenter = sw2.DotCenter,
                DotDensity = sw2.DotDensity,
                DotDown = sw2.DotDown,
                DotUp = sw2.DotUp,
                HyperbolaC = sw2.HyperbolaC,
                IsGoal = sw2.IsGoal || node.Metadata.NodeType == RaceFlowNodeType.Final,
                IsStart = sw2.IsStart && node.Metadata.SegmentOrder == 1,
                MapId = ParseMapId(node.Metadata.MapId),
                Name = GetExportName(node),
                PlaneAngle = sw2.PlaneAngle,
                RadiusWidth = sw2.RadiusWidth > 0 ? sw2.RadiusWidth : node.Metadata.Radius,
                TriggerType = sw2.TriggerType,
                X = Math.Round(node.Metadata.WorldX, 6),
                Y = Math.Round(node.Metadata.WorldY, 6),
                Z = Math.Round(node.Metadata.WorldZ, 6)
            };
        }

        private static string GetExportName(GraphNode node)
        {
            if (!string.IsNullOrWhiteSpace(node.Metadata.DisplayName))
                return node.Metadata.DisplayName;

            if (!string.IsNullOrWhiteSpace(node.Title))
                return node.Title;

            return "Checkpoint";
        }

        private static int ParseMapId(string? raw)
        {
            if (int.TryParse(raw, out int mapId))
                return mapId;

            return 0;
        }
    }

    public sealed class SplitWars2CheckpointExport
    {
        [JsonPropertyName("dot_center")]
        public double DotCenter { get; set; }

        [JsonPropertyName("dot_density")]
        public int DotDensity { get; set; }

        [JsonPropertyName("dot_down")]
        public double DotDown { get; set; }

        [JsonPropertyName("dot_up")]
        public double DotUp { get; set; }

        [JsonPropertyName("hyperbola_c")]
        public int HyperbolaC { get; set; }

        [JsonPropertyName("is_goal")]
        public bool IsGoal { get; set; }

        [JsonPropertyName("is_start")]
        public bool IsStart { get; set; }

        [JsonPropertyName("mapid")]
        public int MapId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("plane_angle")]
        public double PlaneAngle { get; set; }

        [JsonPropertyName("radius_width")]
        public double RadiusWidth { get; set; }

        [JsonPropertyName("trigger_type")]
        public int TriggerType { get; set; }

        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("z")]
        public double Z { get; set; }
    }
}
