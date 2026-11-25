using System;
using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content.Tiles;

/// <summary>
/// Slippery: a piece that moves onto this tile slides one more square in the direction it was moving, in the
/// next wave. Any move counts (the player's move, a push, another slide).
///
/// <para>Direction (B6): the unit step of the move, i.e. the sign of each axis for straight and diagonal moves
/// (sliders, king, pawn). For a move that is neither (a knight's L), it is the unit step along the
/// <b>longer leg</b>: a knight going two up and one right slides one more square up. The prototype took the
/// sign of both axes, which sent knights off diagonally.</para>
///
/// <para>Blocked slides, as in the prototype: if the next square is off the board or occupied, the piece simply
/// stays on the slippery tile. A slide never captures. Unlike the prototype it also respects tiles: it doesn't
/// enter a square that blocks landing or a wall (walls stop sliding pieces). A slide that lands on another
/// slippery tile slides again, in the same direction, so a row of ice carries a piece until something stops it.
/// A slide onto an ally's <see cref="TombTile"/> opens the tomb like any other move.</para>
///
/// Frozen pieces still slide: being frozen stops a piece from acting, not from being moved (as with WindGust).
/// </summary>
public sealed class SlipperyTile : TileDef
{
    public const string Cause = "slippery-slide";

    public static readonly SlipperyTile Instance = new();

    private SlipperyTile() : base("slippery", "Slippery") { }

    public override FactKinds Triggers => FactKinds.Moved;

    /// <summary>The one-square step a move from <paramref name="from"/> to <paramref name="to"/> continues with.</summary>
    public static (int Dx, int Dy) SlideStep(IBoardView board, int from, int to)
    {
        int dx = board.FileOf(to) - board.FileOf(from), dy = board.RankOf(to) - board.RankOf(from);
        int ax = Math.Abs(dx), ay = Math.Abs(dy);
        if (dx == 0 || dy == 0 || ax == ay) return (Math.Sign(dx), Math.Sign(dy));
        return ax > ay ? (Math.Sign(dx), 0) : (0, Math.Sign(dy));
    }

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        int sq = ctx.SelfSquare;
        var piece = fact.Piece;
        if (fact.To != sq || piece == null || piece.Square != sq) return;

        var board = ctx.Board;
        var (dx, dy) = SlideStep(board, fact.From, fact.To);
        int x = board.FileOf(sq) + dx, y = board.RankOf(sq) + dy;
        if ((dx == 0 && dy == 0) || !board.InBounds(x, y)) return;

        int next = board.Sq(x, y);
        var tile = board.TileAt(next).Def;
        if (board.PieceAt(next) != null || tile.BlocksLanding || tile.BlocksPassage) return;

        ctx.Propose(Proposal.Move(piece, next, Cause));
    }
}
