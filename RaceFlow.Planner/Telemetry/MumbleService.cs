using System;
using Gw2Sharp;
using Gw2Sharp.Mumble;
using Newtonsoft.Json.Linq;

namespace RaceFlow.Planner.Telemetry
{
    internal static class MumbleService
    {
        private const string MumbleLinkName = "MumbleLink";

        private static readonly object ReadLock = new();
        private static readonly IGw2MumbleClient Client = new Gw2Client().Mumble[MumbleLinkName];

        private static int? _lastTick;

        public sealed class MumbleSnapshot
        {
            public bool IsUsableForTelemetry { get; set; }
            public int MapId { get; set; }
            public int Tick { get; set; }

            public float PlayerX { get; set; }
            public float PlayerY { get; set; }
            public float PlayerZ { get; set; }

            public string RawIdentity { get; set; } = string.Empty;
            public string CharacterName { get; set; } = string.Empty;
        }

        public static bool TryGetSnapshotDetailed(out MumbleSnapshot? snapshot, out string failureReason)
        {
            lock (ReadLock)
            {
                snapshot = null;
                failureReason = string.Empty;

                try
                {
                    Client.Update();

                    if (!Client.IsAvailable)
                    {
                        failureReason = "MumbleLink is not available.";
                        return false;
                    }

                    int mapId = Client.MapId;
                    int currentTick = Client.Tick;
                    string rawIdentity = Client.RawIdentity ?? string.Empty;
                    string characterName = string.Empty;

                    if (!string.IsNullOrWhiteSpace(rawIdentity))
                    {
                        try
                        {
                            JObject json = JObject.Parse(rawIdentity);
                            characterName = (string?)json["name"] ?? string.Empty;
                        }
                        catch
                        {
                        }
                    }

                    bool tickAdvanced = !_lastTick.HasValue || currentTick != _lastTick.Value;
                    _lastTick = currentTick;

                    float playerX = (float)Client.AvatarPosition.X;
                    float playerY = (float)Client.AvatarPosition.Y;
                    float playerZ = (float)Client.AvatarPosition.Z;

                    bool hasValidPosition =
                        !float.IsNaN(playerX) && !float.IsInfinity(playerX) &&
                        !float.IsNaN(playerY) && !float.IsInfinity(playerY) &&
                        !float.IsNaN(playerZ) && !float.IsInfinity(playerZ);

                    bool usableForTelemetry =
                        mapId > 0 &&
                        tickAdvanced &&
                        hasValidPosition;

                    snapshot = new MumbleSnapshot
                    {
                        IsUsableForTelemetry = usableForTelemetry,
                        MapId = mapId,
                        Tick = currentTick,
                        PlayerX = playerX,
                        PlayerY = playerY,
                        PlayerZ = playerZ,
                        RawIdentity = rawIdentity,
                        CharacterName = characterName
                    };

                    failureReason = usableForTelemetry
                        ? string.Empty
                        : $"MumbleLink available, waiting for fresh telemetry (mapId={mapId}, tick={currentTick}, tickAdvanced={tickAdvanced}).";

                    return true;
                }
                catch (Exception ex)
                {
                    failureReason = $"{ex.GetType().Name}: {ex.Message}";
                    return false;
                }
            }
        }

        public static void ResetTickGate()
        {
            lock (ReadLock)
            {
                _lastTick = null;
            }
        }
    }
}
