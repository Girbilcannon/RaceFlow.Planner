using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace RaceFlow.Planner.Telemetry
{
    public sealed class TelemetryJsonReader : IDisposable
    {
        private readonly HttpClient _client = new();
        private readonly string _url;

        public TelemetryJsonReader(string url)
        {
            _url = url;
            _client.Timeout = TimeSpan.FromMilliseconds(750);
        }

        public async Task<TelemetrySnapshot?> ReadAsync()
        {
            string json = await _client.GetStringAsync(_url);

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            var snapshot = new TelemetrySnapshot
            {
                Available = GetBoolean(root, "available"),
                GameRunning = GetBoolean(root, "gameRunning"),
                TelemetryReady = GetBoolean(root, "telemetryReady"),
                FailureReason = GetString(root, "failureReason"),
                MapId = GetInt(root, "mapId"),
                LastUpdateUtc = GetDateTime(root, "lastUpdateUtc")
            };

            if (root.TryGetProperty("position", out JsonElement position) && position.ValueKind == JsonValueKind.Object)
            {
                snapshot.X = GetDouble(position, "x");
                snapshot.Y = GetDouble(position, "y");
                snapshot.Z = GetDouble(position, "z");
            }

            return snapshot;
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        private static bool GetBoolean(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement value))
                return false;

            return value.ValueKind == JsonValueKind.True ||
                   (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out bool parsed) && parsed);
        }

        private static string GetString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement value))
                return string.Empty;

            return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
        }

        private static int? GetInt(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement value))
                return null;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int parsedNumber))
                return parsedNumber;

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out int parsedString))
                return parsedString;

            return null;
        }

        private static double GetDouble(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement value))
                return 0;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double parsedNumber))
                return parsedNumber;

            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out double parsedString))
                return parsedString;

            return 0;
        }

        private static DateTime GetDateTime(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement value))
                return default;

            if (value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), out DateTime parsed))
                return parsed.ToUniversalTime();

            return default;
        }
    }
}
