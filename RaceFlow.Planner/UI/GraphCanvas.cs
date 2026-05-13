using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.IO;
using RaceFlow.Planner.Models;
using RaceFlow.Planner.Core;
using RaceFlow.Planner.Planner;
using RaceFlow.Planner.Services;
using RaceFlow.Planner.Import;
using RaceFlow.Planner.Telemetry;

namespace RaceFlow.Planner.UI
{
    public class GraphCanvas : Control
    {
        private readonly GraphDocument _document = new();
        private readonly List<GraphNode> _selectedNodes = new();
        private readonly Dictionary<GraphNode, Point> _dragStartPositions = new();

        private GraphNode? _dragNode;
        private Point _dragStartWorld;
        private bool _isDraggingNodes;

        private string? _dragSegmentId;
        private readonly Dictionary<GraphNode, Point> _dragSegmentStartPositions = new();

        private bool _isMarqueeSelecting;
        private Point _marqueeStartScreen;
        private Point _marqueeCurrentScreen;

        private bool _isMiddlePanning;
        private Point _lastPanScreenPoint;

        private NodeSocket? _pendingSocket;
        private Point _mouseWorldPoint;
        private TelemetrySnapshot? _latestTelemetry;

        private GraphNode? _selectedNode;
        private NodeConnection? _selectedConnection;

        private float _zoom = 1.0f;
        private float _panX;
        private float _panY;

        private const float MinZoom = 0.4f;
        private const float MaxZoom = 2.5f;
        private const float ZoomStep = 0.1f;
        private const int PanStep = 40;
        private const int SnapGridSize = 20;

        public event Action<GraphNode?>? SelectedNodeChanged;
        public event Action<GraphNode?>? SelectedSegmentChanged;

        public IReadOnlyList<GraphNode> SelectedNodes => _selectedNodes;

        public GraphDocument Document => _document;

        public void SetLatestTelemetry(TelemetrySnapshot? snapshot)
        {
            _latestTelemetry = snapshot != null && snapshot.HasUsableTelemetry ? snapshot : null;
        }

        public IReadOnlyList<GraphNode> GetAllNodesForPlanner() => _document.Nodes;

        public bool SnapToGrid { get; set; } = true;
        public bool ShowGrid { get; set; }

        public GraphCanvas()
        {
            DoubleBuffered = true;
            BackColor = Theme.AppBack;
            TabStop = true;
            MouseWheel += GraphCanvas_MouseWheel;
        }

        public void SaveToFile(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (extension == ".planrf")
                GraphSerializer.SavePlan(filePath, _document);
            else
                GraphSerializer.Save(filePath, _document);
        }

        public void LoadFromFile(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            bool isRaceFlowPlan = extension == ".planrf";

            GraphDocument loaded =
                isRaceFlowPlan
                ? GraphSerializer.LoadPlan(filePath)
                : GraphSerializer.Load(filePath);

            _document.Nodes.Clear();
            _document.Connections.Clear();
            _document.Tuning = loaded.Tuning ?? new FlowMapTuningData();
            _document.Theme = loaded.Theme ?? new RaceFlow.Planner.ThemeBuilder.ThemeProject();

            foreach (var node in loaded.Nodes)
            {
                NormalizeNodeAfterLoad(node, isRaceFlowPlan);
                _document.Nodes.Add(node);
            }

            RebuildSocketReferences(loaded);

            foreach (var connection in loaded.Connections)
                _document.Connections.Add(connection);

            ClearSelection();

            if (_document.Nodes.Count > 0)
                CenterView();
            else
                Invalidate();
        }

        public void ZoomIn() => ZoomAt(new Point(Width / 2, Height / 2), ZoomStep);
        public void ZoomOut() => ZoomAt(new Point(Width / 2, Height / 2), -ZoomStep);

        public void RefreshSelectedNode()
        {
            Invalidate();
        }

        public void ToggleSnapToGrid()
        {
            SnapToGrid = !SnapToGrid;
            Invalidate();
        }

        public void ToggleGrid()
        {
            ShowGrid = !ShowGrid;
            Invalidate();
        }

        public void SetSnapToGrid(bool enabled)
        {
            SnapToGrid = enabled;
            Invalidate();
        }

        public void SetShowGrid(bool enabled)
        {
            ShowGrid = enabled;
            Invalidate();
        }

        public void SetSelectedTitle(string title)
        {
            foreach (GraphNode node in _selectedNodes)
                node.Title = title;
            Invalidate();
        }

        public void SetSelectedNotes(string notes)
        {
            foreach (GraphNode node in _selectedNodes)
                node.Notes = notes;
        }

        public void SetSelectedColor(Color color)
        {
            foreach (GraphNode node in _selectedNodes)
                node.NodeColor = color;
            Invalidate();
        }

        public void SetSelectedMetadata(Action<GraphNode> updateNode)
        {
            if (updateNode == null)
                return;

            foreach (GraphNode node in _selectedNodes)
                updateNode(node);

            Invalidate();
        }

        public void SetSelectedBackdropColor(Color color)
        {
            foreach (GraphNode node in _selectedNodes)
                node.Metadata.BackdropColorArgb = color.ToArgb();

            Invalidate();
        }

        public void SetSelectedDisplayName(string displayName)
        {
            foreach (GraphNode node in _selectedNodes)
                node.Metadata.DisplayName = displayName;

            Invalidate();
        }

        public void SetSelectedX(int x)
        {
            // Typed property values are intentional and exact.
            // Grid snapping only applies while dragging or creating nodes.
            foreach (GraphNode node in _selectedNodes)
                node.X = x;
            Invalidate();
        }

        public void SetSelectedY(int y)
        {
            // Typed property values are intentional and exact.
            // Grid snapping only applies while dragging or creating nodes.
            foreach (GraphNode node in _selectedNodes)
                node.Y = y;
            Invalidate();
        }

        public void AddNode()
        {
            GraphNode node;

            if (_selectedNode != null)
            {
                node = DuplicateNode(_selectedNode);
            }
            else
            {
                int nodeNumber = GetNextDefaultNodeNumber();

                // First node should establish a clean graph origin.
                // After that, new standalone nodes appear near the current view center.
                if (_document.Nodes.Count == 0)
                {
                    node = new GraphNode($"Node {nodeNumber}", 0, 0);
                }
                else
                {
                    Point screenCenter = new(Width / 2, Height / 2);
                    Point worldCenter = ScreenToWorld(screenCenter);
                    node = new GraphNode($"Node {nodeNumber}", worldCenter.X, worldCenter.Y);
                }
            }

            ApplySnapToNode(node);
            _document.Nodes.Add(node);

            _selectedConnection = null;
            SetOnlySelectedNode(node);
            EnsureSpareInput(node);
            EnsureSpareOutput(node);
            Invalidate();
        }

        public void AddRaceFlowNode(RaceFlowNodeType nodeType)
        {
            GraphNode node = CreateRaceFlowNode(nodeType, isPathCheckpoint: false);

            _document.Nodes.Add(node);

            TryConnectSelectedNodeToNewNode(node);

            _selectedConnection = null;
            SetOnlySelectedNode(node);
            Invalidate();
        }

        public void AddPathCheckpointNode()
        {
            GraphNode node = CreateRaceFlowNode(RaceFlowNodeType.Checkpoint, isPathCheckpoint: true);

            _document.Nodes.Add(node);

            TryConnectSelectedNodeToNewNode(node);

            _selectedConnection = null;
            SetOnlySelectedNode(node);
            Invalidate();
        }

        private GraphNode CreateRaceFlowNode(RaceFlowNodeType nodeType, bool isPathCheckpoint)
        {
            Point position = GetNewRaceFlowNodePosition(nodeType, isPathCheckpoint);
            string title = GetDefaultRaceFlowNodeTitle(nodeType, isPathCheckpoint);

            var node = new GraphNode(title, position.X, position.Y)
            {
                NodeColor = GetDefaultRaceFlowNodeColor(nodeType, isPathCheckpoint)
            };

            ApplyRaceFlowSocketShape(node, nodeType);
            ApplySnapToNode(node);

            RaceNodeMetadata metadata = BuildDefaultMetadataFor(nodeType, title);
            ApplyLatestTelemetryToMetadata(metadata);
            node.Metadata = metadata;
            node.Notes = BuildDefaultNotesFor(nodeType, isPathCheckpoint);

            return node;
        }

        private Point GetNewRaceFlowNodePosition(RaceFlowNodeType nodeType, bool isPathCheckpoint)
        {
            if (nodeType == RaceFlowNodeType.Start)
            {
                int nextOrder = GetNextSegmentOrder();
                return new Point(0, (nextOrder - 1) * 300);
            }

            int segmentBaseY = GetSelectedSegmentBaseY();

            if (_selectedNode != null)
            {
                if (isPathCheckpoint && _selectedNode.Metadata?.NodeType == RaceFlowNodeType.Split)
                {
                    int outputIndex = GetNextAvailableOutputIndex(_selectedNode);
                    int pathNumber = outputIndex + 1;
                    int pathOffset = GetDefaultPathYOffset(pathNumber);
                    return new Point(_selectedNode.X + 300, _selectedNode.Y + pathOffset);
                }

                if (nodeType == RaceFlowNodeType.Converge)
                    return new Point(_selectedNode.X + 300, segmentBaseY);

                return new Point(_selectedNode.X + 300, _selectedNode.Y);
            }

            if (_document.Nodes.Count == 0)
                return new Point(0, 0);

            Point screenCenter = new(Width / 2, Height / 2);
            return ScreenToWorld(screenCenter);
        }

        private int GetSelectedSegmentBaseY()
        {
            int segmentOrder = _selectedNode?.Metadata?.SegmentOrder ?? 1;
            if (segmentOrder < 1)
                segmentOrder = 1;

            return (segmentOrder - 1) * 300;
        }

        private static int GetDefaultPathYOffset(int pathNumber)
        {
            if (pathNumber <= 1)
                return -100;

            if (pathNumber == 2)
                return 100;

            int magnitude = ((pathNumber + 1) / 2) * 100;
            return pathNumber % 2 == 1 ? -magnitude : magnitude;
        }

        private RaceNodeMetadata BuildDefaultMetadataFor(RaceFlowNodeType nodeType, string title)
        {
            RaceNodeMetadata metadata = new()
            {
                NodeType = nodeType,
                Radius = 5.0,
                Angle = 360.0,
                RuntimeLabel = title,
                DisplayName = string.Empty
            };

            if (_selectedNode?.Metadata != null)
            {
                metadata.SegmentId = _selectedNode.Metadata.SegmentId;
                metadata.RacePath = _selectedNode.Metadata.RacePath;
                metadata.SegmentOrder = _selectedNode.Metadata.SegmentOrder;
                metadata.Side = _selectedNode.Metadata.Side;
                metadata.Direction = _selectedNode.Metadata.Direction;
            }

            if (nodeType == RaceFlowNodeType.Start)
            {
                metadata.SegmentId = CreateNewSegmentId();
                metadata.SegmentOrder = GetNextSegmentOrder();
                metadata.SegmentName = $"Segment {metadata.SegmentOrder}";
                metadata.DisplayName = string.Empty;
                metadata.RacePath = 1;
                metadata.Side = RaceFlowSide.Left;
                metadata.Direction = RaceFlowDirection.BottomToTop;
                metadata.Radius = 5.0;
                metadata.Angle = 360.0;
            }

            if (nodeType == RaceFlowNodeType.Final)
            {
                metadata.IsEndOfRace = true;
                metadata.FinishMode = RaceFlowFinishMode.AutoFinish;
                metadata.LoopCount = 1;
                metadata.LoopCheckpointRequirement = 3;
            }

            return metadata;
        }

        private void ApplyLatestTelemetryToMetadata(RaceNodeMetadata metadata)
        {
            if (_latestTelemetry == null || !_latestTelemetry.HasUsableTelemetry)
                return;

            metadata.MapId = _latestTelemetry.MapId?.ToString() ?? string.Empty;
            metadata.WorldX = _latestTelemetry.X;
            metadata.WorldY = _latestTelemetry.Y;
            metadata.WorldZ = _latestTelemetry.Z;
        }

        private void ApplyRaceFlowSocketShape(GraphNode node, RaceFlowNodeType nodeType)
        {
            node.Inputs.Clear();
            node.Outputs.Clear();

            switch (nodeType)
            {
                case RaceFlowNodeType.Start:
                    node.Inputs.Add(new NodeSocket(node, true, 0));
                    node.Outputs.Add(new NodeSocket(node, false, 0));
                    break;

                case RaceFlowNodeType.Split:
                    node.Inputs.Add(new NodeSocket(node, true, 0));
                    node.Outputs.Add(new NodeSocket(node, false, 0));
                    node.Outputs.Add(new NodeSocket(node, false, 1));
                    break;

                case RaceFlowNodeType.Converge:
                    node.Inputs.Add(new NodeSocket(node, true, 0));
                    node.Inputs.Add(new NodeSocket(node, true, 1));
                    node.Outputs.Add(new NodeSocket(node, false, 0));
                    break;

                case RaceFlowNodeType.EndSegment:
                    node.Inputs.Add(new NodeSocket(node, true, 0));
                    node.Outputs.Add(new NodeSocket(node, false, 0));
                    break;

                case RaceFlowNodeType.Final:
                    node.Inputs.Add(new NodeSocket(node, true, 0));
                    break;

                default:
                    node.Inputs.Add(new NodeSocket(node, true, 0));
                    node.Outputs.Add(new NodeSocket(node, false, 0));
                    break;
            }
        }

        private void TryConnectSelectedNodeToNewNode(GraphNode newNode)
        {
            if (_selectedNode == null)
                return;

            if (_selectedNode.Outputs.Count == 0 || newNode.Inputs.Count == 0)
                return;

            EnsureSpareOutput(_selectedNode);
            EnsureSpareInput(newNode);

            NodeSocket? from = _selectedNode.Outputs.FirstOrDefault(s => !s.IsConnected);
            NodeSocket? to = newNode.Inputs.FirstOrDefault(s => !s.IsConnected);

            if (from == null || to == null)
                return;

            if (!CanConnect(from, to))
                return;

            if (_document.Connections.Any(c => c.From == from && c.To == to))
                return;

            _document.Connections.Add(new NodeConnection(from, to));

            from.IsConnected = true;
            to.IsConnected = true;

            EnsureSpareInput(newNode);
            EnsureSpareOutput(_selectedNode);
            RefreshSocketFlags(newNode);
            RefreshSocketFlags(_selectedNode);
        }

        private string GetDefaultRaceFlowNodeTitle(RaceFlowNodeType nodeType, bool isPathCheckpoint)
        {
            if (isPathCheckpoint)
                return GetNextPathCheckpointTitle();

            return nodeType switch
            {
                RaceFlowNodeType.Start => $"Segment {GetNextSegmentOrder()} - Start",
                RaceFlowNodeType.Checkpoint => $"CP{GetNextCheckpointNumber()}",
                RaceFlowNodeType.Split => $"Split {GetNextRaceFlowNodeNumber("Split")}",
                RaceFlowNodeType.Converge => $"Converge {GetNextRaceFlowNodeNumber("Converge")}",
                RaceFlowNodeType.EndSegment => $"Segment End {GetNextRaceFlowNodeNumber("Segment End")}",
                RaceFlowNodeType.Final => "Final",
                _ => $"Node {GetNextDefaultNodeNumber()}"
            };
        }

        private int GetNextCheckpointNumber()
        {
            int nodeNumber = 1;

            while (_document.Nodes.Any(n => string.Equals(n.Title, $"CP{nodeNumber}", StringComparison.OrdinalIgnoreCase)))
                nodeNumber++;

            return nodeNumber;
        }

        private string GetNextPathCheckpointTitle()
        {
            int splitNumber = 1;
            int pathNumber = 1;

            if (_selectedNode != null)
            {
                if (_selectedNode.Metadata?.NodeType == RaceFlowNodeType.Split)
                {
                    splitNumber = ExtractTrailingNumber(_selectedNode.Title, 1);
                    pathNumber = GetNextAvailableOutputIndex(_selectedNode) + 1;
                    return $"S{splitNumber}_P{pathNumber}_CP1";
                }

                if (TryParsePathCheckpointTitle(_selectedNode.Title, out int selectedSplit, out int selectedPath, out int selectedCp))
                {
                    splitNumber = selectedSplit;
                    pathNumber = selectedPath;
                    return $"S{splitNumber}_P{pathNumber}_CP{selectedCp + 1}";
                }
            }

            return $"S{splitNumber}_P{pathNumber}_CP{GetNextPathCheckpointNumber(splitNumber, pathNumber)}";
        }

        private int GetNextPathCheckpointNumber(int splitNumber, int pathNumber)
        {
            int cpNumber = 1;

            while (_document.Nodes.Any(n => string.Equals(n.Title, $"S{splitNumber}_P{pathNumber}_CP{cpNumber}", StringComparison.OrdinalIgnoreCase)))
                cpNumber++;

            return cpNumber;
        }

        private static bool TryParsePathCheckpointTitle(string title, out int splitNumber, out int pathNumber, out int checkpointNumber)
        {
            splitNumber = 0;
            pathNumber = 0;
            checkpointNumber = 0;

            Match match = Regex.Match(title ?? string.Empty, @"^S(\d+)_P(\d+)_CP(\d+)$", RegexOptions.IgnoreCase);
            if (!match.Success)
                return false;

            splitNumber = int.Parse(match.Groups[1].Value);
            pathNumber = int.Parse(match.Groups[2].Value);
            checkpointNumber = int.Parse(match.Groups[3].Value);
            return true;
        }

        private static int ExtractTrailingNumber(string text, int fallback)
        {
            Match match = Regex.Match(text ?? string.Empty, @"(\d+)\s*$");
            if (!match.Success)
                return fallback;

            return int.TryParse(match.Groups[1].Value, out int value) ? value : fallback;
        }

        private int GetNextAvailableOutputIndex(GraphNode node)
        {
            for (int i = 0; i < node.Outputs.Count; i++)
            {
                if (!node.Outputs[i].IsConnected && !_document.Connections.Any(c => c.From == node.Outputs[i]))
                    return i;
            }

            return node.Outputs.Count;
        }

        private int GetNextRaceFlowNodeNumber(string prefix)
        {
            int nodeNumber = 1;

            while (_document.Nodes.Any(n => n.Title.StartsWith($"{prefix} {nodeNumber}", StringComparison.OrdinalIgnoreCase)))
                nodeNumber++;

            return nodeNumber;
        }

        private int GetNextSegmentOrder()
        {
            int maxOrder = 0;

            foreach (GraphNode node in _document.Nodes)
            {
                if (node.Metadata?.NodeType == RaceFlowNodeType.Start && node.Metadata.SegmentOrder > maxOrder)
                    maxOrder = node.Metadata.SegmentOrder;
            }

            return maxOrder + 1;
        }

        private static string CreateNewSegmentId()
        {
            return "segment_" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        }

        private static string BuildDefaultNotesFor(RaceFlowNodeType nodeType, bool isPathCheckpoint)
        {
            if (isPathCheckpoint)
                return "RaceFlow path checkpoint node.";

            return nodeType switch
            {
                RaceFlowNodeType.Start => "RaceFlow segment start node.",
                RaceFlowNodeType.Split => "RaceFlow split node. Add outputs for each branch.",
                RaceFlowNodeType.Converge => "RaceFlow converge node. Add inputs for incoming branches.",
                RaceFlowNodeType.EndSegment => "RaceFlow segment end node. Can flow into another segment start.",
                RaceFlowNodeType.Final => "RaceFlow final/boss node. End-of-race metadata enabled by default.",
                _ => string.Empty
            };
        }

        private static Color GetDefaultRaceFlowNodeColor(RaceFlowNodeType nodeType, bool isPathCheckpoint)
        {
            if (isPathCheckpoint)
                return Color.FromArgb(38, 48, 62);

            return nodeType switch
            {
                RaceFlowNodeType.Start => Color.FromArgb(44, 120, 78),
                RaceFlowNodeType.Checkpoint => Color.FromArgb(34, 39, 47),
                RaceFlowNodeType.Split => Color.FromArgb(118, 82, 38),
                RaceFlowNodeType.Converge => Color.FromArgb(82, 55, 120),
                RaceFlowNodeType.EndSegment => Color.FromArgb(120, 58, 48),
                RaceFlowNodeType.Final => Color.FromArgb(135, 42, 82),
                _ => Color.FromArgb(34, 39, 47)
            };
        }

        public void CenterView()
        {
            if (_document.Nodes.Count == 0)
            {
                _panX = Width / 2f;
                _panY = Height / 2f;
                Invalidate();
                return;
            }

            int left = _document.Nodes.Min(n => n.Bounds.Left);
            int top = _document.Nodes.Min(n => n.Bounds.Top);
            int right = _document.Nodes.Max(n => n.Bounds.Right);
            int bottom = _document.Nodes.Max(n => n.Bounds.Bottom);

            float graphCenterX = (left + right) * 0.5f;
            float graphCenterY = (top + bottom) * 0.5f;

            _panX = (Width * 0.5f) - (graphCenterX * _zoom);
            _panY = (Height * 0.5f) - (graphCenterY * _zoom);

            Invalidate();
        }

        public void ClearGraph()
        {
            _document.Nodes.Clear();
            _document.Connections.Clear();
            _selectedConnection = null;
            _pendingSocket = null;
            SetOnlySelectedNode(null);
            _panX = Width * 0.5f;
            _panY = Height * 0.5f;
            _zoom = 1.0f;
            Invalidate();
        }
        public int ImportSpeedometerCsv(string filePath, string mapId)
        {
            SpeedometerCsvImportResult importResult = SpeedometerCsvImporter.Import(filePath);
            if (importResult.Checkpoints.Count == 0)
                throw new InvalidOperationException("CSV did not contain any importable checkpoint rows.");

            _document.Nodes.Clear();
            _document.Connections.Clear();
            _selectedConnection = null;
            _pendingSocket = null;

            string segmentId = CreateNewSegmentId();
            var createdNodes = new List<GraphNode>();
            int cpNumber = 1;

            for (int i = 0; i < importResult.Checkpoints.Count; i++)
            {
                SpeedometerCsvCheckpoint row = importResult.Checkpoints[i];
                RaceFlowNodeType nodeType;
                string title;

                if (row.Kind == SpeedometerCsvCheckpointKind.Start)
                {
                    nodeType = RaceFlowNodeType.Start;
                    title = "Segment 1 - Start";
                }
                else if (row.Kind == SpeedometerCsvCheckpointKind.End)
                {
                    nodeType = RaceFlowNodeType.Final;
                    title = "Final";
                }
                else
                {
                    nodeType = RaceFlowNodeType.Checkpoint;
                    title = $"CP{cpNumber}";
                    cpNumber++;
                }

                var node = new GraphNode(title, i * 300, 0)
                {
                    NodeColor = GetDefaultRaceFlowNodeColor(nodeType, isPathCheckpoint: false),
                    Notes = row.Note
                };

                ApplyRaceFlowSocketShape(node, nodeType);

                node.Metadata = new RaceNodeMetadata
                {
                    SegmentId = segmentId,
                    RacePath = 1,
                    SegmentOrder = 1,
                    SegmentName = "Segment 1",
                    DisplayName = row.Kind == SpeedometerCsvCheckpointKind.Start
                        ? "START"
                        : row.Kind == SpeedometerCsvCheckpointKind.End
                            ? "FINAL"
                            : string.Empty,
                    NodeType = nodeType,
                    Side = RaceFlowSide.Left,
                    Direction = RaceFlowDirection.BottomToTop,
                    IsEndOfRace = nodeType == RaceFlowNodeType.Final,
                    FinishMode = RaceFlowFinishMode.AutoFinish,
                    LoopCount = 3,
                    LoopCheckpointRequirement = 3,
                    Radius = row.Radius,
                    Angle = row.Angle,
                    MapId = mapId ?? string.Empty,
                    WorldX = row.X,
                    WorldY = row.Y,
                    WorldZ = row.Z,
                    RuntimeLabel = title
                };

                _document.Nodes.Add(node);
                createdNodes.Add(node);
            }

            for (int i = 0; i < createdNodes.Count - 1; i++)
            {
                GraphNode fromNode = createdNodes[i];
                GraphNode toNode = createdNodes[i + 1];

                if (fromNode.Outputs.Count == 0 || toNode.Inputs.Count == 0)
                    continue;

                NodeSocket from = fromNode.Outputs[0];
                NodeSocket to = toNode.Inputs[0];
                var connection = new NodeConnection(from, to);
                _document.Connections.Add(connection);
                from.IsConnected = true;
                to.IsConnected = true;
            }

            ClearSelection();
            if (createdNodes.Count > 0)
                SetOnlySelectedNode(createdNodes[0]);

            CenterView();
            Invalidate();

            return importResult.IgnoredResetCount;
        }


        private void RebuildSocketReferences(GraphDocument doc)
        {
            foreach (var node in doc.Nodes)
            {
                foreach (var input in node.Inputs)
                    input.Node = node;

                foreach (var output in node.Outputs)
                    output.Node = node;
            }
        }

        private void NormalizeNodeAfterLoad(GraphNode node, bool isRaceFlowPlan)
        {
            if (node.Inputs == null)
                node.Inputs = new();

            if (node.Outputs == null)
                node.Outputs = new();

            if (node.NodeColorArgb == 0)
                node.NodeColorArgb = Color.FromArgb(34, 39, 47).ToArgb();

            if (node.Inputs.Count == 0)
                node.Inputs.Add(new NodeSocket(node, true, 0));

            if (node.Outputs.Count == 0)
                node.Outputs.Add(new NodeSocket(node, false, 0));

            ReindexInputs(node);
            ReindexOutputs(node);

            if (isRaceFlowPlan)
                EnforceRaceFlowSocketShape(node);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys code = keyData & Keys.KeyCode;
            return code switch
            {
                Keys.Delete => true,
                Keys.Left => true,
                Keys.Right => true,
                Keys.Up => true,
                Keys.Down => true,
                Keys.N => true,
                _ => base.IsInputKey(keyData)
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            DrawGrid(e.Graphics);
            DrawSegmentBackdrops(e.Graphics);
            DrawConnections(e.Graphics);
            DrawPendingConnection(e.Graphics);
            DrawNodes(e.Graphics);
            DrawMarquee(e.Graphics);
        }

        private void DrawGrid(Graphics g)
        {
            if (!ShowGrid)
                return;

            using var penMinor = new Pen(Theme.GridMinor);
            using var penMajor = new Pen(Theme.GridMajor);

            const int minor = 20;
            const int major = 100;

            Point topLeft = ScreenToWorld(new Point(0, 0));
            Point bottomRight = ScreenToWorld(new Point(Width, Height));

            int startXMinor = FloorToMultiple(topLeft.X, minor);
            int endXMinor = FloorToMultiple(bottomRight.X + minor, minor);
            int startYMinor = FloorToMultiple(topLeft.Y, minor);
            int endYMinor = FloorToMultiple(bottomRight.Y + minor, minor);

            for (int x = startXMinor; x <= endXMinor; x += minor)
            {
                int sx = WorldToScreenX(x);
                g.DrawLine(penMinor, sx, 0, sx, Height);
            }

            for (int y = startYMinor; y <= endYMinor; y += minor)
            {
                int sy = WorldToScreenY(y);
                g.DrawLine(penMinor, 0, sy, Width, sy);
            }

            int startXMajor = FloorToMultiple(topLeft.X, major);
            int endXMajor = FloorToMultiple(bottomRight.X + major, major);
            int startYMajor = FloorToMultiple(topLeft.Y, major);
            int endYMajor = FloorToMultiple(bottomRight.Y + major, major);

            for (int x = startXMajor; x <= endXMajor; x += major)
            {
                int sx = WorldToScreenX(x);
                g.DrawLine(penMajor, sx, 0, sx, Height);
            }

            for (int y = startYMajor; y <= endYMajor; y += major)
            {
                int sy = WorldToScreenY(y);
                g.DrawLine(penMajor, 0, sy, Width, sy);
            }
        }

        private void DrawSegmentBackdrops(Graphics g)
        {
            foreach (var backdrop in BuildSegmentBackdrops())
            {
                Rectangle screenBounds = WorldToScreen(backdrop.Bounds);
                int cornerRadius = ScaleSize(18);
                int headerHeight = ScaleSize(28);

                using var fill = new SolidBrush(backdrop.FillColor);
                using var border = new Pen(backdrop.BorderColor, Math.Max(1f, 1.5f * _zoom));
                using var path = Theme.CreateRoundRect(screenBounds, cornerRadius);

                g.FillPath(fill, path);
                g.DrawPath(border, path);

                Rectangle headerRect = new(screenBounds.X, screenBounds.Y, screenBounds.Width, headerHeight);
                using (var headerBrush = new SolidBrush(backdrop.HeaderColor))
                using (var headerPath = Theme.CreateRoundRect(headerRect, cornerRadius))
                {
                    g.FillPath(headerBrush, headerPath);
                }

                using var font = new Font("Segoe UI Semibold", Math.Max(8.5f, 9.5f * _zoom), FontStyle.Bold);
                using var textBrush = new SolidBrush(Color.FromArgb(220, 235, 245));
                g.DrawString(backdrop.Title, font, textBrush, screenBounds.X + ScaleSize(12), screenBounds.Y + ScaleSize(6));

                Rectangle gearRect = GetSegmentGearScreenRect(screenBounds);
                using var gearFill = new SolidBrush(Color.FromArgb(65, 10, 14, 18));
                using var gearPen = new Pen(Color.FromArgb(140, 190, 205, 220), Math.Max(1f, 1.0f * _zoom));
                g.FillEllipse(gearFill, gearRect);
                g.DrawEllipse(gearPen, gearRect);

                using var gearFont = new Font("Segoe UI Symbol", Math.Max(8.0f, 9.0f * _zoom), FontStyle.Regular);
                using var gearBrush = new SolidBrush(Color.FromArgb(220, 225, 235, 245));
                using var centerFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("⚙", gearFont, gearBrush, gearRect, centerFormat);
            }
        }

        private List<SegmentBackdropInfo> BuildSegmentBackdrops()
        {
            var groups = _document.Nodes
                .Where(n => !string.IsNullOrWhiteSpace(n.Metadata?.SegmentId))
                .GroupBy(n => n.Metadata.SegmentId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var result = new List<SegmentBackdropInfo>();

            foreach (var group in groups)
            {
                List<GraphNode> nodes = group.ToList();
                if (nodes.Count == 0)
                    continue;

                int left = nodes.Min(n => n.Bounds.Left);
                int top = nodes.Min(n => n.Bounds.Top);
                int right = nodes.Max(n => n.Bounds.Right);
                int bottom = nodes.Max(n => n.Bounds.Bottom);

                const int padX = 55;
                const int padTop = 58;
                const int padBottom = 35;

                Rectangle bounds = Rectangle.FromLTRB(
                    left - padX,
                    top - padTop,
                    right + padX,
                    bottom + padBottom);

                GraphNode? startNode = nodes
                    .FirstOrDefault(n => n.Metadata?.NodeType == RaceFlowNodeType.Start)
                    ?? nodes.OrderBy(n => n.Metadata?.SegmentOrder ?? int.MaxValue).ThenBy(n => n.X).FirstOrDefault();

                int segmentOrder = startNode?.Metadata?.SegmentOrder ?? nodes.Min(n => n.Metadata?.SegmentOrder ?? 1);
                string baseTitle = $"Segment {segmentOrder}";
                string segmentName = startNode?.Metadata?.SegmentName?.Trim() ?? string.Empty;
                string title = string.IsNullOrWhiteSpace(segmentName) || string.Equals(segmentName, baseTitle, StringComparison.OrdinalIgnoreCase)
                    ? baseTitle
                    : $"{baseTitle} - {segmentName}";

                Color baseColor = startNode?.Metadata?.BackdropColorArgb is int argb && argb != 0
                    ? Color.FromArgb(argb)
                    : GetSegmentBackdropBaseColor(group.Key);

                result.Add(new SegmentBackdropInfo(
                    group.Key,
                    title,
                    bounds,
                    Color.FromArgb(46, baseColor),
                    Color.FromArgb(86, baseColor),
                    Color.FromArgb(92, ControlPaint.Light(baseColor, 0.18f))));
            }

            return result
                .OrderBy(b => b.Bounds.Top)
                .ThenBy(b => b.Bounds.Left)
                .ToList();
        }

        private static Color GetSegmentBackdropBaseColor(string segmentId)
        {
            int hash = Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(segmentId ?? string.Empty));

            int r = 55 + (hash % 70);
            int g = 55 + ((hash / 7) % 70);
            int b = 70 + ((hash / 13) % 80);

            return Color.FromArgb(r, g, b);
        }

        private SegmentBackdropInfo? HitTestSegmentBackdrop(Point worldPoint)
        {
            return BuildSegmentBackdrops()
                .Where(b => b.Bounds.Contains(worldPoint))
                .OrderBy(b => b.Bounds.Width * b.Bounds.Height)
                .FirstOrDefault();
        }

        private SegmentBackdropInfo? HitTestSegmentGear(Point screenPoint)
        {
            foreach (SegmentBackdropInfo backdrop in BuildSegmentBackdrops())
            {
                Rectangle screenBounds = WorldToScreen(backdrop.Bounds);
                if (GetSegmentGearScreenRect(screenBounds).Contains(screenPoint))
                    return backdrop;
            }

            return null;
        }

        private static Rectangle GetSegmentGearScreenRect(Rectangle screenBounds)
        {
            int size = Math.Max(16, Math.Min(22, screenBounds.Height / 2));
            return new Rectangle(screenBounds.Right - size - 8, screenBounds.Top + 4, size, size);
        }

        private GraphNode? FindSegmentStartNode(string segmentId)
        {
            return _document.Nodes
                .Where(n => string.Equals(n.Metadata?.SegmentId, segmentId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(n => n.Metadata?.NodeType == RaceFlowNodeType.Start)
                .ThenBy(n => n.X)
                .FirstOrDefault();
        }

        private void DrawNodes(Graphics g)
        {
            foreach (var node in _document.Nodes)
                DrawNode(g, node);
        }

        private void DrawNode(Graphics g, GraphNode node)
        {
            Rectangle worldBounds = node.Bounds;
            Rectangle bounds = WorldToScreen(worldBounds);

            int cornerRadius = ScaleSize(14);
            int headerHeight = ScaleSize(34);
            bool isSelected = _selectedNodes.Contains(node);

            using var cardPath = Theme.CreateRoundRect(bounds, cornerRadius);
            using var cardBrush = new SolidBrush(node.NodeColor);
            using var borderPen = new Pen(isSelected ? Theme.Accent : Theme.Border, isSelected ? 2.5f : 1f);

            g.FillPath(cardBrush, cardPath);
            g.DrawPath(borderPen, cardPath);

            Rectangle headerRect = new(bounds.X, bounds.Y, bounds.Width, headerHeight);
            Color headerColor = ControlPaint.Dark(node.NodeColor, 0.22f);

            using (var headerBrush = new SolidBrush(headerColor))
            using (var headerPath = Theme.CreateRoundRect(headerRect, cornerRadius))
            {
                g.FillPath(headerBrush, headerPath);
            }

            float fontSize = Math.Max(8.5f, 9.5f * _zoom);
            using (var font = new Font("Segoe UI Semibold", fontSize, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Theme.Text))
            {
                g.DrawString(node.Title, font, textBrush, bounds.X + ScaleSize(12), bounds.Y + ScaleSize(9));
            }

            for (int i = 0; i < node.Inputs.Count; i++)
            {
                Point p = WorldToScreen(node.GetInputSocketPosition(i));
                DrawSocket(g, p, node.Inputs[i] == _pendingSocket);
            }

            for (int i = 0; i < node.Outputs.Count; i++)
            {
                Point p = WorldToScreen(node.GetOutputSocketPosition(i));
                DrawSocket(g, p, node.Outputs[i] == _pendingSocket);
            }

            if (node.Metadata?.IsDisabled == true)
                DrawDisabledSlash(g, bounds);
        }

        private void DrawDisabledSlash(Graphics g, Rectangle bounds)
        {
            using var shadowPen = new Pen(Color.FromArgb(210, 20, 0, 0), Math.Max(5f, 7f * _zoom))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };

            using var slashPen = new Pen(Color.FromArgb(235, 230, 55, 55), Math.Max(3f, 4f * _zoom))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };

            Point a = new(bounds.Left + ScaleSize(8), bounds.Bottom - ScaleSize(8));
            Point b = new(bounds.Right - ScaleSize(8), bounds.Top + ScaleSize(8));

            g.DrawLine(shadowPen, a, b);
            g.DrawLine(slashPen, a, b);
        }

        private void DrawSocket(Graphics g, Point center, bool highlight)
        {
            int r = ScaleSize(highlight ? 6 : 5);
            Rectangle rect = new(center.X - r, center.Y - r, r * 2, r * 2);

            using var fill = new SolidBrush(highlight ? Theme.Accent : Theme.SocketFill);
            using var border = new Pen(Theme.Border);

            g.FillEllipse(fill, rect);
            g.DrawEllipse(border, rect);
        }

        private void DrawConnections(Graphics g)
        {
            foreach (var connection in _document.Connections)
            {
                Point p1 = WorldToScreen(connection.From.GetPosition());
                Point p2 = WorldToScreen(connection.To.GetPosition());

                bool selected = connection == _selectedConnection;
                int handle = Math.Max(40, ScaleSize(60));

                using var pen = new Pen(selected ? Theme.Warning : Theme.Accent, selected ? 3f : 2f);

                g.DrawBezier(pen, p1, new Point(p1.X + handle, p1.Y), new Point(p2.X - handle, p2.Y), p2);
            }
        }

        private void DrawPendingConnection(Graphics g)
        {
            if (_pendingSocket == null)
                return;

            Point start = WorldToScreen(_pendingSocket.GetPosition());
            Point end = WorldToScreen(_mouseWorldPoint);

            using var pen = new Pen(Theme.Warning, 2f) { DashStyle = DashStyle.Dash };
            int handle = Math.Max(40, ScaleSize(60));

            if (_pendingSocket.IsInput)
                g.DrawBezier(pen, end, new Point(end.X + handle, end.Y), new Point(start.X - handle, start.Y), start);
            else
                g.DrawBezier(pen, start, new Point(start.X + handle, start.Y), new Point(end.X - handle, end.Y), end);
        }

        private void DrawMarquee(Graphics g)
        {
            if (!_isMarqueeSelecting)
                return;

            Rectangle rect = GetScreenMarqueeRectangle();
            using var fill = new SolidBrush(Color.FromArgb(45, Theme.Accent));
            using var pen = new Pen(Theme.Accent, 1.5f) { DashStyle = DashStyle.Dash };
            g.FillRectangle(fill, rect);
            g.DrawRectangle(pen, rect);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            Point worldPoint = ScreenToWorld(e.Location);
            _mouseWorldPoint = worldPoint;

            if (e.Button == MouseButtons.Middle)
            {
                _isMiddlePanning = true;
                _lastPanScreenPoint = e.Location;
                Cursor = Cursors.SizeAll;
                return;
            }

            if (e.Button == MouseButtons.Right)
            {
                ShowContextMenu(e.Location, worldPoint);
                return;
            }

            SegmentBackdropInfo? gearBackdrop = HitTestSegmentGear(e.Location);
            if (gearBackdrop != null)
            {
                _selectedConnection = null;
                SetOnlySelectedNode(null);
                SelectedSegmentChanged?.Invoke(FindSegmentStartNode(gearBackdrop.SegmentId));
                Invalidate();
                return;
            }

            NodeSocket? socket = HitTestSocket(worldPoint);
            if (socket != null)
            {
                _selectedConnection = null;
                SetOnlySelectedNode(null);
                _pendingSocket = socket;
                Invalidate();
                return;
            }

            GraphNode? node = HitTestNode(worldPoint);
            if (node != null)
            {
                _selectedConnection = null;

                if ((ModifierKeys & Keys.Shift) == Keys.Shift)
                {
                    ToggleNodeSelection(node);
                }
                else if (!_selectedNodes.Contains(node))
                {
                    SetOnlySelectedNode(node);
                }
                else
                {
                    RaiseSelectedNodeChanged();
                }

                _dragNode = node;
                _isDraggingNodes = true;
                _dragStartWorld = worldPoint;
                _dragStartPositions.Clear();
                foreach (GraphNode selected in _selectedNodes)
                    _dragStartPositions[selected] = new Point(selected.X, selected.Y);

                Invalidate();
                return;
            }

            NodeConnection? connection = HitTestConnection(worldPoint);
            if (connection != null)
            {
                _selectedConnection = connection;
                SetOnlySelectedNode(null);
                Invalidate();
                return;
            }

            SegmentBackdropInfo? backdrop = HitTestSegmentBackdrop(worldPoint);
            if (backdrop != null)
            {
                _selectedConnection = null;
                SetOnlySelectedNode(null);

                _dragSegmentId = backdrop.SegmentId;
                _dragStartWorld = worldPoint;
                _dragSegmentStartPositions.Clear();

                foreach (GraphNode nodeInSegment in _document.Nodes.Where(n => string.Equals(n.Metadata?.SegmentId, backdrop.SegmentId, StringComparison.OrdinalIgnoreCase)))
                    _dragSegmentStartPositions[nodeInSegment] = new Point(nodeInSegment.X, nodeInSegment.Y);

                Cursor = Cursors.SizeAll;
                Invalidate();
                return;
            }

            _selectedConnection = null;
            if ((ModifierKeys & Keys.Shift) != Keys.Shift)
                SetOnlySelectedNode(null);

            _isMarqueeSelecting = true;
            _marqueeStartScreen = e.Location;
            _marqueeCurrentScreen = e.Location;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            Point worldPoint = ScreenToWorld(e.Location);
            _mouseWorldPoint = worldPoint;

            if (_isMiddlePanning)
            {
                _panX += e.X - _lastPanScreenPoint.X;
                _panY += e.Y - _lastPanScreenPoint.Y;
                _lastPanScreenPoint = e.Location;
                Invalidate();
                return;
            }

            if (_dragSegmentId != null)
            {
                int dx = worldPoint.X - _dragStartWorld.X;
                int dy = worldPoint.Y - _dragStartWorld.Y;

                foreach (var pair in _dragSegmentStartPositions)
                {
                    GraphNode node = pair.Key;
                    Point start = pair.Value;
                    node.X = start.X + dx;
                    node.Y = start.Y + dy;
                    ApplySnapToNode(node);
                }

                Invalidate();
                return;
            }

            if (_isMarqueeSelecting)
            {
                _marqueeCurrentScreen = e.Location;
                Invalidate();
                return;
            }

            if (_isDraggingNodes && _dragNode != null)
            {
                int dx = worldPoint.X - _dragStartWorld.X;
                int dy = worldPoint.Y - _dragStartWorld.Y;

                foreach (var pair in _dragStartPositions)
                {
                    GraphNode node = pair.Key;
                    Point start = pair.Value;
                    node.X = start.X + dx;
                    node.Y = start.Y + dy;
                    ApplySnapToNode(node);
                }

                Invalidate();
                return;
            }

            if (_pendingSocket != null)
                Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Point worldPoint = ScreenToWorld(e.Location);

            if (e.Button == MouseButtons.Middle)
            {
                _isMiddlePanning = false;
                Cursor = Cursors.Default;
                return;
            }

            if (_dragSegmentId != null)
            {
                _dragSegmentId = null;
                _dragSegmentStartPositions.Clear();
                Cursor = Cursors.Default;
                RaiseSelectedNodeChanged();
                Invalidate();
                return;
            }

            if (_isMarqueeSelecting)
            {
                CompleteMarqueeSelection();
                _isMarqueeSelecting = false;
                Invalidate();
                return;
            }

            if (_isDraggingNodes)
            {
                _dragNode = null;
                _isDraggingNodes = false;
                _dragStartPositions.Clear();
                RaiseSelectedNodeChanged();
                return;
            }

            if (_pendingSocket != null)
            {
                NodeSocket? target = HitTestSocket(worldPoint);

                if (target != null && CanConnect(_pendingSocket, target))
                    CreateConnection(_pendingSocket, target);

                _pendingSocket = null;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            if (_isMiddlePanning)
            {
                _isMiddlePanning = false;
                Cursor = Cursors.Default;
            }

            if (_dragSegmentId != null)
            {
                _dragSegmentId = null;
                _dragSegmentStartPositions.Clear();
                Cursor = Cursors.Default;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.N)
            {
                ToggleSnapToGrid();
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelection();
                e.Handled = true;
                return;
            }

            if (_selectedNodes.Count > 0 && (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down))
            {
                int amount = (e.Modifiers & Keys.Shift) == Keys.Shift ? 10 : 1;
                int dx = e.KeyCode == Keys.Left ? -amount : e.KeyCode == Keys.Right ? amount : 0;
                int dy = e.KeyCode == Keys.Up ? -amount : e.KeyCode == Keys.Down ? amount : 0;
                foreach (GraphNode node in _selectedNodes)
                {
                    node.X += dx;
                    node.Y += dy;
                    ApplySnapToNode(node);
                }
                RaiseSelectedNodeChanged();
                Invalidate();
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Left)
            {
                _panX += PanStep;
                Invalidate();
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Right)
            {
                _panX -= PanStep;
                Invalidate();
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Up)
            {
                _panY += PanStep;
                Invalidate();
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Down)
            {
                _panY -= PanStep;
                Invalidate();
                e.Handled = true;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (_document.Nodes.Count == 0 && _panX == 0f && _panY == 0f)
            {
                _panX = Width * 0.5f;
                _panY = Height * 0.5f;
            }
        }

        private void GraphCanvas_MouseWheel(object? sender, MouseEventArgs e)
        {
            float delta = e.Delta > 0 ? ZoomStep : -ZoomStep;
            ZoomAt(e.Location, delta);
        }

        private void ZoomAt(Point screenPoint, float zoomDelta)
        {
            float oldZoom = _zoom;
            float newZoom = _zoom + zoomDelta;

            if (newZoom < MinZoom)
                newZoom = MinZoom;

            if (newZoom > MaxZoom)
                newZoom = MaxZoom;

            if (Math.Abs(newZoom - oldZoom) < 0.0001f)
                return;

            float worldX = (screenPoint.X - _panX) / oldZoom;
            float worldY = (screenPoint.Y - _panY) / oldZoom;

            _zoom = newZoom;
            _panX = screenPoint.X - (worldX * _zoom);
            _panY = screenPoint.Y - (worldY * _zoom);

            Invalidate();
        }

        private void DeleteSelection()
        {
            if (_selectedConnection != null)
            {
                _document.Connections.Remove(_selectedConnection);
                _selectedConnection = null;
                CleanupSockets();
                Invalidate();
                return;
            }

            if (_selectedNodes.Count > 0)
            {
                var deleteSet = _selectedNodes.ToHashSet();
                _document.Connections.RemoveAll(c => deleteSet.Contains(c.From.Node) || deleteSet.Contains(c.To.Node));
                _document.Nodes.RemoveAll(deleteSet.Contains);
                ClearSelection();
                CleanupSockets();
                Invalidate();
            }
        }

        private void CleanupSockets()
        {
            foreach (var node in _document.Nodes)
            {
                RefreshSocketFlags(node);
                EnforceRaceFlowSocketShape(node);
                TrimTrailingUnusedInputs(node);
                TrimTrailingUnusedOutputs(node);
                EnsureSpareInput(node);
                EnsureSpareOutput(node);
            }
        }

        private void ShowContextMenu(Point screenLocation, Point worldLocation)
        {
            var menu = new ContextMenuStrip();

            menu.Items.Add("Add Node", null, (_, _) =>
            {
                int nodeNumber = GetNextDefaultNodeNumber();
                var node = new GraphNode($"Node {nodeNumber}", worldLocation.X, worldLocation.Y);
                ApplySnapToNode(node);
                _document.Nodes.Add(node);
                SetOnlySelectedNode(node);
                _selectedConnection = null;
                Invalidate();
            });

            menu.Items.Add(SnapToGrid ? "Disable Snap (N)" : "Enable Snap (N)", null, (_, _) => ToggleSnapToGrid());
            menu.Items.Add(ShowGrid ? "Hide Grid" : "Show Grid", null, (_, _) => ToggleGrid());
            menu.Show(this, screenLocation);
        }

        private GraphNode DuplicateNode(GraphNode source)
        {
            string newTitle = GetDuplicatedTitle(source.Title);

            var clone = new GraphNode
            {
                Title = newTitle,
                Notes = source.Notes,
                X = source.X + 40,
                Y = source.Y + 40,
                Width = source.Width,
                HeaderHeight = source.HeaderHeight,
                SocketSpacing = source.SocketSpacing,
                BodyPaddingBottom = source.BodyPaddingBottom,
                NodeColorArgb = source.NodeColorArgb,
                Metadata = source.Metadata == null
                    ? new RaceNodeMetadata()
                    : new RaceNodeMetadata
                    {
                        SegmentId = source.Metadata.SegmentId,
                        RacePath = source.Metadata.RacePath,
                        SegmentOrder = source.Metadata.SegmentOrder,
                        NodeType = source.Metadata.NodeType,
                        Side = source.Metadata.Side,
                        Direction = source.Metadata.Direction,
                        IsEndOfRace = source.Metadata.IsEndOfRace,
                        FinishMode = source.Metadata.FinishMode,
                        LoopCount = source.Metadata.LoopCount,
                        LoopCheckpointRequirement = source.Metadata.LoopCheckpointRequirement,
                        Radius = source.Metadata.Radius,
                        Angle = source.Metadata.Angle,
                        MapId = source.Metadata.MapId,
                        WorldX = source.Metadata.WorldX,
                        WorldY = source.Metadata.WorldY,
                        WorldZ = source.Metadata.WorldZ,
                        RuntimeLabel = source.Metadata.RuntimeLabel
                    }
            };

            clone.Inputs.Clear();
            clone.Outputs.Clear();

            for (int i = 0; i < source.Inputs.Count; i++)
                clone.Inputs.Add(new NodeSocket(clone, true, i));

            for (int i = 0; i < source.Outputs.Count; i++)
                clone.Outputs.Add(new NodeSocket(clone, false, i));

            return clone;
        }

        private int GetNextDefaultNodeNumber()
        {
            int nodeNumber = 1;

            while (_document.Nodes.Any(n => string.Equals(n.Title, $"Node {nodeNumber}", StringComparison.OrdinalIgnoreCase)))
                nodeNumber++;

            return nodeNumber;
        }

        private string GetDuplicatedTitle(string originalTitle)
        {
            var match = Regex.Match(originalTitle, @"^(.*?)(\d+)$");

            if (match.Success)
            {
                string prefix = match.Groups[1].Value;
                int number = int.Parse(match.Groups[2].Value);
                return prefix + (number + 1);
            }

            return originalTitle + " Copy";
        }

        private GraphNode? HitTestNode(Point worldPoint)
        {
            for (int i = _document.Nodes.Count - 1; i >= 0; i--)
            {
                if (_document.Nodes[i].Bounds.Contains(worldPoint))
                    return _document.Nodes[i];
            }

            return null;
        }

        private NodeSocket? HitTestSocket(Point worldPoint)
        {
            double hitRadius = Math.Max(8.0, 10.0 / _zoom);

            for (int n = _document.Nodes.Count - 1; n >= 0; n--)
            {
                var node = _document.Nodes[n];

                foreach (var input in node.Inputs)
                {
                    if (Distance(worldPoint, input.GetPosition()) <= hitRadius)
                        return input;
                }

                foreach (var output in node.Outputs)
                {
                    if (Distance(worldPoint, output.GetPosition()) <= hitRadius)
                        return output;
                }
            }

            return null;
        }

        private NodeConnection? HitTestConnection(Point worldPoint)
        {
            foreach (var connection in _document.Connections)
            {
                Point a = connection.From.GetPosition();
                Point b = connection.To.GetPosition();

                Rectangle roughBounds = Rectangle.FromLTRB(Math.Min(a.X, b.X) - 12, Math.Min(a.Y, b.Y) - 12, Math.Max(a.X, b.X) + 12, Math.Max(a.Y, b.Y) + 12);
                if (roughBounds.Contains(worldPoint))
                    return connection;
            }

            return null;
        }

        private static double Distance(Point a, Point b)
        {
            int dx = a.X - b.X;
            int dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static bool CanConnect(NodeSocket a, NodeSocket b)
        {
            if (a.Node == b.Node)
                return false;

            if (a.IsInput == b.IsInput)
                return false;

            NodeSocket input = a.IsInput ? a : b;
            NodeSocket output = a.IsInput ? b : a;

            if (input.IsConnected && !AllowsMultipleInputs(input.Node))
                return false;

            if (output.IsConnected && !AllowsMultipleOutputs(output.Node))
                return false;

            return true;
        }

        private void CreateConnection(NodeSocket a, NodeSocket b)
        {
            NodeSocket from = a.IsInput ? b : a;
            NodeSocket to = a.IsInput ? a : b;

            if (_document.Connections.Any(c => c.From == from && c.To == to))
                return;

            _document.Connections.Add(new NodeConnection(from, to));

            from.IsConnected = true;
            to.IsConnected = true;

            EnsureSpareInput(to.Node);
            EnsureSpareOutput(from.Node);

            EnforceRaceFlowSocketShape(to.Node);
            EnforceRaceFlowSocketShape(from.Node);

            RefreshSocketFlags(from.Node);
            RefreshSocketFlags(to.Node);
            Invalidate();
        }

        private void RefreshSocketFlags(GraphNode node)
        {
            foreach (var input in node.Inputs)
                input.IsConnected = _document.Connections.Any(c => c.To == input);

            foreach (var output in node.Outputs)
                output.IsConnected = _document.Connections.Any(c => c.From == output);
        }

        private void EnsureSpareInput(GraphNode node)
        {
            if (!AllowsMultipleInputs(node))
                return;

            if (node.Inputs.Count == 0 || node.Inputs.All(s => s.IsConnected))
                node.Inputs.Add(new NodeSocket(node, true, node.Inputs.Count));
        }

        private void EnsureSpareOutput(GraphNode node)
        {
            if (!AllowsMultipleOutputs(node))
                return;

            if (node.Outputs.Count == 0 || node.Outputs.All(s => s.IsConnected))
                node.Outputs.Add(new NodeSocket(node, false, node.Outputs.Count));
        }

        private static bool AllowsMultipleInputs(GraphNode node)
        {
            return node.Metadata?.NodeType == RaceFlowNodeType.Converge;
        }

        private static bool AllowsMultipleOutputs(GraphNode node)
        {
            return node.Metadata?.NodeType == RaceFlowNodeType.Split;
        }

        private void EnforceRaceFlowSocketShape(GraphNode node)
        {
            RaceFlowNodeType? type = node.Metadata?.NodeType;
            if (type == null)
                return;

            if (type == RaceFlowNodeType.Split)
            {
                EnsureMinimumInputCount(node, 1);
                EnsureMinimumOutputCount(node, 2);
                TrimTrailingUnusedInputsTo(node, 1);
                TrimTrailingUnusedOutputs(node);
                EnsureMinimumOutputCount(node, 2);
                return;
            }

            if (type == RaceFlowNodeType.Converge)
            {
                EnsureMinimumInputCount(node, 2);
                EnsureMinimumOutputCount(node, 1);
                TrimTrailingUnusedInputs(node);
                TrimTrailingUnusedOutputsTo(node, 1);
                EnsureMinimumInputCount(node, 2);
                return;
            }

            if (type == RaceFlowNodeType.Final)
            {
                EnsureMinimumInputCount(node, 1);
                TrimTrailingUnusedInputsTo(node, 1);
                RemoveAllUnusedOutputs(node);
                return;
            }

            EnsureMinimumInputCount(node, 1);
            EnsureMinimumOutputCount(node, 1);
            TrimTrailingUnusedInputsTo(node, 1);
            TrimTrailingUnusedOutputsTo(node, 1);
        }

        private static void EnsureMinimumInputCount(GraphNode node, int count)
        {
            while (node.Inputs.Count < count)
                node.Inputs.Add(new NodeSocket(node, true, node.Inputs.Count));

            ReindexInputs(node);
        }

        private static void EnsureMinimumOutputCount(GraphNode node, int count)
        {
            while (node.Outputs.Count < count)
                node.Outputs.Add(new NodeSocket(node, false, node.Outputs.Count));

            ReindexOutputs(node);
        }

        private void TrimTrailingUnusedInputsTo(GraphNode node, int maxCount)
        {
            while (node.Inputs.Count > maxCount)
            {
                NodeSocket last = node.Inputs[node.Inputs.Count - 1];
                if (_document.Connections.Any(c => c.To == last))
                    break;

                node.Inputs.RemoveAt(node.Inputs.Count - 1);
            }

            ReindexInputs(node);
        }

        private void TrimTrailingUnusedOutputsTo(GraphNode node, int maxCount)
        {
            while (node.Outputs.Count > maxCount)
            {
                NodeSocket last = node.Outputs[node.Outputs.Count - 1];
                if (_document.Connections.Any(c => c.From == last))
                    break;

                node.Outputs.RemoveAt(node.Outputs.Count - 1);
            }

            ReindexOutputs(node);
        }

        private void RemoveAllUnusedOutputs(GraphNode node)
        {
            for (int i = node.Outputs.Count - 1; i >= 0; i--)
            {
                NodeSocket output = node.Outputs[i];
                if (!_document.Connections.Any(c => c.From == output))
                    node.Outputs.RemoveAt(i);
            }

            ReindexOutputs(node);
        }

        private void TrimTrailingUnusedInputs(GraphNode node)
        {
            int minimumCount = node.Metadata?.NodeType == RaceFlowNodeType.Converge ? 2 : 1;

            while (node.Inputs.Count > minimumCount)
            {
                var last = node.Inputs[node.Inputs.Count - 1];
                var secondLast = node.Inputs[node.Inputs.Count - 2];

                if (!last.IsConnected && !secondLast.IsConnected)
                    node.Inputs.RemoveAt(node.Inputs.Count - 1);
                else
                    break;
            }

            ReindexInputs(node);
        }

        private void TrimTrailingUnusedOutputs(GraphNode node)
        {
            int minimumCount = node.Metadata?.NodeType == RaceFlowNodeType.Split ? 2 : 1;

            while (node.Outputs.Count > minimumCount)
            {
                var last = node.Outputs[node.Outputs.Count - 1];
                var secondLast = node.Outputs[node.Outputs.Count - 2];

                if (!last.IsConnected && !secondLast.IsConnected)
                    node.Outputs.RemoveAt(node.Outputs.Count - 1);
                else
                    break;
            }

            ReindexOutputs(node);
        }

        private static void ReindexInputs(GraphNode node)
        {
            for (int i = 0; i < node.Inputs.Count; i++)
                node.Inputs[i].Index = i;
        }

        private static void ReindexOutputs(GraphNode node)
        {
            for (int i = 0; i < node.Outputs.Count; i++)
                node.Outputs[i].Index = i;
        }

        private int WorldToScreenX(int worldX) => (int)Math.Round((worldX * _zoom) + _panX);
        private int WorldToScreenY(int worldY) => (int)Math.Round((worldY * _zoom) + _panY);
        private Point WorldToScreen(Point worldPoint) => new(WorldToScreenX(worldPoint.X), WorldToScreenY(worldPoint.Y));

        private Rectangle WorldToScreen(Rectangle worldRect)
        {
            int x = WorldToScreenX(worldRect.X);
            int y = WorldToScreenY(worldRect.Y);
            int width = ScaleSize(worldRect.Width);
            int height = ScaleSize(worldRect.Height);
            return new Rectangle(x, y, width, height);
        }

        private Point ScreenToWorld(Point screenPoint)
        {
            return new Point((int)Math.Round((screenPoint.X - _panX) / _zoom), (int)Math.Round((screenPoint.Y - _panY) / _zoom));
        }

        private int ScaleSize(int value) => Math.Max(1, (int)Math.Round(value * _zoom));

        private static int FloorToMultiple(int value, int multiple)
        {
            if (multiple == 0)
                return value;

            int remainder = value % multiple;
            if (remainder == 0)
                return value;
            if (value >= 0)
                return value - remainder;
            return value - remainder - multiple;
        }

        private void SetOnlySelectedNode(GraphNode? node)
        {
            _selectedNodes.Clear();
            if (node != null)
                _selectedNodes.Add(node);
            SetSelectedNode(node);
        }

        private void ToggleNodeSelection(GraphNode node)
        {
            if (_selectedNodes.Contains(node))
                _selectedNodes.Remove(node);
            else
                _selectedNodes.Add(node);

            SetSelectedNode(_selectedNodes.LastOrDefault());
        }

        private void SetSelectedNode(GraphNode? node)
        {
            bool changed = !ReferenceEquals(_selectedNode, node);
            _selectedNode = node;
            if (changed)
                SelectedNodeChanged?.Invoke(_selectedNode);
            else
                SelectedNodeChanged?.Invoke(_selectedNode);
        }

        private void RaiseSelectedNodeChanged()
        {
            SelectedNodeChanged?.Invoke(_selectedNode);
        }

        private void ClearSelection()
        {
            _selectedConnection = null;
            _pendingSocket = null;
            SetOnlySelectedNode(null);
        }

        private Rectangle GetScreenMarqueeRectangle()
        {
            return Rectangle.FromLTRB(
                Math.Min(_marqueeStartScreen.X, _marqueeCurrentScreen.X),
                Math.Min(_marqueeStartScreen.Y, _marqueeCurrentScreen.Y),
                Math.Max(_marqueeStartScreen.X, _marqueeCurrentScreen.X),
                Math.Max(_marqueeStartScreen.Y, _marqueeCurrentScreen.Y));
        }

        private void CompleteMarqueeSelection()
        {
            Rectangle screenRect = GetScreenMarqueeRectangle();
            if (screenRect.Width < 4 && screenRect.Height < 4)
                return;

            Rectangle worldRect = Rectangle.FromLTRB(
                Math.Min(ScreenToWorld(screenRect.Location).X, ScreenToWorld(new Point(screenRect.Right, screenRect.Bottom)).X),
                Math.Min(ScreenToWorld(screenRect.Location).Y, ScreenToWorld(new Point(screenRect.Right, screenRect.Bottom)).Y),
                Math.Max(ScreenToWorld(screenRect.Location).X, ScreenToWorld(new Point(screenRect.Right, screenRect.Bottom)).X),
                Math.Max(ScreenToWorld(screenRect.Location).Y, ScreenToWorld(new Point(screenRect.Right, screenRect.Bottom)).Y));

            if ((ModifierKeys & Keys.Shift) != Keys.Shift)
                _selectedNodes.Clear();

            foreach (GraphNode node in _document.Nodes)
            {
                if (worldRect.IntersectsWith(node.Bounds) && !_selectedNodes.Contains(node))
                    _selectedNodes.Add(node);
            }

            SetSelectedNode(_selectedNodes.LastOrDefault());
        }

        private void ApplySnapToNode(GraphNode node)
        {
            if (!SnapToGrid)
                return;

            // Snap the stored node position itself.
            // RaceFlow.Planner stores X/Y as top-left values, so snapping should use top-left too.
            node.X = SnapCoordinate(node.X);
            node.Y = SnapCoordinate(node.Y);
        }

        private static int SnapCoordinate(int value)
        {
            return (int)Math.Round(value / (double)SnapGridSize) * SnapGridSize;
        }

        private sealed record SegmentBackdropInfo(
            string SegmentId,
            string Title,
            Rectangle Bounds,
            Color FillColor,
            Color HeaderColor,
            Color BorderColor);
    }
}
