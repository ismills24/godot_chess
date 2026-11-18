using System;
using System.Collections.Generic;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.Content;

[Flags]
public enum PieceTags
{
    None = 0,

    /// <summary>Losing all royal pieces loses the game; standard rules forbid leaving one attacked.</summary>
    Royal = 1,

    /// <summary>Pawn-like: can be captured en passant.</summary>
    Pawn = 2,

    /// <summary>Can castle with an unmoved royal piece (the rook's role).</summary>
    CastlesWithRoyal = 4,
}

/// <summary>
/// Stateless description of a piece type. Shared by every instance; all per-piece state lives on
/// <see cref="Piece"/> inside the <see cref="GameState"/>. Most pieces are pure data; subclass and
/// override the <see cref="Behavior"/> hooks for intrinsic behavior.
/// </summary>
public class PieceDef : Behavior
{
    public PieceDef(string id, string name, char symbol, int value, IReadOnlyList<Movement> movements, PieceTags tags = PieceTags.None)
    {
        Id = id;
        Name = name;
        Symbol = char.ToLowerInvariant(symbol);
        Value = value;
        Movements = movements;
        Tags = tags;
        HashId = Zobrist.StableHash(id);
    }

    /// <summary>Stable unique id. Also feeds the position hash, so renaming it changes hashes.</summary>
    public string Id { get; }
    public string Name { get; }

    /// <summary>Lower-case letter used in FEN and move strings.</summary>
    public char Symbol { get; }

    /// <summary>Material value for the AI. Royal pieces are scored by the win rules instead.</summary>
    public int Value { get; }

    public IReadOnlyList<Movement> Movements { get; }
    public PieceTags Tags { get; }

    public bool IsRoyal => (Tags & PieceTags.Royal) != 0;
    public bool IsPawn => (Tags & PieceTags.Pawn) != 0;
    public bool CastlesWithRoyal => (Tags & PieceTags.CastlesWithRoyal) != 0;

    internal ulong HashId { get; }

    /// <summary>Whether "has moved" changes this piece's rules (castling), so it must be part of the hash.</summary>
    internal bool HashesMovedFlag => (Tags & (PieceTags.Royal | PieceTags.CastlesWithRoyal)) != 0;

    public override string ToString() => Id;
}
