using System;
using System.Collections.Generic;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.Content;

/// <summary>
/// A movement primitive a <see cref="PieceDef"/> is built from. Most pieces need only the built-in
/// <see cref="Slide"/>, <see cref="Leap"/> and <see cref="PawnMovement"/>; subclass this for anything else.
/// </summary>
public abstract class Movement
{
    /// <summary>Append pseudo-legal moves for <paramref name="piece"/> (owned by the side to move).</summary>
    public abstract void Generate(IBoardView state, Piece piece, List<Move> moves);

    /// <summary>Whether <paramref name="piece"/> could capture something standing on <paramref name="target"/>.</summary>
    public abstract bool Attacks(IBoardView state, Piece piece, int target);

    protected static bool CanLandOn(IBoardView state, int sq) => !state.TileAt(sq).Def.BlocksLanding;

    /// <summary>Whether a piece standing on <paramref name="sq"/> can be captured by a move.</summary>
    protected static bool CanCaptureOn(IBoardView state, int sq)
    {
        var def = state.TileAt(sq).Def;
        return !def.BlocksLanding && !def.BlocksCapture;
    }
}

/// <summary>Moves any number of squares (up to <see cref="MaxRange"/>, 0 = unlimited) along each direction.</summary>
public sealed class Slide : Movement
{
    public static readonly (int Dx, int Dy)[] Orthogonal = { (1, 0), (-1, 0), (0, 1), (0, -1) };
    public static readonly (int Dx, int Dy)[] Diagonal = { (1, 1), (1, -1), (-1, 1), (-1, -1) };
    public static readonly (int Dx, int Dy)[] AllDirections = { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };

    private readonly (int Dx, int Dy)[] _dirs;

    public Slide((int Dx, int Dy)[] directions, int maxRange = 0)
    {
        _dirs = directions;
        MaxRange = maxRange;
    }

    public int MaxRange { get; }
    public IReadOnlyList<(int Dx, int Dy)> Directions => _dirs;

    public override void Generate(IBoardView s, Piece p, List<Move> moves)
    {
        int px = s.FileOf(p.Square), py = s.RankOf(p.Square);
        foreach (var (dx, dy) in _dirs)
        {
            int x = px, y = py;
            for (int r = 1; MaxRange == 0 || r <= MaxRange; r++)
            {
                x += dx;
                y += dy;
                if (!s.InBounds(x, y)) break;
                int sq = s.Sq(x, y);
                var tile = s.TileAt(sq).Def;
                var occupant = s.PieceAt(sq);
                if (occupant != null)
                {
                    if (occupant.Side != p.Side && CanCaptureOn(s, sq))
                        moves.Add(new Move(p.Square, sq, MoveFlags.Capture));
                    break;
                }
                if (!tile.BlocksLanding) moves.Add(new Move(p.Square, sq));
                if (tile.BlocksPassage) break;
            }
        }
    }

    public override bool Attacks(IBoardView s, Piece p, int target)
    {
        if (!CanCaptureOn(s, target)) return false;
        int px = s.FileOf(p.Square), py = s.RankOf(p.Square);
        int dx = s.FileOf(target) - px, dy = s.RankOf(target) - py;
        foreach (var (ddx, ddy) in _dirs)
        {
            int k = ddx != 0 ? dx / ddx : (ddy != 0 ? dy / ddy : 0);
            if (k <= 0 || k * ddx != dx || k * ddy != dy) continue;
            if (MaxRange > 0 && k > MaxRange) continue;

            bool blocked = false;
            for (int i = 1; i < k && !blocked; i++)
            {
                int sq = s.Sq(px + i * ddx, py + i * ddy);
                blocked = s.PieceAt(sq) != null || s.TileAt(sq).Def.BlocksPassage;
            }
            if (!blocked) return true;
        }
        return false;
    }
}

/// <summary>Jumps directly to fixed offsets, ignoring anything in between.</summary>
public sealed class Leap : Movement
{
    public static readonly (int Dx, int Dy)[] KnightOffsets = { (1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2) };
    public static readonly (int Dx, int Dy)[] KingOffsets = { (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1) };

    private readonly (int Dx, int Dy)[] _offsets;

    public Leap((int Dx, int Dy)[] offsets) => _offsets = offsets;

    public IReadOnlyList<(int Dx, int Dy)> Offsets => _offsets;

    public override void Generate(IBoardView s, Piece p, List<Move> moves)
    {
        int px = s.FileOf(p.Square), py = s.RankOf(p.Square);
        foreach (var (dx, dy) in _offsets)
        {
            int x = px + dx, y = py + dy;
            if (!s.InBounds(x, y)) continue;
            int sq = s.Sq(x, y);
            if (!CanLandOn(s, sq)) continue;
            var occupant = s.PieceAt(sq);
            var jump = Math.Max(Math.Abs(dx), Math.Abs(dy)) > 1 ? MoveFlags.Jump : MoveFlags.None;
            if (occupant == null) moves.Add(new Move(p.Square, sq, jump));
            else if (occupant.Side != p.Side && CanCaptureOn(s, sq)) moves.Add(new Move(p.Square, sq, MoveFlags.Capture | jump));
        }
    }

    public override bool Attacks(IBoardView s, Piece p, int target)
    {
        if (!CanCaptureOn(s, target)) return false;
        int dx = s.FileOf(target) - s.FileOf(p.Square), dy = s.RankOf(target) - s.RankOf(p.Square);
        foreach (var (ox, oy) in _offsets)
            if (ox == dx && oy == dy) return true;
        return false;
    }
}

/// <summary>
/// Pawn movement: one step forward (two from the second rank), diagonal captures, en passant, and
/// promotion on the far rank using the ruleset's promotion options.
/// </summary>
public sealed class PawnMovement : Movement
{
    public PawnMovement(bool doubleStep = true) => DoubleStep = doubleStep;

    public bool DoubleStep { get; }

    public override void Generate(IBoardView s, Piece p, List<Move> moves)
    {
        int dir = p.Side.Forward();
        int px = s.FileOf(p.Square), py = s.RankOf(p.Square);
        int startRank = p.Side == Side.White ? 1 : s.Height - 2;

        if (s.InBounds(px, py + dir))
        {
            int one = s.Sq(px, py + dir);
            if (s.PieceAt(one) == null && Passable(s, one))
            {
                AddPawnMove(s, p, one, MoveFlags.None, moves);
                if (DoubleStep && py == startRank && s.InBounds(px, py + 2 * dir))
                {
                    int two = s.Sq(px, py + 2 * dir);
                    if (s.PieceAt(two) == null && Passable(s, two))
                        AddPawnMove(s, p, two, MoveFlags.DoublePush, moves);
                }
            }
        }

        for (int cx = -1; cx <= 1; cx += 2)
        {
            int x = px + cx, y = py + dir;
            if (!s.InBounds(x, y)) continue;
            int sq = s.Sq(x, y);
            if (!CanLandOn(s, sq)) continue;
            var occupant = s.PieceAt(sq);
            if (occupant != null)
            {
                if (occupant.Side != p.Side && CanCaptureOn(s, sq)) AddPawnMove(s, p, sq, MoveFlags.Capture, moves);
            }
            else if (s.Rules.EnPassant && sq == s.EnPassantSquare)
            {
                int victimSq = s.Sq(x, py);
                var victim = s.PieceAt(victimSq);
                if (victim != null && victim.Side != p.Side && victim.Def.IsPawn && CanCaptureOn(s, victimSq))
                    moves.Add(new Move(p.Square, sq, MoveFlags.EnPassant, Aux: victimSq));
            }
        }
    }

    public override bool Attacks(IBoardView s, Piece p, int target)
    {
        if (!CanCaptureOn(s, target)) return false;
        int dx = s.FileOf(target) - s.FileOf(p.Square), dy = s.RankOf(target) - s.RankOf(p.Square);
        return dy == p.Side.Forward() && (dx == 1 || dx == -1);
    }

    private static bool Passable(IBoardView s, int sq)
    {
        var def = s.TileAt(sq).Def;
        return !def.BlocksLanding && !def.BlocksPassage;
    }

    private static void AddPawnMove(IBoardView s, Piece p, int to, MoveFlags flags, List<Move> moves)
    {
        int lastRank = p.Side == Side.White ? s.Height - 1 : 0;
        var options = s.Rules.PromotionOptions;
        if (s.RankOf(to) == lastRank && options.Count > 0)
        {
            for (int i = 0; i < options.Count; i++)
                moves.Add(new Move(p.Square, to, flags | MoveFlags.Promotion, Promotion: (sbyte)i));
        }
        else
        {
            moves.Add(new Move(p.Square, to, flags));
        }
    }
}
