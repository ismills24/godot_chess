using System.Collections.Generic;
using RogueChess.Engine.Content;
using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Rules;

public static class MoveGenerator
{
    /// <summary>
    /// Append every move the side to move's pieces can make, ignoring self-check: each def's movement
    /// primitives, then each ability's <see cref="Content.AbilityDef.ModifyMoves"/>, then castling.
    /// </summary>
    public static void GeneratePseudoLegal(GameState s, List<Move> moves)
    {
        var side = s.SideToMove;
        for (int sq = 0; sq < s.Area; sq++)
        {
            var p = s.PieceAt(sq);
            if (p == null || p.Side != side || p.IsImmobile) continue;
            int first = moves.Count;
            foreach (var movement in p.Def.Movements)
                movement.Generate(s, p, moves);
            foreach (var ability in p.Abilities)
                ability.Def.ModifyMoves(s, p, ability, moves, first);
            if (s.Rules.Castling && p.Def.IsRoyal && p.MovesMade == 0)
                GenerateCastling(s, p, moves);
        }
        GenerateConsumables(s, moves);
    }

    [System.ThreadStatic] private static List<int>? _targets;

    /// <summary>One <see cref="Move.UseConsumable"/> per useful target of each consumable the side to move holds a charge of.</summary>
    private static void GenerateConsumables(GameState s, List<Move> moves)
    {
        var globals = s.Globals;
        for (int i = 0; i < globals.Count; i++)
        {
            if (globals[i].Def is not ConsumableDef def || globals[i].Var(ConsumableDef.ChargesVar(s.SideToMove)) <= 0) continue;
            var targets = _targets ??= new List<int>();
            targets.Clear();
            def.GenerateTargets(s, s.SideToMove, targets);
            foreach (int t in targets) moves.Add(Move.UseConsumable(i, t));
        }
    }

    /// <summary>
    /// Append the legal moves for the side to move. Under <see cref="RuleSet.ForbidSelfCheck"/> each move is
    /// resolved through the full resolver (so abilities and tiles count) and kept only if it is accepted and
    /// leaves no royal piece attacked. Without that rule this is the pseudo-legal list: a move rejected by the
    /// resolver's watchdog is reported by <see cref="Game.PlayTurn"/> instead.
    /// </summary>
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
            bool ok = Game.ResolveAction(s, m, default, null, out _) && !IsInCheck(s, mover);
            s.UndoTo(mark);
            if (ok) moves[write++] = m;
        }
        moves.RemoveRange(write, moves.Count - write);
    }

    /// <summary>Whether any piece of <paramref name="by"/> could capture on <paramref name="target"/>.</summary>
    public static bool IsAttacked(IBoardView s, int target, Side by)
    {
        for (int sq = 0; sq < s.Area; sq++)
        {
            var p = s.PieceAt(sq);
            if (p == null || p.Side != by || p.IsImmobile) continue;
            foreach (var movement in p.Def.Movements)
                if (movement.Attacks(s, p, target)) return true;
        }
        return false;
    }

    /// <summary>Whether any royal piece of <paramref name="side"/> is attacked.</summary>
    public static bool IsInCheck(IBoardView s, Side side)
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
