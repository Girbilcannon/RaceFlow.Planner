using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using RaceFlow.Planner.Models;
using RaceFlow.Planner.Core;
using RaceFlow.Planner.Planner;
using RaceFlow.Planner.UI;
using RaceFlow.Planner.Export;
using RaceFlow.Planner.Services;
using RaceFlow.Planner.Telemetry;
using RaceFlow.Planner.ThemeBuilder;
using RaceFlow.Planner.Output;

namespace RaceFlow.Planner
{
    public class MainForm : Form
    {
        private const int ResizeBorderSize = 8;
        private const int OuterMargin = 8;
        private const int TitleBarHeight = 46;
        private const int ToolbarHeight = 88;
        private const int RightPaneWidth = 390;
        private const int FooterHeight = 56;

        private Panel _titleBar = null!;
        private Panel _toolbar = null!;
        private Label _lblTitle = null!;
        private HudButton _btnNewSegment = null!;
        private HudButton _btnCheckpoint = null!;
        private HudButton _btnSplit = null!;
        private HudButton _btnPathCheckpoint = null!;
        private HudButton _btnConverge = null!;
        private HudButton _btnEndSegment = null!;
        private HudButton _btnFinal = null!;
        private HudButton _btnRecenter = null!;
        private HudButton _btnTelemetryFlowTab = null!;
        private HudButton _btnThemeBuilderTab = null!;
        private HudButton _btnMinimize = null!;
        private HudButton _btnMaximize = null!;
        private HudButton _btnClose = null!;

        private GraphCanvas _graph = null!;
        private ThemeBuilderCanvas _themeBuilder = null!;
        private bool _isThemeBuilderActive;

        private System.Windows.Forms.Timer _telemetryTimer = null!;
        private Panel _pnlTelemetryLight = null!;
        private Label _lblTelemetryStatus = null!;

        private Panel _rightPane = null!;
        private Label _lblPropHeader = null!;
        private Label _lblNodeName = null!;
        private TextBox _txtNodeName = null!;
        private Label _lblPosX = null!;
        private NumericUpDown _numNodeX = null!;
        private Label _lblPosY = null!;
        private NumericUpDown _numNodeY = null!;
        private Label _lblNotes = null!;
        private TextBox _txtNotes = null!;
        private Label _lblColor = null!;
        private Panel _pnlColorPreview = null!;
        private HudButton _btnPickColor = null!;
        private CheckBox _chkSnapToGrid = null!;
        private CheckBox _chkShowGrid = null!;

        private HudButton _btnSave = null!;
        private HudButton _btnLoad = null!;
        private Button _btnImportCsv = null!;
        private Button _btnExportFlowMap = null!;
        private HudButton _btnZoomOut = null!;
        private HudButton _btnZoomIn = null!;
        private Label _lblZoom = null!;
        private Button _btnClearGraph = null!;
        private HudButton _btnNewTheme = null!;
        private HudButton _btnLoadTheme = null!;
        private HudButton _btnExportTheme = null!;
        private HudButton _btnImportIcons = null!;
        private HudButton _btnNodeTypeOverride = null!;
        private HudButton _btnSegmentOverride = null!;
        private HudButton _btnNodeOverride = null!;
        private HudButton _btnObsOutput = null!;
        private PlannerObsOutputServer? _obsOutputServer;

        private Point _dragStart;
        private bool _updatingProperties;
        private GraphNode? _currentNode;
        private bool _showingSegmentProperties;
        private ThemeBuilderCanvas.ThemeBuilderSelection? _currentThemeSelection;

        private readonly List<Control> _commonPropertyControls = new();
        private readonly List<Control> _segmentPropertyControls = new();
        private readonly List<Control> _startPropertyControls = new();
        private readonly List<Control> _disablePropertyControls = new();
        private readonly List<Control> _finalPropertyControls = new();
        private readonly List<Control> _themeTuningPropertyControls = new();
        private readonly List<Control> _themeNodeTypeOverridePropertyControls = new();
        private readonly HashSet<string> _collapsedThemeGroups = new(StringComparer.OrdinalIgnoreCase);

        private Label _lblNoSelection = null!;
        private Label _lblSegmentHeader = null!;
        private Label _lblRacePath = null!;
        private NumericUpDown _numRacePath = null!;
        private Label _lblSegmentIndex = null!;
        private NumericUpDown _numSegmentIndex = null!;
        private Label _lblSegmentName = null!;
        private TextBox _txtSegmentName = null!;
        private Label _lblScreenSection = null!;
        private ComboBox _cmbScreenSection = null!;
        private Label _lblFlowDirection = null!;
        private ComboBox _cmbFlowDirection = null!;
        private Label _lblBackdropColor = null!;
        private Panel _pnlBackdropColorPreview = null!;
        private HudButton _btnPickBackdropColor = null!;
        private Label _lblStartHeader = null!;
        private Label _lblDisplayName = null!;
        private TextBox _txtDisplayName = null!;
        private Label _lblTelemetryHeader = null!;
        private Label _lblNodeOptionsHeader = null!;
        private CheckBox _chkNodeDisabled = null!;
        private Label _lblFinishHeader = null!;
        private Label _lblFinishMode = null!;
        private ComboBox _cmbFinishMode = null!;
        private Label _lblLoopCount = null!;
        private NumericUpDown _numLoopCount = null!;
        private Label _lblLoopRequirement = null!;
        private NumericUpDown _numLoopRequirement = null!;
        private Label _lblMapId = null!;
        private TextBox _txtMapId = null!;
        private Label _lblWorldX = null!;
        private NumericUpDown _numWorldX = null!;
        private Label _lblWorldY = null!;
        private NumericUpDown _numWorldY = null!;
        private Label _lblWorldZ = null!;
        private NumericUpDown _numWorldZ = null!;
        private HudButton _btnUpdatePosition = null!;
        private Label _lblRadius = null!;
        private NumericUpDown _numRadius = null!;
        private Label _lblAngle = null!;
        private NumericUpDown _numAngle = null!;
        private Label _lblThemeTuningHeader = null!;
        private Label _lblThemeTuningTarget = null!;
        private Label _lblThemeTuningScale = null!;
        private NumericUpDown _numThemeTuningScale = null!;
        private Label _lblThemeTuningOffsetX = null!;
        private NumericUpDown _numThemeTuningOffsetX = null!;
        private Label _lblThemeTuningOffsetY = null!;
        private NumericUpDown _numThemeTuningOffsetY = null!;
        private Label _lblThemeAdminTextScale = null!;
        private NumericUpDown _numThemeAdminTextScale = null!;
        private Label _lblThemeRacerTextScale = null!;
        private NumericUpDown _numThemeRacerTextScale = null!;
        private ToolTip _toolTip = null!;

        public MainForm()
        {
            Text = "RaceFlow.Planner";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1000, 700);
            Width = 1400;
            Height = 900;
            BackColor = Theme.AppBack;
            ForeColor = Theme.Text;
            DoubleBuffered = true;
            KeyPreview = true;

            BuildUI();
        }

        private void BuildUI()
        {
            SuspendLayout();

            BuildTitleBar();
            BuildToolbar();
            BuildRightPane();
            BuildGraphCanvas();
            BuildThemeBuilderCanvas();
            BuildFooterButtons();
            BuildTelemetryIndicator();

            ResumeLayout(false);
        }

        private void BuildTitleBar()
        {
            _titleBar = new Panel
            {
                BackColor = Theme.TitleBar
            };

            _titleBar.MouseDown += TitleBar_MouseDown;
            _titleBar.MouseMove += TitleBar_MouseMove;
            _titleBar.DoubleClick += (_, _) => ToggleMaximize();

            _lblTitle = new Label
            {
                Text = "RaceFlow.Planner",
                Left = 18,
                Top = 11,
                AutoSize = true,
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 13.5f, FontStyle.Bold)
            };

            _btnMinimize = new HudButton
            {
                Text = "—",
                Width = 38,
                Height = 28,
                Top = 9
            };
            _btnMinimize.Click += (_, _) => WindowState = FormWindowState.Minimized;

            _btnMaximize = new HudButton
            {
                Text = "□",
                Width = 38,
                Height = 28,
                Top = 9
            };
            _btnMaximize.Click += (_, _) => ToggleMaximize();

            _btnClose = new HudButton
            {
                Text = "×",
                Width = 38,
                Height = 28,
                Top = 9
            };
            _btnClose.Click += (_, _) => Close();

            _btnTelemetryFlowTab = new HudButton
            {
                Text = "Telemetry && Flow",
                Width = 150,
                Height = 30,
                Top = 8
            };

            _btnThemeBuilderTab = new HudButton
            {
                Text = "Theme Builder",
                Width = 140,
                Height = 30,
                Top = 8
            };

            _btnTelemetryFlowTab.Click += (_, _) => ShowTelemetryFlowWorkspace();
            _btnThemeBuilderTab.Click += (_, _) => ShowThemeBuilderWorkspace();

            _titleBar.Controls.Add(_lblTitle);
            _titleBar.Controls.Add(_btnTelemetryFlowTab);
            _titleBar.Controls.Add(_btnThemeBuilderTab);
            _titleBar.Controls.Add(_btnMinimize);
            _titleBar.Controls.Add(_btnMaximize);
            _titleBar.Controls.Add(_btnClose);

            Controls.Add(_titleBar);
        }

        private void BuildToolbar()
        {
            _toolbar = new Panel
            {
                BackColor = Theme.CardBackAlt
            };

            _btnNewSegment = MakeTitleButton("New Segment", 106);
            _btnNewSegment.Click += (_, _) => _graph.AddRaceFlowNode(RaceFlowNodeType.Start);

            _btnCheckpoint = MakeTitleButton("Checkpoint", 96);
            _btnCheckpoint.Click += (_, _) => _graph.AddRaceFlowNode(RaceFlowNodeType.Checkpoint);

            _btnPathCheckpoint = MakeTitleButton("Path CP", 82);
            _btnPathCheckpoint.Click += (_, _) => _graph.AddPathCheckpointNode();

            _btnSplit = MakeTitleButton("Split", 64);
            _btnSplit.Click += (_, _) => _graph.AddRaceFlowNode(RaceFlowNodeType.Split);

            _btnConverge = MakeTitleButton("Converge", 88);
            _btnConverge.Click += (_, _) => _graph.AddRaceFlowNode(RaceFlowNodeType.Converge);

            _btnEndSegment = MakeTitleButton("End Segment", 112);
            _btnEndSegment.Click += (_, _) => _graph.AddRaceFlowNode(RaceFlowNodeType.EndSegment);

            _btnFinal = MakeTitleButton("Final", 64);
            _btnFinal.Click += (_, _) => _graph.AddRaceFlowNode(RaceFlowNodeType.Final);

            _btnRecenter = MakeTitleButton("Recenter", 96);
            _btnRecenter.Click += (_, _) => _graph.CenterView();

            _toolbar.Controls.Add(_btnNewSegment);
            _toolbar.Controls.Add(_btnCheckpoint);
            _toolbar.Controls.Add(_btnPathCheckpoint);
            _toolbar.Controls.Add(_btnSplit);
            _toolbar.Controls.Add(_btnConverge);
            _toolbar.Controls.Add(_btnEndSegment);
            _toolbar.Controls.Add(_btnFinal);
            _toolbar.Controls.Add(_btnRecenter);

            Controls.Add(_toolbar);
        }

        private static HudButton MakeTitleButton(string text, int width)
        {
            return new HudButton
            {
                Text = text,
                Top = 11,
                Width = width,
                Height = 32
            };
        }

        private void BuildRightPane()
        {
            _rightPane = new Panel
            {
                BackColor = Theme.CardBackAlt,
                AutoScroll = true
            };

            _toolTip = new ToolTip();

            _lblPropHeader = new Label
            {
                Text = "Properties",
                Left = 18,
                Top = 18,
                AutoSize = true,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold)
            };

            _lblNoSelection = new Label
            {
                Text = "Select a node to edit its properties.",
                Left = 18,
                Top = 58,
                Width = 260,
                Height = 44,
                ForeColor = Theme.MutedText,
                BackColor = Color.Transparent
            };

            int y = 58;

            _lblSegmentHeader = MakeSectionHeader("Segment Properties", y);
            y += 34;

            _lblRacePath = Theme.MakeLabel("Race Path", 18, y, true, true);
            _numRacePath = MakeIntegerBox(18, y + 22, 126, 1, 999);
            _numRacePath.ValueChanged += RacePath_ValueChanged;
            _toolTip.SetToolTip(_numRacePath, "Use Race Path 1 for normal races. Use Race Path 2, 3, etc. only when multiple independent race flows run at the same time.");
            _toolTip.SetToolTip(_lblRacePath, "Use Race Path 1 for normal races. Use Race Path 2, 3, etc. only when multiple independent race flows run at the same time.");

            _lblSegmentIndex = Theme.MakeLabel("Segment Index", 162, y, true, true);
            _numSegmentIndex = MakeIntegerBox(162, y + 22, 126, 1, 999);
            _numSegmentIndex.ValueChanged += SegmentIndex_ValueChanged;
            y += 68;

            _lblSegmentName = Theme.MakeLabel("Segment Name", 18, y, true, true);
            _txtSegmentName = MakeTextBox(18, y + 22, 270);
            _txtSegmentName.TextChanged += SegmentName_TextChanged;
            y += 66;

            _lblScreenSection = Theme.MakeLabel("Screen Section", 18, y, true, true);
            _cmbScreenSection = MakeComboBox(18, y + 22, 126);
            _cmbScreenSection.Items.AddRange(new object[] { "Left", "Top", "Right", "Bottom" });
            _cmbScreenSection.SelectedIndexChanged += ScreenSection_SelectedIndexChanged;

            _lblFlowDirection = Theme.MakeLabel("Flow Direction", 162, y, true, true);
            _cmbFlowDirection = MakeComboBox(162, y + 22, 126);
            _cmbFlowDirection.Items.AddRange(new object[] { "BottomToTop", "TopToBottom", "LeftToRight", "RightToLeft" });
            _cmbFlowDirection.SelectedIndexChanged += FlowDirection_SelectedIndexChanged;
            y += 68;

            _lblBackdropColor = Theme.MakeLabel("Backdrop Color", 18, y, true, true);
            _pnlBackdropColorPreview = MakeColorPreview(18, y + 24);
            _btnPickBackdropColor = new HudButton
            {
                Text = "Pick Color",
                Left = 74,
                Top = y + 19,
                Width = 110,
                Height = 32
            };
            _btnPickBackdropColor.Click += (_, _) => PickBackdropColor();
            y += 62;

            _lblStartHeader = MakeSectionHeader("Start Node Properties", y);
            y += 34;

            _lblNodeName = Theme.MakeLabel("Node Name", 18, y, true, true);
            _txtNodeName = MakeTextBox(18, y + 22, 270);
            _txtNodeName.TextChanged += NodeName_TextChanged;
            y += 66;

            _lblDisplayName = Theme.MakeLabel("Display Name", 18, y, true, true);
            _txtDisplayName = MakeTextBox(18, y + 22, 270);
            _txtDisplayName.TextChanged += DisplayName_TextChanged;
            y += 66;

            _lblColor = Theme.MakeLabel("Node Color", 18, y, true, true);
            _pnlColorPreview = MakeColorPreview(18, y + 24);
            _btnPickColor = new HudButton
            {
                Text = "Pick Color",
                Left = 74,
                Top = y + 19,
                Width = 110,
                Height = 32
            };
            _btnPickColor.Click += (_, _) => PickNodeColor();
            y += 62;

            _lblPosX = Theme.MakeLabel("Node Position X", 18, y, true, true);
            _numNodeX = MakeIntegerBox(18, y + 22, 126, -100000, 100000);
            _numNodeX.ValueChanged += NodeX_ValueChanged;

            _lblPosY = Theme.MakeLabel("Node Position Y", 162, y, true, true);
            _numNodeY = MakeIntegerBox(162, y + 22, 126, -100000, 100000);
            _numNodeY.ValueChanged += NodeY_ValueChanged;
            y += 70;

            _lblNodeOptionsHeader = MakeSectionHeader("Node Options", y);
            y += 34;

            _chkNodeDisabled = new CheckBox
            {
                Text = "Disable this node",
                Left = 18,
                Top = y,
                Width = 270,
                Height = 24,
                ForeColor = Theme.Text,
                BackColor = Color.Transparent
            };
            _chkNodeDisabled.CheckedChanged += NodeDisabled_CheckedChanged;
            y += 48;

            _lblFinishHeader = MakeSectionHeader("Final Node Finish", y);
            y += 34;

            _lblFinishMode = Theme.MakeLabel("Finish Trigger Type", 18, y, true, true);
            _cmbFinishMode = MakeComboBox(18, y + 22, 270);
            _cmbFinishMode.Items.AddRange(new object[]
            {
                "Auto Finish — checkpoint locks final time",
                "Manual Finish — checkpoint marks yellow until admin confirms",
                "Loop Finish — loop/finish point counts laps"
            });
            _cmbFinishMode.SelectedIndexChanged += FinishMode_SelectedIndexChanged;
            y += 68;

            _lblLoopCount = Theme.MakeLabel("Lap Count", 18, y, true, true);
            _numLoopCount = MakeIntegerBox(18, y + 22, 126, 1, 999);
            _numLoopCount.ValueChanged += LoopCount_ValueChanged;

            _lblLoopRequirement = Theme.MakeLabel("Required Checkpoints", 162, y, true, true);
            _numLoopRequirement = MakeIntegerBox(162, y + 22, 126, 0, 9999);
            _numLoopRequirement.ValueChanged += LoopRequirement_ValueChanged;
            y += 70;

            _lblTelemetryHeader = MakeSectionHeader("Node Telemetry", y);
            y += 34;

            _lblMapId = Theme.MakeLabel("Map ID", 18, y, true, true);
            _txtMapId = MakeTextBox(18, y + 22, 126);
            _txtMapId.TextChanged += MapId_TextChanged;
            y += 66;

            _lblWorldX = Theme.MakeLabel("X", 18, y, true, true);
            _numWorldX = MakeDecimalBox(18, y + 22, 82, -1000000, 1000000, 6);
            _numWorldX.ValueChanged += WorldX_ValueChanged;

            _lblWorldY = Theme.MakeLabel("Y", 110, y, true, true);
            _numWorldY = MakeDecimalBox(110, y + 22, 82, -1000000, 1000000, 6);
            _numWorldY.ValueChanged += WorldY_ValueChanged;

            _lblWorldZ = Theme.MakeLabel("Z", 202, y, true, true);
            _numWorldZ = MakeDecimalBox(202, y + 22, 86, -1000000, 1000000, 6);
            _numWorldZ.ValueChanged += WorldZ_ValueChanged;
            y += 70;

            _btnUpdatePosition = new HudButton
            {
                Text = "Update Position",
                Left = 18,
                Top = y,
                Width = 270,
                Height = 32
            };
            _btnUpdatePosition.Click += (_, _) => UpdateSelectedNodePositionFromTelemetry();
            y += 50;

            _lblRadius = Theme.MakeLabel("Checkpoint Radius", 18, y, true, true);
            _numRadius = MakeDecimalBox(18, y + 22, 126, 0, 10000, 2);
            _numRadius.ValueChanged += Radius_ValueChanged;

            _lblAngle = Theme.MakeLabel("Checkpoint Angle", 162, y, true, true);
            _numAngle = MakeDecimalBox(162, y + 22, 126, -1, 360, 0);
            _numAngle.ValueChanged += Angle_ValueChanged;
            y += 70;

            _lblNotes = Theme.MakeLabel("Notes", 18, y, true, true);
            _txtNotes = new TextBox
            {
                Left = 18,
                Top = y + 22,
                Width = 270,
                Height = 130,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            Theme.ApplyTextBoxStyle(_txtNotes);
            _txtNotes.TextChanged += Notes_TextChanged;

            _rightPane.Controls.Add(_lblPropHeader);
            _rightPane.Controls.Add(_lblNoSelection);

            _lblThemeTuningHeader = MakeSectionHeader("FlowMap Tuning", y + 180);
            _lblThemeTuningTarget = Theme.MakeLabel("Global / Admin Output", 18, y + 214, true, true);

            _lblThemeTuningScale = Theme.MakeLabel("Scale", 18, y + 248, true, true);
            _numThemeTuningScale = MakeDecimalBox(18, y + 270, 126, 0.1m, 10m, 2);
            _numThemeTuningScale.ValueChanged += ThemeTuningScale_ValueChanged;

            _lblThemeTuningOffsetX = Theme.MakeLabel("Offset X", 162, y + 248, true, true);
            _numThemeTuningOffsetX = MakeDecimalBox(162, y + 270, 126, -100000, 100000, 0);
            _numThemeTuningOffsetX.ValueChanged += ThemeTuningOffsetX_ValueChanged;

            _lblThemeTuningOffsetY = Theme.MakeLabel("Offset Y", 18, y + 318, true, true);
            _numThemeTuningOffsetY = MakeDecimalBox(18, y + 340, 126, -100000, 100000, 0);
            _numThemeTuningOffsetY.ValueChanged += ThemeTuningOffsetY_ValueChanged;

            _lblThemeAdminTextScale = Theme.MakeLabel("Node Text Scale", 18, y + 388, true, true);
            _numThemeAdminTextScale = MakeDecimalBox(18, y + 410, 126, 0.1m, 10m, 2);
            _numThemeAdminTextScale.ValueChanged += ThemeAdminTextScale_ValueChanged;

            _lblThemeRacerTextScale = Theme.MakeLabel("Racer Text Scale", 162, y + 388, true, true);
            _numThemeRacerTextScale = MakeDecimalBox(162, y + 410, 126, 0.1m, 10m, 2);
            _numThemeRacerTextScale.ValueChanged += ThemeRacerTextScale_ValueChanged;

            _rightPane.Controls.Add(_lblThemeTuningHeader);
            _rightPane.Controls.Add(_lblThemeTuningTarget);
            _rightPane.Controls.Add(_lblThemeTuningScale);
            _rightPane.Controls.Add(_numThemeTuningScale);
            _rightPane.Controls.Add(_lblThemeTuningOffsetX);
            _rightPane.Controls.Add(_numThemeTuningOffsetX);
            _rightPane.Controls.Add(_lblThemeTuningOffsetY);
            _rightPane.Controls.Add(_numThemeTuningOffsetY);
            _rightPane.Controls.Add(_lblThemeAdminTextScale);
            _rightPane.Controls.Add(_numThemeAdminTextScale);
            _rightPane.Controls.Add(_lblThemeRacerTextScale);
            _rightPane.Controls.Add(_numThemeRacerTextScale);

            AddThemeTuningPropertyControls(
                _lblThemeTuningHeader, _lblThemeTuningTarget,
                _lblThemeTuningScale, _numThemeTuningScale,
                _lblThemeTuningOffsetX, _numThemeTuningOffsetX,
                _lblThemeTuningOffsetY, _numThemeTuningOffsetY,
                _lblThemeAdminTextScale, _numThemeAdminTextScale,
                _lblThemeRacerTextScale, _numThemeRacerTextScale);

            AddCommonPropertyControls(
                _lblNodeName, _txtNodeName,
                _lblDisplayName, _txtDisplayName,
                _lblColor, _pnlColorPreview, _btnPickColor,
                _lblPosX, _numNodeX,
                _lblPosY, _numNodeY,
                _lblNotes, _txtNotes);

            AddSegmentPropertyControls(
                _lblSegmentHeader,
                _lblRacePath, _numRacePath,
                _lblSegmentIndex, _numSegmentIndex,
                _lblSegmentName, _txtSegmentName,
                _lblScreenSection, _cmbScreenSection,
                _lblFlowDirection, _cmbFlowDirection,
                _lblBackdropColor, _pnlBackdropColorPreview, _btnPickBackdropColor);

            AddStartPropertyControls(
                _lblStartHeader,
                _lblTelemetryHeader,
                _lblMapId, _txtMapId,
                _lblWorldX, _numWorldX,
                _lblWorldY, _numWorldY,
                _lblWorldZ, _numWorldZ,
                _btnUpdatePosition,
                _lblRadius, _numRadius,
                _lblAngle, _numAngle);

            AddDisablePropertyControls(
                _lblNodeOptionsHeader,
                _chkNodeDisabled);

            AddFinalPropertyControls(
                _lblFinishHeader,
                _lblFinishMode, _cmbFinishMode,
                _lblLoopCount, _numLoopCount,
                _lblLoopRequirement, _numLoopRequirement);

            SetPropertyControlsVisible(false);

            Controls.Add(_rightPane);

        }

        private Label MakeSectionHeader(string text, int top)
        {
            return new Label
            {
                Text = text,
                Left = 18,
                Top = top,
                Width = 270,
                Height = 22,
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold)
            };
        }

        private TextBox MakeTextBox(int left, int top, int width)
        {
            var textBox = new TextBox
            {
                Left = left,
                Top = top,
                Width = width
            };
            Theme.ApplyTextBoxStyle(textBox);
            textBox.KeyDown += SuppressEnterDing_KeyDown;
            return textBox;
        }

        private ComboBox MakeComboBox(int left, int top, int width)
        {
            var comboBox = new ComboBox
            {
                Left = left,
                Top = top,
                Width = width,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            return comboBox;
        }

        private NumericUpDown MakeIntegerBox(int left, int top, int width, int min, int max)
        {
            var box = new NumericUpDown
            {
                Left = left,
                Top = top,
                Width = width,
                Minimum = min,
                Maximum = max,
                DecimalPlaces = 0,
                Increment = 1
            };
            box.KeyDown += SuppressEnterDing_KeyDown;
            return box;
        }

        private NumericUpDown MakeDecimalBox(int left, int top, int width, decimal min, decimal max, int decimalPlaces)
        {
            decimal increment = decimalPlaces >= 6 ? 0.000001m : decimalPlaces >= 2 ? 0.01m : 1m;

            var box = new NumericUpDown
            {
                Left = left,
                Top = top,
                Width = width,
                Minimum = min,
                Maximum = max,
                DecimalPlaces = decimalPlaces,
                Increment = increment
            };
            box.KeyDown += SuppressEnterDing_KeyDown;
            return box;
        }

        private Panel MakeColorPreview(int left, int top)
        {
            var panel = new Panel
            {
                Left = left,
                Top = top,
                Width = 44,
                Height = 24,
                BackColor = Theme.CardBack
            };
            panel.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
            };
            return panel;
        }

        private void AddCommonPropertyControls(params Control[] controls)
        {
            foreach (Control control in controls)
            {
                _commonPropertyControls.Add(control);
                _rightPane.Controls.Add(control);
            }
        }

        private void AddSegmentPropertyControls(params Control[] controls)
        {
            foreach (Control control in controls)
            {
                _segmentPropertyControls.Add(control);
                _rightPane.Controls.Add(control);
            }
        }

        private void AddStartPropertyControls(params Control[] controls)
        {
            foreach (Control control in controls)
            {
                _startPropertyControls.Add(control);
                _rightPane.Controls.Add(control);
            }
        }

        private void AddDisablePropertyControls(params Control[] controls)
        {
            foreach (Control control in controls)
            {
                _disablePropertyControls.Add(control);
                _rightPane.Controls.Add(control);
            }
        }

        private void AddFinalPropertyControls(params Control[] controls)
        {
            foreach (Control control in controls)
            {
                _finalPropertyControls.Add(control);
                _rightPane.Controls.Add(control);
            }
        }

        private void AddThemeTuningPropertyControls(params Control[] controls)
        {
            foreach (Control control in controls)
                _themeTuningPropertyControls.Add(control);
        }

        private void SetThemeTuningPropertyControlsVisible(bool visible)
        {
            foreach (Control control in _themeTuningPropertyControls)
                control.Visible = visible;
        }

        private void SetPropertyControlsVisible(bool visible)
        {
            _lblNoSelection.Visible = !visible;

            foreach (Control control in _commonPropertyControls)
                control.Visible = visible;

            foreach (Control control in _segmentPropertyControls)
                control.Visible = false;

            foreach (Control control in _startPropertyControls)
                control.Visible = false;

            foreach (Control control in _disablePropertyControls)
                control.Visible = false;

            foreach (Control control in _finalPropertyControls)
                control.Visible = false;

            SetThemeTuningPropertyControlsVisible(false);
        }

        private void SetSegmentPropertyControlsVisible(bool visible)
        {
            foreach (Control control in _segmentPropertyControls)
                control.Visible = visible;
        }

        private void SetStartPropertyControlsVisible(bool visible)
        {
            foreach (Control control in _startPropertyControls)
                control.Visible = visible;
        }

        private void SetDisablePropertyControlsVisible(bool visible)
        {
            foreach (Control control in _disablePropertyControls)
                control.Visible = visible;
        }

        private void SetFinalPropertyControlsVisible(bool visible)
        {
            foreach (Control control in _finalPropertyControls)
                control.Visible = visible;
            SetThemeTuningPropertyControlsVisible(false);
        }

        private void BuildGraphCanvas()
        {
            _graph = new GraphCanvas();
            _graph.SelectedNodeChanged += Graph_SelectedNodeChanged;
            _graph.SelectedSegmentChanged += Graph_SelectedSegmentChanged;
            Controls.Add(_graph);
        }

        private void BuildThemeBuilderCanvas()
        {
            _themeBuilder = new ThemeBuilderCanvas
            {
                Visible = false
            };
            _themeBuilder.SelectionChanged += ThemeBuilder_SelectionChanged;
            _themeBuilder.TuningChanged += ThemeBuilder_TuningChanged;
            _themeBuilder.SetDocument(_graph.Document);
            Controls.Add(_themeBuilder);
        }

        private void BuildFooterButtons()
        {
            _chkSnapToGrid = new CheckBox
            {
                Text = "Grid Snapping",
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Checked = _graph?.SnapToGrid ?? false,
                AutoSize = false
            };
            _chkSnapToGrid.CheckedChanged += (_, _) =>
            {
                if (_graph != null)
                    _graph.SnapToGrid = _chkSnapToGrid.Checked;
            };

            _chkShowGrid = new CheckBox
            {
                Text = "Show Grid",
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Checked = _graph?.ShowGrid ?? true,
                AutoSize = false
            };
            _chkShowGrid.CheckedChanged += (_, _) =>
            {
                if (_graph != null)
                    _graph.ShowGrid = _chkShowGrid.Checked;
            };

            _btnSave = new HudButton
            {
                Text = "Save",
                Top = 10,
                Width = 88,
                Height = 32
            };
            _btnSave.Click += (_, _) => SaveLayout();

            _btnLoad = new HudButton
            {
                Text = "Load",
                Top = 10,
                Width = 88,
                Height = 32
            };
            _btnLoad.Click += (_, _) => LoadLayout();

            _btnImportCsv = new Button
            {
                Text = "Import CSV",
                Top = 10,
                Width = 110,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.CardBackAlt,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnImportCsv.FlatAppearance.BorderColor = Theme.Border;
            _btnImportCsv.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 56, 66);
            _btnImportCsv.FlatAppearance.MouseDownBackColor = Color.FromArgb(36, 42, 50);
            _btnImportCsv.Click += (_, _) => ImportSpeedometerCsv();

            _btnExportFlowMap = new Button
            {
                Text = "Export...",
                Top = 48,
                Width = 184,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 150, 92),
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnExportFlowMap.FlatAppearance.BorderColor = Color.FromArgb(80, 190, 120);
            _btnExportFlowMap.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 170, 105);
            _btnExportFlowMap.FlatAppearance.MouseDownBackColor = Color.FromArgb(42, 130, 80);
            _btnExportFlowMap.Click += (_, _) => ExportProject();

            _btnZoomOut = new HudButton
            {
                Text = "-",
                Top = 11,
                Width = 44,
                Height = 32
            };
            _btnZoomOut.Click += (_, _) => _graph?.ZoomOut();

            _btnZoomIn = new HudButton
            {
                Text = "+",
                Top = 11,
                Width = 44,
                Height = 32
            };
            _btnZoomIn.Click += (_, _) => _graph?.ZoomIn();

            _lblZoom = new Label
            {
                Text = "Zoom",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Theme.MutedText,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                Width = 96,
                Height = 18
            };

            _btnClearGraph = new Button
            {
                Text = "Clear Graph",
                Top = 10,
                Width = 110,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.CardBackAlt,
                ForeColor = Color.FromArgb(220, 90, 90),
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnClearGraph.FlatAppearance.BorderColor = Theme.Border;
            _btnClearGraph.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 25, 25);
            _btnClearGraph.FlatAppearance.MouseDownBackColor = Color.FromArgb(55, 30, 30);
            _btnClearGraph.Click += (_, _) => ConfirmClearGraph();

            _btnNewTheme = MakeTitleButton("New Theme", 104);
            _btnNewTheme.Click += (_, _) => CreateNewTheme();

            _btnLoadTheme = MakeTitleButton("Load Theme", 108);
            _btnLoadTheme.Click += (_, _) => LoadTheme();

            _btnExportTheme = MakeTitleButton("Export Theme", 118);
            _btnExportTheme.Click += (_, _) => ExportTheme();

            _btnImportIcons = MakeTitleButton("Import Icons", 112);
            _btnImportIcons.Click += (_, _) => ImportThemeIcons();

            _btnNodeTypeOverride = MakeTitleButton("Node Type Override", 158);
            _btnNodeTypeOverride.Click += (_, _) => ShowNodeTypeOverrideDialog();

            _btnSegmentOverride = MakeTitleButton("Segment Override", 144);
            _btnSegmentOverride.Click += (_, _) => AddSegmentOverrideToSelected();

            _btnNodeOverride = MakeTitleButton("Node Override", 120);
            _btnNodeOverride.Click += (_, _) => AddNodeOverrideToSelected();

            _btnObsOutput = MakeTitleButton("OBS Output", 116);
            _btnObsOutput.Click += (_, _) => OpenObsOutput();

            _chkSnapToGrid.Top = 15;
            _chkSnapToGrid.Height = 24;
            _chkSnapToGrid.Width = 130;
            _chkShowGrid.Top = 15;
            _chkShowGrid.Height = 24;
            _chkShowGrid.Width = 110;

            _toolbar.Controls.Add(_btnSave);
            _toolbar.Controls.Add(_btnLoad);
            _toolbar.Controls.Add(_btnImportCsv);
            _toolbar.Controls.Add(_btnExportFlowMap);
            _toolbar.Controls.Add(_btnClearGraph);
            _toolbar.Controls.Add(_chkSnapToGrid);
            _toolbar.Controls.Add(_chkShowGrid);
            _toolbar.Controls.Add(_btnNewTheme);
            _toolbar.Controls.Add(_btnLoadTheme);
            _toolbar.Controls.Add(_btnExportTheme);
            _toolbar.Controls.Add(_btnImportIcons);
            _toolbar.Controls.Add(_btnNodeTypeOverride);
            _toolbar.Controls.Add(_btnSegmentOverride);
            _toolbar.Controls.Add(_btnNodeOverride);
            _toolbar.Controls.Add(_btnObsOutput);

            Controls.Add(_lblZoom);
            Controls.Add(_btnZoomOut);
            Controls.Add(_btnZoomIn);
            _lblZoom.BringToFront();
            _btnZoomOut.BringToFront();
            _btnZoomIn.BringToFront();
        }

        private void BuildTelemetryIndicator()
        {
            _pnlTelemetryLight = new Panel
            {
                Width = 12,
                Height = 12,
                BackColor = Color.Gray
            };

            _lblTelemetryStatus = new Label
            {
                Text = "Telemetry: Waiting for game telemetry",
                AutoSize = false,
                Height = 22,
                ForeColor = Theme.MutedText,
                BackColor = Color.FromArgb(160, Theme.AppBack),
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            Controls.Add(_lblTelemetryStatus);
            Controls.Add(_pnlTelemetryLight);
            _lblTelemetryStatus.BringToFront();
            _pnlTelemetryLight.BringToFront();

            PlannerTelemetryRuntime.Start();

            _telemetryTimer = new System.Windows.Forms.Timer
            {
                Interval = 250
            };
            _telemetryTimer.Tick += TelemetryTimer_Tick;
            _telemetryTimer.Start();
        }

        private void TelemetryTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                TelemetrySnapshot? snapshot = PlannerTelemetryRuntime.GetSnapshot();
                _graph?.SetLatestTelemetry(snapshot);
                UpdateTelemetryIndicator(snapshot, null);
            }
            catch (Exception ex)
            {
                _graph?.SetLatestTelemetry(null);
                UpdateTelemetryIndicator(null, ex.Message);
            }
        }

        private void UpdateTelemetryIndicator(TelemetrySnapshot? snapshot, string? errorMessage)
        {
            if (_pnlTelemetryLight == null || _lblTelemetryStatus == null)
                return;

            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                _pnlTelemetryLight.BackColor = Color.FromArgb(210, 70, 70);
                _lblTelemetryStatus.Text = "Telemetry: Read error";
                return;
            }

            if (snapshot == null)
            {
                _pnlTelemetryLight.BackColor = Color.Gray;
                _lblTelemetryStatus.Text = "Telemetry: Waiting for game telemetry";
                return;
            }

            if (!snapshot.HasUsableTelemetry)
            {
                _pnlTelemetryLight.BackColor = Color.FromArgb(210, 170, 60);
                _lblTelemetryStatus.Text = "Telemetry: Waiting for game data";
                return;
            }

            if (!snapshot.IsLive)
            {
                _pnlTelemetryLight.BackColor = Color.FromArgb(210, 170, 60);
                _lblTelemetryStatus.Text = $"Telemetry: Stale | Map {snapshot.MapId}";
                return;
            }

            _pnlTelemetryLight.BackColor = Color.FromArgb(80, 220, 120);
            _lblTelemetryStatus.Text =
                $"Telemetry: Connected | Map {snapshot.MapId} | X {snapshot.X:0.000000} Y {snapshot.Y:0.000000} Z {snapshot.Z:0.000000}";
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LayoutCustomUi();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutCustomUi();
        }

        private void LayoutCustomUi()
        {
            if (_titleBar == null || _toolbar == null || _graph == null || _themeBuilder == null || _rightPane == null ||
                _btnSave == null || _btnLoad == null || _btnImportCsv == null || _btnExportFlowMap == null || _btnZoomOut == null ||
                _btnZoomIn == null || _lblZoom == null || _btnClearGraph == null ||
                _chkSnapToGrid == null || _chkShowGrid == null ||
                _btnNewSegment == null || _btnCheckpoint == null || _btnSplit == null ||
                _btnPathCheckpoint == null || _btnConverge == null || _btnEndSegment == null || _btnFinal == null ||
                _btnRecenter == null || _btnTelemetryFlowTab == null || _btnThemeBuilderTab == null || _btnMinimize == null || _btnMaximize == null || _btnClose == null ||
                _btnNewTheme == null || _btnLoadTheme == null || _btnExportTheme == null || _btnImportIcons == null ||
                _btnNodeTypeOverride == null || _btnSegmentOverride == null || _btnNodeOverride == null || _btnObsOutput == null ||
                _pnlTelemetryLight == null || _lblTelemetryStatus == null)
            {
                return;
            }

            int contentLeft = OuterMargin;
            int contentTop = OuterMargin;
            int contentWidth = ClientSize.Width - (OuterMargin * 2);
            int contentHeight = ClientSize.Height - (OuterMargin * 2);

            if (contentWidth <= 0 || contentHeight <= 0)
                return;

            _titleBar.SetBounds(
                contentLeft,
                contentTop,
                contentWidth,
                TitleBarHeight);

            _toolbar.SetBounds(
                contentLeft,
                contentTop + TitleBarHeight,
                contentWidth,
                ToolbarHeight);

            _btnMinimize.Left = _titleBar.Width - 126;
            _btnMaximize.Left = _titleBar.Width - 84;
            _btnClose.Left = _titleBar.Width - 42;

            int tabGap = 8;
            int tabGroupWidth = _btnTelemetryFlowTab.Width + _btnThemeBuilderTab.Width + tabGap;
            int tabLeft = Math.Max(_lblTitle.Right + 24, (_titleBar.Width - tabGroupWidth) / 2);
            int tabMaxLeft = Math.Max(_lblTitle.Right + 24, _btnMinimize.Left - tabGroupWidth - 18);
            tabLeft = Math.Min(tabLeft, tabMaxLeft);
            _btnTelemetryFlowTab.SetBounds(tabLeft, 8, _btnTelemetryFlowTab.Width, 30);
            _btnThemeBuilderTab.SetBounds(_btnTelemetryFlowTab.Right + tabGap, 8, _btnThemeBuilderTab.Width, 30);

            bool themeMode = _isThemeBuilderActive;

            SetToolbarModeVisibility(themeMode);

            if (themeMode)
            {
                // Theme Builder left toolbar group.
                int themeLeft = 12;
                _btnNewTheme.Top = 28;
                _btnLoadTheme.Top = 28;
                _btnExportTheme.Top = 28;

                _btnNewTheme.Left = themeLeft;
                _btnLoadTheme.Left = _btnNewTheme.Right + 8;
                _btnExportTheme.Left = _btnLoadTheme.Right + 8;

                // Theme Builder center toolbar group.
                Control[] themeTools =
                {
                    _btnImportIcons,
                    _btnNodeTypeOverride,
                    _btnSegmentOverride,
                    _btnNodeOverride
                };

                int themeToolGroupWidth = themeTools.Sum(b => b.Width) + ((themeTools.Length - 1) * 8);
                int themeToolLeft = Math.Max(_btnExportTheme.Right + 24, (_toolbar.Width - themeToolGroupWidth) / 2);

                foreach (Control button in themeTools)
                {
                    button.Top = 28;
                    button.Left = themeToolLeft;
                    themeToolLeft += button.Width + 8;
                }

                // Theme Builder right toolbar group: live OBS/browser preview.
                _btnObsOutput.Top = 28;
                _btnObsOutput.Left = Math.Max(themeToolLeft + 24, _toolbar.Width - _btnObsOutput.Width - 12);
            }
            else
            {
                // Left toolbar group: project/file actions.
                int leftGroup = 12;
                _btnLoad.Top = 10;
                _btnSave.Top = 10;
                _btnImportCsv.Top = 10;
                _btnExportFlowMap.Top = 48;
                _btnClearGraph.Top = 10;

                _btnLoad.Left = leftGroup;
                _btnSave.Left = _btnLoad.Right + 8;
                _btnExportFlowMap.Left = leftGroup;

                _btnImportCsv.Left = _btnSave.Right + 24;
                _btnClearGraph.Left = _btnImportCsv.Right + 8;
                leftGroup = _btnClearGraph.Right;

                // Center toolbar group: RaceFlow authoring tools.
                Control[] toolButtons =
                {
                    _btnNewSegment,
                    _btnCheckpoint,
                    _btnPathCheckpoint,
                    _btnSplit,
                    _btnConverge,
                    _btnEndSegment,
                    _btnFinal
                };

                int toolGroupWidth = toolButtons.Sum(b => b.Width) + ((toolButtons.Length - 1) * 8);
                int toolLeft = Math.Max(leftGroup + 20, (_toolbar.Width - toolGroupWidth) / 2);

                foreach (Control button in toolButtons)
                {
                    button.Top = 28;
                    button.Left = toolLeft;
                    toolLeft += button.Width + 8;
                }

                // Right toolbar group: view/grid controls.
                _btnRecenter.Top = 28;
                _chkSnapToGrid.Top = 32;
                _chkShowGrid.Top = 32;
                int rightGroup = _toolbar.Width - 12;
                LayoutToolbarRight(_chkShowGrid, ref rightGroup);
                LayoutToolbarRight(_chkSnapToGrid, ref rightGroup);
                rightGroup -= 10;
                LayoutToolbarRight(_btnRecenter, ref rightGroup);
            }

            int bodyTop = contentTop + TitleBarHeight + ToolbarHeight;
            int bodyHeight = ClientSize.Height - bodyTop - OuterMargin;

            _rightPane.SetBounds(
                ClientSize.Width - OuterMargin - RightPaneWidth,
                bodyTop,
                RightPaneWidth,
                ClientSize.Height - bodyTop - OuterMargin);

            RelayoutPropertyControlsForCurrentSelection();

            int graphWidth = _rightPane.Left - contentLeft;
            int graphHeight = bodyHeight;

            Rectangle workspaceBounds = new Rectangle(
                contentLeft,
                bodyTop,
                Math.Max(100, graphWidth),
                Math.Max(100, graphHeight));

            _graph.SetBounds(workspaceBounds.X, workspaceBounds.Y, workspaceBounds.Width, workspaceBounds.Height);
            _themeBuilder.SetBounds(workspaceBounds.X, workspaceBounds.Y, workspaceBounds.Width, workspaceBounds.Height);

            _graph.Visible = !_isThemeBuilderActive;
            _themeBuilder.Visible = _isThemeBuilderActive;
            if (_isThemeBuilderActive)
                _themeBuilder.SetDocument(_graph.Document);

            // Bottom-left telemetry indicator.
            int telemetryLeft = _graph.Left + 12;
            int telemetryTop = _graph.Bottom - 34;
            _pnlTelemetryLight.SetBounds(telemetryLeft, telemetryTop + 5, 12, 12);
            _lblTelemetryStatus.SetBounds(telemetryLeft + 18, telemetryTop, Math.Min(620, Math.Max(220, _graph.Width - 160)), 22);

            _pnlTelemetryLight.Visible = !_isThemeBuilderActive;
            _lblTelemetryStatus.Visible = !_isThemeBuilderActive;

            if (!_isThemeBuilderActive)
            {
                _pnlTelemetryLight.BringToFront();
                _lblTelemetryStatus.BringToFront();
            }

            // Graph-local zoom controls.
            int zoomTop = _graph.Bottom - 70;
            int zoomLeft = _graph.Right - 116;

            _lblZoom.SetBounds(zoomLeft, zoomTop, 96, 18);
            _btnZoomOut.SetBounds(zoomLeft, zoomTop + 22, 44, 32);
            _btnZoomIn.SetBounds(zoomLeft + 52, zoomTop + 22, 44, 32);

            _lblZoom.Visible = !_isThemeBuilderActive;
            _btnZoomOut.Visible = !_isThemeBuilderActive;
            _btnZoomIn.Visible = !_isThemeBuilderActive;

            if (!_isThemeBuilderActive)
            {
                _lblZoom.BringToFront();
                _btnZoomOut.BringToFront();
                _btnZoomIn.BringToFront();
            }
            else
            {
                _themeBuilder.BringToFront();
                _rightPane.BringToFront();
            }

        }

        private static void LayoutTitleButton(Control button, ref int left)
        {
            button.Left = left;
            left += button.Width + 8;
        }

        private void SetToolbarModeVisibility(bool themeMode)
        {
            _btnLoad.Visible = !themeMode;
            _btnSave.Visible = !themeMode;
            _btnImportCsv.Visible = !themeMode;
            _btnExportFlowMap.Visible = !themeMode;
            _btnClearGraph.Visible = !themeMode;
            _btnNewSegment.Visible = !themeMode;
            _btnCheckpoint.Visible = !themeMode;
            _btnPathCheckpoint.Visible = !themeMode;
            _btnSplit.Visible = !themeMode;
            _btnConverge.Visible = !themeMode;
            _btnEndSegment.Visible = !themeMode;
            _btnFinal.Visible = !themeMode;
            _btnRecenter.Visible = !themeMode;
            _chkSnapToGrid.Visible = !themeMode;
            _chkShowGrid.Visible = !themeMode;

            _btnNewTheme.Visible = themeMode;
            _btnLoadTheme.Visible = themeMode;
            _btnExportTheme.Visible = themeMode;
            _btnImportIcons.Visible = themeMode;
            _btnNodeTypeOverride.Visible = themeMode;
            _btnSegmentOverride.Visible = themeMode;
            _btnNodeOverride.Visible = themeMode;
            _btnObsOutput.Visible = themeMode;

            bool hasTheme = HasActiveTheme();

            // FlowMap tuning can be edited without a theme, but anything that writes
            // theme JSON data should stay locked until a theme is created or loaded.
            _btnNewTheme.Enabled = themeMode;
            _btnLoadTheme.Enabled = themeMode;
            _btnExportTheme.Enabled = themeMode && hasTheme;
            _btnImportIcons.Enabled = themeMode && hasTheme;
            _btnNodeTypeOverride.Enabled = themeMode && hasTheme;
            _btnSegmentOverride.Enabled = themeMode && hasTheme;
            _btnNodeOverride.Enabled = themeMode && hasTheme;
            _btnObsOutput.Enabled = themeMode;
        }

        private bool HasActiveTheme()
        {
            ThemeProject? theme = _graph?.Document?.Theme;
            return theme != null && !string.IsNullOrWhiteSpace(theme.ThemeJsonPath);
        }

        private static void LayoutToolbarRight(Control control, ref int right)
        {
            right -= control.Width;
            control.Left = right;
            right -= 8;
        }

        private void ShowTelemetryFlowWorkspace()
        {
            _isThemeBuilderActive = false;
            _lblNoSelection.Text = "Select a node to edit its properties.";

            if (_showingSegmentProperties)
                Graph_SelectedSegmentChanged(_currentNode);
            else
                Graph_SelectedNodeChanged(_currentNode);

            LayoutCustomUi();
        }

        private void ShowThemeBuilderWorkspace()
        {
            _isThemeBuilderActive = true;
            _themeBuilder.SetDocument(_graph.Document);

            // Theme Builder is a read-only preview workspace for now.
            // Do not clear _currentNode or allow hidden property-field changes to write back
            // to the active graph selection while switching workspaces.
            _updatingProperties = true;

            SetPropertyControlsVisible(false);
            SetSegmentPropertyControlsVisible(false);
            SetStartPropertyControlsVisible(false);
            SetDisablePropertyControlsVisible(false);
            SetFinalPropertyControlsVisible(false);
            ClearPropertyValues();

            _lblPropHeader.Text = "Theme Builder";
            _lblNoSelection.Visible = false;
            SetThemeTuningPropertyControlsVisible(true);
            _currentThemeSelection = ThemeBuilderCanvas.ThemeBuilderSelection.ForGlobal();
            PopulateThemeTuningProperties();
            UpdateThemeBuilderHeader();

            _updatingProperties = false;

            LayoutCustomUi();
            _themeBuilder.SelectGlobalTuning();
            _themeBuilder.Invalidate();
        }

        private void RefreshThemeBuilderPreview()
        {
            if (_themeBuilder == null)
                return;

            _themeBuilder.SetDocument(_graph.Document);
        }

        private void UpdateThemeBuilderHeader()
        {
            if (!_isThemeBuilderActive)
                return;

            ThemeProject? theme = _graph?.Document?.Theme;
            string themeName = theme == null || string.IsNullOrWhiteSpace(theme.DisplayName)
                ? "No Theme Loaded"
                : theme.DisplayName;

            _lblPropHeader.Text = "Theme Builder";
            if (_currentThemeSelection == null || _currentThemeSelection.Kind == ThemeBuilderCanvas.ThemeBuilderSelectionKind.Global)
                _lblThemeTuningTarget.Text = $"Global / Admin Output  |  Theme: {themeName}";
        }


        private void RelayoutPropertyControlsForCurrentSelection()
        {
            if (_rightPane == null || _lblPropHeader == null)
                return;

            RemoveThemeNodeTypeOverridePropertyControls();

            int left = 18;
            int paneWidth = Math.Max(280, _rightPane.ClientSize.Width - 36);
            int colGap = 18;
            int colWidth = Math.Max(110, (paneWidth - colGap) / 2);
            int rightCol = left + colWidth + colGap;

            _lblPropHeader.SetBounds(left, 18, paneWidth, 22);

            if (_isThemeBuilderActive)
            {
                int themeY = 58;
                bool canShowFlowMapTuning = _currentThemeSelection == null || _currentThemeSelection.Kind != ThemeBuilderCanvas.ThemeBuilderSelectionKind.Nodes;
                bool flowOpen = canShowFlowMapTuning && PlaceCollapsibleHeader(_lblThemeTuningHeader, "FlowMap Tuning", "flowmap", ref themeY, paneWidth);
                _lblThemeTuningHeader.Visible = canShowFlowMapTuning;

                _lblThemeTuningTarget.Visible = flowOpen;
                _lblThemeTuningScale.Visible = flowOpen;
                _numThemeTuningScale.Visible = flowOpen;
                _lblThemeTuningOffsetX.Visible = flowOpen;
                _numThemeTuningOffsetX.Visible = flowOpen;
                _lblThemeTuningOffsetY.Visible = flowOpen;
                _numThemeTuningOffsetY.Visible = flowOpen;

                if (flowOpen)
                {
                    _lblThemeTuningTarget.SetBounds(left, themeY, paneWidth, 24);
                    themeY += 42;
                    PlaceFullRow(_lblThemeTuningScale, _numThemeTuningScale, left, colWidth, ref themeY);
                    PlaceTwoColumnRow(_lblThemeTuningOffsetX, _numThemeTuningOffsetX, _lblThemeTuningOffsetY, _numThemeTuningOffsetY, left, rightCol, colWidth, ref themeY);
                }

                bool isGlobalTheme = _currentThemeSelection == null || _currentThemeSelection.Kind == ThemeBuilderCanvas.ThemeBuilderSelectionKind.Global;
                _lblThemeAdminTextScale.Visible = isGlobalTheme && flowOpen;
                _numThemeAdminTextScale.Visible = isGlobalTheme && flowOpen;
                _lblThemeRacerTextScale.Visible = isGlobalTheme && flowOpen;
                _numThemeRacerTextScale.Visible = isGlobalTheme && flowOpen;
                if (isGlobalTheme && flowOpen)
                    PlaceTwoColumnRow(_lblThemeAdminTextScale, _numThemeAdminTextScale, _lblThemeRacerTextScale, _numThemeRacerTextScale, left, rightCol, colWidth, ref themeY);

                bool hasActiveTheme = HasActiveTheme();

                if (isGlobalTheme && hasActiveTheme)
                {
                    BuildDefaultThemeSettings(ref themeY, left, rightCol, colWidth, paneWidth);
                    BuildThemeNodeTypeOverridePropertyCards(ref themeY, left, rightCol, colWidth, paneWidth);
                }

                if (hasActiveTheme)
                {
                    BuildSelectedSegmentOverridePropertyCards(ref themeY, left, rightCol, colWidth, paneWidth);
                    BuildSelectedNodeOverridePropertyCards(ref themeY, left, rightCol, colWidth, paneWidth);
                }

                return;
            }

            if (_lblNoSelection.Visible)
            {
                _lblNoSelection.SetBounds(left, 58, paneWidth, 44);
                return;
            }

            bool isSegmentProperties = _segmentPropertyControls.Any(c => c.Visible);
            bool hasTelemetry = _lblTelemetryHeader.Visible;
            bool hasDisable = _disablePropertyControls.Any(c => c.Visible);
            bool hasFinal = _finalPropertyControls.Any(c => c.Visible);
            bool showLoop = hasFinal && IsLoopFinishSelected();

            int y = 58;

            if (isSegmentProperties)
            {
                PlaceSectionHeader(_lblSegmentHeader, ref y, paneWidth);
                PlaceTwoColumnRow(_lblRacePath, _numRacePath, _lblSegmentIndex, _numSegmentIndex, left, rightCol, colWidth, ref y);
                PlaceFullRow(_lblSegmentName, _txtSegmentName, left, paneWidth, ref y);
                PlaceTwoColumnRow(_lblScreenSection, _cmbScreenSection, _lblFlowDirection, _cmbFlowDirection, left, rightCol, colWidth, ref y);
                PlaceColorRow(_lblBackdropColor, _pnlBackdropColorPreview, _btnPickBackdropColor, left, ref y);
                return;
            }

            if (_lblStartHeader.Visible)
                PlaceSectionHeader(_lblStartHeader, ref y, paneWidth);

            PlaceFullRow(_lblNodeName, _txtNodeName, left, paneWidth, ref y);
            PlaceFullRow(_lblDisplayName, _txtDisplayName, left, paneWidth, ref y);
            PlaceColorRow(_lblColor, _pnlColorPreview, _btnPickColor, left, ref y);
            PlaceTwoColumnRow(_lblPosX, _numNodeX, _lblPosY, _numNodeY, left, rightCol, colWidth, ref y);

            if (hasDisable)
            {
                PlaceSectionHeader(_lblNodeOptionsHeader, ref y, paneWidth);
                _chkNodeDisabled.SetBounds(left, y, paneWidth, 24);
                y += 42;
            }

            if (hasFinal)
            {
                PlaceSectionHeader(_lblFinishHeader, ref y, paneWidth);
                PlaceFullRow(_lblFinishMode, _cmbFinishMode, left, paneWidth, ref y);

                _lblLoopCount.Visible = showLoop;
                _numLoopCount.Visible = showLoop;
                _lblLoopRequirement.Visible = showLoop;
                _numLoopRequirement.Visible = showLoop;

                if (showLoop)
                    PlaceTwoColumnRow(_lblLoopCount, _numLoopCount, _lblLoopRequirement, _numLoopRequirement, left, rightCol, colWidth, ref y);
            }

            if (hasTelemetry)
            {
                PlaceSectionHeader(_lblTelemetryHeader, ref y, paneWidth);
                PlaceFullRow(_lblMapId, _txtMapId, left, colWidth, ref y);
                PlaceThreeColumnRow(_lblWorldX, _numWorldX, _lblWorldY, _numWorldY, _lblWorldZ, _numWorldZ, left, paneWidth, ref y);
                if (_btnUpdatePosition.Visible)
                {
                    _btnUpdatePosition.SetBounds(left, y, Math.Min(180, paneWidth), 32);
                    y += 50;
                }
                PlaceTwoColumnRow(_lblRadius, _numRadius, _lblAngle, _numAngle, left, rightCol, colWidth, ref y);
            }

            PlaceFullRow(_lblNotes, _txtNotes, left, paneWidth, ref y, 130);
        }

        private bool IsLoopFinishSelected()
        {
            return _cmbFinishMode != null && _cmbFinishMode.SelectedIndex == 2;
        }

        private static void PlaceSectionHeader(Label label, ref int y, int width)
        {
            if (!label.Visible) return;
            label.SetBounds(18, y, width, 22);
            y += 34;
        }

        private bool PlaceCollapsibleHeader(Label label, string title, string key, ref int y, int width)
        {
            bool open = !_collapsedThemeGroups.Contains(key);
            label.Text = (open ? "v " : "> ") + title;
            label.Visible = true;
            label.Cursor = Cursors.Hand;
            label.SetBounds(18, y, width, 24);

            label.Click -= ThemeGroupHeader_Click;
            label.Click += ThemeGroupHeader_Click;
            label.Tag = key;

            y += 34;
            return open;
        }

        private void ThemeGroupHeader_Click(object? sender, EventArgs e)
        {
            if (sender is not Label label || label.Tag is not string key)
                return;

            if (_collapsedThemeGroups.Contains(key))
                _collapsedThemeGroups.Remove(key);
            else
                _collapsedThemeGroups.Add(key);

            LayoutCustomUi();
        }

        private static void PlaceFullRow(Label label, Control control, int left, int width, ref int y, int? fixedHeight = null)
        {
            if (!label.Visible || !control.Visible) return;

            label.SetBounds(left, y, width, 20);
            int height = fixedHeight ?? control.Height;
            control.SetBounds(left, y + 22, width, height);
            y += 22 + height + 18;
        }

        private static void PlaceTwoColumnRow(Label labelA, Control controlA, Label labelB, Control controlB, int leftA, int leftB, int width, ref int y)
        {
            if (!labelA.Visible || !controlA.Visible) return;

            labelA.SetBounds(leftA, y, width, 20);
            controlA.SetBounds(leftA, y + 22, width, controlA.Height);

            if (labelB.Visible && controlB.Visible)
            {
                labelB.SetBounds(leftB, y, width, 20);
                controlB.SetBounds(leftB, y + 22, width, controlB.Height);
            }

            y += 22 + Math.Max(controlA.Height, controlB.Visible ? controlB.Height : controlA.Height) + 18;
        }

        private static void PlaceThreeColumnRow(Label labelA, Control controlA, Label labelB, Control controlB, Label labelC, Control controlC, int left, int totalWidth, ref int y)
        {
            if (!labelA.Visible || !controlA.Visible) return;

            int gap = 10;
            int width = Math.Max(70, (totalWidth - (gap * 2)) / 3);
            int leftB = left + width + gap;
            int leftC = leftB + width + gap;

            labelA.SetBounds(left, y, width, 20);
            controlA.SetBounds(left, y + 22, width, controlA.Height);
            labelB.SetBounds(leftB, y, width, 20);
            controlB.SetBounds(leftB, y + 22, width, controlB.Height);
            labelC.SetBounds(leftC, y, width, 20);
            controlC.SetBounds(leftC, y + 22, width, controlC.Height);

            y += 22 + Math.Max(controlA.Height, Math.Max(controlB.Height, controlC.Height)) + 18;
        }

        private static void PlaceColorRow(Label label, Panel preview, Control button, int left, ref int y)
        {
            if (!label.Visible || !preview.Visible || !button.Visible) return;

            label.SetBounds(left, y, 260, 20);
            preview.SetBounds(left, y + 24, 44, 24);
            button.SetBounds(left + 58, y + 19, button.Width, button.Height);
            y += 62;
        }


        private void ThemeBuilder_SelectionChanged(ThemeBuilderCanvas.ThemeBuilderSelection selection)
        {
            if (!_isThemeBuilderActive)
                return;

            _currentThemeSelection = selection;
            PopulateThemeTuningProperties();
            LayoutCustomUi();
        }

        private void ThemeBuilder_TuningChanged()
        {
            if (!_isThemeBuilderActive)
                return;

            PopulateThemeTuningProperties();
        }

        private void PopulateThemeTuningProperties()
        {
            if (_graph == null || _graph.Document == null)
                return;

            _updatingProperties = true;

            _currentThemeSelection ??= ThemeBuilderCanvas.ThemeBuilderSelection.ForGlobal();
            _lblThemeTuningTarget.Text = _currentThemeSelection.DisplayName;

            if (_currentThemeSelection.Kind == ThemeBuilderCanvas.ThemeBuilderSelectionKind.Global)
            {
                var admin = _graph.Document.Tuning.Admin;
                _numThemeTuningScale.Value = ClampDecimal((decimal)admin.OutputScale, _numThemeTuningScale.Minimum, _numThemeTuningScale.Maximum);
                _numThemeTuningOffsetX.Value = ClampDecimal((decimal)admin.OutputOffsetX, _numThemeTuningOffsetX.Minimum, _numThemeTuningOffsetX.Maximum);
                _numThemeTuningOffsetY.Value = ClampDecimal((decimal)admin.OutputOffsetY, _numThemeTuningOffsetY.Minimum, _numThemeTuningOffsetY.Maximum);
                _numThemeAdminTextScale.Value = ClampDecimal((decimal)admin.OutputNodeTextScale, _numThemeAdminTextScale.Minimum, _numThemeAdminTextScale.Maximum);
                _numThemeRacerTextScale.Value = ClampDecimal((decimal)admin.OutputRacerTextScale, _numThemeRacerTextScale.Minimum, _numThemeRacerTextScale.Maximum);
            }
            else
            {
                FlowMapLayoutTuning? tuning = _themeBuilder.GetSelectedLayoutTuning();
                if (tuning != null)
                {
                    _numThemeTuningScale.Value = ClampDecimal((decimal)tuning.VisualScale, _numThemeTuningScale.Minimum, _numThemeTuningScale.Maximum);
                    _numThemeTuningOffsetX.Value = ClampDecimal((decimal)tuning.OffsetX, _numThemeTuningOffsetX.Minimum, _numThemeTuningOffsetX.Maximum);
                    _numThemeTuningOffsetY.Value = ClampDecimal((decimal)tuning.OffsetY, _numThemeTuningOffsetY.Minimum, _numThemeTuningOffsetY.Maximum);
                }
            }

            _updatingProperties = false;
        }

        private void ThemeTuningScale_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || !_isThemeBuilderActive)
                return;

            if (_currentThemeSelection == null || _currentThemeSelection.Kind == ThemeBuilderCanvas.ThemeBuilderSelectionKind.Global)
                _graph.Document.Tuning.Admin.OutputScale = (float)_numThemeTuningScale.Value;
            else if (_themeBuilder.GetSelectedLayoutTuning() is FlowMapLayoutTuning tuning)
                tuning.VisualScale = (float)_numThemeTuningScale.Value;

            _themeBuilder.RefreshTuningPreview();
        }

        private void ThemeTuningOffsetX_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || !_isThemeBuilderActive)
                return;

            if (_currentThemeSelection == null || _currentThemeSelection.Kind == ThemeBuilderCanvas.ThemeBuilderSelectionKind.Global)
                _graph.Document.Tuning.Admin.OutputOffsetX = (float)_numThemeTuningOffsetX.Value;
            else if (_themeBuilder.GetSelectedLayoutTuning() is FlowMapLayoutTuning tuning)
                tuning.OffsetX = (float)_numThemeTuningOffsetX.Value;

            _themeBuilder.RefreshTuningPreview();
        }

        private void ThemeTuningOffsetY_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || !_isThemeBuilderActive)
                return;

            if (_currentThemeSelection == null || _currentThemeSelection.Kind == ThemeBuilderCanvas.ThemeBuilderSelectionKind.Global)
                _graph.Document.Tuning.Admin.OutputOffsetY = (float)_numThemeTuningOffsetY.Value;
            else if (_themeBuilder.GetSelectedLayoutTuning() is FlowMapLayoutTuning tuning)
                tuning.OffsetY = (float)_numThemeTuningOffsetY.Value;

            _themeBuilder.RefreshTuningPreview();
        }

        private void ThemeAdminTextScale_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || !_isThemeBuilderActive)
                return;

            _graph.Document.Tuning.Admin.OutputNodeTextScale = (float)_numThemeAdminTextScale.Value;
            _themeBuilder.RefreshTuningPreview();
        }

        private void ThemeRacerTextScale_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || !_isThemeBuilderActive)
                return;

            _graph.Document.Tuning.Admin.OutputRacerTextScale = (float)_numThemeRacerTextScale.Value;
            _themeBuilder.RefreshTuningPreview();
        }


        private void RemoveThemeNodeTypeOverridePropertyControls()
        {
            if (_themeNodeTypeOverridePropertyControls.Count == 0)
                return;

            foreach (Control control in _themeNodeTypeOverridePropertyControls)
            {
                _rightPane.Controls.Remove(control);
                control.Dispose();
            }

            _themeNodeTypeOverridePropertyControls.Clear();
        }

        private void AddThemeNodeTypeOverrideControl(Control control)
        {
            _themeNodeTypeOverridePropertyControls.Add(control);
            _rightPane.Controls.Add(control);
        }

        private Label AddDynamicThemeHeader(string title, string key, ref int y, int paneWidth)
        {
            Label header = MakeSectionHeader(title, y);
            AddThemeNodeTypeOverrideControl(header);
            PlaceCollapsibleHeader(header, title, key, ref y, paneWidth);
            return header;
        }

        private void BuildDefaultThemeSettings(ref int y, int left, int rightCol, int colWidth, int paneWidth)
        {
            ThemeProject? theme = _graph?.Document?.Theme;
            if (theme == null)
                return;

            theme.Nodes ??= new Dictionary<string, string>();

            y += 10;
            bool open = !_collapsedThemeGroups.Contains("defaultTheme");
            Label header = MakeSectionHeader((open ? "v " : "> ") + "Default Theme Settings", y);
            header.Width = paneWidth;
            header.Tag = "defaultTheme";
            header.Cursor = Cursors.Hand;
            header.Click += ThemeGroupHeader_Click;
            AddThemeNodeTypeOverrideControl(header);
            y += 36;
            if (!open)
                return;

            var defaultImageRows = new (string Key, string LabelText)[]
            {
                ("start", "Default Start Image"),
                ("checkpoint", "Default Checkpoint Image"),
                ("split", "Default Split Image"),
                ("converge", "Default Converge Image"),
                ("end", "Default End Image"),
                ("boss", "Default Final/Boss Image")
            };

            foreach (var row in defaultImageRows)
            {
                AddThemeIconFileRow(
                    row.LabelText,
                    theme.Nodes.TryGetValue(row.Key, out string? value) ? value ?? string.Empty : string.Empty,
                    selectedFileName =>
                    {
                        theme.Nodes[row.Key] = selectedFileName;
                        RefreshThemeBuilderPreview();
                    },
                    left,
                    paneWidth,
                    ref y);
            }

            AddThemeColorPickerRow(
                "Default Line Color",
                theme.Lines.DefaultColor,
                selectedColor =>
                {
                    theme.Lines.DefaultColor = selectedColor;
                    RefreshThemeBuilderPreview();
                },
                left,
                colWidth,
                ref y);

            AddThemeColorPickerRow(
                "Default Split Line Color",
                theme.Lines.SplitColor,
                selectedColor =>
                {
                    theme.Lines.SplitColor = selectedColor;
                    RefreshThemeBuilderPreview();
                },
                left,
                colWidth,
                ref y);

            AddThemeColorPickerRow(
                "Default Converge Line Color",
                theme.Lines.ConvergeColor,
                selectedColor =>
                {
                    theme.Lines.ConvergeColor = selectedColor;
                    RefreshThemeBuilderPreview();
                },
                left,
                colWidth,
                ref y);

            Label lineThicknessLabel = Theme.MakeLabel("Line Thickness", left, y, true, true);
            NumericUpDown lineThicknessBox = MakeIntegerBox(left, y + 22, colWidth, 1, 100);
            lineThicknessBox.Value = ClampDecimal(theme.Lines.Thickness, lineThicknessBox.Minimum, lineThicknessBox.Maximum);
            lineThicknessBox.ValueChanged += (_, _) => { theme.Lines.Thickness = (int)lineThicknessBox.Value; RefreshThemeBuilderPreview(); };
            AddThemeNodeTypeOverrideControl(lineThicknessLabel);
            AddThemeNodeTypeOverrideControl(lineThicknessBox);
            y += 66;

            Label nodeScaleLabel = Theme.MakeLabel("Default Node Scale", left, y, true, true);
            NumericUpDown nodeScaleBox = MakeDecimalBox(left, y + 22, colWidth, 0.1m, 10m, 2);
            nodeScaleBox.Value = ClampDecimal((decimal)theme.Settings.NodeScale, nodeScaleBox.Minimum, nodeScaleBox.Maximum);
            nodeScaleBox.ValueChanged += (_, _) => { theme.Settings.NodeScale = (double)nodeScaleBox.Value; RefreshThemeBuilderPreview(); };

            Label titleScaleLabel = Theme.MakeLabel("Default Title Scale", rightCol, y, true, true);
            NumericUpDown titleScaleBox = MakeDecimalBox(rightCol, y + 22, colWidth, 0.1m, 10m, 2);
            titleScaleBox.Value = ClampDecimal((decimal)theme.Settings.TitleScale, titleScaleBox.Minimum, titleScaleBox.Maximum);
            titleScaleBox.ValueChanged += (_, _) => { theme.Settings.TitleScale = (double)titleScaleBox.Value; RefreshThemeBuilderPreview(); };
            AddThemeNodeTypeOverrideControl(nodeScaleLabel);
            AddThemeNodeTypeOverrideControl(nodeScaleBox);
            AddThemeNodeTypeOverrideControl(titleScaleLabel);
            AddThemeNodeTypeOverrideControl(titleScaleBox);
            y += 66;

            CheckBox nodeVisible = MakeThemeCheckBox("Node Visible", left, y, theme.Settings.NodeVisibility, checkedValue => { theme.Settings.NodeVisibility = checkedValue; RefreshThemeBuilderPreview(); });
            CheckBox titleVisible = MakeThemeCheckBox("Title Visible", rightCol, y, theme.Settings.TitleVisible, checkedValue => { theme.Settings.TitleVisible = checkedValue; RefreshThemeBuilderPreview(); });
            AddThemeNodeTypeOverrideControl(nodeVisible);
            AddThemeNodeTypeOverrideControl(titleVisible);
            y += 42;

            Label titleOffsetXLabel = Theme.MakeLabel("Title Offset X", left, y, true, true);
            NumericUpDown titleOffsetXBox = MakeIntegerBox(left, y + 22, colWidth, -10000, 10000);
            titleOffsetXBox.Value = ClampDecimal(theme.Settings.TitleOffsetX, titleOffsetXBox.Minimum, titleOffsetXBox.Maximum);
            titleOffsetXBox.ValueChanged += (_, _) => { theme.Settings.TitleOffsetX = (int)titleOffsetXBox.Value; RefreshThemeBuilderPreview(); };

            Label titleOffsetYLabel = Theme.MakeLabel("Title Offset Y", rightCol, y, true, true);
            NumericUpDown titleOffsetYBox = MakeIntegerBox(rightCol, y + 22, colWidth, -10000, 10000);
            titleOffsetYBox.Value = ClampDecimal(theme.Settings.TitleOffsetY, titleOffsetYBox.Minimum, titleOffsetYBox.Maximum);
            titleOffsetYBox.ValueChanged += (_, _) => { theme.Settings.TitleOffsetY = (int)titleOffsetYBox.Value; RefreshThemeBuilderPreview(); };
            AddThemeNodeTypeOverrideControl(titleOffsetXLabel);
            AddThemeNodeTypeOverrideControl(titleOffsetXBox);
            AddThemeNodeTypeOverrideControl(titleOffsetYLabel);
            AddThemeNodeTypeOverrideControl(titleOffsetYBox);
            y += 66;

            CheckBox shadowEnabled = MakeThemeCheckBox("Shadow Enabled", left, y, theme.Settings.ShadowEnabled, checkedValue => { theme.Settings.ShadowEnabled = checkedValue; RefreshThemeBuilderPreview(); });
            AddThemeNodeTypeOverrideControl(shadowEnabled);
            y += 42;

            AddThemeColorPickerRow(
                "Shadow Color",
                theme.Settings.ShadowColor,
                selectedColor =>
                {
                    theme.Settings.ShadowColor = selectedColor;
                    RefreshThemeBuilderPreview();
                },
                left,
                colWidth,
                ref y);

            Label shadowOpacityLabel = Theme.MakeLabel("Shadow Opacity", left, y, true, true);
            NumericUpDown shadowOpacityBox = MakeDecimalBox(left, y + 22, colWidth, 0m, 1m, 2);
            shadowOpacityBox.Value = ClampDecimal((decimal)theme.Settings.ShadowOpacity, shadowOpacityBox.Minimum, shadowOpacityBox.Maximum);
            shadowOpacityBox.ValueChanged += (_, _) => { theme.Settings.ShadowOpacity = (double)shadowOpacityBox.Value; RefreshThemeBuilderPreview(); };

            Label shadowBlurLabel = Theme.MakeLabel("Shadow Blur", rightCol, y, true, true);
            NumericUpDown shadowBlurBox = MakeIntegerBox(rightCol, y + 22, colWidth, 0, 200);
            shadowBlurBox.Value = ClampDecimal(theme.Settings.ShadowBlur, shadowBlurBox.Minimum, shadowBlurBox.Maximum);
            shadowBlurBox.ValueChanged += (_, _) => { theme.Settings.ShadowBlur = (int)shadowBlurBox.Value; RefreshThemeBuilderPreview(); };
            AddThemeNodeTypeOverrideControl(shadowOpacityLabel);
            AddThemeNodeTypeOverrideControl(shadowOpacityBox);
            AddThemeNodeTypeOverrideControl(shadowBlurLabel);
            AddThemeNodeTypeOverrideControl(shadowBlurBox);
            y += 66;

            Label shadowOffsetXLabel = Theme.MakeLabel("Shadow Offset X", left, y, true, true);
            NumericUpDown shadowOffsetXBox = MakeIntegerBox(left, y + 22, colWidth, -10000, 10000);
            shadowOffsetXBox.Value = ClampDecimal(theme.Settings.ShadowOffsetX, shadowOffsetXBox.Minimum, shadowOffsetXBox.Maximum);
            shadowOffsetXBox.ValueChanged += (_, _) => { theme.Settings.ShadowOffsetX = (int)shadowOffsetXBox.Value; RefreshThemeBuilderPreview(); };

            Label shadowOffsetYLabel = Theme.MakeLabel("Shadow Offset Y", rightCol, y, true, true);
            NumericUpDown shadowOffsetYBox = MakeIntegerBox(rightCol, y + 22, colWidth, -10000, 10000);
            shadowOffsetYBox.Value = ClampDecimal(theme.Settings.ShadowOffsetY, shadowOffsetYBox.Minimum, shadowOffsetYBox.Maximum);
            shadowOffsetYBox.ValueChanged += (_, _) => { theme.Settings.ShadowOffsetY = (int)shadowOffsetYBox.Value; RefreshThemeBuilderPreview(); };
            AddThemeNodeTypeOverrideControl(shadowOffsetXLabel);
            AddThemeNodeTypeOverrideControl(shadowOffsetXBox);
            AddThemeNodeTypeOverrideControl(shadowOffsetYLabel);
            AddThemeNodeTypeOverrideControl(shadowOffsetYBox);
            y += 66;

            Label racerDotLabel = Theme.MakeLabel("Racer Dot Size", left, y, true, true);
            NumericUpDown racerDotBox = MakeIntegerBox(left, y + 22, colWidth, 1, 500);
            racerDotBox.Value = ClampDecimal(theme.Racers.DotSize, racerDotBox.Minimum, racerDotBox.Maximum);
            racerDotBox.ValueChanged += (_, _) => theme.Racers.DotSize = (int)racerDotBox.Value;

            Label racerNameScaleLabel = Theme.MakeLabel("Racer Name Scale", rightCol, y, true, true);
            NumericUpDown racerNameScaleBox = MakeDecimalBox(rightCol, y + 22, colWidth, 0.1m, 10m, 2);
            racerNameScaleBox.Value = ClampDecimal((decimal)theme.Racers.NameScale, racerNameScaleBox.Minimum, racerNameScaleBox.Maximum);
            racerNameScaleBox.ValueChanged += (_, _) => theme.Racers.NameScale = (double)racerNameScaleBox.Value;
            AddThemeNodeTypeOverrideControl(racerDotLabel);
            AddThemeNodeTypeOverrideControl(racerDotBox);
            AddThemeNodeTypeOverrideControl(racerNameScaleLabel);
            AddThemeNodeTypeOverrideControl(racerNameScaleBox);
            y += 82;
        }

        private void AddThemeIconFileRow(string labelText, string currentFileName, Action<string> selected, int left, int paneWidth, ref int y)
        {
            Label label = Theme.MakeLabel(labelText, left, y, true, true);
            TextBox box = MakeTextBox(left, y + 22, Math.Max(120, paneWidth - 94));
            box.ReadOnly = true;
            box.Text = currentFileName ?? string.Empty;

            HudButton chooseButton = new HudButton
            {
                Text = "Select...",
                Left = left + box.Width + 8,
                Top = y + 19,
                Width = 86,
                Height = 32
            };

            chooseButton.Click += (_, _) =>
            {
                string? fileName = SelectThemeIconFile();
                if (string.IsNullOrWhiteSpace(fileName))
                    return;

                box.Text = fileName;
                selected(fileName);
            };

            AddThemeNodeTypeOverrideControl(label);
            AddThemeNodeTypeOverrideControl(box);
            AddThemeNodeTypeOverrideControl(chooseButton);
            y += 66;
        }

        private void ImportThemeIcons()
        {
            ThemeProject? theme = _graph?.Document?.Theme;
            if (theme == null)
            {
                MessageBox.Show(this, "Create or load a theme before importing icons.", "Import Icons", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(theme.IconFolderPath))
            {
                MessageBox.Show(this, "The active theme does not have an icon folder yet.", "Import Icons", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Directory.CreateDirectory(theme.IconFolderPath);

            using var dialog = new OpenFileDialog
            {
                Filter = "PNG Images (*.png)|*.png|All Files (*.*)|*.*",
                DefaultExt = "png",
                Title = "Import Theme Icons",
                Multiselect = true
            };

            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.FileNames.Length == 0)
                return;

            int copied = 0;
            int skipped = 0;
            var nonStandardSizes = new List<string>();

            foreach (string sourcePath in dialog.FileNames)
            {
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                {
                    skipped++;
                    continue;
                }

                string fileName = Path.GetFileName(sourcePath);
                string destinationPath = Path.Combine(theme.IconFolderPath, fileName);

                try
                {
                    using (Image image = Image.FromFile(sourcePath))
                    {
                        if (image.Width != 500 || image.Height != 500)
                            nonStandardSizes.Add($"{fileName} ({image.Width}x{image.Height})");
                    }

                    if (File.Exists(destinationPath))
                    {
                        DialogResult overwrite = MessageBox.Show(
                            this,
                            $"The icon file already exists in the theme folder:\n\n{fileName}\n\nOverwrite it?",
                            "Import Icons",
                            MessageBoxButtons.YesNoCancel,
                            MessageBoxIcon.Question);

                        if (overwrite == DialogResult.Cancel)
                            break;

                        if (overwrite == DialogResult.No)
                        {
                            skipped++;
                            continue;
                        }
                    }

                    if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
                        File.Copy(sourcePath, destinationPath, true);

                    copied++;
                }
                catch (Exception ex)
                {
                    skipped++;
                    MessageBox.Show(
                        this,
                        $"Could not import icon:\n\n{fileName}\n\n{ex.Message}",
                        "Import Icons",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }

            RefreshThemeBuilderPreview();

            string message = $"Icon import complete.\n\nImported: {copied}\nSkipped: {skipped}\n\nIcon folder:\n{theme.IconFolderPath}";
            if (nonStandardSizes.Count > 0)
            {
                message += "\n\nWarning: the following images are not 500x500 PNGs:\n";
                message += string.Join("\n", nonStandardSizes.Select(name => "• " + name));
            }

            MessageBox.Show(this, message, "Import Icons", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string? SelectThemeIconFile()
        {
            ThemeProject? theme = _graph?.Document?.Theme;
            if (theme == null)
            {
                MessageBox.Show(this, "Create or load a theme first.", "Select Icon", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            if (string.IsNullOrWhiteSpace(theme.IconFolderPath))
            {
                MessageBox.Show(this, "The active theme does not have an icon folder yet.", "Select Icon", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            Directory.CreateDirectory(theme.IconFolderPath);

            using var dialog = new OpenFileDialog
            {
                Filter = "PNG Images (*.png)|*.png|All Files (*.*)|*.*",
                DefaultExt = "png",
                Title = "Select Theme Icon",
                InitialDirectory = theme.IconFolderPath
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return null;

            string fileName = Path.GetFileName(dialog.FileName);
            string destination = Path.Combine(theme.IconFolderPath, fileName);

            if (!string.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(dialog.FileName, destination, true);
            }

            return fileName;
        }

        private void AddThemeColorPickerRow(string labelText, string currentColor, Action<string> selected, int left, int width, ref int y)
        {
            Label label = Theme.MakeLabel(labelText, left, y, true, true);
            Panel preview = MakeColorPreview(left, y + 24);
            preview.BackColor = TryParseThemeColor(currentColor, out Color parsed) ? parsed : Color.Black;

            TextBox textBox = MakeTextBox(left + 58, y + 22, Math.Max(82, width - 58));
            textBox.Text = string.IsNullOrWhiteSpace(currentColor) ? "#000000" : currentColor.Trim();
            textBox.TextChanged += (_, _) =>
            {
                if (_updatingProperties)
                    return;

                string value = textBox.Text.Trim();
                if (TryParseThemeColor(value, out Color color))
                    preview.BackColor = color;

                selected(value);
            };

            HudButton pickButton = new HudButton
            {
                Text = "Pick",
                Left = left + width + 10,
                Top = y + 19,
                Width = 70,
                Height = 32
            };
            pickButton.Click += (_, _) =>
            {
                Color startingColor = TryParseThemeColor(textBox.Text, out Color existing) ? existing : Color.Black;
                using var dialog = new ColorDialog
                {
                    FullOpen = true,
                    AnyColor = true,
                    SolidColorOnly = false,
                    Color = startingColor
                };

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                string hex = ThemeColorToHex(dialog.Color);
                preview.BackColor = dialog.Color;
                textBox.Text = hex;
                selected(hex);
            };

            AddThemeNodeTypeOverrideControl(label);
            AddThemeNodeTypeOverrideControl(preview);
            AddThemeNodeTypeOverrideControl(textBox);
            AddThemeNodeTypeOverrideControl(pickButton);
            y += 66;
        }

        private static bool TryParseThemeColor(string? value, out Color color)
        {
            color = Color.Black;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            try
            {
                color = ColorTranslator.FromHtml(value.Trim());
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ThemeColorToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private CheckBox MakeThemeCheckBox(string text, int left, int top, bool isChecked, Action<bool> changed)
        {
            var check = new CheckBox
            {
                Text = text,
                Left = left,
                Top = top,
                Width = 170,
                Height = 26,
                Checked = isChecked,
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            check.CheckedChanged += (_, _) => changed(check.Checked);
            return check;
        }

        private void BuildThemeNodeTypeOverridePropertyCards(ref int y, int left, int rightCol, int colWidth, int paneWidth)
        {
            ThemeProject? theme = _graph?.Document?.Theme;
            if (theme == null || theme.NodeTypeOverrides == null || theme.NodeTypeOverrides.Count == 0)
                return;

            y += 18;

            bool open = !_collapsedThemeGroups.Contains("nodeTypeOverrides");
            Label header = MakeSectionHeader((open ? "v " : "> ") + "Node Type Overrides", y);
            header.Width = paneWidth;
            header.Tag = "nodeTypeOverrides";
            header.Cursor = Cursors.Hand;
            header.Click += ThemeGroupHeader_Click;
            AddThemeNodeTypeOverrideControl(header);
            y += 36;
            if (!open)
                return;

            foreach (var pair in theme.NodeTypeOverrides.OrderBy(p => GetNodeTypeOverrideSortOrder(p.Key)))
            {
                string key = pair.Key;
                ThemeNodeOverride value = pair.Value;

                Label cardTitle = Theme.MakeLabel(GetNodeTypeDisplayName(key), left, y, true, true);
                cardTitle.Width = paneWidth;
                cardTitle.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
                AddThemeNodeTypeOverrideControl(cardTitle);
                y += 28;

                AddThemeIconFileRow(
                    "Image",
                    value.Image ?? string.Empty,
                    selectedFileName =>
                    {
                        value.Image = selectedFileName;
                        RefreshThemeBuilderPreview();
                    },
                    left,
                    paneWidth,
                    ref y);

                CheckBox nodeVisible = MakeThemeCheckBox(
                    "Node/Image Visible",
                    left,
                    y,
                    value.NodeVisibility,
                    checkedValue =>
                    {
                        value.NodeVisibility = checkedValue;
                        RefreshThemeBuilderPreview();
                    });
                AddThemeNodeTypeOverrideControl(nodeVisible);
                y += 42;

                Label scaleLabel = Theme.MakeLabel("Scale", left, y, true, true);
                NumericUpDown scaleBox = MakeDecimalBox(left, y + 22, colWidth, 0.1m, 10m, 2);
                scaleBox.Value = ClampDecimal((decimal)value.Scale, scaleBox.Minimum, scaleBox.Maximum);
                scaleBox.ValueChanged += (_, _) =>
                {
                    if (_updatingProperties)
                        return;
                    value.Scale = (double)scaleBox.Value;
                    RefreshThemeBuilderPreview();
                };

                Label titleScaleLabel = Theme.MakeLabel("Title Scale", rightCol, y, true, true);
                NumericUpDown titleScaleBox = MakeDecimalBox(rightCol, y + 22, colWidth, 0.1m, 10m, 2);
                titleScaleBox.Value = ClampDecimal((decimal)value.TitleScale, titleScaleBox.Minimum, titleScaleBox.Maximum);
                titleScaleBox.ValueChanged += (_, _) =>
                {
                    if (_updatingProperties)
                        return;
                    value.TitleScale = (double)titleScaleBox.Value;
                    RefreshThemeBuilderPreview();
                };

                AddThemeNodeTypeOverrideControl(scaleLabel);
                AddThemeNodeTypeOverrideControl(scaleBox);
                AddThemeNodeTypeOverrideControl(titleScaleLabel);
                AddThemeNodeTypeOverrideControl(titleScaleBox);
                y += 66;

                CheckBox titleVisible = new CheckBox
                {
                    Left = left,
                    Top = y,
                    Width = paneWidth,
                    Height = 26,
                    Text = "Title Visible",
                    Checked = value.TitleVisible,
                    ForeColor = Theme.Text,
                    BackColor = Color.Transparent,
                    Font = new Font("Segoe UI", 9f, FontStyle.Regular)
                };
                titleVisible.CheckedChanged += (_, _) =>
                {
                    if (_updatingProperties)
                        return;
                    value.TitleVisible = titleVisible.Checked;
                    RefreshThemeBuilderPreview();
                };
                AddThemeNodeTypeOverrideControl(titleVisible);
                y += 42;

                Label titleOffsetXLabel = Theme.MakeLabel("Title Offset X", left, y, true, true);
                NumericUpDown titleOffsetXBox = MakeIntegerBox(left, y + 22, colWidth, -10000, 10000);
                titleOffsetXBox.Value = ClampDecimal(value.TitleOffsetX, titleOffsetXBox.Minimum, titleOffsetXBox.Maximum);
                titleOffsetXBox.ValueChanged += (_, _) =>
                {
                    if (_updatingProperties)
                        return;
                    value.TitleOffsetX = (int)titleOffsetXBox.Value;
                    RefreshThemeBuilderPreview();
                };

                Label titleOffsetYLabel = Theme.MakeLabel("Title Offset Y", rightCol, y, true, true);
                NumericUpDown titleOffsetYBox = MakeIntegerBox(rightCol, y + 22, colWidth, -10000, 10000);
                titleOffsetYBox.Value = ClampDecimal(value.TitleOffsetY, titleOffsetYBox.Minimum, titleOffsetYBox.Maximum);
                titleOffsetYBox.ValueChanged += (_, _) =>
                {
                    if (_updatingProperties)
                        return;
                    value.TitleOffsetY = (int)titleOffsetYBox.Value;
                    RefreshThemeBuilderPreview();
                };

                AddThemeNodeTypeOverrideControl(titleOffsetXLabel);
                AddThemeNodeTypeOverrideControl(titleOffsetXBox);
                AddThemeNodeTypeOverrideControl(titleOffsetYLabel);
                AddThemeNodeTypeOverrideControl(titleOffsetYBox);
                y += 66;

                Label imageOffsetXLabel = Theme.MakeLabel("Image Offset X", left, y, true, true);
                NumericUpDown imageOffsetXBox = MakeIntegerBox(left, y + 22, colWidth, -10000, 10000);
                imageOffsetXBox.Value = ClampDecimal(value.ImageOffsetX, imageOffsetXBox.Minimum, imageOffsetXBox.Maximum);
                imageOffsetXBox.ValueChanged += (_, _) =>
                {
                    if (_updatingProperties)
                        return;
                    value.ImageOffsetX = (int)imageOffsetXBox.Value;
                    RefreshThemeBuilderPreview();
                };

                Label imageOffsetYLabel = Theme.MakeLabel("Image Offset Y", rightCol, y, true, true);
                NumericUpDown imageOffsetYBox = MakeIntegerBox(rightCol, y + 22, colWidth, -10000, 10000);
                imageOffsetYBox.Value = ClampDecimal(value.ImageOffsetY, imageOffsetYBox.Minimum, imageOffsetYBox.Maximum);
                imageOffsetYBox.ValueChanged += (_, _) =>
                {
                    if (_updatingProperties)
                        return;
                    value.ImageOffsetY = (int)imageOffsetYBox.Value;
                    RefreshThemeBuilderPreview();
                };

                AddThemeNodeTypeOverrideControl(imageOffsetXLabel);
                AddThemeNodeTypeOverrideControl(imageOffsetXBox);
                AddThemeNodeTypeOverrideControl(imageOffsetYLabel);
                AddThemeNodeTypeOverrideControl(imageOffsetYBox);
                y += 82;
            }
        }

        private void BuildSelectedSegmentOverridePropertyCards(ref int y, int left, int rightCol, int colWidth, int paneWidth)
        {
            ThemeProject? theme = _graph?.Document?.Theme;
            if (theme == null || _themeBuilder == null)
                return;

            theme.SegmentOverrides ??= new Dictionary<string, ThemeSegmentOverride>();
            List<string> ids = _themeBuilder.SelectedSegmentIds.Where(id => theme.SegmentOverrides.ContainsKey(id)).ToList();
            if (ids.Count == 0)
                return;

            y += 18;
            bool open = !_collapsedThemeGroups.Contains("segmentOverrides");
            Label header = MakeSectionHeader((open ? "v " : "> ") + $"Segment Overrides ({ids.Count})", y);
            header.Width = paneWidth;
            header.Tag = "segmentOverrides";
            header.Cursor = Cursors.Hand;
            header.Click += ThemeGroupHeader_Click;
            AddThemeNodeTypeOverrideControl(header);
            y += 36;
            if (!open)
                return;

            ThemeSegmentOverride first = theme.SegmentOverrides[ids[0]];

            AddThemeColorPickerRow(
                "Line Color",
                first.LineColor,
                selectedColor => ApplyToSegmentOverrides(ids, ov => ov.LineColor = selectedColor),
                left,
                colWidth,
                ref y);

            AddThemeColorPickerRow(
                "Split Line Color",
                string.IsNullOrWhiteSpace(first.SplitLineColor) ? (_graph?.Document?.Theme?.Lines?.SplitColor ?? "#AA823C") : first.SplitLineColor,
                selectedColor => ApplyToSegmentOverrides(ids, ov => ov.SplitLineColor = selectedColor),
                left,
                colWidth,
                ref y);

            AddThemeColorPickerRow(
                "Converge Line Color",
                string.IsNullOrWhiteSpace(first.ConvergeLineColor) ? (_graph?.Document?.Theme?.Lines?.ConvergeColor ?? "#8C5AAA") : first.ConvergeLineColor,
                selectedColor => ApplyToSegmentOverrides(ids, ov => ov.ConvergeLineColor = selectedColor),
                left,
                colWidth,
                ref y);

            Label thicknessLabel = Theme.MakeLabel("Line Thickness", left, y, true, true);
            NumericUpDown thicknessBox = MakeIntegerBox(left, y + 22, colWidth, 1, 100);
            thicknessBox.Value = ClampDecimal(first.Thickness, thicknessBox.Minimum, thicknessBox.Maximum);
            thicknessBox.ValueChanged += (_, _) => ApplyToSegmentOverrides(ids, ov => ov.Thickness = (int)thicknessBox.Value);
            AddThemeNodeTypeOverrideControl(thicknessLabel);
            AddThemeNodeTypeOverrideControl(thicknessBox);
            y += 66;

            CheckBox nodeVisible = MakeThemeCheckBox(
                "Node/Image Visible",
                left,
                y,
                first.NodeVisibility,
                checkedValue => ApplyToSegmentOverrides(ids, ov => ov.NodeVisibility = checkedValue));

            CheckBox titleVisible = MakeThemeCheckBox(
                "Title Visible",
                rightCol,
                y,
                first.TitleVisible,
                checkedValue => ApplyToSegmentOverrides(ids, ov => ov.TitleVisible = checkedValue));

            AddThemeNodeTypeOverrideControl(nodeVisible);
            AddThemeNodeTypeOverrideControl(titleVisible);
            y += 42;

            Label nodeScaleLabel = Theme.MakeLabel("Node/Image Scale", left, y, true, true);
            NumericUpDown nodeScaleBox = MakeDecimalBox(left, y + 22, colWidth, 0.1m, 10m, 2);
            nodeScaleBox.Value = ClampDecimal((decimal)first.NodeScale, nodeScaleBox.Minimum, nodeScaleBox.Maximum);
            nodeScaleBox.ValueChanged += (_, _) => ApplyToSegmentOverrides(ids, ov => ov.NodeScale = (double)nodeScaleBox.Value);

            Label titleScaleLabel = Theme.MakeLabel("Title Scale", rightCol, y, true, true);
            NumericUpDown titleScaleBox = MakeDecimalBox(rightCol, y + 22, colWidth, 0.1m, 10m, 2);
            titleScaleBox.Value = ClampDecimal((decimal)first.TitleScale, titleScaleBox.Minimum, titleScaleBox.Maximum);
            titleScaleBox.ValueChanged += (_, _) => ApplyToSegmentOverrides(ids, ov => ov.TitleScale = (double)titleScaleBox.Value);

            AddThemeNodeTypeOverrideControl(nodeScaleLabel);
            AddThemeNodeTypeOverrideControl(nodeScaleBox);
            AddThemeNodeTypeOverrideControl(titleScaleLabel);
            AddThemeNodeTypeOverrideControl(titleScaleBox);
            y += 66;

            Label imageOffsetXLabel = Theme.MakeLabel("Image Offset X", left, y, true, true);
            NumericUpDown imageOffsetXBox = MakeIntegerBox(left, y + 22, colWidth, -10000, 10000);
            imageOffsetXBox.Value = ClampDecimal(first.ImageOffsetX, imageOffsetXBox.Minimum, imageOffsetXBox.Maximum);
            imageOffsetXBox.ValueChanged += (_, _) => ApplyToSegmentOverrides(ids, ov => ov.ImageOffsetX = (int)imageOffsetXBox.Value);

            Label imageOffsetYLabel = Theme.MakeLabel("Image Offset Y", rightCol, y, true, true);
            NumericUpDown imageOffsetYBox = MakeIntegerBox(rightCol, y + 22, colWidth, -10000, 10000);
            imageOffsetYBox.Value = ClampDecimal(first.ImageOffsetY, imageOffsetYBox.Minimum, imageOffsetYBox.Maximum);
            imageOffsetYBox.ValueChanged += (_, _) => ApplyToSegmentOverrides(ids, ov => ov.ImageOffsetY = (int)imageOffsetYBox.Value);

            AddThemeNodeTypeOverrideControl(imageOffsetXLabel);
            AddThemeNodeTypeOverrideControl(imageOffsetXBox);
            AddThemeNodeTypeOverrideControl(imageOffsetYLabel);
            AddThemeNodeTypeOverrideControl(imageOffsetYBox);
            y += 66;

            Label titleOffsetXLabel = Theme.MakeLabel("Title Offset X", left, y, true, true);
            NumericUpDown titleOffsetXBox = MakeIntegerBox(left, y + 22, colWidth, -10000, 10000);
            titleOffsetXBox.Value = ClampDecimal(first.TitleOffsetX, titleOffsetXBox.Minimum, titleOffsetXBox.Maximum);
            titleOffsetXBox.ValueChanged += (_, _) => ApplyToSegmentOverrides(ids, ov => ov.TitleOffsetX = (int)titleOffsetXBox.Value);

            Label titleOffsetYLabel = Theme.MakeLabel("Title Offset Y", rightCol, y, true, true);
            NumericUpDown titleOffsetYBox = MakeIntegerBox(rightCol, y + 22, colWidth, -10000, 10000);
            titleOffsetYBox.Value = ClampDecimal(first.TitleOffsetY, titleOffsetYBox.Minimum, titleOffsetYBox.Maximum);
            titleOffsetYBox.ValueChanged += (_, _) => ApplyToSegmentOverrides(ids, ov => ov.TitleOffsetY = (int)titleOffsetYBox.Value);

            AddThemeNodeTypeOverrideControl(titleOffsetXLabel);
            AddThemeNodeTypeOverrideControl(titleOffsetXBox);
            AddThemeNodeTypeOverrideControl(titleOffsetYLabel);
            AddThemeNodeTypeOverrideControl(titleOffsetYBox);
            y += 82;
        }

        private void BuildSelectedNodeOverridePropertyCards(ref int y, int left, int rightCol, int colWidth, int paneWidth)
        {
            ThemeProject? theme = _graph?.Document?.Theme;
            if (theme == null || _themeBuilder == null)
                return;

            theme.NodeOverrides ??= new Dictionary<string, ThemeNodeOverride>();
            List<string> ids = _themeBuilder.SelectedNodeIds.Where(id => theme.NodeOverrides.ContainsKey(id)).ToList();
            if (ids.Count == 0)
                return;

            y += 18;
            bool open = !_collapsedThemeGroups.Contains("nodeOverrides");
            Label header = MakeSectionHeader((open ? "v " : "> ") + $"Node Overrides ({ids.Count})", y);
            header.Width = paneWidth;
            header.Tag = "nodeOverrides";
            header.Cursor = Cursors.Hand;
            header.Click += ThemeGroupHeader_Click;
            AddThemeNodeTypeOverrideControl(header);
            y += 36;
            if (!open)
                return;

            ThemeNodeOverride first = theme.NodeOverrides[ids[0]];
            BuildSharedNodeOverrideControls(ids, first, left, rightCol, colWidth, paneWidth, ref y, ApplyToNodeOverrides);
        }

        private void BuildSharedNodeOverrideControls(List<string> ids, ThemeNodeOverride first, int left, int rightCol, int colWidth, int paneWidth, ref int y, Action<List<string>, Action<ThemeNodeOverride>> apply)
        {
            AddThemeIconFileRow(
                "Image",
                first.Image,
                selectedFileName => apply(ids, ov => ov.Image = selectedFileName),
                left,
                paneWidth,
                ref y);

            CheckBox nodeVisible = MakeThemeCheckBox(
                "Node/Image Visible",
                left,
                y,
                first.NodeVisibility,
                checkedValue => apply(ids, ov => ov.NodeVisibility = checkedValue));
            AddThemeNodeTypeOverrideControl(nodeVisible);
            y += 42;

            Label scaleLabel = Theme.MakeLabel("Scale", left, y, true, true);
            NumericUpDown scaleBox = MakeDecimalBox(left, y + 22, colWidth, 0.1m, 10m, 2);
            scaleBox.Value = ClampDecimal((decimal)first.Scale, scaleBox.Minimum, scaleBox.Maximum);
            scaleBox.ValueChanged += (_, _) => apply(ids, ov => ov.Scale = (double)scaleBox.Value);

            Label titleScaleLabel = Theme.MakeLabel("Title Scale", rightCol, y, true, true);
            NumericUpDown titleScaleBox = MakeDecimalBox(rightCol, y + 22, colWidth, 0.1m, 10m, 2);
            titleScaleBox.Value = ClampDecimal((decimal)first.TitleScale, titleScaleBox.Minimum, titleScaleBox.Maximum);
            titleScaleBox.ValueChanged += (_, _) => apply(ids, ov => ov.TitleScale = (double)titleScaleBox.Value);
            AddThemeNodeTypeOverrideControl(scaleLabel);
            AddThemeNodeTypeOverrideControl(scaleBox);
            AddThemeNodeTypeOverrideControl(titleScaleLabel);
            AddThemeNodeTypeOverrideControl(titleScaleBox);
            y += 66;

            CheckBox titleVisible = MakeThemeCheckBox("Title Visible", left, y, first.TitleVisible, checkedValue => apply(ids, ov => ov.TitleVisible = checkedValue));
            AddThemeNodeTypeOverrideControl(titleVisible);
            y += 42;

            Label titleOffsetXLabel = Theme.MakeLabel("Title Offset X", left, y, true, true);
            NumericUpDown titleOffsetXBox = MakeIntegerBox(left, y + 22, colWidth, -10000, 10000);
            titleOffsetXBox.Value = ClampDecimal(first.TitleOffsetX, titleOffsetXBox.Minimum, titleOffsetXBox.Maximum);
            titleOffsetXBox.ValueChanged += (_, _) => apply(ids, ov => ov.TitleOffsetX = (int)titleOffsetXBox.Value);

            Label titleOffsetYLabel = Theme.MakeLabel("Title Offset Y", rightCol, y, true, true);
            NumericUpDown titleOffsetYBox = MakeIntegerBox(rightCol, y + 22, colWidth, -10000, 10000);
            titleOffsetYBox.Value = ClampDecimal(first.TitleOffsetY, titleOffsetYBox.Minimum, titleOffsetYBox.Maximum);
            titleOffsetYBox.ValueChanged += (_, _) => apply(ids, ov => ov.TitleOffsetY = (int)titleOffsetYBox.Value);
            AddThemeNodeTypeOverrideControl(titleOffsetXLabel);
            AddThemeNodeTypeOverrideControl(titleOffsetXBox);
            AddThemeNodeTypeOverrideControl(titleOffsetYLabel);
            AddThemeNodeTypeOverrideControl(titleOffsetYBox);
            y += 66;

            Label imageOffsetXLabel = Theme.MakeLabel("Image Offset X", left, y, true, true);
            NumericUpDown imageOffsetXBox = MakeIntegerBox(left, y + 22, colWidth, -10000, 10000);
            imageOffsetXBox.Value = ClampDecimal(first.ImageOffsetX, imageOffsetXBox.Minimum, imageOffsetXBox.Maximum);
            imageOffsetXBox.ValueChanged += (_, _) => apply(ids, ov => ov.ImageOffsetX = (int)imageOffsetXBox.Value);

            Label imageOffsetYLabel = Theme.MakeLabel("Image Offset Y", rightCol, y, true, true);
            NumericUpDown imageOffsetYBox = MakeIntegerBox(rightCol, y + 22, colWidth, -10000, 10000);
            imageOffsetYBox.Value = ClampDecimal(first.ImageOffsetY, imageOffsetYBox.Minimum, imageOffsetYBox.Maximum);
            imageOffsetYBox.ValueChanged += (_, _) => apply(ids, ov => ov.ImageOffsetY = (int)imageOffsetYBox.Value);
            AddThemeNodeTypeOverrideControl(imageOffsetXLabel);
            AddThemeNodeTypeOverrideControl(imageOffsetXBox);
            AddThemeNodeTypeOverrideControl(imageOffsetYLabel);
            AddThemeNodeTypeOverrideControl(imageOffsetYBox);
            y += 82;
        }

        private void ApplyToSegmentOverrides(List<string> ids, Action<ThemeSegmentOverride> apply)
        {
            if (_updatingProperties || _graph?.Document?.Theme == null)
                return;
            foreach (string id in ids)
                if (_graph.Document.Theme.SegmentOverrides.TryGetValue(id, out ThemeSegmentOverride? ov))
                    apply(ov);
            RefreshThemeBuilderPreview();
        }

        private void ApplyToNodeOverrides(List<string> ids, Action<ThemeNodeOverride> apply)
        {
            if (_updatingProperties || _graph?.Document?.Theme == null)
                return;
            foreach (string id in ids)
                if (_graph.Document.Theme.NodeOverrides.TryGetValue(id, out ThemeNodeOverride? ov))
                    apply(ov);
            RefreshThemeBuilderPreview();
        }

        private static int GetNodeTypeOverrideSortOrder(string key)
        {
            return key switch
            {
                "start" => 0,
                "checkpoint" => 1,
                "split" => 2,
                "converge" => 3,
                "end" => 4,
                "boss" => 5,
                _ => 100
            };
        }

        private static string GetNodeTypeDisplayName(string key)
        {
            return key switch
            {
                "start" => "Start Node Type",
                "checkpoint" => "Checkpoint Node Type",
                "split" => "Split Node Type",
                "converge" => "Converge Node Type",
                "end" => "End Segment / End Node Type",
                "boss" => "Final / Boss Node Type",
                _ => key
            };
        }

        private void Graph_SelectedSegmentChanged(GraphNode? segmentStartNode)
        {
            _showingSegmentProperties = segmentStartNode != null;
            _currentNode = segmentStartNode;
            _updatingProperties = true;

            bool hasSegment = segmentStartNode != null;

            SetPropertyControlsVisible(false);
            _lblNoSelection.Visible = !hasSegment;
            SetSegmentPropertyControlsVisible(hasSegment);
            SetStartPropertyControlsVisible(false);

            if (!hasSegment || segmentStartNode == null)
            {
                _lblPropHeader.Text = "Properties";
                ClearPropertyValues();
            }
            else
            {
                _lblPropHeader.Text = "Container";
                LoadStartNodeProperties(segmentStartNode);
            }

            RelayoutPropertyControlsForCurrentSelection();
            _pnlBackdropColorPreview.Invalidate();
            _updatingProperties = false;
            RefreshThemeBuilderPreview();
        }

        private void Graph_SelectedNodeChanged(GraphNode? node)
        {
            _showingSegmentProperties = false;
            _currentNode = node;
            _updatingProperties = true;

            int selectedCount = _graph?.SelectedNodes.Count ?? 0;
            bool hasNode = selectedCount > 0 && node != null;
            RaceFlowNodeType nodeType = hasNode ? node!.Metadata.NodeType : RaceFlowNodeType.Checkpoint;
            bool isSingleStartNode = hasNode && selectedCount == 1 && nodeType == RaceFlowNodeType.Start;
            bool isSingleFinalNode = hasNode && selectedCount == 1 && nodeType == RaceFlowNodeType.Final;
            bool canDisable = hasNode && selectedCount == 1 && CanDisableNode(nodeType);

            SetPropertyControlsVisible(hasNode);
            SetSegmentPropertyControlsVisible(false);
            SetStartPropertyControlsVisible(hasNode);
            SetDisablePropertyControlsVisible(canDisable);
            SetFinalPropertyControlsVisible(isSingleFinalNode);

            _lblStartHeader.Visible = isSingleStartNode;
            _lblTelemetryHeader.Text = isSingleStartNode ? "Start Node Telemetry" : "Node Telemetry";

            if (!hasNode || node == null)
            {
                _lblPropHeader.Text = "Properties";
                ClearPropertyValues();
            }
            else
            {
                _lblPropHeader.Text = selectedCount == 1
                    ? "Properties"
                    : $"Properties ({selectedCount} selected)";

                _txtNodeName.Text = node.Title;
                _txtDisplayName.Text = node.Metadata.DisplayName;
                _numNodeX.Value = ClampDecimal(node.X, _numNodeX.Minimum, _numNodeX.Maximum);
                _numNodeY.Value = ClampDecimal(node.Y, _numNodeY.Minimum, _numNodeY.Maximum);
                _txtNotes.Text = node.Notes;
                _pnlColorPreview.BackColor = node.NodeColor;

                LoadNodeMetadataProperties(node);

                if (isSingleStartNode)
                    LoadStartNodeProperties(node);

                if (isSingleFinalNode)
                    LoadFinalNodeProperties(node);
            }

            RelayoutPropertyControlsForCurrentSelection();

            if (_graph != null && _chkSnapToGrid != null && _chkShowGrid != null)
            {
                _chkSnapToGrid.Checked = _graph.SnapToGrid;
                _chkShowGrid.Checked = _graph.ShowGrid;
            }

            _pnlColorPreview.Invalidate();
            _pnlBackdropColorPreview.Invalidate();
            _updatingProperties = false;
            RefreshThemeBuilderPreview();
        }

        private static bool CanDisableNode(RaceFlowNodeType nodeType)
        {
            return nodeType != RaceFlowNodeType.Start &&
                   nodeType != RaceFlowNodeType.EndSegment &&
                   nodeType != RaceFlowNodeType.Final;
        }

        private void ClearPropertyValues()
        {
            _txtNodeName.Text = string.Empty;
            _txtDisplayName.Text = string.Empty;
            _numNodeX.Value = 0;
            _numNodeY.Value = 0;
            _txtNotes.Text = string.Empty;
            _pnlColorPreview.BackColor = Theme.CardBack;
            _pnlBackdropColorPreview.BackColor = Theme.CardBack;
            _txtSegmentName.Text = string.Empty;
            _txtMapId.Text = string.Empty;
            _chkNodeDisabled.Checked = false;
            _cmbFinishMode.SelectedIndex = -1;
            _numLoopCount.Value = 3;
            _numLoopRequirement.Value = 3;
        }

        private void LoadNodeMetadataProperties(GraphNode node)
        {
            RaceNodeMetadata metadata = node.Metadata;

            _chkNodeDisabled.Checked = metadata.IsDisabled;
            _txtMapId.Text = metadata.MapId;
            _numWorldX.Value = ClampDecimal((decimal)metadata.WorldX, _numWorldX.Minimum, _numWorldX.Maximum);
            _numWorldY.Value = ClampDecimal((decimal)metadata.WorldY, _numWorldY.Minimum, _numWorldY.Maximum);
            _numWorldZ.Value = ClampDecimal((decimal)metadata.WorldZ, _numWorldZ.Minimum, _numWorldZ.Maximum);
            _numRadius.Value = ClampDecimal((decimal)metadata.Radius, _numRadius.Minimum, _numRadius.Maximum);
            _numAngle.Value = ClampDecimal((decimal)metadata.Angle, _numAngle.Minimum, _numAngle.Maximum);
        }

        private void LoadFinalNodeProperties(GraphNode node)
        {
            RaceNodeMetadata metadata = node.Metadata;

            _cmbFinishMode.SelectedIndex = metadata.FinishMode switch
            {
                RaceFlowFinishMode.ManualFinish => 1,
                RaceFlowFinishMode.LoopFinish => 2,
                _ => 0
            };

            int loopCount = metadata.LoopCount <= 0 ? 3 : metadata.LoopCount;
            int requirement = metadata.LoopCheckpointRequirement <= 0
                ? CountMainCheckpointNodes()
                : metadata.LoopCheckpointRequirement;

            _numLoopCount.Value = ClampDecimal(loopCount, _numLoopCount.Minimum, _numLoopCount.Maximum);
            _numLoopRequirement.Value = ClampDecimal(requirement, _numLoopRequirement.Minimum, _numLoopRequirement.Maximum);
        }

        private int CountMainCheckpointNodes()
        {
            if (_graph == null)
                return 3;

            int count = _graph.GetAllNodesForPlanner()
                .Count(n => n.Metadata.NodeType == RaceFlowNodeType.Checkpoint && !IsPathCheckpointName(n.Title));

            return Math.Max(3, count);
        }

        private static bool IsPathCheckpointName(string title)
        {
            return !string.IsNullOrWhiteSpace(title) &&
                   System.Text.RegularExpressions.Regex.IsMatch(title.Trim(), @"^S\d+_P\d+_CP\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private void LoadStartNodeProperties(GraphNode node)
        {
            RaceNodeMetadata metadata = node.Metadata;

            _numRacePath.Value = ClampDecimal(metadata.RacePath, _numRacePath.Minimum, _numRacePath.Maximum);
            _numSegmentIndex.Value = ClampDecimal(metadata.SegmentOrder, _numSegmentIndex.Minimum, _numSegmentIndex.Maximum);

            string defaultSegmentName = $"Segment {metadata.SegmentOrder}";
            _txtSegmentName.Text = string.IsNullOrWhiteSpace(metadata.SegmentName)
                ? defaultSegmentName
                : metadata.SegmentName;

            _cmbScreenSection.SelectedItem = metadata.Side.ToString();
            _cmbFlowDirection.SelectedItem = metadata.Direction.ToString();

            _pnlBackdropColorPreview.BackColor = metadata.BackdropColorArgb != 0
                ? Color.FromArgb(metadata.BackdropColorArgb)
                : Color.FromArgb(70, 90, 110);

            _txtMapId.Text = metadata.MapId;
            _numWorldX.Value = ClampDecimal((decimal)metadata.WorldX, _numWorldX.Minimum, _numWorldX.Maximum);
            _numWorldY.Value = ClampDecimal((decimal)metadata.WorldY, _numWorldY.Minimum, _numWorldY.Maximum);
            _numWorldZ.Value = ClampDecimal((decimal)metadata.WorldZ, _numWorldZ.Minimum, _numWorldZ.Maximum);
            _numRadius.Value = ClampDecimal((decimal)metadata.Radius, _numRadius.Minimum, _numRadius.Maximum);
            _numAngle.Value = ClampDecimal((decimal)metadata.Angle, _numAngle.Minimum, _numAngle.Maximum);
        }

        private void NodeName_TextChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || _graph.SelectedNodes.Count == 0)
                return;

            _graph.SetSelectedTitle(_txtNodeName.Text);
        }

        private void NodeX_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || _graph.SelectedNodes.Count == 0)
                return;

            _graph.SetSelectedX((int)_numNodeX.Value);
        }

        private void NodeY_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || _graph.SelectedNodes.Count == 0)
                return;

            _graph.SetSelectedY((int)_numNodeY.Value);
        }

        private void Notes_TextChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || _graph.SelectedNodes.Count == 0)
                return;

            _graph.SetSelectedNotes(_txtNotes.Text);
        }

        private void PickNodeColor()
        {
            if (_graph.SelectedNodes.Count == 0)
                return;

            Color startingColor = _currentNode?.NodeColor ?? Color.FromArgb(34, 39, 47);

            using var dialog = new ColorDialog
            {
                FullOpen = true,
                AnyColor = true,
                SolidColorOnly = false,
                Color = startingColor
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _graph.SetSelectedColor(dialog.Color);
                _pnlColorPreview.BackColor = dialog.Color;
                _pnlColorPreview.Invalidate();
            }
        }

        private void DisplayName_TextChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || _graph.SelectedNodes.Count == 0)
                return;

            _graph.SetSelectedDisplayName(_txtDisplayName.Text);
        }

        private void RacePath_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || _currentNode == null)
                return;

            if (_showingSegmentProperties && _currentNode != null)
            {
                _currentNode.Metadata.RacePath = (int)_numRacePath.Value;
                _graph.RefreshSelectedNode();
                return;
            }

            _graph.SetSelectedMetadata(node => node.Metadata.RacePath = (int)_numRacePath.Value);
        }

        private void SegmentIndex_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || _currentNode == null)
                return;

            int value = (int)_numSegmentIndex.Value;
            if (_showingSegmentProperties && _currentNode != null)
            {
                _currentNode.Metadata.SegmentOrder = value;
                if (_currentNode.Metadata.NodeType == RaceFlowNodeType.Start && _currentNode.Title.StartsWith("Segment ", StringComparison.OrdinalIgnoreCase))
                    _currentNode.Title = $"Segment {value} - Start";
                if (string.IsNullOrWhiteSpace(_currentNode.Metadata.SegmentName))
                    _currentNode.Metadata.SegmentName = $"Segment {value}";
                _graph.RefreshSelectedNode();
            }
            else
            {
                _graph.SetSelectedMetadata(node =>
                {
                    node.Metadata.SegmentOrder = value;
                    if (node.Metadata.NodeType == RaceFlowNodeType.Start && node.Title.StartsWith("Segment ", StringComparison.OrdinalIgnoreCase))
                        node.Title = $"Segment {value} - Start";
                    if (string.IsNullOrWhiteSpace(node.Metadata.SegmentName))
                        node.Metadata.SegmentName = $"Segment {value}";
                });
            }

            if (_txtNodeName.Text.StartsWith("Segment ", StringComparison.OrdinalIgnoreCase))
                _txtNodeName.Text = $"Segment {value} - Start";
        }

        private void SegmentName_TextChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || _currentNode == null)
                return;

            if (_showingSegmentProperties && _currentNode != null)
            {
                _currentNode.Metadata.SegmentName = _txtSegmentName.Text;
                _graph.RefreshSelectedNode();
                return;
            }

            _graph.SetSelectedMetadata(node => node.Metadata.SegmentName = _txtSegmentName.Text);
        }

        private void ScreenSection_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || _currentNode == null || _cmbScreenSection.SelectedItem == null)
                return;

            if (!Enum.TryParse(_cmbScreenSection.SelectedItem.ToString(), out RaceFlowSide side))
                return;

            RaceFlowDirection defaultDirection = GetDefaultDirectionForSide(side);

            if (_showingSegmentProperties && _currentNode != null)
            {
                _currentNode.Metadata.Side = side;
                _currentNode.Metadata.Direction = defaultDirection;
                _graph.RefreshSelectedNode();
            }
            else
            {
                _graph.SetSelectedMetadata(node =>
                {
                    node.Metadata.Side = side;
                    node.Metadata.Direction = defaultDirection;
                });
            }

            _updatingProperties = true;
            _cmbFlowDirection.SelectedItem = defaultDirection.ToString();
            _updatingProperties = false;
        }

        private void FlowDirection_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || _currentNode == null || _cmbFlowDirection.SelectedItem == null)
                return;

            if (!Enum.TryParse(_cmbFlowDirection.SelectedItem.ToString(), out RaceFlowDirection direction))
                return;

            if (_showingSegmentProperties && _currentNode != null)
            {
                _currentNode.Metadata.Direction = direction;
                _graph.RefreshSelectedNode();
                return;
            }

            _graph.SetSelectedMetadata(node => node.Metadata.Direction = direction);
        }

        private void PickBackdropColor()
        {
            if (_currentNode == null)
                return;

            Color startingColor = _currentNode.Metadata.BackdropColorArgb != 0
                ? Color.FromArgb(_currentNode.Metadata.BackdropColorArgb)
                : Color.FromArgb(70, 90, 110);

            using var dialog = new ColorDialog
            {
                FullOpen = true,
                AnyColor = true,
                SolidColorOnly = false,
                Color = startingColor
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                if (_showingSegmentProperties && _currentNode != null)
                {
                    _currentNode.Metadata.BackdropColorArgb = dialog.Color.ToArgb();
                    _graph.RefreshSelectedNode();
                }
                else
                {
                    _graph.SetSelectedBackdropColor(dialog.Color);
                }

                _pnlBackdropColorPreview.BackColor = dialog.Color;
                _pnlBackdropColorPreview.Invalidate();
            }
        }

        private void MapId_TextChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || _currentNode == null)
                return;

            _graph.SetSelectedMetadata(node => node.Metadata.MapId = _txtMapId.Text.Trim());
        }

        private void WorldX_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || _currentNode == null)
                return;

            _graph.SetSelectedMetadata(node => node.Metadata.WorldX = (double)_numWorldX.Value);
        }

        private void WorldY_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || _currentNode == null)
                return;

            _graph.SetSelectedMetadata(node => node.Metadata.WorldY = (double)_numWorldY.Value);
        }

        private void WorldZ_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || _currentNode == null)
                return;

            _graph.SetSelectedMetadata(node => node.Metadata.WorldZ = (double)_numWorldZ.Value);
        }

        private void UpdateSelectedNodePositionFromTelemetry()
        {
            if (_currentNode == null || _graph.SelectedNodes.Count == 0)
                return;

            TelemetrySnapshot? snapshot = PlannerTelemetryRuntime.GetSnapshot();
            if (snapshot == null || !snapshot.HasUsableTelemetry)
            {
                MessageBox.Show(this,
                    "No live telemetry is currently available.",
                    "Update Position",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string mapId = snapshot.MapId?.ToString() ?? string.Empty;
            double x = snapshot.X;
            double y = snapshot.Y;
            double z = snapshot.Z;

            _graph.SetSelectedMetadata(node =>
            {
                node.Metadata.MapId = mapId;
                node.Metadata.WorldX = x;
                node.Metadata.WorldY = y;
                node.Metadata.WorldZ = z;
            });

            _updatingProperties = true;
            _txtMapId.Text = mapId;
            _numWorldX.Value = ClampDecimal((decimal)x, _numWorldX.Minimum, _numWorldX.Maximum);
            _numWorldY.Value = ClampDecimal((decimal)y, _numWorldY.Minimum, _numWorldY.Maximum);
            _numWorldZ.Value = ClampDecimal((decimal)z, _numWorldZ.Minimum, _numWorldZ.Maximum);
            _updatingProperties = false;
        }

        private void Radius_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || _currentNode == null)
                return;

            _graph.SetSelectedMetadata(node => node.Metadata.Radius = (double)_numRadius.Value);
        }

        private void Angle_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || _currentNode == null)
                return;

            _graph.SetSelectedMetadata(node => node.Metadata.Angle = (double)_numAngle.Value);
        }

        private void NodeDisabled_CheckedChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || _currentNode == null)
                return;

            _graph.SetSelectedMetadata(node => node.Metadata.IsDisabled = _chkNodeDisabled.Checked);
        }

        private void FinishMode_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || _currentNode == null)
                return;

            RaceFlowFinishMode mode = _cmbFinishMode.SelectedIndex switch
            {
                1 => RaceFlowFinishMode.ManualFinish,
                2 => RaceFlowFinishMode.LoopFinish,
                _ => RaceFlowFinishMode.AutoFinish
            };

            _currentNode.Metadata.FinishMode = mode;
            _currentNode.Metadata.IsEndOfRace = true;

            if (mode == RaceFlowFinishMode.LoopFinish)
            {
                if (_currentNode.Metadata.LoopCount <= 1)
                    _currentNode.Metadata.LoopCount = 3;

                if (_currentNode.Metadata.LoopCheckpointRequirement <= 0)
                    _currentNode.Metadata.LoopCheckpointRequirement = CountMainCheckpointNodes();

                _updatingProperties = true;
                _numLoopCount.Value = ClampDecimal(_currentNode.Metadata.LoopCount, _numLoopCount.Minimum, _numLoopCount.Maximum);
                _numLoopRequirement.Value = ClampDecimal(_currentNode.Metadata.LoopCheckpointRequirement, _numLoopRequirement.Minimum, _numLoopRequirement.Maximum);
                _updatingProperties = false;
            }

            RelayoutPropertyControlsForCurrentSelection();
            _graph.RefreshSelectedNode();
        }

        private void LoopCount_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || _currentNode == null)
                return;

            _currentNode.Metadata.LoopCount = (int)_numLoopCount.Value;
            _graph.RefreshSelectedNode();
        }

        private void LoopRequirement_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingProperties || _currentNode == null)
                return;

            _currentNode.Metadata.LoopCheckpointRequirement = (int)_numLoopRequirement.Value;
            _graph.RefreshSelectedNode();
        }

        private static RaceFlowDirection GetDefaultDirectionForSide(RaceFlowSide side)
        {
            return side switch
            {
                RaceFlowSide.Left => RaceFlowDirection.BottomToTop,
                RaceFlowSide.Right => RaceFlowDirection.TopToBottom,
                RaceFlowSide.Top => RaceFlowDirection.LeftToRight,
                RaceFlowSide.Bottom => RaceFlowDirection.RightToLeft,
                _ => RaceFlowDirection.BottomToTop
            };
        }


        private static void SuppressEnterDing_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.SuppressKeyPress = true;
            e.Handled = true;
        }


        private void CreateNewTheme()
        {
            ThemeCreationRequest? request = PromptForNewTheme();
            if (request == null)
                return;

            try
            {
                string safeName = ThemeSerializer.ToSafeFileName(request.ThemeName);
                if (string.IsNullOrWhiteSpace(safeName))
                    safeName = "raceflow_theme";

                string jsonPath = Path.Combine(request.FolderPath, safeName + ".json");
                string iconFolder = Path.Combine(request.FolderPath, safeName);

                Directory.CreateDirectory(request.FolderPath);
                Directory.CreateDirectory(iconFolder);

                ThemeProject theme = ThemeProject.CreateDefault(request.ThemeName.Trim(), jsonPath, iconFolder);
                _graph.Document.Theme = theme;
                ThemeSerializer.Save(theme, jsonPath);

                RefreshThemeBuilderPreview();
                PopulateThemeTuningProperties();
                UpdateThemeBuilderHeader();
                LayoutCustomUi();

                MessageBox.Show(
                    this,
                    $"Theme created successfully.\n\nTheme JSON:\n{jsonPath}\n\nIcon folder:\n{iconFolder}",
                    "New Theme",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Theme creation failed:\n\n" + ex.Message,
                    "New Theme",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void LoadTheme()
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "RaceFlow Theme JSON (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = "json",
                Title = "Load RaceFlow Theme"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                ThemeProject theme = ThemeSerializer.Load(dialog.FileName);
                _graph.Document.Theme = theme;

                RefreshThemeBuilderPreview();
                PopulateThemeTuningProperties();
                UpdateThemeBuilderHeader();
                LayoutCustomUi();

                string iconFolderMessage = Directory.Exists(theme.IconFolderPath)
                    ? theme.IconFolderPath
                    : theme.IconFolderPath + "\n(folder not found yet; Import Icons will create/use this location later)";

                MessageBox.Show(
                    this,
                    $"Theme loaded into this Planner project.\n\nTheme: {theme.DisplayName}\n\nTheme JSON:\n{theme.ThemeJsonPath}\n\nIcon folder:\n{iconFolderMessage}",
                    "Load Theme",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Theme load failed:\n\n" + ex.Message,
                    "Load Theme",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void AddSegmentOverrideToSelected()
        {
            if (_graph?.Document?.Theme == null)
            {
                MessageBox.Show(this, "Create or load a theme before adding segment overrides.", "Segment Override", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _graph.Document.Theme.SegmentOverrides ??= new Dictionary<string, ThemeSegmentOverride>();
            List<string> selectedIds = _themeBuilder.SelectedSegmentIds.ToList();
            if (selectedIds.Count == 0)
            {
                MessageBox.Show(this, "Select one or more segments in the Theme Builder preview first. Use Shift-click for multiple segments.", "Segment Override", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (string id in selectedIds)
            {
                if (!_graph.Document.Theme.SegmentOverrides.ContainsKey(id))
                    _graph.Document.Theme.SegmentOverrides[id] = new ThemeSegmentOverride();
            }

            SetThemeTuningPropertyControlsVisible(true);
            PopulateThemeTuningProperties();
            RelayoutPropertyControlsForCurrentSelection();
            RefreshThemeBuilderPreview();
        }

        private void AddNodeOverrideToSelected()
        {
            if (_graph?.Document?.Theme == null)
            {
                MessageBox.Show(this, "Create or load a theme before adding node overrides.", "Node Override", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _graph.Document.Theme.NodeOverrides ??= new Dictionary<string, ThemeNodeOverride>();
            List<string> selectedIds = _themeBuilder.SelectedNodeIds.ToList();
            if (selectedIds.Count == 0)
            {
                MessageBox.Show(this, "Select one or more nodes in the Theme Builder preview first. Use Shift-click for multiple nodes.", "Node Override", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (string id in selectedIds)
            {
                if (!_graph.Document.Theme.NodeOverrides.ContainsKey(id))
                    _graph.Document.Theme.NodeOverrides[id] = new ThemeNodeOverride();
            }

            SetThemeTuningPropertyControlsVisible(true);
            PopulateThemeTuningProperties();
            RelayoutPropertyControlsForCurrentSelection();
            RefreshThemeBuilderPreview();
        }

        private void ShowNodeTypeOverrideDialog()
        {
            if (_graph?.Document == null)
                return;

            if (_graph.Document.Theme == null)
            {
                MessageBox.Show(
                    this,
                    "Create or load a theme before adding node type overrides.",
                    "Node Type Override",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            ThemeProject theme = _graph.Document.Theme;
            theme.NodeTypeOverrides ??= new Dictionary<string, ThemeNodeOverride>();

            using var form = new Form
            {
                Text = "Node Type Overrides",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ClientSize = new Size(420, 360),
                BackColor = Theme.CardBackAlt,
                ForeColor = Theme.Text
            };

            var title = new Label
            {
                Left = 18,
                Top = 16,
                Width = 370,
                Height = 42,
                Text = "Choose which node types should get theme overrides.",
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
            };

            var help = new Label
            {
                Left = 18,
                Top = 58,
                Width = 370,
                Height = 38,
                Text = "Check the node types you want to add/edit. Selected types will appear below Global settings.",
                ForeColor = Theme.MutedText,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
            };

            var items = new (string Key, string Label)[]
            {
                ("start", "Start"),
                ("checkpoint", "Checkpoint"),
                ("split", "Split"),
                ("converge", "Converge"),
                ("end", "End Segment / End"),
                ("boss", "Final / Boss")
            };

            var checks = new List<CheckBox>();
            int y = 106;
            foreach (var item in items)
            {
                var check = new CheckBox
                {
                    Left = 24,
                    Top = y,
                    Width = 340,
                    Height = 26,
                    Text = item.Label,
                    Tag = item.Key,
                    Checked = theme.NodeTypeOverrides.ContainsKey(item.Key),
                    ForeColor = Theme.Text,
                    BackColor = Color.Transparent,
                    Font = new Font("Segoe UI", 9f, FontStyle.Regular)
                };
                checks.Add(check);
                form.Controls.Add(check);
                y += 32;
            }

            var ok = new Button
            {
                Text = "Apply",
                Left = 218,
                Top = 304,
                Width = 86,
                Height = 32,
                DialogResult = DialogResult.OK
            };

            var cancel = new Button
            {
                Text = "Cancel",
                Left = 312,
                Top = 304,
                Width = 86,
                Height = 32,
                DialogResult = DialogResult.Cancel
            };

            form.Controls.Add(title);
            form.Controls.Add(help);
            form.Controls.Add(ok);
            form.Controls.Add(cancel);
            form.AcceptButton = ok;
            form.CancelButton = cancel;

            if (form.ShowDialog(this) != DialogResult.OK)
                return;

            foreach (CheckBox check in checks)
            {
                if (check.Tag is not string key)
                    continue;

                if (check.Checked)
                {
                    if (!theme.NodeTypeOverrides.ContainsKey(key))
                        theme.NodeTypeOverrides[key] = CreateDefaultNodeTypeOverride(theme, key);
                }
                else
                {
                    theme.NodeTypeOverrides.Remove(key);
                }
            }

            PopulateThemeTuningProperties();
            UpdateThemeBuilderHeader();
            RefreshThemeBuilderPreview();
            LayoutCustomUi();

            MessageBox.Show(
                this,
                "Node type override selection updated.",
                "Node Type Override",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private static ThemeNodeOverride CreateDefaultNodeTypeOverride(ThemeProject theme, string nodeTypeKey)
        {
            return new ThemeNodeOverride
            {
                Image = string.Empty,
                Scale = 1.0,
                TitleVisible = true,
                TitleScale = 1.0,
                TitleOffsetX = 0,
                TitleOffsetY = 0,
                ImageOffsetX = 0,
                ImageOffsetY = 0
            };
        }

        private void ExportTheme()
        {
            if (_graph?.Document?.Theme == null || string.IsNullOrWhiteSpace(_graph.Document.Theme.ThemeJsonPath))
            {
                MessageBox.Show(
                    this,
                    "No active theme is linked to this project yet. Use New Theme or Load Theme first.",
                    "Export Theme",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                ThemeSerializer.Save(_graph.Document.Theme, _graph.Document.Theme.ThemeJsonPath);

                MessageBox.Show(
                    this,
                    $"Theme exported successfully.\n\n{_graph.Document.Theme.ThemeJsonPath}",
                    "Export Theme",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Theme export failed:\n\n" + ex.Message,
                    "Export Theme",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private ThemeCreationRequest? PromptForNewTheme()
        {
            using var form = new Form
            {
                Text = "New Theme",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ClientSize = new Size(560, 220),
                BackColor = Theme.CardBackAlt,
                ForeColor = Theme.Text
            };

            var nameLabel = new Label
            {
                Left = 18,
                Top = 18,
                Width = 500,
                Height = 20,
                Text = "Theme Name",
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
            };

            var nameText = new TextBox
            {
                Left = 18,
                Top = 42,
                Width = 510,
                Text = "New RaceFlow Theme"
            };
            Theme.ApplyTextBoxStyle(nameText);
            nameText.KeyDown += SuppressEnterDing_KeyDown;

            var folderLabel = new Label
            {
                Left = 18,
                Top = 80,
                Width = 500,
                Height = 20,
                Text = "Theme Save Location",
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
            };

            var folderText = new TextBox
            {
                Left = 18,
                Top = 104,
                Width = 400
            };
            Theme.ApplyTextBoxStyle(folderText);
            folderText.KeyDown += SuppressEnterDing_KeyDown;

            var browseButton = new Button
            {
                Text = "Browse...",
                Left = 430,
                Top = 101,
                Width = 98,
                Height = 30
            };
            browseButton.Click += (_, _) =>
            {
                using var folderDialog = new FolderBrowserDialog
                {
                    Description = "Choose where the theme JSON and icon folder should be created."
                };

                if (folderDialog.ShowDialog(form) == DialogResult.OK)
                    folderText.Text = folderDialog.SelectedPath;
            };

            var hint = new Label
            {
                Left = 18,
                Top = 142,
                Width = 510,
                Height = 32,
                Text = "Planner will create an underscored .json file and a matching icon folder in this location.",
                ForeColor = Theme.MutedText,
                BackColor = Color.Transparent
            };

            var okButton = new Button
            {
                Text = "Create Theme",
                Left = 290,
                Top = 178,
                Width = 120,
                Height = 32,
                DialogResult = DialogResult.OK
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                Left = 420,
                Top = 178,
                Width = 108,
                Height = 32,
                DialogResult = DialogResult.Cancel
            };

            form.Controls.Add(nameLabel);
            form.Controls.Add(nameText);
            form.Controls.Add(folderLabel);
            form.Controls.Add(folderText);
            form.Controls.Add(browseButton);
            form.Controls.Add(hint);
            form.Controls.Add(okButton);
            form.Controls.Add(cancelButton);
            form.AcceptButton = okButton;
            form.CancelButton = cancelButton;

            while (true)
            {
                DialogResult result = form.ShowDialog(this);
                if (result != DialogResult.OK)
                    return null;

                string themeName = nameText.Text.Trim();
                string folderPath = folderText.Text.Trim();

                if (string.IsNullOrWhiteSpace(themeName))
                {
                    MessageBox.Show(form, "Enter a theme name.", "New Theme", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(folderPath))
                {
                    MessageBox.Show(form, "Choose a theme save location.", "New Theme", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    continue;
                }

                return new ThemeCreationRequest(themeName, folderPath);
            }
        }

        private sealed class ThemeCreationRequest
        {
            public ThemeCreationRequest(string themeName, string folderPath)
            {
                ThemeName = themeName;
                FolderPath = folderPath;
            }

            public string ThemeName { get; }
            public string FolderPath { get; }
        }

        private void SaveLayout()
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "RaceFlow Planner Project (*.planrf)|*.planrf|PewPlanner Graph (*.pew)|*.pew",
                DefaultExt = "planrf",
                AddExtension = true,
                FileName = "race.planrf"
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
                _graph.SaveToFile(dialog.FileName);
        }

        private void LoadLayout()
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "RaceFlow Planner Project (*.planrf)|*.planrf|PewPlanner Graph (*.pew)|*.pew",
                DefaultExt = "planrf"
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _graph.LoadFromFile(dialog.FileName);
                RefreshThemeBuilderPreview();
            }
        }

        private void ImportSpeedometerCsv()
        {
            if (_graph == null)
                return;

            using var dialog = new OpenFileDialog
            {
                Filter = "Speedometer CSV (*.csv)|*.csv|All Files (*.*)|*.*",
                DefaultExt = "csv",
                Title = "Import Speedometer Checkpoint CSV"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            string? mapId = PromptForCsvMapId();
            if (mapId == null)
                return;

            if (string.IsNullOrWhiteSpace(mapId))
            {
                MessageBox.Show(
                    this,
                    "Please remember to update Map IDs on all nodes before you export.",
                    "CSV Import",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            try
            {
                int ignoredResetCount = _graph.ImportSpeedometerCsv(dialog.FileName, mapId.Trim());
                RefreshThemeBuilderPreview();

                string message = "CSV imported successfully.";
                if (ignoredResetCount > 0)
                    message += $"\n\nIgnored legacy Speedometer reset trigger(s): {ignoredResetCount}";

                MessageBox.Show(
                    this,
                    message,
                    "CSV Import",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "CSV import failed:\n\n" + ex.Message,
                    "CSV Import",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private string? PromptForCsvMapId()
        {
            using var form = new Form
            {
                Text = "CSV Map ID",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ClientSize = new Size(420, 170),
                BackColor = Theme.CardBackAlt,
                ForeColor = Theme.Text
            };

            var label = new Label
            {
                Left = 18,
                Top = 18,
                Width = 380,
                Height = 42,
                Text = "Legacy Speedometer CSV files do not contain a map ID. Enter the map ID to apply to all imported checkpoints.",
                ForeColor = Theme.Text,
                BackColor = Color.Transparent
            };

            var textBox = new TextBox
            {
                Left = 18,
                Top = 70,
                Width = 160
            };
            Theme.ApplyTextBoxStyle(textBox);
            textBox.KeyDown += SuppressEnterDing_KeyDown;

            var btnConfirm = new Button
            {
                Text = "Confirm",
                Left = 18,
                Top = 112,
                Width = 100,
                Height = 32,
                DialogResult = DialogResult.OK
            };

            var btnWithout = new Button
            {
                Text = "Continue Without MapID",
                Left = 130,
                Top = 112,
                Width = 170,
                Height = 32,
                DialogResult = DialogResult.Ignore
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                Left = 312,
                Top = 112,
                Width = 86,
                Height = 32,
                DialogResult = DialogResult.Cancel
            };

            form.Controls.Add(label);
            form.Controls.Add(textBox);
            form.Controls.Add(btnConfirm);
            form.Controls.Add(btnWithout);
            form.Controls.Add(btnCancel);
            form.AcceptButton = btnConfirm;
            form.CancelButton = btnCancel;

            DialogResult result = form.ShowDialog(this);
            if (result == DialogResult.OK)
                return textBox.Text.Trim();
            if (result == DialogResult.Ignore)
                return string.Empty;

            return null;
        }

        private void ExportProject()
        {
            string? exportType = PromptForExportType();
            if (exportType == null)
                return;

            if (exportType == "xml")
                ExportCheckpointXml();
            else
                ExportFlowMap();
        }

        private string? PromptForExportType()
        {
            using var form = new Form
            {
                Text = "Export",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ClientSize = new Size(390, 150),
                BackColor = Theme.CardBackAlt,
                ForeColor = Theme.Text
            };

            var label = new Label
            {
                Left = 18,
                Top = 18,
                Width = 350,
                Height = 28,
                Text = "Choose export type:",
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold)
            };

            var btnFlowMap = new Button
            {
                Text = "FlowMap JSON",
                Left = 18,
                Top = 66,
                Width = 150,
                Height = 36,
                DialogResult = DialogResult.Yes
            };

            var btnXml = new Button
            {
                Text = "Checkpoint XML",
                Left = 184,
                Top = 66,
                Width = 150,
                Height = 36,
                DialogResult = DialogResult.No
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                Left = 252,
                Top = 112,
                Width = 82,
                Height = 28,
                DialogResult = DialogResult.Cancel
            };

            form.Controls.Add(label);
            form.Controls.Add(btnFlowMap);
            form.Controls.Add(btnXml);
            form.Controls.Add(btnCancel);
            form.CancelButton = btnCancel;

            DialogResult result = form.ShowDialog(this);
            if (result == DialogResult.Yes)
                return "flowmap";
            if (result == DialogResult.No)
                return "xml";

            return null;
        }

        private void ExportFlowMap()
        {
            if (_graph == null)
                return;

            List<string> issues = FlowMapExportValidator.Validate(_graph.Document);
            if (issues.Count > 0)
            {
                string message =
                    "Export validation found the following issue(s):\n\n" +
                    string.Join("\n", issues.Select(i => "• " + i)) +
                    "\n\nExport anyway?";

                DialogResult result = MessageBox.Show(
                    this,
                    message,
                    "Export FlowMap",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                    return;
            }

            using var dialog = new SaveFileDialog
            {
                Filter = "Flow Map JSON (*.json)|*.json",
                DefaultExt = "json",
                AddExtension = true,
                FileName = "flowmap.json"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                FlowMapExporter.ExportToFile(_graph.Document, dialog.FileName, "");

                MessageBox.Show(
                    this,
                    "FlowMap exported successfully.",
                    "Export FlowMap",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "FlowMap export failed:\n\n" + ex.Message,
                    "Export FlowMap",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ExportCheckpointXml()
        {
            if (_graph == null)
                return;

            using var dialog = new SaveFileDialog
            {
                Filter = "Checkpoint XML (*.xml)|*.xml",
                DefaultExt = "xml",
                AddExtension = true,
                FileName = "checkpoints.xml"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                CheckpointXmlExporter.ExportToFile(_graph.Document, dialog.FileName);

                MessageBox.Show(
                    this,
                    "Checkpoint XML exported successfully.",
                    "Export XML",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Checkpoint XML export failed:\n\n" + ex.Message,
                    "Export XML",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OpenObsOutput()
        {
            if (_themeBuilder == null)
                return;

            try
            {
                _obsOutputServer ??= new PlannerObsOutputServer(_themeBuilder, 5057);
                _obsOutputServer.Start();
                _obsOutputServer.OpenInBrowser();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "OBS Output could not be started:\n\n" + ex.Message,
                    "OBS Output",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try
            {
                _telemetryTimer?.Stop();
                _telemetryTimer?.Dispose();
                PlannerTelemetryRuntime.StopAsync().GetAwaiter().GetResult();
                _obsOutputServer?.Stop();
                _obsOutputServer = null;
            }
            catch
            {
            }

            base.OnFormClosed(e);
        }

        private void ConfirmClearGraph()
        {
            var result = MessageBox.Show(
                this,
                "Clear the entire graph?\n\nThis will remove all nodes and connections.",
                "Clear Graph",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                _graph.ClearGraph();
                RefreshThemeBuilderPreview();
            }
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
        }

        private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                _dragStart = new Point(e.X, e.Y);
        }

        private void TitleBar_MouseMove(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || WindowState == FormWindowState.Maximized)
                return;

            Left += e.X - _dragStart.X;
            Top += e.Y - _dragStart.Y;
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            const int HTCLIENT = 1;
            const int HTLEFT = 10;
            const int HTRIGHT = 11;
            const int HTTOP = 12;
            const int HTTOPLEFT = 13;
            const int HTTOPRIGHT = 14;
            const int HTBOTTOM = 15;
            const int HTBOTTOMLEFT = 16;
            const int HTBOTTOMRIGHT = 17;

            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);

                if ((int)m.Result == HTCLIENT)
                {
                    Point p = PointToClient(GetMousePointFromLParam(m.LParam));

                    bool left = p.X >= 0 && p.X < ResizeBorderSize;
                    bool right = p.X <= ClientSize.Width && p.X > ClientSize.Width - ResizeBorderSize;
                    bool top = p.Y >= 0 && p.Y < ResizeBorderSize;
                    bool bottom = p.Y <= ClientSize.Height && p.Y > ClientSize.Height - ResizeBorderSize;

                    if (left && top)
                        m.Result = (IntPtr)HTTOPLEFT;
                    else if (right && top)
                        m.Result = (IntPtr)HTTOPRIGHT;
                    else if (left && bottom)
                        m.Result = (IntPtr)HTBOTTOMLEFT;
                    else if (right && bottom)
                        m.Result = (IntPtr)HTBOTTOMRIGHT;
                    else if (left)
                        m.Result = (IntPtr)HTLEFT;
                    else if (right)
                        m.Result = (IntPtr)HTRIGHT;
                    else if (top)
                        m.Result = (IntPtr)HTTOP;
                    else if (bottom)
                        m.Result = (IntPtr)HTBOTTOM;
                }

                return;
            }

            base.WndProc(ref m);
        }

        private static decimal ClampDecimal(decimal value, decimal min, decimal max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static Point GetMousePointFromLParam(IntPtr lParam)
        {
            int value = lParam.ToInt32();
            int x = (short)(value & 0xFFFF);
            int y = (short)((value >> 16) & 0xFFFF);
            return new Point(x, y);
        }
    }
}