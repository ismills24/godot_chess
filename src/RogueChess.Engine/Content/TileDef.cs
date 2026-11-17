using RogueChess.Engine.Core;

namespace RogueChess.Engine.Content;

/// <summary>
/// Stateless description of a tile type. Movement restrictions are flags that move generation reads
/// directly, so tiles need no code to block or allow movement.
/// </summary>
public sealed class TileDef
{
    public static readonly TileDef Standard = new("standard", "Standard");

    public TileDef(string id, string name, bool blocksPassage = false, bool blocksLanding = false)
    {
        Id = id;
        Name = name;
        BlocksPassage = blocksPassage;
        BlocksLanding = blocksLanding;
        HashId = Zobrist.StableHash(id);
    }

    public string Id { get; }
    public string Name { get; }

    /// <summary>Sliding pieces stop on this square: they may land here but not continue past (e.g. Wall).</summary>
    public bool BlocksPassage { get; }

    /// <summary>No piece may end a move here, and a piece standing here can't be captured by a move.</summary>
    public bool BlocksLanding { get; }

    public bool IsDefault => ReferenceEquals(this, Standard);

    internal ulong HashId { get; }

    public override string ToString() => Id;
}
