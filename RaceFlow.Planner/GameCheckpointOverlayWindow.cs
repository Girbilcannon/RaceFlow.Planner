using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RaceFlow.Planner.Models;
using RaceFlow.Planner.Planner;
using RaceFlow.Planner.Telemetry;
using RaceFlow.Planner.Core;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace RaceFlow.Planner.UI
{
    public sealed class GameCheckpointOverlayWindow : Form
    {
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const bool ShowDebugOverlay = false;
        private const double FadeStartDistance = 30.0;
        private const double FadeEndDistance = 100.0;

        // Avoid magenta color fringing around anti-aliased overlay elements.
        // TransparencyKey only removes the exact key color, so semi-transparent
        // drawing over magenta produced purple SW2 dots. A near-black key leaves
        // softer neutral edges instead.
        private static readonly Color OverlayTransparentKey = Color.FromArgb(1, 0, 1);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

        private readonly WinFormsTimer _renderTimer = new();
        private GraphDocument? _document;
        private TelemetrySnapshot? _snapshot;

        public GameCheckpointOverlayWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            BackColor = OverlayTransparentKey;
            TransparencyKey = OverlayTransparentKey;
            Opacity = 0.70;
            Text = "RaceFlow Planner Game Overlay";

            _renderTimer.Interval = 16;
            _renderTimer.Tick += (_, _) =>
            {
                TelemetrySnapshot? freshSnapshot = PlannerTelemetryRuntime.GetSnapshot();
                if (freshSnapshot != null)
                    _snapshot = freshSnapshot;

                Invalidate();
            };
            _renderTimer.Start();
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        public void UpdateScene(GraphDocument? document, TelemetrySnapshot? snapshot)
        {
            _document = document;
            _snapshot = snapshot;

            Rectangle gameBounds = Rectangle.Empty;
            bool hasGameBounds = TryGetGuildWars2WindowBounds(out gameBounds);

            bool shouldShow =
                document != null &&
                snapshot != null &&
                snapshot.IsLive &&
                IsAllowedForegroundWindow() &&
                hasGameBounds;

            if (shouldShow)
            {
                if (WindowState != FormWindowState.Normal)
                    WindowState = FormWindowState.Normal;

                if (Bounds != gameBounds)
                    Bounds = gameBounds;

                if (!Visible)
                    Show();
            }
            else if (Visible)
            {
                Hide();
            }

            Invalidate();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _renderTimer.Stop();
            _renderTimer.Dispose();
            base.OnFormClosed(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(OverlayTransparentKey);

            int currentMapId = _snapshot?.MapId ?? -1;
            int totalNodes = _document?.Nodes.Count ?? 0;
            int metadataNodes = _document?.Nodes.Count(n => n.Metadata != null) ?? 0;

            List<GraphNode> drawableAnyMapNodes = _document?.Nodes
                .Where(n => n.Metadata != null)
                .Where(n => !n.Metadata!.IsDisabled)
                .Where(n => IsDrawableCheckpoint(n.Metadata!))
                .ToList() ?? new List<GraphNode>();

            List<GraphNode> drawableNodes = currentMapId > 0
                ? drawableAnyMapNodes
                    .Where(n => int.TryParse(n.Metadata!.MapId, out int mapId) && mapId == currentMapId)
                    .ToList()
                : new List<GraphNode>();

            int projectedDots = 0;

            bool canProject =
                _document != null &&
                _snapshot != null &&
                _snapshot.IsLive &&
                _snapshot.HasUsableCamera &&
                currentMapId > 0;

            if (canProject && drawableNodes.Count > 0)
            {
                CameraFrame camera = CameraFrame.FromSnapshot(_snapshot!);

                foreach (GraphNode node in drawableNodes)
                {
                    RaceNodeMetadata metadata = node.Metadata!;
                    SplitWars2NodeMetadata? sw2 = metadata.AlternateSystem?.SplitWars2;

                    Color color = ResolveCheckpointColor(metadata, sw2);
                    float fade = ResolveDistanceFade(metadata);

                    if (fade <= 0.001f)
                        continue;

                    color = ApplyFadeToColor(color, fade);

                    bool isSplitOrConverge =
                        metadata.NodeType == RaceFlowNodeType.Split ||
                        metadata.NodeType == RaceFlowNodeType.Converge;

                    bool isSplitWars2Node =
                        sw2 != null &&
                        string.Equals(
                            metadata.AlternateSystem?.SystemKey,
                            AlternateRaceSystemKeys.SplitWars2,
                            StringComparison.OrdinalIgnoreCase);

                    if (isSplitWars2Node && isSplitOrConverge)
                    {
                        string label = metadata.NodeType == RaceFlowNodeType.Split ? "Split" : "Converge";
                        projectedDots += DrawSplitWars2FlowMarker(e.Graphics, camera, metadata, color, label, fade);
                        continue;
                    }

                    if (!isSplitWars2Node)
                    {
                        projectedDots += DrawRaceFlowRingCheckpoint(e.Graphics, camera, metadata, color, fade);
                        continue;
                    }

                    int triggerType = sw2!.TriggerType;

                    if (triggerType == SplitWars2TriggerTypes.Square)
                    {
                        projectedDots += DrawPlaneCheckpoint(e.Graphics, camera, metadata, sw2, color, fade);
                    }
                    else
                    {
                        projectedDots += DrawCircleCheckpoint(e.Graphics, camera, metadata, sw2, color, fade);
                    }
                }
            }

            if (ShowDebugOverlay)
            {
                DrawDebugOverlay(
                    e.Graphics,
                    currentMapId,
                    totalNodes,
                    metadataNodes,
                    drawableAnyMapNodes.Count,
                    drawableNodes.Count,
                    projectedDots);
            }
        }



        private void DrawDebugOverlay(
            Graphics g,
            int currentMapId,
            int totalNodes,
            int metadataNodes,
            int drawableAnyMapNodes,
            int drawableSameMapNodes,
            int projectedDots)
        {
            if (_snapshot == null)
                return;

            string cameraStatus = _snapshot.HasUsableCamera ? "YES" : "NO";
            string liveStatus = _snapshot.IsLive ? "YES" : "NO";
            string telemetryStatus = _snapshot.HasUsableTelemetry ? "YES" : "NO";

            string text =
                "RFPlanner Overlay\n" +
                $"Live: {liveStatus}  Telemetry: {telemetryStatus}  Camera: {cameraStatus}\n" +
                $"Map: {currentMapId}  FOV: {_snapshot.Fov:0.###}\n" +
                $"Player: {_snapshot.X:0.###}, {_snapshot.Y:0.###}, {_snapshot.Z:0.###}\n" +
                $"Camera: {_snapshot.CameraX:0.###}, {_snapshot.CameraY:0.###}, {_snapshot.CameraZ:0.###}\n" +
                $"Front: {_snapshot.CameraFrontX:0.###}, {_snapshot.CameraFrontY:0.###}, {_snapshot.CameraFrontZ:0.###}\n" +
                $"Top: {_snapshot.CameraTopX:0.###}, {_snapshot.CameraTopY:0.###}, {_snapshot.CameraTopZ:0.###}\n" +
                $"Nodes: {totalNodes}  Metadata: {metadataNodes}\n" +
                $"Drawable: {drawableAnyMapNodes}  Same-map: {drawableSameMapNodes}\n" +
                $"Projected dots: {projectedDots}";

            using Font font = new("Consolas", 10f, FontStyle.Bold);
            SizeF size = g.MeasureString(text, font);
            RectangleF bg = new(12, 12, size.Width + 18, size.Height + 14);

            using SolidBrush bgBrush = new(Color.FromArgb(165, 0, 0, 0));
            using SolidBrush textBrush = new(Color.FromArgb(240, 120, 255, 150));
            using Pen border = new(Color.FromArgb(180, 120, 255, 150), 1f);

            g.FillRectangle(bgBrush, bg);
            g.DrawRectangle(border, bg.X, bg.Y, bg.Width, bg.Height);
            g.DrawString(text, font, textBrush, bg.X + 9, bg.Y + 7);
        }

        private static bool IsAllowedForegroundWindow()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return false;

            GetWindowThreadProcessId(hwnd, out uint processId);
            if (processId == 0)
                return false;

            try
            {
                int currentProcessId = Environment.ProcessId;
                if (processId == currentProcessId)
                    return true;

                using Process process = Process.GetProcessById((int)processId);
                return IsGuildWars2ProcessName(process.ProcessName);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetGuildWars2WindowBounds(out Rectangle bounds)
        {
            bounds = Rectangle.Empty;

            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    if (!IsGuildWars2ProcessName(process.ProcessName))
                        continue;

                    IntPtr hwnd = process.MainWindowHandle;
                    if (hwnd == IntPtr.Zero)
                        continue;

                    if (!GetWindowRect(hwnd, out NativeRect rect))
                        continue;

                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;

                    if (width <= 0 || height <= 0)
                        continue;

                    bounds = new Rectangle(rect.Left, rect.Top, width, height);
                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        private static bool IsGuildWars2ProcessName(string? processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return false;

            return
                string.Equals(processName, "Gw2-64", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(processName, "Gw2", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(processName, "Guild Wars 2", StringComparison.OrdinalIgnoreCase);
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct NativeRect
        {
            public readonly int Left;
            public readonly int Top;
            public readonly int Right;
            public readonly int Bottom;
        }

        private static bool IsDrawableCheckpoint(RaceNodeMetadata metadata)
        {
            return metadata.NodeType is RaceFlowNodeType.Start or
                RaceFlowNodeType.Checkpoint or
                RaceFlowNodeType.Split or
                RaceFlowNodeType.Converge or
                RaceFlowNodeType.EndSegment or
                RaceFlowNodeType.Final;
        }

        private static Color ResolveCheckpointColor(RaceNodeMetadata metadata, SplitWars2NodeMetadata? sw2)
        {
            bool isSplitWars2Node =
                sw2 != null &&
                string.Equals(
                    metadata.AlternateSystem?.SystemKey,
                    AlternateRaceSystemKeys.SplitWars2,
                    StringComparison.OrdinalIgnoreCase);

            if (isSplitWars2Node)
            {
                if (sw2?.IsStart == true)
                    return Color.FromArgb(255, 80, 255, 130);

                if (sw2?.IsGoal == true)
                    return Color.FromArgb(255, 80, 170, 255);

                if (metadata.NodeType == RaceFlowNodeType.Split)
                    return Color.FromArgb(255, 255, 185, 70);

                if (metadata.NodeType == RaceFlowNodeType.Converge)
                    return Color.FromArgb(255, 180, 105, 255);

                if (sw2?.TriggerType == SplitWars2TriggerTypes.MapChange)
                    return Color.FromArgb(255, 255, 170, 80);

                // SW2's normal checkpoint dots are white.
                return Color.FromArgb(255, 245, 245, 245);
            }

            if (metadata.NodeType == RaceFlowNodeType.Start)
                return Color.FromArgb(255, 80, 255, 130);

            if (metadata.NodeType == RaceFlowNodeType.Final || metadata.IsEndOfRace)
                return Color.FromArgb(255, 80, 170, 255);

            if (metadata.NodeType == RaceFlowNodeType.Split)
                return Color.FromArgb(255, 255, 185, 70);

            if (metadata.NodeType == RaceFlowNodeType.Converge)
                return Color.FromArgb(255, 180, 105, 255);

            if (metadata.NodeType == RaceFlowNodeType.EndSegment)
                return Color.FromArgb(255, 255, 115, 80);

            return Color.FromArgb(255, 245, 245, 245);
        }

        private float ResolveDistanceFade(RaceNodeMetadata metadata)
        {
            if (_snapshot == null)
                return 1.0f;

            double dx = metadata.WorldX - _snapshot.X;
            double dy = metadata.WorldY - _snapshot.Y;
            double dz = metadata.WorldZ - _snapshot.Z;
            double distance = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));

            if (distance <= FadeStartDistance)
                return 1.0f;

            if (distance >= FadeEndDistance)
                return 0.0f;

            double t = (distance - FadeStartDistance) / (FadeEndDistance - FadeStartDistance);
            return (float)(1.0 - t);
        }

        private static Color ApplyFadeToColor(Color color, float fade)
        {
            fade = Math.Max(0f, Math.Min(1f, fade));
            int alpha = (int)Math.Round(color.A * fade);
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        private int DrawRaceFlowRingCheckpoint(Graphics g, CameraFrame camera, RaceNodeMetadata metadata, Color color, float fade)
        {
            double radius = Math.Max(0.25, metadata.Radius);

            // RaceFlow native checkpoints render as a simple, semi-transparent washer.
            // Keep this intentionally flat and cheap: no radial gradient, no stacked inner rings.
            const int segments = 72;
            const double innerRadiusMultiplier = 0.78;

            List<PointF> outerPoints = new(segments);
            List<PointF> innerPoints = new(segments);
            int projectedPoints = 0;

            for (int i = 0; i < segments; i++)
            {
                double angle = (Math.PI * 2.0) * i / segments;
                double cos = Math.Cos(angle);
                double sin = Math.Sin(angle);

                Vector3 outerWorld = new(
                    (float)(metadata.WorldX + cos * radius),
                    (float)metadata.WorldY,
                    (float)(metadata.WorldZ + sin * radius));

                Vector3 innerWorld = new(
                    (float)(metadata.WorldX + cos * radius * innerRadiusMultiplier),
                    (float)metadata.WorldY,
                    (float)(metadata.WorldZ + sin * radius * innerRadiusMultiplier));

                if (!camera.TryProject(outerWorld, ClientRectangle, out PointF outerScreen))
                    return projectedPoints;

                if (!camera.TryProject(innerWorld, ClientRectangle, out PointF innerScreen))
                    return projectedPoints;

                outerPoints.Add(outerScreen);
                innerPoints.Add(innerScreen);
                projectedPoints += 2;
            }

            if (outerPoints.Count < 3 || innerPoints.Count < 3)
                return projectedPoints;

            float clampedFade = Math.Max(0.0f, Math.Min(1.0f, fade));
            int fillAlpha = (int)Math.Round(118 * clampedFade);
            int edgeAlpha = (int)Math.Round(145 * clampedFade);

            if (fillAlpha <= 0 && edgeAlpha <= 0)
                return projectedPoints;

            SmoothingMode previousSmoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            try
            {
                PointF[] outerArray = outerPoints.ToArray();
                PointF[] innerArray = innerPoints.AsEnumerable().Reverse().ToArray();

                using GraphicsPath ringPath = new(FillMode.Alternate);
                ringPath.AddPolygon(outerArray);
                ringPath.AddPolygon(innerArray);

                using SolidBrush fillBrush = new(Color.FromArgb(fillAlpha, color.R, color.G, color.B));
                g.FillPath(fillBrush, ringPath);

                // One subtle outer edge keeps the shape readable without creating visible inner bands.
                using GraphicsPath outerPath = new();
                outerPath.AddPolygon(outerArray);

                using Pen outerEdge = new(
                    Color.FromArgb(edgeAlpha, color.R, color.G, color.B),
                    Math.Max(1.25f, 2.25f * clampedFade))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };

                g.DrawPath(outerEdge, outerPath);
            }
            finally
            {
                g.SmoothingMode = previousSmoothing;
            }

            return projectedPoints;
        }

        private int DrawSplitWars2FlowMarker(Graphics g, CameraFrame camera, RaceNodeMetadata metadata, Color color, string label, float fade)
        {
            Vector3 world = new((float)metadata.WorldX, (float)metadata.WorldY, (float)metadata.WorldZ);
            if (!camera.TryProject(world, ClientRectangle, out PointF screen))
                return 0;

            float size = Math.Max(5f, 12f * fade);
            using SolidBrush brush = new(color);
            DrawDot(g, brush, screen, size);

            using Font font = new("Segoe UI", Math.Max(8f, 11f * fade), FontStyle.Bold);
            using SolidBrush textBrush = new(color);
            using StringFormat format = new()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Far
            };

            RectangleF textRect = new(screen.X - 80f, screen.Y - size - 22f, 160f, 20f);
            g.DrawString(label, font, textBrush, textRect, format);

            return 1;
        }

        private int DrawCircleCheckpoint(Graphics g, CameraFrame camera, RaceNodeMetadata metadata, SplitWars2NodeMetadata? sw2, Color color, float fade)
        {
            int projectedDots = 0;
            double radius = Math.Max(0.25, sw2?.RadiusWidth ?? metadata.Radius);
            int density = Math.Max(32, sw2?.DotDensity ?? 200);

            // Split Wars 2 treats Center/Up/Down on circular checkpoints as sphere latitude angles,
            // not world-space vertical meters. 0 is the equator, +90 is the top pole, -90 is the bottom pole.
            double centerDegrees = Clamp(sw2?.DotCenter ?? 0.0, -90.0, 90.0);
            double upDegrees = Math.Max(0.0, sw2?.DotUp ?? 10.0);
            double downDegrees = Math.Max(0.0, sw2?.DotDown ?? 0.0);

            double minLatitude = Clamp(centerDegrees - downDegrees, -90.0, 90.0);
            double maxLatitude = Clamp(centerDegrees + upDegrees, -90.0, 90.0);

            int ringCount = Math.Max(1, Math.Min(18, (int)Math.Ceiling(Math.Sqrt(density) * 0.65)));
            int baseDotsPerRing = Math.Max(12, density / Math.Max(1, ringCount));

            // Match SW2 more closely: larger, softer, lower-opacity dots rather than sharp pixels.
            SmoothingMode previousSmoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            try
            {
                for (int ring = 0; ring < ringCount; ring++)
                {
                    double t = ringCount == 1 ? 0.0 : ring / (double)(ringCount - 1);
                    double latitudeDegrees = minLatitude + ((maxLatitude - minLatitude) * t);
                    double latitudeRadians = latitudeDegrees * Math.PI / 180.0;

                    double yOffset = Math.Sin(latitudeRadians) * radius;
                    double horizontalRadius = Math.Cos(latitudeRadians) * radius;

                    // Subtle vertical fade: SW2 dots become gentler up the volume but do not vanish harshly.
                    float verticalFade = (float)(1.0 - (0.35 * t));
                    float combinedFade = Math.Max(0.0f, Math.Min(1.0f, fade * verticalFade));
                    if (combinedFade <= 0.01f)
                        continue;

                    Color dotColor = Color.FromArgb(
                        (int)Math.Round(170 * combinedFade),
                        color.R,
                        color.G,
                        color.B);

                    using SolidBrush brush = new(dotColor);

                    int dotsThisRing = Math.Max(8, (int)Math.Round(baseDotsPerRing * Math.Max(0.18, Math.Abs(Math.Cos(latitudeRadians)))));
                    double ringPhase = ring * 2.399963229728653; // golden angle phase, avoids obvious vertical seams

                    for (int i = 0; i < dotsThisRing; i++)
                    {
                        double angle = ringPhase + ((Math.PI * 2.0) * i / dotsThisRing);

                        Vector3 world = new(
                            (float)(metadata.WorldX + Math.Cos(angle) * horizontalRadius),
                            (float)(metadata.WorldY + yOffset),
                            (float)(metadata.WorldZ + Math.Sin(angle) * horizontalRadius));

                        if (camera.TryProject(world, ClientRectangle, out PointF screen))
                        {
                            DrawSoftDot(g, brush, screen, Math.Max(4.8f, 7.8f * Math.Max(0.75f, combinedFade)));
                            projectedDots++;
                        }
                    }
                }
            }
            finally
            {
                g.SmoothingMode = previousSmoothing;
            }

            return projectedDots;
        }

        private int DrawPlaneCheckpoint(Graphics g, CameraFrame camera, RaceNodeMetadata metadata, SplitWars2NodeMetadata? sw2, Color color, float fade)
        {
            int projectedDots = 0;
            double width = Math.Max(0.25, sw2?.RadiusWidth ?? metadata.Radius);
            int density = Math.Max(32, sw2?.DotDensity ?? 200);
            double center = sw2?.DotCenter ?? 0.0;
            double up = Math.Max(0.0, sw2?.DotUp ?? 10.0);
            double down = Math.Max(0.0, sw2?.DotDown ?? 0.0);
            double planeAngle = (sw2?.PlaneAngle ?? 0.0) * Math.PI / 180.0;

            // SW2 plane angle uses an X/Z axis where 0 degrees runs along +Z.
            Vector3 horizontal = new((float)-Math.Sin(planeAngle), 0f, (float)Math.Cos(planeAngle));
            int columns = Math.Max(6, Math.Min(40, (int)Math.Sqrt(density)));
            int rows = Math.Max(6, Math.Min(40, density / Math.Max(1, columns)));

            SmoothingMode previousSmoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            try
            {
                for (int row = 0; row < rows; row++)
                {
                    double vt = rows == 1 ? 0.0 : row / (double)(rows - 1);
                    double vertical = center - down + ((up + down) * vt);

                    float verticalFade = (float)(1.0 - (0.35 * vt));
                    float combinedFade = Math.Max(0.0f, Math.Min(1.0f, fade * verticalFade));
                    if (combinedFade <= 0.01f)
                        continue;

                    Color dotColor = Color.FromArgb(
                        (int)Math.Round(170 * combinedFade),
                        color.R,
                        color.G,
                        color.B);

                    using SolidBrush brush = new(dotColor);

                    for (int column = 0; column < columns; column++)
                    {
                        double ht = columns == 1 ? 0.5 : column / (double)(columns - 1);
                        double side = (ht - 0.5) * width;

                        Vector3 world = new(
                            (float)metadata.WorldX,
                            (float)(metadata.WorldY + vertical),
                            (float)metadata.WorldZ);

                        world += horizontal * (float)side;

                        if (camera.TryProject(world, ClientRectangle, out PointF screen))
                        {
                            DrawSoftDot(g, brush, screen, Math.Max(4.8f, 6.8f * Math.Max(0.6f, combinedFade)));
                            projectedDots++;
                        }
                    }
                }
            }
            finally
            {
                g.SmoothingMode = previousSmoothing;
            }

            return projectedDots;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }

        private static void DrawDot(Graphics g, Brush brush, PointF point, float size)
        {
            float half = size * 0.5f;
            g.FillEllipse(brush, point.X - half, point.Y - half, size, size);
        }

        private static void DrawSoftDot(Graphics g, Brush brush, PointF point, float size)
        {
            float half = size * 0.5f;
            RectangleF rect = new(point.X - half, point.Y - half, size, size);
            g.FillEllipse(brush, rect);
        }

        private readonly struct CameraFrame
        {
            private readonly Vector3 _position;
            private readonly Vector3 _forward;
            private readonly Vector3 _up;
            private readonly Vector3 _right;
            private readonly float _fovRadians;

            private CameraFrame(Vector3 position, Vector3 forward, float fovRadians)
            {
                _position = position;
                _forward = NormalizeOrDefault(forward, Vector3.UnitZ);

                // Match the Nexus/SW2-style projection basis: derive camera right/up from CameraFront
                // and GW2 world-up. Do not depend on CameraTop, because Gw2Sharp may not expose it reliably.
                Vector3 worldUp = Vector3.UnitY;
                Vector3 right = Vector3.Cross(worldUp, _forward);

                if (right.LengthSquared() < 0.000001f)
                    right = Vector3.UnitX;

                _right = Vector3.Normalize(right);
                _up = Vector3.Normalize(Vector3.Cross(_forward, _right));
                _fovRadians = Math.Max(0.1f, fovRadians);
            }

            public static CameraFrame FromSnapshot(TelemetrySnapshot snapshot)
            {
                return new CameraFrame(
                    new Vector3((float)snapshot.CameraX, (float)snapshot.CameraY, (float)snapshot.CameraZ),
                    new Vector3((float)snapshot.CameraFrontX, (float)snapshot.CameraFrontY, (float)snapshot.CameraFrontZ),
                    (float)snapshot.Fov);
            }

            public bool TryProject(Vector3 world, Rectangle viewport, out PointF screen)
            {
                screen = PointF.Empty;

                if (viewport.Width <= 1 || viewport.Height <= 1)
                    return false;

                Vector3 relative = world - _position;

                float cameraX = Vector3.Dot(relative, _right);
                float cameraY = Vector3.Dot(relative, _up);
                float cameraZ = Vector3.Dot(relative, _forward);

                if (cameraZ <= 0.05f)
                    return false;

                float focalY = (viewport.Height * 0.5f) / MathF.Tan(_fovRadians * 0.5f);

                float screenX = viewport.Left + (viewport.Width * 0.5f) + ((cameraX * focalY) / cameraZ);
                float screenY = viewport.Top + (viewport.Height * 0.5f) - ((cameraY * focalY) / cameraZ);

                if (screenX < viewport.Left - 2000 || screenX > viewport.Right + 2000 ||
                    screenY < viewport.Top - 2000 || screenY > viewport.Bottom + 2000)
                {
                    return false;
                }

                screen = new PointF(screenX, screenY);
                return true;
            }

            private static Vector3 NormalizeOrDefault(Vector3 value, Vector3 fallback)
            {
                if (value.LengthSquared() < 0.000001f)
                    return fallback;

                return Vector3.Normalize(value);
            }
        }
    }
}
