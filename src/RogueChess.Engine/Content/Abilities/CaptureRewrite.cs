using System;
using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content.Abilities;

/// <summary>Shared helpers for abilities that rewrite their own piece's capture (Piercing, Bouncer).</summary>
internal static class CaptureRewrite
{
    /// <summary>
    /// Find the capture the piece's own action proposes in this batch: <c>Destroy(target, capture)</c> from this
    /// piece plus its <c>Move(piece → target's square)</c>. Only normal captures made as the player's action
    /// count (not en passant, not captures some other effect causes).
    /// </summary>
    public static bool FindOwnCapture(ModifierContext ctx, out int destroyIndex, out int moveIndex, out Piece target)
    {
        var self = ctx.SelfPiece!;
        var batch = ctx.Batch;
        for (int i = 0; i < batch.Count; i++)
        {
            var p = batch[i];
            if (p.Kind != ProposalKind.Destroy || p.Cause != Causes.Capture || p.Source.Piece != self) continue;
            var victim = ctx.Board.GetPiece(p.PieceId);
            if (victim == null || !victim.IsOnBoard) continue;
            for (int j = 0; j < batch.Count; j++)
            {
                var m = batch[j];
                if (m.Kind == ProposalKind.Move && m.PieceId == self.Id && m.Square == victim.Square && m.Cause == Causes.Action)
                {
                    destroyIndex = i;
                    moveIndex = j;
                    target = victim;
                    return true;
                }
            }
        }
        destroyIndex = moveIndex = -1;
        target = null!;
        return false;
    }

    /// <summary>
    /// The square one step past <paramref name="target"/>, going from <paramref name="from"/> towards it: the sign
    /// of the attack vector on each axis, as in the prototype (so after a knight's L it is a diagonal step).
    /// -1 when that square is off the board.
    /// </summary>
    public static int SquareBehind(IBoardView board, int from, int target)
    {
        int dx = Math.Sign(board.FileOf(target) - board.FileOf(from));
        int dy = Math.Sign(board.RankOf(target) - board.RankOf(from));
        int x = board.FileOf(target) + dx, y = board.RankOf(target) + dy;
        return board.InBounds(x, y) ? board.Sq(x, y) : -1;
    }
}
