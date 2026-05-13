using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace RaceFlow.Planner.Import
{
    public enum SpeedometerCsvCheckpointKind
    {
        Start,
        Checkpoint,
        End
    }

    public sealed class SpeedometerCsvCheckpoint
    {
        public int Step { get; set; }
        public string StepName { get; set; } = string.Empty;
        public SpeedometerCsvCheckpointKind Kind { get; set; } = SpeedometerCsvCheckpointKind.Checkpoint;
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Radius { get; set; } = 15.0;
        public double Angle { get; set; } = 360.0;
        public string Note { get; set; } = string.Empty;
    }

    public sealed class SpeedometerCsvImportResult
    {
        public List<SpeedometerCsvCheckpoint> Checkpoints { get; } = new();
        public int IgnoredResetCount { get; set; }
    }

    public static class SpeedometerCsvImporter
    {
        public static SpeedometerCsvImportResult Import(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("CSV file path is required.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("CSV file was not found.", filePath);

            var result = new SpeedometerCsvImportResult();
            string[] lines = File.ReadAllLines(filePath);

            foreach (string rawLine in lines)
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                    continue;

                string line = rawLine.Trim();
                if (line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                string[] parts = SplitCsvLine(line);
                if (parts.Length < 6)
                    continue;

                if (IsHeader(parts))
                    continue;

                if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int step))
                    continue;

                string stepName = parts[1].Trim();
                string normalizedName = stepName.ToLowerInvariant();

                if (step == -1 && normalizedName == "reset")
                {
                    result.IgnoredResetCount++;
                    continue;
                }

                if (!TryParseDouble(parts[2], out double x) ||
                    !TryParseDouble(parts[3], out double y) ||
                    !TryParseDouble(parts[4], out double z))
                {
                    continue;
                }

                double radius = 15.0;
                if (parts.Length >= 6 && TryParseDouble(parts[5], out double parsedRadius))
                    radius = parsedRadius;

                double angle = 360.0;
                if (parts.Length >= 7 && TryParseDouble(parts[6], out double parsedAngle))
                    angle = parsedAngle;

                SpeedometerCsvCheckpointKind kind = normalizedName switch
                {
                    "start" => SpeedometerCsvCheckpointKind.Start,
                    "end" => SpeedometerCsvCheckpointKind.End,
                    _ => SpeedometerCsvCheckpointKind.Checkpoint
                };

                result.Checkpoints.Add(new SpeedometerCsvCheckpoint
                {
                    Step = step,
                    StepName = stepName,
                    Kind = kind,
                    X = x,
                    Y = y,
                    Z = z,
                    Radius = radius,
                    Angle = angle,
                    Note = normalizedName is "start" or "end" ? $"Imported Speedometer {normalizedName} checkpoint." : string.Empty
                });
            }

            result.Checkpoints.Sort((a, b) => a.Step.CompareTo(b.Step));
            return result;
        }

        private static bool IsHeader(string[] parts)
        {
            return parts.Length > 0 &&
                   string.Equals(parts[0].Trim(), "STEP", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseDouble(string text, out double value)
        {
            return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string[] SplitCsvLine(string line)
        {
            var values = new List<string>();
            var current = new List<char>();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Add('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(new string(current.ToArray()));
                    current.Clear();
                }
                else
                {
                    current.Add(c);
                }
            }

            values.Add(new string(current.ToArray()));
            return values.ToArray();
        }
    }
}
