using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using RaceFlow.Planner.Core;
using RaceFlow.Planner.Models;
using RaceFlow.Planner.UI;

namespace RaceFlow.Planner.ThemeBuilder
{
    public sealed class ThemeBuilderCanvas : Control
    {
        private GraphDocument? _document;
        private readonly Dictionary<RaceFlowSide, RectangleF> _sectionRects = new();
        private readonly Dictionary<string, SegmentPreviewLayout> _segmentLayouts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RectangleF> _nodeHitRects = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _selectedSegmentIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _selectedNodeIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Image> _imageCache = new(StringComparer.OrdinalIgnoreCase);
        private ThemeBuilderSelection _selection = ThemeBuilderSelection.ForGlobal();
        private bool _draggingTuning;
        private Point _lastMouse;

        public event Action<ThemeBuilderSelection>? SelectionChanged;
        public event Action? TuningChanged;

        public ThemeBuilderSelection CurrentSelection => _selection;
        public IReadOnlyList<string> SelectedSegmentIds => _selectedSegmentIds.ToList();
        public IReadOnlyList<string> SelectedNodeIds => _selectedNodeIds.ToList();

        public ThemeBuilderCanvas()
        {
            DoubleBuffered = true;
            BackColor = Theme.AppBack;
            ForeColor = Theme.Text;
        }

        public void SetDocument(GraphDocument? document)
        {
            _document = document;
            if (_document != null)
                _document.Tuning ??= new FlowMapTuningData();
            Invalidate();
        }

        public void SelectGlobalTuning()
        {
            _selection = ThemeBuilderSelection.ForGlobal();
            _selectedSegmentIds.Clear();
            _selectedNodeIds.Clear();
            SelectionChanged?.Invoke(_selection);
            Invalidate();
        }

        public void RefreshTuningPreview()
        {
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RenderPreview(g, ClientRectangle, includeBuilderBackdrops: true, transparentBackground: false);
        }

        public Bitmap RenderOutputBitmap(int width, int height)
        {
            width = Math.Max(320, width);
            height = Math.Max(180, height);

            var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using Graphics g = Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            RenderPreview(g, new Rectangle(0, 0, width, height), includeBuilderBackdrops: false, transparentBackground: true);
            return bitmap;
        }

        private void RenderPreview(Graphics g, Rectangle client, bool includeBuilderBackdrops, bool transparentBackground)
        {
            if (!transparentBackground)
                g.Clear(Theme.AppBack);
            else
                g.Clear(Color.Transparent);

            if (client.Width <= 0 || client.Height <= 0)
                return;

            if (_document == null || _document.Nodes.Count == 0)
            {
                if (includeBuilderBackdrops)
                    DrawCenteredMessage(g, client, "Theme Builder preview will appear here once the race graph has nodes.");
                return;
            }

            Dictionary<RaceFlowSide, RectangleF> sectionRects = BuildSectionRects(client, _document);

            // Only the editor preview should update hit-test/selection state.
            // The OBS/browser output renders on the UI thread too, so it must be
            // a passive render pass and never replace editor hit rectangles with
            // output-sized rectangles.
            if (includeBuilderBackdrops)
            {
                _sectionRects.Clear();
                foreach (var pair in sectionRects)
                    _sectionRects[pair.Key] = pair.Value;

                DrawSections(g, sectionRects);
            }

            List<SegmentPreviewGroup> groups = BuildSegmentGroups(_document);
            if (groups.Count == 0)
            {
                if (includeBuilderBackdrops)
                    DrawCenteredMessage(g, client, "No RaceFlow segment data found yet.");
                return;
            }

            Dictionary<string, SegmentPreviewLayout> layouts = BuildSegmentLayouts(groups, sectionRects, _document);
            if (includeBuilderBackdrops)
            {
                _segmentLayouts.Clear();
                foreach (var pair in layouts)
                    _segmentLayouts[pair.Key] = pair.Value;
            }

            Dictionary<string, PointF> nodePoints = BuildNodePoints(layouts);

            if (includeBuilderBackdrops)
                DrawSegmentBounds(g, layouts);

            DrawEdgeShadows(g, _document, nodePoints);
            DrawNodeShadows(g, _document, nodePoints);
            DrawEdges(g, _document, nodePoints);
            DrawNodes(g, _document, nodePoints, includeSelection: includeBuilderBackdrops, updateHitRects: includeBuilderBackdrops);
        }

        private static Dictionary<RaceFlowSide, RectangleF> BuildSectionRects(Rectangle client, GraphDocument? document)
        {
            float margin = 22f;
            float gap = 16f;
            RectangleF full = new RectangleF(
                client.Left + margin,
                client.Top + margin,
                Math.Max(1f, client.Width - (margin * 2f)),
                Math.Max(1f, client.Height - (margin * 2f)));

            if (document?.Tuning != null)
            {
                var admin = document.Tuning.Admin;
                float scale = admin.OutputScale <= 0 ? 1.0f : admin.OutputScale;
                float width = Math.Max(1f, full.Width * scale);
                float height = Math.Max(1f, full.Height * scale);
                full = new RectangleF(
                    full.Left + ((full.Width - width) * 0.5f) + admin.OutputOffsetX,
                    full.Top + ((full.Height - height) * 0.5f) + admin.OutputOffsetY,
                    width,
                    height);
            }

            // Keep the side-lane thickness consistent across all four regions:
            // Left/Right section width should match Top/Bottom section height.
            // This makes a simple one-line race feel evenly padded regardless of side.
            float laneThickness = Math.Max(120f, Math.Min(full.Width, full.Height) * 0.16f);
            laneThickness = Math.Min(laneThickness, Math.Max(80f, (full.Width - (gap * 2f)) / 3f));
            laneThickness = Math.Min(laneThickness, Math.Max(80f, (full.Height - (gap * 2f)) / 3f));

            float sideWidth = laneThickness;
            float topHeight = laneThickness;

            var rects = new Dictionary<RaceFlowSide, RectangleF>
            {
                [RaceFlowSide.Left] = new RectangleF(full.Left, full.Top, sideWidth, full.Height),
                [RaceFlowSide.Top] = new RectangleF(full.Left + sideWidth + gap, full.Top, Math.Max(1f, full.Width - (sideWidth * 2f) - (gap * 2f)), topHeight),
                [RaceFlowSide.Right] = new RectangleF(full.Right - sideWidth, full.Top, sideWidth, full.Height),
                [RaceFlowSide.Bottom] = new RectangleF(full.Left + sideWidth + gap, full.Bottom - topHeight, Math.Max(1f, full.Width - (sideWidth * 2f) - (gap * 2f)), topHeight)
            };

            if (document?.Tuning != null)
            {
                foreach (RaceFlowSide side in rects.Keys.ToList())
                    rects[side] = ApplyLayoutTuning(rects[side], document.Tuning.GetSection(side));
            }

            return rects;
        }

        private void DrawSections(Graphics g, Dictionary<RaceFlowSide, RectangleF> sectionRects)
        {
            using var fill = new SolidBrush(Color.FromArgb(42, 34, 39, 46));
            using var outline = new Pen(Color.FromArgb(95, 75, 86, 100), 1f);
            using var font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
            using var text = new SolidBrush(Color.FromArgb(185, 215, 222, 232));

            foreach (var pair in sectionRects)
            {
                g.FillRectangle(fill, pair.Value);
                g.DrawRectangle(outline, pair.Value.X, pair.Value.Y, pair.Value.Width, pair.Value.Height);
                if (_selection.Kind == ThemeBuilderSelectionKind.Section && _selection.Side == pair.Key)
                {
                    using var selectedPen = new Pen(Color.FromArgb(235, 255, 214, 90), 3f);
                    g.DrawRectangle(selectedPen, pair.Value.X + 2f, pair.Value.Y + 2f, pair.Value.Width - 4f, pair.Value.Height - 4f);
                }
                g.DrawString(pair.Key.ToString(), font, text, pair.Value.Left + 8f, pair.Value.Top + 6f);
            }
        }

        private static List<SegmentPreviewGroup> BuildSegmentGroups(GraphDocument document)
        {
            var groups = new List<SegmentPreviewGroup>();

            foreach (IGrouping<string, GraphNode> grouping in document.Nodes
                .Where(n => n.Metadata != null && !string.IsNullOrWhiteSpace(n.Metadata.SegmentId))
                .GroupBy(n => n.Metadata.SegmentId))
            {
                GraphNode? startNode = grouping.FirstOrDefault(n => n.Metadata.NodeType == RaceFlowNodeType.Start);
                GraphNode metadataSource = startNode ?? grouping.First();

                groups.Add(new SegmentPreviewGroup(
                    grouping.Key,
                    metadataSource.Metadata.RacePath,
                    metadataSource.Metadata.SegmentOrder,
                    metadataSource.Metadata.SegmentName,
                    metadataSource.Metadata.Side,
                    metadataSource.Metadata.Direction,
                    grouping.ToList()));
            }

            return groups
                .OrderBy(g => g.RacePath)
                .ThenBy(g => g.SegmentOrder)
                .ThenBy(g => g.SegmentId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static Dictionary<string, SegmentPreviewLayout> BuildSegmentLayouts(
            List<SegmentPreviewGroup> groups,
            Dictionary<RaceFlowSide, RectangleF> sectionRects,
            GraphDocument? document)
        {
            var result = new Dictionary<string, SegmentPreviewLayout>(StringComparer.OrdinalIgnoreCase);

            foreach (IGrouping<RaceFlowSide, SegmentPreviewGroup> sectionGroup in groups.GroupBy(g => g.Side))
            {
                if (!sectionRects.TryGetValue(sectionGroup.Key, out RectangleF sectionRect))
                    continue;

                List<SegmentPreviewGroup> sectionSegments = sectionGroup
                    .OrderBy(g => g.RacePath)
                    .ThenBy(g => g.SegmentOrder)
                    .ToList();

                for (int i = 0; i < sectionSegments.Count; i++)
                {
                    SegmentPreviewGroup segment = sectionSegments[i];
                    RectangleF target = GetSegmentRect(sectionRect, sectionGroup.Key, segment.Direction, i, sectionSegments.Count);
                    if (document?.Tuning != null)
                        target = ApplyLayoutTuning(target, document.Tuning.GetSegment(segment.SegmentId));

                    RectangleF source = GetSourceBounds(segment.Nodes, sectionGroup.Key, segment.Direction);
                    result[segment.SegmentId] = new SegmentPreviewLayout(segment, target, source);
                }
            }

            return result;
        }

        private static RectangleF GetSegmentRect(RectangleF sectionRect, RaceFlowSide side, RaceFlowDirection direction, int index, int count)
        {
            RectangleF content = RectangleF.Inflate(sectionRect, -12f, -24f);
            if (content.Width <= 1f || content.Height <= 1f)
                content = sectionRect;

            int slotIndex = index;

            if (side == RaceFlowSide.Left || side == RaceFlowSide.Right)
            {
                if (direction == RaceFlowDirection.BottomToTop)
                    slotIndex = (count - 1) - index;

                float h = content.Height / Math.Max(1, count);
                return new RectangleF(content.Left, content.Top + (h * slotIndex), content.Width, Math.Max(1f, h));
            }

            if (direction == RaceFlowDirection.RightToLeft)
                slotIndex = (count - 1) - index;

            float w = content.Width / Math.Max(1, count);
            return new RectangleF(content.Left + (w * slotIndex), content.Top, Math.Max(1f, w), content.Height);
        }

        private static RectangleF GetSourceBounds(List<GraphNode> nodes, RaceFlowSide side, RaceFlowDirection direction)
        {
            List<PointF> points = nodes.Select(n => TransformRawPoint(n.X, n.Y, side)).ToList();
            if (points.Count == 0)
                return new RectangleF(0, 0, 1, 1);

            float minX = points.Min(p => p.X);
            float minY = points.Min(p => p.Y);
            float maxX = points.Max(p => p.X);
            float maxY = points.Max(p => p.Y);
            RectangleF bounds = new RectangleF(minX, minY, Math.Max(1f, maxX - minX), Math.Max(1f, maxY - minY));

            List<PointF> directed = points.Select(p => ApplyDirectionFlip(p, bounds, side, direction)).ToList();
            minX = directed.Min(p => p.X);
            minY = directed.Min(p => p.Y);
            maxX = directed.Max(p => p.X);
            maxY = directed.Max(p => p.Y);

            return new RectangleF(minX, minY, Math.Max(1f, maxX - minX), Math.Max(1f, maxY - minY));
        }

        private static Dictionary<string, PointF> BuildNodePoints(Dictionary<string, SegmentPreviewLayout> layouts)
        {
            var result = new Dictionary<string, PointF>(StringComparer.OrdinalIgnoreCase);

            foreach (SegmentPreviewLayout layout in layouts.Values)
            {
                foreach (GraphNode node in layout.Group.Nodes)
                    result[node.Id] = layout.Transform(node);
            }

            return result;
        }

        private void DrawSegmentBounds(Graphics g, Dictionary<string, SegmentPreviewLayout> layouts)
        {
            using var font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold);

            foreach (SegmentPreviewLayout layout in layouts.Values)
            {
                Color baseColor = layout.Group.GetBackdropColor();
                using var fill = new SolidBrush(Color.FromArgb(34, baseColor));
                using var outline = new Pen(Color.FromArgb(120, baseColor), 1.5f);
                using var text = new SolidBrush(Color.FromArgb(210, 230, 235, 245));

                g.FillRectangle(fill, layout.TargetRect);
                g.DrawRectangle(outline, layout.TargetRect.X, layout.TargetRect.Y, layout.TargetRect.Width, layout.TargetRect.Height);
                if (_selectedSegmentIds.Contains(layout.Group.SegmentId))
                {
                    using var selectedPen = new Pen(Color.FromArgb(245, 255, 214, 90), 3f);
                    g.DrawRectangle(selectedPen, layout.TargetRect.X + 2f, layout.TargetRect.Y + 2f, layout.TargetRect.Width - 4f, layout.TargetRect.Height - 4f);
                }

                string title = string.IsNullOrWhiteSpace(layout.Group.SegmentName)
                    ? $"Segment {layout.Group.SegmentOrder}"
                    : $"Segment {layout.Group.SegmentOrder} - {layout.Group.SegmentName.Replace($"Segment {layout.Group.SegmentOrder}", string.Empty).Trim(' ', '-')}";

                title = title.Trim();
                if (title.EndsWith("-", StringComparison.Ordinal))
                    title = title.Substring(0, title.Length - 1).Trim();

                g.DrawString(title, font, text, layout.TargetRect.Left + 8f, layout.TargetRect.Top + 6f);
            }
        }

        private void DrawEdgeShadows(Graphics g, GraphDocument document, Dictionary<string, PointF> nodePoints)
        {
            ThemeProject? theme = document.Theme;
            if (theme != null && !theme.Settings.LineVisibility)
                return;

            ThemeRenderSettings? settings = theme?.Settings;
            if (settings != null && !settings.ShadowEnabled)
                return;

            var nodeById = document.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

            foreach (NodeConnection connection in document.Connections)
            {
                string? fromId = connection.From?.Node?.Id ?? connection.FromNodeId;
                string? toId = connection.To?.Node?.Id ?? connection.ToNodeId;

                if (string.IsNullOrWhiteSpace(fromId) || string.IsNullOrWhiteSpace(toId))
                    continue;

                if (!nodePoints.TryGetValue(fromId, out PointF from) || !nodePoints.TryGetValue(toId, out PointF to))
                    continue;

                nodeById.TryGetValue(fromId, out GraphNode? fromNode);

                float thickness = GetLineThickness(theme, fromNode);
                DrawSoftLineShadow(g, from, to, thickness, settings);
            }
        }

        private void DrawEdges(Graphics g, GraphDocument document, Dictionary<string, PointF> nodePoints)
        {
            ThemeProject? theme = document.Theme;
            if (theme != null && !theme.Settings.LineVisibility)
                return;

            var nodeById = document.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

            foreach (NodeConnection connection in document.Connections)
            {
                string? fromId = connection.From?.Node?.Id ?? connection.FromNodeId;
                string? toId = connection.To?.Node?.Id ?? connection.ToNodeId;

                if (string.IsNullOrWhiteSpace(fromId) || string.IsNullOrWhiteSpace(toId))
                    continue;

                if (!nodePoints.TryGetValue(fromId, out PointF from) || !nodePoints.TryGetValue(toId, out PointF to))
                    continue;

                nodeById.TryGetValue(fromId, out GraphNode? fromNode);
                nodeById.TryGetValue(toId, out GraphNode? toNode);

                float thickness = GetLineThickness(theme, fromNode);
                Color lineColor = GetLineColor(theme, fromNode, toNode);

                using var pen = new Pen(lineColor, Math.Max(1f, thickness))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                g.DrawLine(pen, from, to);
            }
        }

        private void DrawNodeShadows(Graphics g, GraphDocument document, Dictionary<string, PointF> nodePoints)
        {
            ThemeRenderSettings? settings = document.Theme?.Settings;
            if (settings != null && !settings.ShadowEnabled)
                return;

            foreach (GraphNode node in document.Nodes.OrderBy(n => n.Metadata?.SegmentOrder ?? 0).ThenBy(n => n.X))
            {
                if (!nodePoints.TryGetValue(node.Id, out PointF p))
                    continue;

                NodePreviewStyle style = BuildNodePreviewStyle(document.Theme, node, document.Tuning?.Admin?.OutputNodeTextScale ?? 1.0f);
                if (!style.NodeVisible)
                    continue;

                bool hasImage = GetThemeImage(document.Theme, style.ImageFileName) != null;
                float baseSize = hasImage
                    ? GetNodeIconBaseSize(node.Metadata.NodeType)
                    : GetFallbackNodeSize(node.Metadata.NodeType);
                float size = Math.Max(4f, baseSize * (float)style.NodeScale);
                RectangleF iconRect = new RectangleF(
                    p.X - (size / 2f) + style.ImageOffsetX,
                    p.Y - (size / 2f) + style.ImageOffsetY,
                    size,
                    size);

                DrawNodeShadow(g, iconRect, settings);
            }
        }

        private void DrawNodes(Graphics g, GraphDocument document, Dictionary<string, PointF> nodePoints, bool includeSelection = true, bool updateHitRects = true)
        {
            if (updateHitRects)
                _nodeHitRects.Clear();

            foreach (GraphNode node in document.Nodes.OrderBy(n => n.Metadata?.SegmentOrder ?? 0).ThenBy(n => n.X))
            {
                if (!nodePoints.TryGetValue(node.Id, out PointF p))
                    continue;

                NodePreviewStyle style = BuildNodePreviewStyle(document.Theme, node, document.Tuning?.Admin?.OutputNodeTextScale ?? 1.0f);
                if (!style.NodeVisible)
                    continue;

                Image? image = GetThemeImage(document.Theme, style.ImageFileName);
                float baseSize = image != null
                    ? GetNodeIconBaseSize(node.Metadata.NodeType)
                    : GetFallbackNodeSize(node.Metadata.NodeType);
                float size = Math.Max(4f, baseSize * (float)style.NodeScale);
                RectangleF iconRect = new RectangleF(
                    p.X - (size / 2f) + style.ImageOffsetX,
                    p.Y - (size / 2f) + style.ImageOffsetY,
                    size,
                    size);

                if (updateHitRects)
                    _nodeHitRects[node.Id] = RectangleF.Inflate(iconRect, 8f, 8f);

                Color color = GetNodeColor(node);
                bool selectedNode = includeSelection && _selectedNodeIds.Contains(node.Id);

                if (image != null)
                {
                    DrawNodeImage(g, image, iconRect, selectedNode, document.Theme?.Settings);
                }
                else
                {
                    DrawFallbackNodeShape(g, node, iconRect, color, selectedNode, document.Theme?.Settings);
                }

                if (style.TitleVisible)
                {
                    string title = !string.IsNullOrWhiteSpace(node.Metadata.DisplayName) ? node.Metadata.DisplayName : node.Title;
                    float titleSize = Math.Max(4f, 8f * (float)style.TitleScale);
                    using var labelFont = new Font("Segoe UI", titleSize, FontStyle.Bold);
                    using var label = new SolidBrush(Color.White);
                    SizeF textSize = g.MeasureString(title, labelFont);
                    float titleX = p.X - (textSize.Width / 2f) + style.TitleOffsetX;
                    float titleY = p.Y + (size / 2f) + 5f + style.TitleOffsetY;
                    g.DrawString(title, labelFont, label, titleX, titleY);
                }
            }
        }

        private void DrawNodeImage(Graphics g, Image image, RectangleF rect, bool selected, ThemeRenderSettings? settings)
        {
            g.DrawImage(image, rect);

            if (selected)
            {
                using var selectedPen = new Pen(Color.FromArgb(245, 255, 214, 90), 3f);
                RectangleF selectedRect = RectangleF.Inflate(rect, 5f, 5f);
                g.DrawRectangle(selectedPen, selectedRect.X, selectedRect.Y, selectedRect.Width, selectedRect.Height);
            }
        }

        private void DrawFallbackNodeShape(Graphics g, GraphNode node, RectangleF rect, Color color, bool selected, ThemeRenderSettings? settings)
        {
            using var glow = new SolidBrush(Color.FromArgb(60, color));
            using var fill = new SolidBrush(color);
            using var outline = new Pen(Color.FromArgb(235, 245, 248, 255), 1.5f);

            RectangleF glowRect = RectangleF.Inflate(rect, rect.Width * 0.45f, rect.Height * 0.45f);
            g.FillEllipse(glow, glowRect);

            if (node.Metadata.NodeType == RaceFlowNodeType.Split || node.Metadata.NodeType == RaceFlowNodeType.Converge)
            {
                g.FillRectangle(fill, rect);
                g.DrawRectangle(outline, rect.X, rect.Y, rect.Width, rect.Height);
                if (selected)
                {
                    using var selectedPen = new Pen(Color.FromArgb(245, 255, 214, 90), 3f);
                    RectangleF selectedRect = RectangleF.Inflate(rect, 5f, 5f);
                    g.DrawRectangle(selectedPen, selectedRect.X, selectedRect.Y, selectedRect.Width, selectedRect.Height);
                }
            }
            else
            {
                g.FillEllipse(fill, rect);
                g.DrawEllipse(outline, rect);
                if (selected)
                {
                    using var selectedPen = new Pen(Color.FromArgb(245, 255, 214, 90), 3f);
                    RectangleF selectedRect = RectangleF.Inflate(rect, 5f, 5f);
                    g.DrawEllipse(selectedPen, selectedRect);
                }
            }
        }

        private static void DrawNodeShadow(Graphics g, RectangleF rect, ThemeRenderSettings? settings)
        {
            if (settings != null && !settings.ShadowEnabled)
                return;

            Color shadowColor = GetShadowColor(settings);
            float shadowOffsetX = settings?.ShadowOffsetX ?? 2f;
            float shadowOffsetY = settings?.ShadowOffsetY ?? 2f;
            float blur = Math.Max(0f, settings?.ShadowBlur ?? 4f);
            RectangleF shadowRect = new RectangleF(
                rect.X + shadowOffsetX,
                rect.Y + shadowOffsetY,
                rect.Width,
                rect.Height);

            if (blur <= 0.01f)
            {
                using var brush = new SolidBrush(shadowColor);
                g.FillEllipse(brush, shadowRect);
                return;
            }

            // Soft shadow pass: draw the same-sized shape at nearby offsets with
            // fading alpha. This blurs the edge without scaling the shadow shape.
            foreach (var pass in BuildShadowPasses(shadowColor, blur))
            {
                using var brush = new SolidBrush(pass.Color);
                RectangleF passRect = new RectangleF(
                    shadowRect.X + pass.OffsetX,
                    shadowRect.Y + pass.OffsetY,
                    shadowRect.Width,
                    shadowRect.Height);
                g.FillEllipse(brush, passRect);
            }
        }

        private static void DrawSoftLineShadow(Graphics g, PointF from, PointF to, float thickness, ThemeRenderSettings? settings)
        {
            if (settings != null && !settings.ShadowEnabled)
                return;

            Color shadowColor = GetShadowColor(settings);
            float shadowOffsetX = settings?.ShadowOffsetX ?? 2f;
            float shadowOffsetY = settings?.ShadowOffsetY ?? 2f;
            float blur = Math.Max(0f, settings?.ShadowBlur ?? 4f);
            float penWidth = Math.Max(1f, thickness);

            PointF baseFrom = new PointF(from.X + shadowOffsetX, from.Y + shadowOffsetY);
            PointF baseTo = new PointF(to.X + shadowOffsetX, to.Y + shadowOffsetY);

            if (blur <= 0.01f)
            {
                using var pen = new Pen(shadowColor, penWidth)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                g.DrawLine(pen, baseFrom, baseTo);
                return;
            }

            foreach (var pass in BuildShadowPasses(shadowColor, blur))
            {
                using var pen = new Pen(pass.Color, penWidth)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };

                g.DrawLine(
                    pen,
                    baseFrom.X + pass.OffsetX,
                    baseFrom.Y + pass.OffsetY,
                    baseTo.X + pass.OffsetX,
                    baseTo.Y + pass.OffsetY);
            }
        }

        private static List<ShadowPass> BuildShadowPasses(Color baseColor, float blur)
        {
            var passes = new List<ShadowPass>();
            int baseAlpha = baseColor.A;
            if (baseAlpha <= 0)
                return passes;

            // Center pass keeps the shadow body from becoming hollow.
            passes.Add(new ShadowPass(0f, 0f, Color.FromArgb(
                Math.Clamp((int)Math.Round(baseAlpha * 0.32), 0, 255),
                baseColor.R, baseColor.G, baseColor.B)));

            int rings = Math.Clamp((int)Math.Ceiling(blur / 3f), 1, 8);
            const int samplesPerRing = 12;

            for (int ring = 1; ring <= rings; ring++)
            {
                float radius = blur * ring / rings;
                double fade = 1.0 - ((double)ring / (rings + 1));
                int alpha = Math.Clamp((int)Math.Round(baseAlpha * 0.18 * fade), 0, 255);
                if (alpha <= 0)
                    continue;

                Color color = Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
                for (int i = 0; i < samplesPerRing; i++)
                {
                    double angle = (Math.PI * 2.0 * i) / samplesPerRing;
                    passes.Add(new ShadowPass(
                        (float)Math.Cos(angle) * radius,
                        (float)Math.Sin(angle) * radius,
                        color));
                }
            }

            return passes;
        }

        private readonly record struct ShadowPass(float OffsetX, float OffsetY, Color Color);

        private static float GetFallbackNodeSize(RaceFlowNodeType type)
        {
            return type switch
            {
                RaceFlowNodeType.Start => 15f,
                RaceFlowNodeType.Split => 16f,
                RaceFlowNodeType.Converge => 16f,
                RaceFlowNodeType.EndSegment => 16f,
                RaceFlowNodeType.Final => 18f,
                _ => 12f
            };
        }

        private static float GetNodeIconBaseSize(RaceFlowNodeType type)
        {
            // Imported 500x500 theme icons should feel like a normal RaceFlow icon at scale 1.0.
            // User-facing scale values are applied after this base size, so 0.75/1.0/1.5 are useful ranges.
            return type switch
            {
                RaceFlowNodeType.Start => 56f,
                RaceFlowNodeType.Split => 58f,
                RaceFlowNodeType.Converge => 58f,
                RaceFlowNodeType.EndSegment => 56f,
                RaceFlowNodeType.Final => 62f,
                _ => 50f
            };
        }

        private static Color GetNodeColor(GraphNode node)
        {
            if (node.NodeColorArgb != 0)
                return Color.FromArgb(node.NodeColorArgb);

            return node.Metadata.NodeType switch
            {
                RaceFlowNodeType.Start => Color.FromArgb(44, 180, 90),
                RaceFlowNodeType.Split => Color.FromArgb(225, 150, 55),
                RaceFlowNodeType.Converge => Color.FromArgb(160, 95, 210),
                RaceFlowNodeType.EndSegment => Color.FromArgb(205, 70, 70),
                RaceFlowNodeType.Final => Color.FromArgb(220, 60, 120),
                _ => Color.FromArgb(70, 150, 225)
            };
        }

        private static float GetLineThickness(ThemeProject? theme, GraphNode? fromNode)
        {
            float thickness = theme?.Lines?.Thickness ?? 2.5f;
            if (theme != null && fromNode != null && !string.IsNullOrWhiteSpace(fromNode.Metadata.SegmentId) &&
                theme.SegmentOverrides != null &&
                theme.SegmentOverrides.TryGetValue(fromNode.Metadata.SegmentId, out ThemeSegmentOverride? segmentOverride))
            {
                thickness = segmentOverride.Thickness;
            }

            return Math.Max(1f, thickness);
        }

        private static Color GetLineColor(ThemeProject? theme, GraphNode? fromNode, GraphNode? toNode)
        {
            bool isSplitLine = fromNode?.Metadata.NodeType == RaceFlowNodeType.Split;
            bool isConvergeLine = toNode?.Metadata.NodeType == RaceFlowNodeType.Converge;

            string color = theme?.Lines?.DefaultColor ?? "#5F788C";

            if (isSplitLine && !string.IsNullOrWhiteSpace(theme?.Lines?.SplitColor))
                color = theme!.Lines.SplitColor;
            else if (isConvergeLine && !string.IsNullOrWhiteSpace(theme?.Lines?.ConvergeColor))
                color = theme!.Lines.ConvergeColor;

            if (theme != null && fromNode != null && !string.IsNullOrWhiteSpace(fromNode.Metadata.SegmentId) &&
                theme.SegmentOverrides != null &&
                theme.SegmentOverrides.TryGetValue(fromNode.Metadata.SegmentId, out ThemeSegmentOverride? segmentOverride))
            {
                if (isSplitLine && !string.IsNullOrWhiteSpace(segmentOverride.SplitLineColor))
                    color = segmentOverride.SplitLineColor;
                else if (isConvergeLine && !string.IsNullOrWhiteSpace(segmentOverride.ConvergeLineColor))
                    color = segmentOverride.ConvergeLineColor;
                else if (!isSplitLine && !isConvergeLine && !string.IsNullOrWhiteSpace(segmentOverride.LineColor))
                    color = segmentOverride.LineColor;
            }

            return ParseThemeColor(color, Color.FromArgb(210, 95, 120, 140));
        }

        private static Color GetShadowColor(ThemeRenderSettings? settings)
        {
            if (settings == null)
                return Color.FromArgb(80, 0, 0, 0);

            Color baseColor = ParseThemeColor(settings.ShadowColor, Color.Black);
            int alpha = Math.Clamp((int)Math.Round(255.0 * Math.Clamp(settings.ShadowOpacity, 0.0, 1.0)), 0, 255);
            return Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
        }

        private NodePreviewStyle BuildNodePreviewStyle(ThemeProject? theme, GraphNode node, float adminTextScale)
        {
            var style = new NodePreviewStyle
            {
                NodeVisible = true,
                NodeScale = 1.0,
                TitleVisible = true,
                TitleScale = Math.Max(0.1, adminTextScale),
                TitleOffsetX = 0,
                TitleOffsetY = 0,
                ImageOffsetX = 0,
                ImageOffsetY = 0,
                ImageFileName = string.Empty
            };

            if (theme == null)
                return style;

            string typeKey = GetNodeTypeKey(node.Metadata.NodeType);
            style.NodeVisible = theme.Settings.NodeVisibility;
            style.NodeScale = theme.Settings.NodeScale;
            style.TitleVisible = theme.Settings.TitleVisible;
            style.TitleScale = Math.Max(0.1, adminTextScale) * theme.Settings.TitleScale;
            style.TitleOffsetX = theme.Settings.TitleOffsetX;
            style.TitleOffsetY = theme.Settings.TitleOffsetY;

            if (theme.Nodes != null && theme.Nodes.TryGetValue(typeKey, out string? defaultImage))
                style.ImageFileName = defaultImage ?? string.Empty;

            if (theme.NodeTypeOverrides != null && theme.NodeTypeOverrides.TryGetValue(typeKey, out ThemeNodeOverride? typeOverride))
                ApplyNodeOverride(style, typeOverride, adminTextScale, replaceScale: true);

            if (!string.IsNullOrWhiteSpace(node.Metadata.SegmentId) && theme.SegmentOverrides != null &&
                theme.SegmentOverrides.TryGetValue(node.Metadata.SegmentId, out ThemeSegmentOverride? segmentOverride))
            {
                style.NodeVisible = segmentOverride.NodeVisibility;
                style.NodeScale = segmentOverride.NodeScale;
                style.ImageOffsetX = segmentOverride.ImageOffsetX;
                style.ImageOffsetY = segmentOverride.ImageOffsetY;
                style.TitleVisible = segmentOverride.TitleVisible;
                style.TitleScale = Math.Max(0.1, adminTextScale) * segmentOverride.TitleScale;
                style.TitleOffsetX = segmentOverride.TitleOffsetX;
                style.TitleOffsetY = segmentOverride.TitleOffsetY;
            }

            if (theme.NodeOverrides != null && theme.NodeOverrides.TryGetValue(node.Id, out ThemeNodeOverride? nodeOverride))
                ApplyNodeOverride(style, nodeOverride, adminTextScale, replaceScale: true);

            return style;
        }

        private static void ApplyNodeOverride(NodePreviewStyle style, ThemeNodeOverride nodeOverride, float adminTextScale, bool replaceScale)
        {
            if (!string.IsNullOrWhiteSpace(nodeOverride.Image))
                style.ImageFileName = nodeOverride.Image;

            style.NodeVisible = nodeOverride.NodeVisibility;

            if (replaceScale)
                style.NodeScale = nodeOverride.Scale;
            else
                style.NodeScale *= nodeOverride.Scale;

            style.TitleVisible = nodeOverride.TitleVisible;
            style.TitleScale = Math.Max(0.1, adminTextScale) * nodeOverride.TitleScale;
            style.TitleOffsetX = nodeOverride.TitleOffsetX;
            style.TitleOffsetY = nodeOverride.TitleOffsetY;
            style.ImageOffsetX = nodeOverride.ImageOffsetX;
            style.ImageOffsetY = nodeOverride.ImageOffsetY;
        }

        private Image? GetThemeImage(ThemeProject? theme, string? fileName)
        {
            if (theme == null || string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(theme.IconFolderPath))
                return null;

            string path = Path.Combine(theme.IconFolderPath, fileName.Trim());
            if (!File.Exists(path))
                return null;

            try
            {
                string key = Path.GetFullPath(path);
                if (_imageCache.TryGetValue(key, out Image? cached))
                    return cached;

                using var source = Image.FromFile(path);
                Image clone = new Bitmap(source);
                _imageCache[key] = clone;
                return clone;
            }
            catch
            {
                return null;
            }
        }

        private static string GetNodeTypeKey(RaceFlowNodeType type)
        {
            return type switch
            {
                RaceFlowNodeType.Start => "start",
                RaceFlowNodeType.Split => "split",
                RaceFlowNodeType.Converge => "converge",
                RaceFlowNodeType.EndSegment => "end",
                RaceFlowNodeType.Final => "boss",
                _ => "checkpoint"
            };
        }

        private static Color ParseThemeColor(string? value, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            try
            {
                return ColorTranslator.FromHtml(value.Trim());
            }
            catch
            {
                return fallback;
            }
        }

        private sealed class NodePreviewStyle
        {
            public bool NodeVisible { get; set; }
            public double NodeScale { get; set; }
            public bool TitleVisible { get; set; }
            public double TitleScale { get; set; }
            public int TitleOffsetX { get; set; }
            public int TitleOffsetY { get; set; }
            public int ImageOffsetX { get; set; }
            public int ImageOffsetY { get; set; }
            public string ImageFileName { get; set; } = string.Empty;
        }


        private static RectangleF ApplyLayoutTuning(RectangleF rect, FlowMapLayoutTuning tuning)
        {
            float scale = tuning.VisualScale <= 0 ? 1.0f : tuning.VisualScale;
            float width = Math.Max(1f, rect.Width * scale);
            float height = Math.Max(1f, rect.Height * scale);
            float x = rect.Left + ((rect.Width - width) * 0.5f) + tuning.OffsetX;
            float y = rect.Top + ((rect.Height - height) * 0.5f) + tuning.OffsetY;
            return new RectangleF(x, y, width, height);
        }

        private static PointF TransformRawPoint(float x, float y, RaceFlowSide side)
        {
            if (side == RaceFlowSide.Left || side == RaceFlowSide.Right)
                return new PointF(y, x);

            return new PointF(x, y);
        }

        private static PointF ApplyDirectionFlip(PointF point, RectangleF bounds, RaceFlowSide side, RaceFlowDirection direction)
        {
            float x = point.X;
            float y = point.Y;

            bool vertical = side == RaceFlowSide.Left || side == RaceFlowSide.Right;

            if (vertical && direction == RaceFlowDirection.BottomToTop)
            {
                float relativeY = y - bounds.Top;
                y = bounds.Top + (bounds.Height - relativeY);
            }
            else if (!vertical && direction == RaceFlowDirection.RightToLeft)
            {
                float relativeX = x - bounds.Left;
                x = bounds.Left + (bounds.Width - relativeX);
            }

            return new PointF(x, y);
        }


        private bool SetSelectionIfChanged(ThemeBuilderSelection selection)
        {
            if (IsSameSelection(_selection, selection))
                return false;

            _selection = selection;
            SelectionChanged?.Invoke(_selection);
            return true;
        }

        private static bool IsSameSelection(ThemeBuilderSelection a, ThemeBuilderSelection b)
        {
            if (a.Kind != b.Kind)
                return false;

            if (a.Side != b.Side || !string.Equals(a.SegmentId, b.SegmentId, StringComparison.OrdinalIgnoreCase))
                return false;

            return a.SegmentIds.SequenceEqual(b.SegmentIds, StringComparer.OrdinalIgnoreCase) &&
                   a.NodeIds.SequenceEqual(b.NodeIds, StringComparer.OrdinalIgnoreCase);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (_document == null || e.Button != MouseButtons.Left)
                return;

            bool multiSelect = (ModifierKeys & Keys.Shift) == Keys.Shift;
            _lastMouse = e.Location;

            foreach (var pair in _nodeHitRects.Reverse())
            {
                if (!pair.Value.Contains(e.Location))
                    continue;

                if (!multiSelect)
                {
                    _selectedNodeIds.Clear();
                    _selectedSegmentIds.Clear();
                }

                if (multiSelect && _selectedNodeIds.Contains(pair.Key))
                    _selectedNodeIds.Remove(pair.Key);
                else
                    _selectedNodeIds.Add(pair.Key);

                SetSelectionIfChanged(ThemeBuilderSelection.ForNodes(_selectedNodeIds.ToList()));
                Invalidate();
                return;
            }

            foreach (SegmentPreviewLayout layout in _segmentLayouts.Values.Reverse())
            {
                if (!layout.TargetRect.Contains(e.Location))
                    continue;

                if (!multiSelect)
                {
                    _selectedSegmentIds.Clear();
                    _selectedNodeIds.Clear();
                }

                if (multiSelect && _selectedSegmentIds.Contains(layout.Group.SegmentId))
                    _selectedSegmentIds.Remove(layout.Group.SegmentId);
                else
                    _selectedSegmentIds.Add(layout.Group.SegmentId);

                SetSelectionIfChanged(ThemeBuilderSelection.ForSegment(layout.Group.SegmentId, layout.Group.SegmentName, layout.Group.Side, layout.Group.SegmentOrder, _selectedSegmentIds.ToList()));
                _draggingTuning = !multiSelect;
                Capture = _draggingTuning;
                Invalidate();
                return;
            }

            foreach (var pair in _sectionRects.Reverse())
            {
                if (!pair.Value.Contains(e.Location))
                    continue;

                _selectedSegmentIds.Clear();
                _selectedNodeIds.Clear();
                SetSelectionIfChanged(ThemeBuilderSelection.ForSection(pair.Key));
                _draggingTuning = true;
                Capture = true;
                Invalidate();
                return;
            }

            // Empty preview space clears an active selection back to Global.
            // If Global is already selected, do nothing so the properties pane
            // does not rebuild/reset scroll position from harmless clicks.
            if (_selection.Kind != ThemeBuilderSelectionKind.Global ||
                _selectedSegmentIds.Count > 0 ||
                _selectedNodeIds.Count > 0)
            {
                _selectedSegmentIds.Clear();
                _selectedNodeIds.Clear();
                SetSelectionIfChanged(ThemeBuilderSelection.ForGlobal());
                Invalidate();
            }
            // Users often click the canvas while editing deep settings; keep selection stable.
            return;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!_draggingTuning || _document == null)
                return;

            int dx = e.X - _lastMouse.X;
            int dy = e.Y - _lastMouse.Y;
            if (dx == 0 && dy == 0)
                return;

            _lastMouse = e.Location;

            if (_selection.Kind == ThemeBuilderSelectionKind.Section && _selection.Side.HasValue)
            {
                FlowMapLayoutTuning tuning = _document.Tuning.GetSection(_selection.Side.Value);
                tuning.OffsetX += dx;
                tuning.OffsetY += dy;
                TuningChanged?.Invoke();
                Invalidate();
            }
            else if (_selection.Kind == ThemeBuilderSelectionKind.Segment && !string.IsNullOrWhiteSpace(_selection.SegmentId))
            {
                FlowMapLayoutTuning tuning = _document.Tuning.GetSegment(_selection.SegmentId);
                tuning.OffsetX += dx;
                tuning.OffsetY += dy;
                TuningChanged?.Invoke();
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _draggingTuning = false;
            Capture = false;
        }

        private static void DrawCenteredMessage(Graphics g, Rectangle client, string message)
        {
            using var font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold);
            using var brush = new SolidBrush(Theme.MutedText);
            SizeF size = g.MeasureString(message, font);
            g.DrawString(message, font, brush, client.Left + ((client.Width - size.Width) / 2f), client.Top + ((client.Height - size.Height) / 2f));
        }



        public enum ThemeBuilderSelectionKind
        {
            Global,
            Section,
            Segment,
            Nodes
        }

        public sealed class ThemeBuilderSelection
        {
            public ThemeBuilderSelectionKind Kind { get; private set; }
            public RaceFlowSide? Side { get; private set; }
            public string SegmentId { get; private set; } = string.Empty;
            public IReadOnlyList<string> SegmentIds { get; private set; } = Array.Empty<string>();
            public IReadOnlyList<string> NodeIds { get; private set; } = Array.Empty<string>();
            public string DisplayName { get; private set; } = string.Empty;
            public int SegmentOrder { get; private set; }

            public static ThemeBuilderSelection ForGlobal()
            {
                return new ThemeBuilderSelection
                {
                    Kind = ThemeBuilderSelectionKind.Global,
                    DisplayName = "Global / Admin Output"
                };
            }

            public static ThemeBuilderSelection ForSection(RaceFlowSide side)
            {
                return new ThemeBuilderSelection
                {
                    Kind = ThemeBuilderSelectionKind.Section,
                    Side = side,
                    DisplayName = $"{side} Section"
                };
            }

            public static ThemeBuilderSelection ForSegment(string segmentId, string segmentName, RaceFlowSide side, int segmentOrder, IReadOnlyList<string>? selectedSegmentIds = null)
            {
                string name = string.IsNullOrWhiteSpace(segmentName) ? $"Segment {segmentOrder}" : segmentName;
                IReadOnlyList<string> ids = selectedSegmentIds != null && selectedSegmentIds.Count > 0
                    ? selectedSegmentIds
                    : new[] { segmentId };

                return new ThemeBuilderSelection
                {
                    Kind = ThemeBuilderSelectionKind.Segment,
                    Side = side,
                    SegmentId = segmentId,
                    SegmentIds = ids,
                    SegmentOrder = segmentOrder,
                    DisplayName = ids.Count > 1 ? $"{ids.Count} Segments Selected" : name
                };
            }

            public static ThemeBuilderSelection ForNodes(IReadOnlyList<string> nodeIds)
            {
                return new ThemeBuilderSelection
                {
                    Kind = ThemeBuilderSelectionKind.Nodes,
                    NodeIds = nodeIds,
                    DisplayName = nodeIds.Count == 1 ? "1 Node Selected" : $"{nodeIds.Count} Nodes Selected"
                };
            }
        }

        public FlowMapLayoutTuning? GetSelectedLayoutTuning()
        {
            if (_document == null)
                return null;

            return _selection.Kind switch
            {
                ThemeBuilderSelectionKind.Section when _selection.Side.HasValue => _document.Tuning.GetSection(_selection.Side.Value),
                ThemeBuilderSelectionKind.Segment when !string.IsNullOrWhiteSpace(_selection.SegmentId) => _document.Tuning.GetSegment(_selection.SegmentId),
                _ => null
            };
        }

        private sealed class SegmentPreviewGroup
        {
            public string SegmentId { get; }
            public int RacePath { get; }
            public int SegmentOrder { get; }
            public string SegmentName { get; }
            public RaceFlowSide Side { get; }
            public RaceFlowDirection Direction { get; }
            public List<GraphNode> Nodes { get; }

            public SegmentPreviewGroup(string segmentId, int racePath, int segmentOrder, string segmentName, RaceFlowSide side, RaceFlowDirection direction, List<GraphNode> nodes)
            {
                SegmentId = segmentId;
                RacePath = racePath;
                SegmentOrder = segmentOrder;
                SegmentName = segmentName;
                Side = side;
                Direction = direction;
                Nodes = nodes;
            }

            public Color GetBackdropColor()
            {
                GraphNode? start = Nodes.FirstOrDefault(n => n.Metadata.NodeType == RaceFlowNodeType.Start);
                int argb = start?.Metadata.BackdropColorArgb ?? 0;
                if (argb != 0)
                    return Color.FromArgb(argb);

                int hash = Math.Abs(SegmentId.GetHashCode());
                return Color.FromArgb(80 + (hash % 80), 90 + ((hash / 3) % 70), 110 + ((hash / 7) % 70));
            }
        }

        private sealed class SegmentPreviewLayout
        {
            public SegmentPreviewGroup Group { get; }
            public RectangleF TargetRect { get; }
            public RectangleF SourceBounds { get; }

            private readonly float _scaleX;
            private readonly float _scaleY;
            private readonly float _offsetX;
            private readonly float _offsetY;

            public SegmentPreviewLayout(SegmentPreviewGroup group, RectangleF targetRect, RectangleF sourceBounds)
            {
                Group = group;
                TargetRect = targetRect;
                SourceBounds = sourceBounds;

                float usableWidth = Math.Max(1f, targetRect.Width - 30f);
                float usableHeight = Math.Max(1f, targetRect.Height - 36f);

                // Theme Builder preview is screen-space first, but only the flow axis
                // should be forced to fill the segment slot. The branch axis should keep
                // the authored Planner spacing as closely as possible.
                //
                // Horizontal sections: X is flow axis, Y is branch axis.
                // Vertical sections:   Y is flow axis, X is branch axis.
                bool verticalFlow = group.Side == RaceFlowSide.Left || group.Side == RaceFlowSide.Right;

                if (verticalFlow)
                {
                    _scaleY = usableHeight / Math.Max(1f, sourceBounds.Height);
                    _scaleX = _scaleY;

                    float branchWidth = sourceBounds.Width * _scaleX;
                    if (branchWidth > usableWidth)
                        _scaleX = usableWidth / Math.Max(1f, sourceBounds.Width);
                }
                else
                {
                    _scaleX = usableWidth / Math.Max(1f, sourceBounds.Width);
                    _scaleY = _scaleX;

                    float branchHeight = sourceBounds.Height * _scaleY;
                    if (branchHeight > usableHeight)
                        _scaleY = usableHeight / Math.Max(1f, sourceBounds.Height);
                }

                float scaledWidth = sourceBounds.Width * _scaleX;
                float scaledHeight = sourceBounds.Height * _scaleY;

                _offsetX = targetRect.Left + ((targetRect.Width - scaledWidth) * 0.5f) - (sourceBounds.Left * _scaleX);
                _offsetY = targetRect.Top + ((targetRect.Height - scaledHeight) * 0.5f) - (sourceBounds.Top * _scaleY) + 8f;
            }

            public PointF Transform(GraphNode node)
            {
                PointF point = TransformRawPoint(node.X, node.Y, Group.Side);
                point = ApplyDirectionFlip(point, SourceBounds, Group.Side, Group.Direction);
                return new PointF((point.X * _scaleX) + _offsetX, (point.Y * _scaleY) + _offsetY);
            }
        }
    }
}
