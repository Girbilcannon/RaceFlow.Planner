using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RaceFlow.Planner.ThemeBuilder
{
    public static class ThemeSerializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static void Save(ThemeProject theme, string filePath)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Theme file path is required.", nameof(filePath));

            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            theme.ThemeJsonPath = filePath;
            if (string.IsNullOrWhiteSpace(theme.DisplayName))
                theme.DisplayName = Path.GetFileNameWithoutExtension(filePath);

            JsonNode? root = JsonSerializer.SerializeToNode(theme, Options);
            if (root is JsonObject obj)
            {
                // Theme JSON should stay portable/shareable. Absolute Planner paths live in .planrf only.
                obj.Remove("themeJsonPath");
                obj.Remove("iconFolderPath");
            }

            string json = root?.ToJsonString(Options) ?? JsonSerializer.Serialize(theme, Options);
            File.WriteAllText(filePath, json);
        }

        public static ThemeProject Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Theme file path is required.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Theme JSON file not found.", filePath);

            string json = File.ReadAllText(filePath);
            ThemeProject? theme = JsonSerializer.Deserialize<ThemeProject>(json, Options);
            theme ??= new ThemeProject();

            // Older theme files won't have these Planner-only references. Fill them in from the loaded path.
            theme.ThemeJsonPath = filePath;
            if (string.IsNullOrWhiteSpace(theme.DisplayName))
            {
                try
                {
                    JsonNode? root = JsonNode.Parse(json);
                    string? nameFromJson = root?["themeName"]?.GetValue<string>();
                    theme.DisplayName = string.IsNullOrWhiteSpace(nameFromJson)
                        ? Path.GetFileNameWithoutExtension(filePath)
                        : nameFromJson;
                }
                catch
                {
                    theme.DisplayName = Path.GetFileNameWithoutExtension(filePath);
                }
            }

            if (string.IsNullOrWhiteSpace(theme.IconFolderPath))
            {
                string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
                string folderName = Path.GetFileNameWithoutExtension(filePath);
                theme.IconFolderPath = Path.Combine(directory, folderName);
            }

            return theme;
        }

        public static string ToSafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var builder = new StringBuilder();
            bool previousWasUnderscore = false;

            foreach (char raw in name.Trim())
            {
                char c = char.ToLowerInvariant(raw);
                bool valid = char.IsLetterOrDigit(c);
                if (valid)
                {
                    builder.Append(c);
                    previousWasUnderscore = false;
                    continue;
                }

                if (!previousWasUnderscore)
                {
                    builder.Append('_');
                    previousWasUnderscore = true;
                }
            }

            return builder.ToString().Trim('_');
        }
    }
}
