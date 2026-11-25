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
    /// Use a consumable instead of moving a piece (see <see cref="Move.UseConsumable"/>). A free action: the
    /// same side moves again afterwards.
    /// </summary>
    UseConsumable = 128,
}

/// <summary>
/// A candidate action for the side to move. Small value type so move lists don't allocate per move.
/// </summary>
/// <param name="From">Square of the moving piece (-1 for a consumable use).</param>
/// <param name="To">Destination square (for castling: the royal piece's destination; for a consumable: its target square, or -1).</param>
/// <param name="Flags">What kind of move this is.</param>
/// <param name="Aux">En passant: the captured pawn's square. Castling: the partner rook's square. Consumable: index of its
/// global in <see cref="GameState.Globals"/>. Otherwise -1.</param>
/// <param name="Promotion">Index into <see cref="Rules.RuleSet.PromotionOptions"/>, or -1.</param>
public readonly record struct Move(int From, int To, MoveFlags Flags = MoveFlags.None, int Aux = -1, sbyte Promotion = -1)
{
    public bool IsCapture => (Flags & (MoveFlags.Capture | MoveFlags.EnPassant)) != 0;
    public bool Is(MoveFlags flag) => (Flags & flag) != 0;

    /// <summary>True for a consumable use (no piece moves; <see cref="From"/> is -1).</summary>
    public bool IsConsumable => (Flags & MoveFlags.UseConsumable) != 0;

    /// <summary>Use the consumable held in global <paramref name="globalIndex"/>, aimed at <paramref name="target"/> (-1 if untargeted).</summary>
    public static Move UseConsumable(int globalIndex, int target = -1)
        => new(-1, target, MoveFlags.UseConsumable, Aux: globalIndex);
}

/// <summary>One entry of <see cref="GameState.History"/>: who played what, at which ply.</summary>
public readonly record struct PlayedAction(Side Side, int Ply, Move Move);
