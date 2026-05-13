using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RaceFlow.Planner.Core;
using RaceFlow.Planner.Models;

namespace RaceFlow.Planner.Export
{
    public static class CheckpointXmlExporter
    {
        public static void ExportToFile(GraphDocument document, string filePath)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var checkpoints = document.Nodes
                .Where(n => n.Metadata != null && !n.Metadata.IsDisabled)
                .Where(n => ShouldExportCheckpoint(n.Metadata.NodeType))
                .OrderBy(n => n.Metadata.RacePath)
                .ThenBy(n => n.Metadata.SegmentOrder)
                .ThenBy(n => n.X)
                .ThenBy(n => n.Y)
                .ToList();

            string rootMapId = checkpoints
                .Select(n => n.Metadata.MapId)
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
                ?? string.Empty;

            var root = new XElement("Checkpoints",
                new XAttribute("mapId", rootMapId));

            for (int i = 0; i < checkpoints.Count; i++)
            {
                GraphNode node = checkpoints[i];
                string label = string.IsNullOrWhiteSpace(node.Metadata.DisplayName)
                    ? node.Title
                    : node.Metadata.DisplayName;

                root.Add(new XElement("checkpoint",
                    new XAttribute("index", i + 1),
                    new XAttribute("label", label),
                    new XAttribute("mapId", node.Metadata.MapId ?? string.Empty),
                    new XAttribute("x", FormatDouble(node.Metadata.WorldX)),
                    new XAttribute("y", FormatDouble(node.Metadata.WorldY)),
                    new XAttribute("z", FormatDouble(node.Metadata.WorldZ)),
                    new XAttribute("timestamp", string.Empty),
                    new XAttribute("radius", FormatDouble(node.Metadata.Radius)),
                    new XAttribute("angle", FormatDouble(node.Metadata.Angle)),
                    new XAttribute("note", node.Notes ?? string.Empty)));
            }

            var documentXml = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                root);

            documentXml.Save(filePath);
        }

        private static bool ShouldExportCheckpoint(RaceFlowNodeType nodeType)
        {
            return nodeType == RaceFlowNodeType.Start ||
                   nodeType == RaceFlowNodeType.Checkpoint ||
                   nodeType == RaceFlowNodeType.EndSegment ||
                   nodeType == RaceFlowNodeType.Final;
        }

        private static string FormatDouble(double value)
        {
            return Math.Round(value, 6).ToString("0.######", CultureInfo.InvariantCulture);
        }
    }
}
