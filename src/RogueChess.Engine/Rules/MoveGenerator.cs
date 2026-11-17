using System.Collections.Generic;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.Rules;

public static class MoveGenerator
{
    /// <summary>Append every move the side to move's pieces can make, ignoring self-check.</summary>
    public static void GeneratePseudoLegal(GameState s, List<Move> moves)
    {
        var side = s.SideToMove;
        for (int sq = 0; sq < s.Area; sq++)
        {
            var p = s.PieceAt(sq);
            if (p == null || p.Side != side) continue;
            foreach (var movement in p.Def.Movements)
                movement.Generate(s, p, moves);
            if (s.Rules.Castling && p.Def.IsRoyal && p.MovesMade == 0)
                GenerateCastling(s, p, moves);
        }
    }

    /// <summary>Append the legal moves for the side to move under <see cref="RuleSet.ForbidSelfCheck"/>.</summary>
    public static void GenerateLegal(GameState s, List<Move> moves)
    {
        int start = moves.Count;
        GeneratePseudoLegal(s, moves);
        if (!s.Rules.ForbidSelfCheck) return;

        var mover = s.SideToMove;
        int write = start;
        for (int i = start; i < moves.Count; i++)
        {
            var m = moves[i];
            int mark = s.UndoDepth;
            MoveApplier.Apply(s, m);
            bool illegal = IsInCheck(s, mover);
            s.UndoTo(mark);
            if (!illegal) moves[write++] = m;
        }
        moves.RemoveRange(write, moves.Count - write);
    }

    /// <summary>Whether any piece of <paramref name="by"/> could capture on <paramref name="target"/>.</summary>
    public static bool IsAttacked(GameState s, int target, Side by)
    {
        for (int sq = 0; sq < s.Area; sq++)
        {
            var p = s.PieceAt(sq);
            if (p == null || p.Side != by) continue;
            foreach (var movement in p.Def.Movements)
                if (movement.Attacks(s, p, target)) return true;
        }
        return false;
    }

    /// <summary>Whether any royal piece of <paramref name="side"/> is attacked.</summary>
    public static bool IsInCheck(GameState s, Side side)
    {
        for (int sq = 0; sq < s.Area; sq++)
        {
            var p = s.PieceAt(sq);
            if (p != null && p.Side == side && p.Def.IsRoyal && IsAttacked(s, sq, side.Opponent()))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Castling generalised to any board width: an unmoved royal piece and the nearest unmoved castling
    /// partner on its rank, at least 3 squares apart with only empty, unobstructed squares between.
    /// The royal piece moves two squares toward the partner, which lands on the square it crossed.
    /// The royal piece may not start in, pass through, or land on an attacked square.
    /// </summary>
    private static void GenerateCastling(GameState s, Piece king, List<Move> moves)
    {
        var enemy = king.Side.Opponent();
        int kx = s.FileOf(king.Square), ky = s.RankOf(king.Square);
        bool? inCheck = null;

        for (int dir = -1; dir <= 1; dir += 2)
        {
            Piece? partner = null;
            int dist = 1;
            for (int x = kx + dir; s.InBounds(x, ky); x += dir, dist++)
            {
                int sq = s.Sq(x, ky);
                partner = s.PieceAt(sq);
                if (partner != null) break;
                var tile = s.TileAt(sq).Def;
                if (tile.BlocksPassage || tile.BlocksLanding) break;
            }

            if (partner == null || partner.Side != king.Side || !partner.Def.CastlesWithRoyal
                || partner.MovesMade != 0 || dist < 3)
                continue;

            inCheck ??= IsAttacked(s, king.Square, enemy);
            if (inCheck.Value) return;

            int crossed = s.Sq(kx + dir, ky), kingTo = s.Sq(kx + 2 * dir, ky);
            if (IsAttacked(s, crossed, enemy) || IsAttacked(s, kingTo, enemy)) continue;

            moves.Add(new Move(king.Square, kingTo, MoveFlags.Castle, Aux: partner.Square));
        }
    }
}
