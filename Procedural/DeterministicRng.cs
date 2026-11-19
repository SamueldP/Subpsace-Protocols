namespace SubspaceProtocols.Procedural;

/// <summary>
/// FNV-1a string seed hasher and Mulberry32 deterministic PRNG
/// defined in /design/08_Physical_Look/Procedural_Art_Bible_and_Asset_Spec.md (Block 3).
/// </summary>
public sealed class DeterministicRng
{
    private uint _state;

    public DeterministicRng(string seedString)
    {
        _state = HashSeedFnv1a(seedString);
    }

    public DeterministicRng(uint numericSeed)
    {
        _state = numericSeed == 0 ? 0x6D2B79F5u : numericSeed;
    }

    public static uint HashSeedFnv1a(string seed)
    {
        if (string.IsNullOrEmpty(seed))
            return 2166136261u;

        uint hash = 2166136261u;
        foreach (char c in seed)
        {
            hash ^= c;
            hash *= 16777619u;
        }
        return hash == 0 ? 1u : hash;
    }

    /// <summary>
    /// Mulberry32 next float in [0, 1).
    /// </summary>
    public float NextFloat()
    {
        uint t = _state += 0x6D2B79F5u;
        t = (t ^ (t >> 15)) * (t | 1u);
        t ^= t + (t ^ (t >> 7)) * (t | 61u);
        uint result = t ^ (t >> 14);
        return (result & 0x00FFFFFFu) / 16777216.0f;
    }

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
}
