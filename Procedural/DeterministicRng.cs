namespace SubspaceProtocols.Procedural;

/// <summary>
/// FNV-1a 32-bit string seed hasher feeding a Mulberry32 deterministic PRNG
/// defined in /design/08_Physical_Look/Procedural_Art_Bible_and_Asset_Spec.md (Block 3).
/// Ensures any NPC ID or Ship ID string produces 100% deterministic, repeatable traits
/// across viewscreens, holo-photos, recognition manuals, and repeat visits.
/// </summary>
public sealed class DeterministicRng
{
    private uint _state;

    public string SeedString { get; }
    public uint InitialHash { get; }

    public DeterministicRng(string seedString)
    {
        SeedString = seedString ?? string.Empty;
        InitialHash = HashSeedFnv1a(SeedString);
        _state = InitialHash;
    }

    public DeterministicRng(uint numericSeed)
    {
        SeedString = numericSeed.ToString();
        InitialHash = numericSeed == 0 ? 0x6D2B79F5u : numericSeed;
        _state = InitialHash;
    }

    /// <summary>
    /// FNV-1a 32-bit string hash (offset basis 2166136261, prime 16777619).
    /// </summary>
    public static uint HashSeedFnv1a(string seed)
    {
        if (string.IsNullOrEmpty(seed))
            return 2166136261u;

        uint hash = 2166136261u;
        for (int i = 0; i < seed.Length; i++)
        {
            hash ^= seed[i];
            hash *= 16777619u;
        }
        return hash == 0 ? 1u : hash;
    }

    /// <summary>
    /// Creates an independent deterministic branch RNG seeded from "{SeedString}:{branchKey}".
    /// </summary>
    public DeterministicRng Fork(string branchKey) =>
        new($"{SeedString}:{branchKey}");

    /// <summary>
    /// Mulberry32 next unsigned 32-bit integer.
    /// </summary>
    public uint NextUInt32()
    {
        uint t = _state += 0x6D2B79F5u;
        t = (t ^ (t >> 15)) * (t | 1u);
        t ^= t + (t ^ (t >> 7)) * (t | 61u);
        return t ^ (t >> 14);
    }

    /// <summary>
    /// Mulberry32 next float in [0.0, 1.0).
    /// </summary>
    public float NextFloat()
    {
        uint result = NextUInt32();
        return (result & 0x00FFFFFFu) / 16777216.0f;
    }

    public float NextRange(float minInclusive, float maxInclusive) =>
        minInclusive + NextFloat() * (maxInclusive - minInclusive);

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            return minInclusive;
        return minInclusive + (int)(NextFloat() * (maxExclusive - minInclusive));
    }

    public bool NextBool(float trueProbability = 0.5f) =>
        NextFloat() < trueProbability;

    public T Pick<T>(ReadOnlySpan<T> items) =>
        items[NextInt(0, items.Length)];

    public T Pick<T>(T[] items) =>
        items[NextInt(0, items.Length)];

    public T Pick<T>(IReadOnlyList<T> items) =>
        items[NextInt(0, items.Count)];
}
