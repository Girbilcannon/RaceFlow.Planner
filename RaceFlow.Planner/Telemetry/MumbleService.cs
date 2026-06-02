using System;
using Gw2Sharp;
using Gw2Sharp.Mumble;
using Newtonsoft.Json.Linq;
using System.Reflection;

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

            public bool HasCameraData { get; set; }

            public float CameraX { get; set; }
            public float CameraY { get; set; }
            public float CameraZ { get; set; }

            public float CameraFrontX { get; set; }
            public float CameraFrontY { get; set; }
            public float CameraFrontZ { get; set; }

            public float CameraTopX { get; set; }
            public float CameraTopY { get; set; }
            public float CameraTopZ { get; set; }

            public float Fov { get; set; }

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

                    float cameraX = 0f;
                    float cameraY = 0f;
                    float cameraZ = 0f;

                    float cameraFrontX = 0f;
                    float cameraFrontY = 1f;
                    float cameraFrontZ = 0f;

                    float cameraTopX = 0f;
                    float cameraTopY = 0f;
                    float cameraTopZ = 1f;

                    bool hasCameraPosition = TryReadVectorProperty(Client, "CameraPosition", out cameraX, out cameraY, out cameraZ);
                    bool hasCameraFront = TryReadVectorProperty(Client, "CameraFront", out cameraFrontX, out cameraFrontY, out cameraFrontZ);
                    bool hasCameraTop = TryReadVectorProperty(Client, "CameraTop", out cameraTopX, out cameraTopY, out cameraTopZ);

                    // Gw2Sharp exposes camera position/front reliably in current testing, but CameraTop may
                    // be unavailable depending on the wrapper/runtime. The MumbleLink data structure does
                    // contain CameraTop, so keep this as a real field when available; otherwise use a safe
                    // world-up fallback so overlay projection can still be debugged/tuned.
                    if (!hasCameraTop)
                    {
                        cameraTopX = 0f;
                        cameraTopY = 0f;
                        cameraTopZ = 1f;
                    }

                    bool hasCameraData = hasCameraPosition && hasCameraFront;

                    float fov = TryReadFloatProperty(Client, out float parsedFov, "Fov", "FOV", "FieldOfView")
                        ? parsedFov
                        : 1.134f;

                    bool hasValidPosition =
                        !float.IsNaN(playerX) && !float.IsInfinity(playerX) &&
                        !float.IsNaN(playerY) && !float.IsInfinity(playerY) &&
                        !float.IsNaN(playerZ) && !float.IsInfinity(playerZ);

                    bool usableForTelemetry =
                        mapId > 0 &&
                        hasValidPosition;

                    snapshot = new MumbleSnapshot
                    {
                        IsUsableForTelemetry = usableForTelemetry,
                        MapId = mapId,
                        Tick = currentTick,
                        PlayerX = playerX,
                        PlayerY = playerY,
                        PlayerZ = playerZ,
                        HasCameraData = hasCameraData,
                        CameraX = cameraX,
                        CameraY = cameraY,
                        CameraZ = cameraZ,
                        CameraFrontX = cameraFrontX,
                        CameraFrontY = cameraFrontY,
                        CameraFrontZ = cameraFrontZ,
                        CameraTopX = cameraTopX,
                        CameraTopY = cameraTopY,
                        CameraTopZ = cameraTopZ,
                        Fov = fov,
                        RawIdentity = rawIdentity,
                        CharacterName = characterName
                    };

                    failureReason = usableForTelemetry
                        ? string.Empty
                        : $"MumbleLink available, waiting for usable telemetry (mapId={mapId}, tick={currentTick}, tickAdvanced={tickAdvanced}).";

                    return true;
                }
                catch (Exception ex)
                {
                    failureReason = $"{ex.GetType().Name}: {ex.Message}";
                    return false;
                }
            }
        }

        private static bool TryReadVectorProperty(object source, string propertyName, out float x, out float y, out float z)
        {
            x = y = z = 0f;

            try
            {
                PropertyInfo? property = source.GetType().GetProperty(propertyName);
                if (property == null)
                    return false;

                object? value = property.GetValue(source);
                if (value == null)
                    return false;

                if (!TryReadFloatMember(value, "X", out x) ||
                    !TryReadFloatMember(value, "Y", out y) ||
                    !TryReadFloatMember(value, "Z", out z))
                {
                    x = y = z = 0f;
                    return false;
                }

                return IsFinite(x) && IsFinite(y) && IsFinite(z);
            }
            catch
            {
                x = y = z = 0f;
                return false;
            }
        }

        private static bool TryReadFloatProperty(object source, out float value, params string[] propertyNames)
        {
            value = 0f;

            foreach (string propertyName in propertyNames)
            {
                try
                {
                    PropertyInfo? property = source.GetType().GetProperty(propertyName);
                    if (property == null)
                        continue;

                    object? raw = property.GetValue(source);
                    if (raw == null)
                        continue;

                    value = Convert.ToSingle(raw);
                    return IsFinite(value);
                }
                catch
                {
                }
            }

            return false;
        }

        private static bool TryReadFloatMember(object source, string memberName, out float value)
        {
            value = 0f;

            try
            {
                Type type = source.GetType();

                PropertyInfo? property = type.GetProperty(memberName);
                if (property != null)
                {
                    object? raw = property.GetValue(source);
                    if (raw != null)
                    {
                        value = Convert.ToSingle(raw);
                        return IsFinite(value);
                    }
                }

                FieldInfo? field = type.GetField(memberName);
                if (field != null)
                {
                    object? raw = field.GetValue(source);
                    if (raw != null)
                    {
                        value = Convert.ToSingle(raw);
                        return IsFinite(value);
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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
