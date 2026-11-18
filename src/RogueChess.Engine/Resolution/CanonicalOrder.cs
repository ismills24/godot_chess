using System;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.Resolution;

/// <summary>
/// The engine's single "who wins a tie" rule (RESOLUTION_RULES.md §8):
/// source category, then distance to the contested square, then bearing clockwise from north, then id.
/// </summary>
internal static class CanonicalOrder
{
    /// <summary>Compare two sources contesting the same square (or -1 when there is no square).</summary>
    public static int Compare(in SourceRef a, in SourceRef b, int contested, IBoardView board)
        => Compare(a, contested, b, contested, board);

    /// <summary>
    /// Compare two sources, each measured from its own contested square. When both squares are the same this is
    /// the plain canonical order; otherwise it is still a consistent total order (used to rank modifier claims).
    /// </summary>
    public static int Compare(in SourceRef a, int contestedA, in SourceRef b, int contestedB, IBoardView board)
    {
        int c = a.Category.CompareTo(b.Category);
        if (c != 0) return c;

        bool spatial = contestedA >= 0 && contestedB >= 0 && a.Square >= 0 && b.Square >= 0;
        if (spatial)
        {
            c = Distance(board, a.Square, contestedA).CompareTo(Distance(board, b.Square, contestedB));
            if (c != 0) return c;
            c = CompareBearing(
                board.FileOf(a.Square) - board.FileOf(contestedA), board.RankOf(a.Square) - board.RankOf(contestedA),
                board.FileOf(b.Square) - board.FileOf(contestedB), board.RankOf(b.Square) - board.RankOf(contestedB));
            if (c != 0) return c;
        }
        else
        {
            c = a.Square.CompareTo(b.Square);
            if (c != 0) return c;
        }

        c = (a.Piece?.Id ?? 0).CompareTo(b.Piece?.Id ?? 0);
        if (c != 0) return c;
        c = a.AbilityIndex.CompareTo(b.AbilityIndex);
        if (c != 0) return c;
        return a.GlobalIndex.CompareTo(b.GlobalIndex);
    }

    /// <summary>Chebyshev (king-move) distance.</summary>
    public static int Distance(IBoardView board, int a, int b)
        => Math.Max(Math.Abs(board.FileOf(a) - board.FileOf(b)), Math.Abs(board.RankOf(a) - board.RankOf(b)));

    /// <summary>
    /// Order two offsets by bearing, clockwise starting at north (towards higher ranks): N, NE, E, SE, S, SW, W, NW.
    /// Exact integer arithmetic. (0,0) sorts first.
    /// </summary>
    public static int CompareBearing(long dxA, long dyA, long dxB, long dyB)
    {
        // The counter-clockwise angle of q = (dy, dx) from +x equals the clockwise bearing of (dx, dy) from north.
        long ax = dyA, ay = dxA, bx = dyB, by = dxB;
        int ha = Half(ax, ay), hb = Half(bx, by);
        if (ha != hb) return ha.CompareTo(hb);
        long cross = ax * by - ay * bx;
        return cross > 0 ? -1 : cross < 0 ? 1 : 0;
    }

    private static int Half(long x, long y) => y < 0 || (y == 0 && x < 0) ? 1 : 0;
}
