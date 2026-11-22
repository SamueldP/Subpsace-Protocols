namespace SubspaceProtocols.Core;

/// <summary>
/// Silent audio event logger and hook stub.
/// Place AudioHooks.Play("event_id") at every gameplay feedback point
/// (citations, hailing, printer ticks, dock lock, detain alarm, terminal keys).
/// </summary>
public static class AudioHooks
{
    private static readonly List<string> RecentEvents = [];
    private const int MaxLoggedEvents = 32;

    public static IReadOnlyList<string> LoggedEvents => RecentEvents;
    public static string LastPlayedSoundId { get; private set; } = "NONE";
    public static float MasterVolume { get; set; } = 0.80f;
    public static bool IsMuted { get; set; } = false;

    public static void Play(string soundId)
    {
        if (string.IsNullOrWhiteSpace(soundId))
            return;

        LastPlayedSoundId = IsMuted ? $"{soundId} [MUTED]" : soundId;
        if (RecentEvents.Count >= MaxLoggedEvents)
        {
            RecentEvents.RemoveAt(0);
        }
        RecentEvents.Add(soundId);
    }
}
