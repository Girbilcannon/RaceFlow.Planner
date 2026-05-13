using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RaceFlow.Planner.ThemeBuilder
{
    public sealed class ThemeProject
    {
        [JsonPropertyName("themeName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("themeJsonPath")]
        public string ThemeJsonPath { get; set; } = string.Empty;

        [JsonPropertyName("iconFolderPath")]
        public string IconFolderPath { get; set; } = string.Empty;

        [JsonPropertyName("nodes")]
        public Dictionary<string, string> Nodes { get; set; } = CreateDefaultNodes();

        [JsonPropertyName("lines")]
        public ThemeLineSettings Lines { get; set; } = new();

        [JsonPropertyName("settings")]
        public ThemeRenderSettings Settings { get; set; } = new();

        [JsonPropertyName("nodeTypeOverrides")]
        public Dictionary<string, ThemeNodeOverride> NodeTypeOverrides { get; set; } = new();

        [JsonPropertyName("segmentOverrides")]
        public Dictionary<string, ThemeSegmentOverride> SegmentOverrides { get; set; } = new();

        [JsonPropertyName("nodeOverrides")]
        public Dictionary<string, ThemeNodeOverride> NodeOverrides { get; set; } = new();

        [JsonPropertyName("racers")]
        public ThemeRacerSettings Racers { get; set; } = new();

        public static ThemeProject CreateDefault(string displayName, string themeJsonPath, string iconFolderPath)
        {
            return new ThemeProject
            {
                DisplayName = displayName,
                ThemeJsonPath = themeJsonPath,
                IconFolderPath = iconFolderPath
            };
        }

        private static Dictionary<string, string> CreateDefaultNodes()
        {
            return new Dictionary<string, string>
            {
                ["start"] = string.Empty,
                ["checkpoint"] = string.Empty,
                ["split"] = string.Empty,
                ["converge"] = string.Empty,
                ["end"] = string.Empty,
                ["boss"] = string.Empty
            };
        }

    }

    public sealed class ThemeLineSettings
    {
        [JsonPropertyName("defaultColor")]
        public string DefaultColor { get; set; } = "#5F788C";

        [JsonPropertyName("splitColor")]
        public string SplitColor { get; set; } = "#AA823C";

        [JsonPropertyName("convergeColor")]
        public string ConvergeColor { get; set; } = "#8C5AAA";

        [JsonPropertyName("thickness")]
        public int Thickness { get; set; } = 3;
    }

    public sealed class ThemeRenderSettings
    {
        [JsonPropertyName("nodeScale")]
        public double NodeScale { get; set; } = 1.0;

        [JsonPropertyName("nodeVisibility")]
        public bool NodeVisibility { get; set; } = true;

        [JsonPropertyName("titleVisible")]
        public bool TitleVisible { get; set; } = true;

        [JsonPropertyName("titleScale")]
        public double TitleScale { get; set; } = 1.0;

        [JsonPropertyName("titleOffsetX")]
        public int TitleOffsetX { get; set; } = 0;

        [JsonPropertyName("titleOffsetY")]
        public int TitleOffsetY { get; set; } = -40;

        [JsonPropertyName("lineVisibility")]
        public bool LineVisibility { get; set; } = true;

        [JsonPropertyName("shadowEnabled")]
        public bool ShadowEnabled { get; set; } = true;

        [JsonPropertyName("shadowOpacity")]
        public double ShadowOpacity { get; set; } = 0.5;

        [JsonPropertyName("shadowColor")]
        public string ShadowColor { get; set; } = "#000000";

        [JsonPropertyName("shadowBlur")]
        public int ShadowBlur { get; set; } = 8;

        [JsonPropertyName("shadowOffsetX")]
        public int ShadowOffsetX { get; set; } = 4;

        [JsonPropertyName("shadowOffsetY")]
        public int ShadowOffsetY { get; set; } = 4;
    }

    public sealed class ThemeNodeOverride
    {
        [JsonPropertyName("image")]
        public string Image { get; set; } = string.Empty;

        [JsonPropertyName("scale")]
        public double Scale { get; set; } = 1.0;

        [JsonPropertyName("nodeVisibility")]
        public bool NodeVisibility { get; set; } = true;

        [JsonPropertyName("titleVisible")]
        public bool TitleVisible { get; set; } = true;

        [JsonPropertyName("titleScale")]
        public double TitleScale { get; set; } = 1.0;

        [JsonPropertyName("titleOffsetX")]
        public int TitleOffsetX { get; set; } = 0;

        [JsonPropertyName("titleOffsetY")]
        public int TitleOffsetY { get; set; } = 0;

        [JsonPropertyName("imageOffsetX")]
        public int ImageOffsetX { get; set; } = 0;

        [JsonPropertyName("imageOffsetY")]
        public int ImageOffsetY { get; set; } = 0;
    }

    public sealed class ThemeSegmentOverride
    {
        [JsonPropertyName("lineColor")]
        public string LineColor { get; set; } = "#00FF00";

        [JsonPropertyName("splitLineColor")]
        public string SplitLineColor { get; set; } = string.Empty;

        [JsonPropertyName("convergeLineColor")]
        public string ConvergeLineColor { get; set; } = string.Empty;

        [JsonPropertyName("thickness")]
        public int Thickness { get; set; } = 4;

        // Legacy field retained for compatibility with older theme files/Admin builds.
        // RaceFlow.Planner no longer exposes this in Segment Override UI because
        // icon replacement belongs in Node Type Override or Node Override.
        [JsonPropertyName("nodeImage")]
        public string NodeImage { get; set; } = string.Empty;

        [JsonPropertyName("nodeVisibility")]
        public bool NodeVisibility { get; set; } = true;

        [JsonPropertyName("nodeScale")]
        public double NodeScale { get; set; } = 1.0;

        [JsonPropertyName("imageOffsetX")]
        public int ImageOffsetX { get; set; } = 0;

        [JsonPropertyName("imageOffsetY")]
        public int ImageOffsetY { get; set; } = 0;

        [JsonPropertyName("titleVisible")]
        public bool TitleVisible { get; set; } = true;

        [JsonPropertyName("titleScale")]
        public double TitleScale { get; set; } = 1.0;

        [JsonPropertyName("titleOffsetX")]
        public int TitleOffsetX { get; set; } = 0;

        [JsonPropertyName("titleOffsetY")]
        public int TitleOffsetY { get; set; } = 0;
    }

    public sealed class ThemeRacerSettings
    {
        [JsonPropertyName("dotSize")]
        public int DotSize { get; set; } = 18;

        [JsonPropertyName("inactiveDotSize")]
        public int InactiveDotSize { get; set; } = 14;

        [JsonPropertyName("glowScale")]
        public double GlowScale { get; set; } = 1.8;

        [JsonPropertyName("glowOpacity")]
        public double GlowOpacity { get; set; } = 0.28;

        [JsonPropertyName("nameVisible")]
        public bool NameVisible { get; set; } = true;

        [JsonPropertyName("nameScale")]
        public double NameScale { get; set; } = 1.0;

        [JsonPropertyName("nameOffsetX")]
        public int NameOffsetX { get; set; } = 12;

        [JsonPropertyName("nameOffsetY")]
        public int NameOffsetY { get; set; } = -16;
    }
}
