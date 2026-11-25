using System;

namespace RogueChess.Engine.Core;

[Flags]
public enum MoveFlags : byte
{
    None = 0,
    Capture = 1,
    DoublePush = 2,
    EnPassant = 4,
    Castle = 8,
    Promotion = 16,

    /// <summary>
    /// An action supplied by one of the moving piece's abilities instead of (or besides) a plain move, e.g. a
    /// Marksman's ranged shot. <see cref="Move.Aux"/> is that ability's index on the piece, and the ability's
    /// <see cref="Content.AbilityDef.ProposeAction"/> turns the action into wave-0 proposals. Combine with
    /// <see cref="Capture"/> when the action takes the piece on <see cref="Move.To"/>, so ordering and
    /// quiescence treat it as a capture.
    /// </summary>
    Ability = 32,
}

/// <summary>
/// A candidate action for the side to move. Small value type so move lists don't allocate per move.
/// </summary>
/// <param name="From">Square of the moving piece.</param>
/// <param name="To">Destination square (for castling: the royal piece's destination).</param>
/// <param name="Flags">What kind of move this is.</param>
/// <param name="Aux">En passant: the captured pawn's square. Castling: the partner rook's square.
/// Ability action: the index of the ability on the moving piece. Otherwise -1.</param>
/// <param name="Promotion">Index into <see cref="Rules.RuleSet.PromotionOptions"/>, or -1.</param>
public readonly record struct Move(int From, int To, MoveFlags Flags = MoveFlags.None, int Aux = -1, sbyte Promotion = -1)
{
    public bool IsCapture => (Flags & (MoveFlags.Capture | MoveFlags.EnPassant)) != 0;
    public bool Is(MoveFlags flag) => (Flags & flag) != 0;
}
