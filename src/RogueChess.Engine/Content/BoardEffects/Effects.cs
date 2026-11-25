using System;
using System.Collections.Generic;
using RogueChess.Engine.Content.Abilities;
using RogueChess.Engine.Content.Tiles;
using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content.BoardEffects;

/// <summary>
/// Turns up to <c>wallCount</c> random empty squares into walls. Squares with a piece and existing walls are
/// skipped; any other tile can be replaced. The draw uses the game's seeded generator, so it is reproducible and undone.
/// </summary>
public sealed class WallsPlacementEffect : BoardEffect
{
    public const string Cause = "walls-placement";

    public WallsPlacementEffect(int wallCount) => WallCount = wallCount;

    public int WallCount { get; }

    public override string Key => $"WallsPlacement({WallCount})";
    public override string Describe() => $"Places {WallCount} wall(s) on random empty squares.";

    public override void Apply(TriggerContext ctx, int timesFired)
    {
        var board = ctx.Board;
        var candidates = new List<int>();
        for (int sq = 0; sq < board.Area; sq++)
            if (board.PieceAt(sq) == null && board.TileAt(sq).Def != WallTile.Instance) candidates.Add(sq);

        int count = Math.Min(WallCount, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            int j = i + ctx.Random(candidates.Count - i);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            ctx.Propose(Proposal.ChangeTile(candidates[i], WallTile.Instance, Cause));
        }
    }
}

/// <summary>
/// A gust blowing toward rank 1 (y - 1, White's home side). In each file, <c>side</c>'s piece furthest up the
/// board is pushed one square down. If that square is occupied (by either side), neither moves and both are
/// frozen for <c>freezeDuration</c> turns. Pieces on the bottom rank stay put.
/// The direction is fixed, as in the prototype: for White it pushes pieces back, for Black it pushes them forward.
/// </summary>
public sealed class WindGustEffect : BoardEffect
{
    public const string PushCause = "wind-push";
    public const string FreezeCause = "wind-freeze";

    public WindGustEffect(Side side, int freezeDuration = 2)
    {
        Side = side;
        FreezeDuration = freezeDuration;
    }

    public Side Side { get; }
    public int FreezeDuration { get; }

    public override string Key => $"WindGust({Side}, freeze {FreezeDuration})";
    public override string Describe() => $"Pushes {Side}'s furthest piece in each column back one square; a blocked push freezes both pieces for {FreezeDuration} turn(s).";

    public override void Apply(TriggerContext ctx, int timesFired)
    {
        var board = ctx.Board;
        for (int x = 0; x < board.Width; x++)
        {
            Piece? furthest = null;
            for (int y = board.Height - 1; y >= 0 && furthest == null; y--)
                if (board.PieceAt(x, y) is { } p && p.Side == Side) furthest = p;
            if (furthest == null) continue;

            int targetY = board.RankOf(furthest.Square) - 1;
            if (targetY < 0) continue;

            var blocker = board.PieceAt(x, targetY);
            if (blocker == null)
            {
                ctx.Propose(Proposal.Move(furthest, board.Sq(x, targetY), PushCause));
            }
            else
            {
                ctx.Propose(Proposal.AddAbility(furthest, FrozenAbility.Instance, FreezeCause, FreezeDuration));
                ctx.Propose(Proposal.AddAbility(blocker, FrozenAbility.Instance, FreezeCause, FreezeDuration));
            }
        }
    }
}

/// <summary>Which squares a <see cref="CheckerboardWipeEffect"/> targets, in the prototype's convention.</summary>
public enum SquareColor
{
    /// <summary>Squares where x + y is even (a1, c1...; the dark squares of a real chessboard).</summary>
    White,

    /// <summary>Squares where x + y is odd.</summary>
    Black,
}

/// <summary>
/// Freezes every piece (both sides) standing on one colour of square for <c>freezeDuration</c> turns. Pieces that
/// are already frozen are left as they are. With <c>alternating</c>, the colour flips every time it fires,
/// starting with <c>targetColor</c>.
/// </summary>
public sealed class CheckerboardWipeEffect : BoardEffect
{
    public const string Cause = "checkerboard-freeze";

    public CheckerboardWipeEffect(SquareColor targetColor, int freezeDuration = 2, bool alternating = false)
    {
        TargetColor = targetColor;
        FreezeDuration = freezeDuration;
        Alternating = alternating;
    }

    public SquareColor TargetColor { get; }
    public int FreezeDuration { get; }
    public bool Alternating { get; }

    public override string Key => $"CheckerboardWipe({TargetColor}, freeze {FreezeDuration}{(Alternating ? ", alternating" : "")})";
    public override string Describe() => Alternating
        ? $"Freezes every piece on alternating squares (starting with {TargetColor.ToString().ToLowerInvariant()}) for {FreezeDuration} turn(s)."
        : $"Freezes every piece on {TargetColor.ToString().ToLowerInvariant()} squares for {FreezeDuration} turn(s).";

    /// <summary>The colour targeted the n-th time this fires (n = times it fired before).</summary>
    public SquareColor ColorFor(int timesFired)
        => Alternating && timesFired % 2 == 1 ? (TargetColor == SquareColor.White ? SquareColor.Black : SquareColor.White) : TargetColor;

    public override void Apply(TriggerContext ctx, int timesFired)
    {
        var board = ctx.Board;
        int parity = ColorFor(timesFired) == SquareColor.White ? 0 : 1;
        foreach (var p in board.PiecesOnBoard())
        {
            if ((board.FileOf(p.Square) + board.RankOf(p.Square)) % 2 != parity) continue;
            if (p.HasAbility(FrozenAbility.Instance)) continue;
            ctx.Propose(Proposal.AddAbility(p, FrozenAbility.Instance, Cause, FreezeDuration));
        }
    }
}
