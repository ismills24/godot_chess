using RogueChess.Engine.Core;

namespace RogueChess.Engine.Content;

/// <summary>
/// Description of a tile type. Movement restrictions are flags that move generation reads directly.
/// Behavior (fire spreading, guardian protection...) comes from overriding the <see cref="Behavior"/> hooks.
/// Per-tile state goes in variables (slots 0..VarCount-1).
/// </summary>
public class TileDef : Behavior
{
    public static readonly TileDef Standard = new("standard", "Standard");

    public TileDef(string id, string name, bool blocksPassage = false, bool blocksLanding = false, int varCount = 0,
        bool blocksCapture = false)
    {
        Id = id;
        Name = name;
        BlocksPassage = blocksPassage;
        BlocksLanding = blocksLanding;
        BlocksCapture = blocksCapture;
        VarCount = varCount;
        HashId = Zobrist.StableHash(id);
    }

    public string Id { get; }
    public string Name { get; }

    /// <summary>Sliding pieces stop on this square: they may land here but not continue past (e.g. Wall).</summary>
    public bool BlocksPassage { get; }

    /// <summary>No piece may end a move here, and a piece standing here can't be captured by a move.</summary>
    public bool BlocksLanding { get; }

    /// <summary>
    /// A piece standing here can't be captured by a move (Fog). The square can still be entered when empty,
    /// and non-move destruction (explosions) still works.
    /// </summary>
    public bool BlocksCapture { get; }

    public int VarCount { get; }

    public bool IsDefault => ReferenceEquals(this, Standard);

    /// <summary>Variables a new tile starts with when none are given (e.g. a fire timer). Default: all zero.</summary>
    public virtual int[] InitialVars() => System.Array.Empty<int>();

    internal ulong HashId { get; }

    public override string ToString() => Id;
}
