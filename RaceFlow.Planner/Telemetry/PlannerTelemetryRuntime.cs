using System;
using System.Threading;
using System.Threading.Tasks;

namespace RaceFlow.Planner.Telemetry
{
    internal static class PlannerTelemetryRuntime
    {
        private static readonly object Sync = new();

        private static CancellationTokenSource? _cts;
        private static Task? _loopTask;
        private static TelemetrySnapshot? _latestSnapshot;

        public static void Start()
        {
            lock (Sync)
            {
                if (_loopTask != null && !_loopTask.IsCompleted)
                    return;

                MumbleService.ResetTickGate();
                _cts = new CancellationTokenSource();
                _loopTask = Task.Run(() => PollLoopAsync(_cts.Token));
            }
        }

        public static async Task StopAsync()
        {
            CancellationTokenSource? cts;
            Task? loopTask;

            lock (Sync)
            {
                cts = _cts;
                loopTask = _loopTask;
                _cts = null;
                _loopTask = null;
            }

            if (cts != null)
                cts.Cancel();

            if (loopTask != null)
            {
                try
                {
                    await loopTask.ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }

        public static TelemetrySnapshot? GetSnapshot()
        {
            lock (Sync)
            {
                return _latestSnapshot;
            }
        }

        private static async Task PollLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    PollOnce();
                }
                catch
                {
                }

                try
                {
                    await Task.Delay(250, token).ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }

        private static void PollOnce()
        {
            bool gameRunning = ProcessHelpers.IsGw2Running();

            bool mumbleAvailable = MumbleService.TryGetSnapshotDetailed(out MumbleService.MumbleSnapshot? mumble, out string failureReason);
            DateTime nowUtc = DateTime.UtcNow;

            TelemetrySnapshot snapshot = new()
            {
                Available = mumbleAvailable,
                GameRunning = gameRunning,
                TelemetryReady = mumbleAvailable && gameRunning && mumble?.IsUsableForTelemetry == true,
                FailureReason = failureReason,
                LastUpdateUtc = nowUtc,
                MapId = mumble != null && mumble.MapId > 0 ? mumble.MapId : null,
                X = mumble?.PlayerX ?? 0,
                Y = mumble?.PlayerY ?? 0,
                Z = mumble?.PlayerZ ?? 0
            };

            lock (Sync)
            {
                _latestSnapshot = snapshot;
            }
        }
    }
}
