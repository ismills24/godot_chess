namespace RogueChess.Engine.Core;

/// <summary>
/// Position hashing. Keys are derived on demand by mixing (category, def, side, square), so there are no
/// tables to size: any board size and any number of content defs work. Def identity comes from a stable
/// hash of the def's string id, so hashes are identical across runs and machines.
/// </summary>
internal static class Zobrist
{
    public const ulong PieceCategory = 1;
    public const ulong MovedCategory = 2;
    public const ulong TileCategory = 3;
    public const ulong SideCategory = 4;
    public const ulong EnPassantCategory = 5;

    /// <summary>SplitMix64 finalizer.</summary>
    public static ulong Mix(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public static ulong Key(ulong category, ulong a, ulong b = 0, ulong c = 0)
        => Mix(Mix(Mix(Mix(category) ^ a) ^ b) ^ c);

    /// <summary>FNV-1a over the string, then mixed. Stable across processes (unlike string.GetHashCode).</summary>
    public static ulong StableHash(string text)
    {
        ulong h = 14695981039346656037UL;
        foreach (char ch in text)
        {
            h ^= ch;
            h *= 1099511628211UL;
        }
        return Mix(h);
    }
}
