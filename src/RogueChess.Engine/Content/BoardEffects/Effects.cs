using System;
using System.Collections.Generic;
using System.Linq;
using RogueChess.Engine.Content.Abilities;
using RogueChess.Engine.Content.Tiles;
using RogueChess.Engine.Core;
using RogueChess.Engine.Encounters;
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
                ctx.Propose(FrozenAbility.FreezeAtTurnEnd(board, furthest, FreezeDuration, FreezeCause));
                ctx.Propose(FrozenAbility.FreezeAtTurnEnd(board, blocker, FreezeDuration, FreezeCause));
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

/// <summary>The edge a <see cref="TidalWaveEffect"/> comes from, which is also the way it drags pieces.</summary>
public enum WaveDirection
{
    /// <summary>Toward the highest rank (Black's home side).</summary>
    North,

    /// <summary>Toward rank 1 (White's home side).</summary>
    South,

    /// <summary>Toward the last file (h on an 8-wide board).</summary>
    East,

    /// <summary>Toward the a-file.</summary>
    West,
}

/// <summary>
/// A tidal wave rolling in from one edge: as it recedes it drags every piece (both sides) standing on the affected
/// squares one square toward that edge, <c>strength</c> times (one wave per step, see <see cref="BoardEffect.Steps"/>).
/// A piece pulled past the edge is washed out and destroyed; nobody is credited. Walls hold pieces back (no move is
/// proposed into a square that blocks passage or landing), and the resolver's sliding-block rules do the rest: a
/// column shifts together, and a pull into a piece that stays put is blocked, leaving the piece where it is.
/// Each step reads the board as it is then, so a piece pulled onto an affected square is pulled again next step.
/// Affected squares off the board are ignored.
/// </summary>
public sealed class TidalWaveEffect : BoardEffect
{
    public const string Cause = "tidal-wave";

    /// <summary>
    /// Highest strength: each step is a wave, and a chain near <see cref="Resolver.MaxWaves"/> would trip the
    /// watchdog and roll back the whole turn end (thaws, timers, other effects) without a word.
    /// </summary>
    public const int MaxStrength = 32;

    public TidalWaveEffect(WaveDirection direction, IReadOnlyList<(int X, int Y)> squares, int strength = 1)
    {
        if (strength < 1 || strength > MaxStrength)
            throw new ArgumentOutOfRangeException(nameof(strength), $"a tidal wave needs a strength from 1 to {MaxStrength}");
        Direction = direction;
        Squares = squares.Distinct().OrderBy(q => q.Y).ThenBy(q => q.X).ToArray();
        Strength = strength;
    }

    public WaveDirection Direction { get; }
    public IReadOnlyList<(int X, int Y)> Squares { get; }
    public int Strength { get; }

    public override int Steps => Strength;

    public override string Key
        => $"TidalWave({Direction}, {string.Join(" ", Squares.Select(q => $"{(char)('a' + q.X)}{q.Y + 1}"))}, strength {Strength})";

    public override string Describe()
        => $"Drags pieces on {Squares.Count} square(s) {Strength} square(s) toward the {Direction.ToString().ToLowerInvariant()} edge; pieces pulled off the board are destroyed.";

    /// <summary>One square in the wave's direction.</summary>
    public (int Dx, int Dy) Step => Direction switch
    {
        WaveDirection.North => (0, 1),
        WaveDirection.South => (0, -1),
        WaveDirection.East => (1, 0),
        _ => (-1, 0),
    };

    public override void Apply(TriggerContext ctx, int timesFired)
    {
        var board = ctx.Board;
        var (dx, dy) = Step;
        foreach (var (x, y) in Squares)
        {
            if (!board.InBounds(x, y) || board.PieceAt(x, y) is not { } piece) continue;
            int tx = x + dx, ty = y + dy;
            if (!board.InBounds(tx, ty))
            {
                ctx.Propose(Proposal.Destroy(piece, Cause));
                continue;
            }
            var tile = board.TileAt(board.Sq(tx, ty)).Def;
            if (tile.BlocksPassage || tile.BlocksLanding) continue;
            ctx.Propose(Proposal.Move(piece, board.Sq(tx, ty), Cause));
        }
    }
}

/// <summary>
/// Spawns a random set of pieces for <see cref="Side"/> on the free squares of an area. The set is bought with
/// <see cref="EncounterGenerator.GenerateArmy"/> (no king, no forced pawns) from the side's roster only, so it
/// holds nothing but the roster's pieces and abilities. Most valuable pieces land first; what doesn't fit doesn't
/// arrive. The seed comes from the game's own generator, so it is reproducible and undone.
/// </summary>
public sealed class ReinforcementsEffect : BoardEffect
{
    public const string Cause = "reinforcements";

    /// <summary>
    /// Highest piece and ability budget. The whole set is generated on every firing (AI search included) before it
    /// is cut to the free squares, so a huge budget would stall the game; 100 already fills any sensible area.
    /// </summary>
    public const int MaxValue = 100;

    private readonly Catalog _catalog;
    private readonly ArmySpec _spec;

    public ReinforcementsEffect(Side side, RosterDef roster, int pieceValue, int abilityValue,
        IReadOnlyList<(int X, int Y)> squares, Catalog catalog)
    {
        if (pieceValue < 1 || pieceValue > MaxValue)
            throw new ArgumentOutOfRangeException(nameof(pieceValue), $"reinforcements need a piece value from 1 to {MaxValue}");
        if (abilityValue < 0 || abilityValue > MaxValue)
            throw new ArgumentOutOfRangeException(nameof(abilityValue), $"reinforcements need an ability value from 0 to {MaxValue}");
        if (squares.Count == 0) throw new ArgumentException("reinforcements need at least one square", nameof(squares));
        Side = side;
        Roster = roster;
        PieceValue = pieceValue;
        AbilityValue = abilityValue;
        Squares = squares.Distinct().OrderBy(q => q.Y).ThenBy(q => q.X).ToArray();
        _catalog = catalog;
        _spec = new ArmySpec
        {
            PieceValue = pieceValue,
            AbilityValue = abilityValue,
            HasKing = false,
            MinimumPawns = 0,
            Pieces = roster.Pieces.Select(p => p.Id).ToArray(),
            Abilities = roster.Abilities.Select(a => a.Id).ToArray(),
        };
    }

    public Side Side { get; }
    public RosterDef Roster { get; }
    public int PieceValue { get; }
    public int AbilityValue { get; }
    public IReadOnlyList<(int X, int Y)> Squares { get; }

    public override string Key
        => $"Reinforcements({Side}, {Roster.Id}, pieces {PieceValue}, abilities {AbilityValue}, {string.Join(" ", Squares.Select(q => $"{(char)('a' + q.X)}{q.Y + 1}"))})";

    public override string Describe()
        => $"Sends {Side} reinforcements from the {Roster.Name} roster (piece value {PieceValue}, ability value {AbilityValue}) onto {Squares.Count} square(s).";

    public override void Apply(TriggerContext ctx, int timesFired)
    {
        // The order of the ctx.Random calls is part of the game's determinism: seed first, then the square shuffle.
        ulong seed = ((ulong)(uint)ctx.Random(int.MaxValue) << 32) | (uint)ctx.Random(int.MaxValue);
        var army = EncounterGenerator.GenerateArmy(_spec, _catalog, seed);

        var board = ctx.Board;
        var free = new List<int>();
        foreach (var (x, y) in Squares)
        {
            if (!board.InBounds(x, y)) continue;
            int sq = board.Sq(x, y);
            var tile = board.TileAt(sq).Def;
            if (board.PieceAt(sq) == null && !tile.BlocksPassage && !tile.BlocksLanding) free.Add(sq);
        }
        for (int i = 0; i < free.Count - 1; i++)
        {
            int j = i + ctx.Random(free.Count - i);
            (free[i], free[j]) = (free[j], free[i]);
        }

        foreach (var (piece, square) in Arrange(army, free))
            ctx.Propose(Proposal.Spawn(piece.Def, Side, square, Cause, piece.Abilities));
    }

    /// <summary>Most valuable first (ties keep the generated order), one per square; the rest is dropped.</summary>
    internal static List<(ArmyPiece Piece, int Square)> Arrange(IReadOnlyList<ArmyPiece> army, IReadOnlyList<int> freeSquares)
        => army.OrderByDescending(p => p.Value).Zip(freeSquares, (p, sq) => (p, sq)).ToList();
}
