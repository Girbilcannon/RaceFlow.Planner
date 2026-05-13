using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RaceFlow.Planner.Core;
using RaceFlow.Planner.Models;

namespace RaceFlow.Planner.Export
{
    public static class FlowMapExporter
    {
        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true
        };

        public static void ExportToFile(GraphDocument document, string filePath, string layoutName = "")
        {
            FlowMapExportDocument exportDocument = Build(document, layoutName);
            string json = JsonSerializer.Serialize(exportDocument, WriteOptions);
            File.WriteAllText(filePath, json);
        }

        public static FlowMapExportDocument Build(GraphDocument document, string layoutName = "")
        {
            var exportDocument = new FlowMapExportDocument
            {
                LayoutName = layoutName,
                ExportedAtUtc = DateTime.UtcNow
            };

            if (document.Tuning != null)
            {
                exportDocument.Admin.OutputScale = document.Tuning.Admin.OutputScale;
                exportDocument.Admin.OutputOffsetX = document.Tuning.Admin.OutputOffsetX;
                exportDocument.Admin.OutputOffsetY = document.Tuning.Admin.OutputOffsetY;
                exportDocument.Admin.OutputNodeTextScale = document.Tuning.Admin.OutputNodeTextScale;
                exportDocument.Admin.OutputRacerTextScale = document.Tuning.Admin.OutputRacerTextScale;
            }

            GraphNode? finalNode = document.Nodes
                .Where(n => !n.Metadata.IsDisabled)
                .FirstOrDefault(n => n.Metadata.NodeType == RaceFlowNodeType.Final);

            if (finalNode != null)
            {
                exportDocument.Race.FinishMode = ToFinishModeText(finalNode.Metadata.FinishMode);
                exportDocument.Race.TotalLoops = Math.Max(1, finalNode.Metadata.LoopCount);
                exportDocument.Race.LoopRetriggerCheckpointCount = Math.Max(1, finalNode.Metadata.LoopCheckpointRequirement);
            }

            var enabledNodes = document.Nodes
                .Where(n => !n.Metadata.IsDisabled)
                .ToList();

            var enabledNodeIds = enabledNodes
                .Select(n => n.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var enabledConnections = document.Connections
                .Where(c => enabledNodeIds.Contains(c.FromNodeId) && enabledNodeIds.Contains(c.ToNodeId))
                .ToList();

            foreach (RaceFlowSide side in new[] { RaceFlowSide.Left, RaceFlowSide.Top, RaceFlowSide.Right, RaceFlowSide.Bottom })
            {
                FlowMapLayoutTuning sectionTuning = document.Tuning?.GetSection(side) ?? new FlowMapLayoutTuning();

                var section = new FlowMapExportSection
                {
                    Side = ToSideText(side),
                    Direction = ToDirectionText(GetDefaultDirectionForSide(side)),
                    VisualScale = sectionTuning.VisualScale,
                    OffsetX = sectionTuning.OffsetX,
                    OffsetY = sectionTuning.OffsetY
                };

                var segmentStartNodes = enabledNodes
                    .Where(n => n.Metadata.NodeType == RaceFlowNodeType.Start && n.Metadata.Side == side)
                    .OrderBy(n => n.Metadata.RacePath)
                    .ThenBy(n => n.Metadata.SegmentOrder)
                    .ThenBy(n => n.X)
                    .ThenBy(n => n.Y)
                    .ToList();

                for (int i = 0; i < segmentStartNodes.Count; i++)
                {
                    GraphNode startNode = segmentStartNodes[i];
                    string segmentId = startNode.Metadata.SegmentId;
                    if (string.IsNullOrWhiteSpace(segmentId))
                        segmentId = startNode.Id;

                    var segmentNodes = enabledNodes
                        .Where(n => string.Equals(n.Metadata.SegmentId, segmentId, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(n => n.X)
                        .ThenBy(n => n.Y)
                        .ToList();

                    if (segmentNodes.Count == 0)
                        segmentNodes.Add(startNode);

                    FlowMapLayoutTuning segmentTuning = document.Tuning?.GetSegment(segmentId) ?? new FlowMapLayoutTuning();

                    var segment = new FlowMapExportSegment
                    {
                        Id = segmentId,
                        Index = i,
                        RacePath = startNode.Metadata.RacePath,
                        SegmentOrder = startNode.Metadata.SegmentOrder,
                        Label = string.IsNullOrWhiteSpace(startNode.Metadata.SegmentName)
                            ? $"Segment {startNode.Metadata.SegmentOrder}"
                            : startNode.Metadata.SegmentName,
                        Side = ToSideText(startNode.Metadata.Side),
                        Direction = ToDirectionText(startNode.Metadata.Direction),
                        VisualScale = segmentTuning.VisualScale,
                        OffsetX = segmentTuning.OffsetX,
                        OffsetY = segmentTuning.OffsetY,
                        NodeCount = segmentNodes.Count,
                        CheckpointRecordCount = segmentNodes.Count
                    };

                    for (int nodeIndex = 0; nodeIndex < segmentNodes.Count; nodeIndex++)
                    {
                        GraphNode node = segmentNodes[nodeIndex];
                        segment.Nodes.Add(BuildNode(node, nodeIndex, segment.Label, enabledConnections, enabledNodes));
                    }

                    section.Segments.Add(segment);
                }

                section.SegmentCount = section.Segments.Count;
                exportDocument.Sections.Add(section);
            }

            return exportDocument;
        }

        private static FlowMapExportNode BuildNode(
            GraphNode node,
            int nodeIndex,
            string segmentLabel,
            List<NodeConnection> enabledConnections,
            List<GraphNode> enabledNodes)
        {
            string displayLabel = string.IsNullOrWhiteSpace(node.Metadata.DisplayName)
                ? node.Title
                : node.Metadata.DisplayName;

            var exportNode = new FlowMapExportNode
            {
                Id = node.Id,
                Index = nodeIndex,
                Label = displayLabel,
                RuntimeLabel = string.IsNullOrWhiteSpace(node.Metadata.RuntimeLabel)
                    ? $"{segmentLabel} | {node.Title} | {displayLabel}"
                    : node.Metadata.RuntimeLabel,
                Type = ToNodeTypeText(node.Metadata.NodeType),
                Planner = new FlowMapExportPlanner
                {
                    Title = node.Title,
                    X = node.X,
                    Y = node.Y,
                    Width = node.Width,
                    Height = node.Height
                },
                Binding = new FlowMapExportBinding
                {
                    IsBound = !string.IsNullOrWhiteSpace(node.Metadata.MapId),
                    IgnoreNode = false,
                    IsEndOfRace = node.Metadata.NodeType == RaceFlowNodeType.Final,
                    EndOfRaceMode = node.Metadata.NodeType == RaceFlowNodeType.Final
                        ? ToFinishModeText(node.Metadata.FinishMode)
                        : null
                },
                Telemetry = new FlowMapExportTelemetry
                {
                    HasCheckpoint = true,
                    Step = nodeIndex,
                    CheckpointLabel = displayLabel,
                    MapId = node.Metadata.MapId,
                    Position = new FlowMapExportVector3
                    {
                        X = Math.Round(node.Metadata.WorldX, 6),
                        Y = Math.Round(node.Metadata.WorldY, 6),
                        Z = Math.Round(node.Metadata.WorldZ, 6)
                    },
                    Trigger = new FlowMapExportTrigger
                    {
                        Radius = node.Metadata.Radius,
                        Angle = node.Metadata.Angle
                    },
                    Note = node.Notes,
                    CheckpointType = ToCheckpointTypeText(node.Metadata.NodeType)
                }
            };

            List<NodeConnection> inputs = enabledConnections
                .Where(c => string.Equals(c.ToNodeId, node.Id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.ToSocketIndex)
                .ToList();

            List<NodeConnection> outputs = enabledConnections
                .Where(c => string.Equals(c.FromNodeId, node.Id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.FromSocketIndex)
                .ToList();

            exportNode.Graph.InputCount = inputs.Count;
            exportNode.Graph.OutputCount = outputs.Count;

            foreach (NodeConnection input in inputs)
            {
                GraphNode? fromNode = enabledNodes.FirstOrDefault(n => string.Equals(n.Id, input.FromNodeId, StringComparison.OrdinalIgnoreCase));
                exportNode.Graph.GraphInputs.Add(new FlowMapExportGraphInput
                {
                    FromNodeId = input.FromNodeId,
                    FromNodeTitle = fromNode?.Title ?? string.Empty,
                    FromSocketIndex = input.FromSocketIndex,
                    ToSocketIndex = input.ToSocketIndex
                });
            }

            foreach (NodeConnection output in outputs)
            {
                GraphNode? toNode = enabledNodes.FirstOrDefault(n => string.Equals(n.Id, output.ToNodeId, StringComparison.OrdinalIgnoreCase));
                exportNode.Graph.GraphOutputs.Add(new FlowMapExportGraphOutput
                {
                    ToNodeId = output.ToNodeId,
                    ToNodeTitle = toNode?.Title ?? string.Empty,
                    FromSocketIndex = output.FromSocketIndex,
                    ToSocketIndex = output.ToSocketIndex
                });
                exportNode.Graph.RuntimeOutputNodeIds.Add(output.ToNodeId);
                exportNode.Binding.OutputOverrideNodeIds.Add(output.ToNodeId);
            }

            return exportNode;
        }

        private static RaceFlowDirection GetDefaultDirectionForSide(RaceFlowSide side)
        {
            return side switch
            {
                RaceFlowSide.Left => RaceFlowDirection.BottomToTop,
                RaceFlowSide.Right => RaceFlowDirection.TopToBottom,
                RaceFlowSide.Top => RaceFlowDirection.LeftToRight,
                RaceFlowSide.Bottom => RaceFlowDirection.RightToLeft,
                _ => RaceFlowDirection.LeftToRight
            };
        }

        private static string ToNodeTypeText(RaceFlowNodeType nodeType)
        {
            return nodeType switch
            {
                RaceFlowNodeType.Start => "start",
                RaceFlowNodeType.Checkpoint => "checkpoint",
                RaceFlowNodeType.Split => "split",
                RaceFlowNodeType.Converge => "converge",
                RaceFlowNodeType.EndSegment => "end",
                RaceFlowNodeType.Final => "boss",
                _ => "checkpoint"
            };
        }

        private static string ToCheckpointTypeText(RaceFlowNodeType nodeType)
        {
            return nodeType switch
            {
                RaceFlowNodeType.Start => "start",
                RaceFlowNodeType.EndSegment => "end",
                RaceFlowNodeType.Final => "end",
                _ => "checkpoint"
            };
        }

        private static string ToFinishModeText(RaceFlowFinishMode finishMode)
        {
            return finishMode switch
            {
                RaceFlowFinishMode.ManualFinish => "manual-finish",
                RaceFlowFinishMode.LoopFinish => "loop-finish",
                _ => "auto-finish"
            };
        }

        private static string ToSideText(RaceFlowSide side)
        {
            return side.ToString().ToLowerInvariant();
        }

        private static string ToDirectionText(RaceFlowDirection direction)
        {
            return direction switch
            {
                RaceFlowDirection.LeftToRight => "left-to-right",
                RaceFlowDirection.RightToLeft => "right-to-left",
                RaceFlowDirection.TopToBottom => "top-to-bottom",
                RaceFlowDirection.BottomToTop => "bottom-to-top",
                _ => "left-to-right"
            };
        }
    }
}
