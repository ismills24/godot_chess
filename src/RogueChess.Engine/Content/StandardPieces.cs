using System.Collections.Generic;

namespace RogueChess.Engine.Content;

/// <summary>The six orthodox chess pieces, built from movement primitives.</summary>
public static class StandardPieces
{
    public static readonly PieceDef Pawn = new("pawn", "Pawn", 'p', 1,
        new Movement[] { new PawnMovement() }, PieceTags.Pawn);

    public static readonly PieceDef Knight = new("knight", "Knight", 'n', 3,
        new Movement[] { new Leap(Leap.KnightOffsets) });

    public static readonly PieceDef Bishop = new("bishop", "Bishop", 'b', 3,
        new Movement[] { new Slide(Slide.Diagonal) });

    public static readonly PieceDef Rook = new("rook", "Rook", 'r', 5,
        new Movement[] { new Slide(Slide.Orthogonal) }, PieceTags.CastlesWithRoyal);

    public static readonly PieceDef Queen = new("queen", "Queen", 'q', 9,
        new Movement[] { new Slide(Slide.AllDirections) });

    public static readonly PieceDef King = new("king", "King", 'k', 0,
        new Movement[] { new Leap(Leap.KingOffsets) }, PieceTags.Royal);

    public static IReadOnlyList<PieceDef> All { get; } = new[] { Pawn, Knight, Bishop, Rook, Queen, King };

    /// <summary>Def for a FEN letter (either case), or null.</summary>
    public static PieceDef? FromSymbol(char symbol)
    {
        char lower = char.ToLowerInvariant(symbol);
        foreach (var def in All)
            if (def.Symbol == lower) return def;
        return null;
    }
}
