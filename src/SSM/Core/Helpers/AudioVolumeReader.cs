using System.Collections.Concurrent;
using NAudio.CoreAudioApi;

namespace SSM.Core.Helpers;

/// <summary>
/// Reads Windows master audio output volume via NAudio CoreAudio API.
/// CoreAudio COM requires STA; HardwareMonitorService's timer runs on MTA thread-pool
/// threads, so queries are marshalled to a dedicated persistent STA thread.
/// </summary>
internal static class AudioVolumeReader
{
    private static readonly StaDispatcher _sta = new();

    public static float GetMasterVolume() => _sta.Invoke(static () =>
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device     = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        return device.AudioEndpointVolume.MasterVolumeLevelScalar * 100f;
    });

    // ── Minimal STA dispatcher ─────────────────────────────────
    // Keeps one long-lived STA thread alive and dispatches work items onto it.

    private sealed class StaDispatcher
    {
        private readonly BlockingCollection<(Func<float> work, TaskCompletionSource<float> tcs)> _queue = new();

        public StaDispatcher()
        {
            var thread = new Thread(Loop) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private void Loop()
        {
            foreach (var (work, tcs) in _queue.GetConsumingEnumerable())
            {
                try   { tcs.SetResult(work()); }
                catch { tcs.SetResult(0f); }
            }
        }

        public float Invoke(Func<float> work)
        {
            var tcs = new TaskCompletionSource<float>();
            _queue.Add((work, tcs));
            return tcs.Task.GetAwaiter().GetResult();
        }
    }
}
