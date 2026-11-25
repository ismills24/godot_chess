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
    /// An ability's cast (FireMage, FreezeMage, WallPlacement): the piece stays on <c>From</c> and the ability
    /// acts on <c>To</c>. <c>Aux</c> is the index of the casting ability in the piece's ability list, so a piece
    /// can carry several casts. The ability turns the action into proposals (<see cref="Content.AbilityDef.ProposeAction"/>).
    /// </summary>
    Cast = 64,
}

/// <summary>
/// A candidate action for the side to move. Small value type so move lists don't allocate per move.
/// </summary>
/// <param name="From">Square of the moving piece.</param>
/// <param name="To">Destination square (for castling: the royal piece's destination).</param>
/// <param name="Flags">What kind of move this is.</param>
/// <param name="Aux">En passant: the captured pawn's square. Castling: the partner rook's square. Cast: the casting ability's index on the piece. Otherwise -1.</param>
/// <param name="Promotion">Index into <see cref="Rules.RuleSet.PromotionOptions"/>, or -1.</param>
public readonly record struct Move(int From, int To, MoveFlags Flags = MoveFlags.None, int Aux = -1, sbyte Promotion = -1)
{
    public bool IsCapture => (Flags & (MoveFlags.Capture | MoveFlags.EnPassant)) != 0;
    public bool Is(MoveFlags flag) => (Flags & flag) != 0;
}
