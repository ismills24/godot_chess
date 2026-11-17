using System;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.Rules;

/// <summary>
/// Applies a move directly through <see cref="GameState"/> operations and passes the turn.
/// This is the plain-chess core; from M2 the event resolver wraps it so pieces, tiles and effects
/// can react. Undo with <see cref="GameState.UndoTo"/> using the depth taken before calling.
/// </summary>
public static class MoveApplier
{
    public static void Apply(GameState s, Move m)
    {
        var mover = s.PieceAt(m.From)
            ?? throw new InvalidOperationException($"No piece on square {m.From} for move {m}.");

        if (m.Is(MoveFlags.EnPassant))
        {
            s.RemovePiece(s.PieceAt(m.Aux)!);
        }
        else if (m.Is(MoveFlags.Capture))
        {
            s.RemovePiece(s.PieceAt(m.To)!);
        }

        if (m.Is(MoveFlags.Castle))
        {
            var partner = s.PieceAt(m.Aux)!;
            int dir = Math.Sign(m.To - m.From);
            s.MovePiece(mover, m.To);
            s.MovePiece(partner, m.From + dir);
            s.SetMovesMade(partner, partner.MovesMade + 1);
        }
        else
        {
            s.MovePiece(mover, m.To);
        }

        s.SetMovesMade(mover, mover.MovesMade + 1);

        if (m.Is(MoveFlags.Promotion))
            s.SetDef(mover, s.Rules.PromotionOptions[m.Promotion]);

        int enPassant = m.Is(MoveFlags.DoublePush) && s.Rules.EnPassant ? (m.From + m.To) / 2 : -1;
        if (enPassant != s.EnPassantSquare) s.SetEnPassant(enPassant);

        s.SetTurn(s.SideToMove.Opponent(), s.Ply + 1);
    }
}
