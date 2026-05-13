using System;
using System.Collections.Generic;
using System.Linq;
using RaceFlow.Planner.Core;
using RaceFlow.Planner.Models;

namespace RaceFlow.Planner.Export
{
    public static class FlowMapExportValidator
    {
        public static List<string> Validate(GraphDocument document)
        {
            var issues = new List<string>();

            if (document.Nodes.Count == 0)
            {
                issues.Add("Project has no nodes.");
                return issues;
            }

            var enabledNodes = document.Nodes
                .Where(n => n.Metadata == null || !n.Metadata.IsDisabled)
                .ToList();

            int disabledCount = document.Nodes.Count - enabledNodes.Count;
            if (disabledCount > 0)
                issues.Add($"{disabledCount} disabled node(s) will be ignored by export.");

            var startNodes = enabledNodes
                .Where(n => n.Metadata.NodeType == RaceFlowNodeType.Start)
                .ToList();

            if (startNodes.Count == 0)
                issues.Add("No enabled New Segment / Start nodes found.");

            var finalNodes = enabledNodes
                .Where(n => n.Metadata.NodeType == RaceFlowNodeType.Final)
                .ToList();

            if (finalNodes.Count == 0)
                issues.Add("No enabled Final node found.");

            foreach (var finalNode in finalNodes)
            {
                bool hasOutgoing = document.Connections.Any(c => c.FromNodeId == finalNode.Id);
                if (hasOutgoing)
                    issues.Add($"Final node '{finalNode.Title}' has outgoing connections.");
            }

            foreach (var node in enabledNodes)
            {
                if (string.IsNullOrWhiteSpace(node.Metadata.MapId))
                    issues.Add($"Node '{node.Title}' has no Map ID.");

                if (node.Metadata.Radius <= 0)
                    issues.Add($"Node '{node.Title}' has checkpoint radius <= 0.");

                if (node.Metadata.Angle != -1 && (node.Metadata.Angle < 0 || node.Metadata.Angle > 360))
                    issues.Add($"Node '{node.Title}' has checkpoint angle outside -1 or 0-360.");
            }

            foreach (var split in enabledNodes.Where(n => n.Metadata.NodeType == RaceFlowNodeType.Split))
            {
                int outputCount = document.Connections.Count(c => c.FromNodeId == split.Id);
                if (outputCount < 2)
                    issues.Add($"Split '{split.Title}' should have at least 2 outgoing paths.");
            }

            foreach (var converge in enabledNodes.Where(n => n.Metadata.NodeType == RaceFlowNodeType.Converge))
            {
                int inputCount = document.Connections.Count(c => c.ToNodeId == converge.Id);
                if (inputCount < 2)
                    issues.Add($"Converge '{converge.Title}' should have at least 2 incoming paths.");
            }

            return issues;
        }
    }
}
