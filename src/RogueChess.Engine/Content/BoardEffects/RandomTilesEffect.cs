using System;
using System.Collections.Generic;
using System.Linq;
using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content.BoardEffects;

/// <summary>
/// One tile type a <see cref="RandomTilesEffect"/> can place.
/// </summary>
/// <param name="Tile">The tile type (any <see cref="TileDef"/>).</param>
/// <param name="Duration">Turns the placed tile stays before turning back into floor (both sides' turns count); 0 = it stays. See <see cref="Tile.IsTimed"/>.</param>
/// <param name="Weight">Relative chance of this type when several are listed (at least 1).</param>
/// <param name="Vars">The tile's own variables (e.g. a fire tile's timer and status length); null = the type's defaults.</param>
public sealed record TileChoice(TileDef Tile, int Duration = 0, int Weight = 1, IReadOnlyList<int>? Vars = null)
{
    internal string Key
        => $"{Tile.Id}{(Vars is { Count: > 0 } ? $"({string.Join(",", Vars)})" : "")}{(Duration > 0 ? $" for {Duration}" : "")}{(Weight != 1 ? $" x{Weight}" : "")}";
}

/// <summary>A tile placed by a <see cref="RandomTilesEffect"/>, as read back from a turn's facts (for animation).</summary>
/// <param name="Square">The square it landed on.</param>
/// <param name="Tile">The tile type placed.</param>
/// <param name="TurnsLeft">Its lifetime when the facts are read right after the turn (0 = permanent).</param>
public readonly record struct TilePlacement(int Square, TileDef Tile, int TurnsLeft);

/// <summary>
/// Places tiles on <see cref="Count"/> random squares (a bartender's spilled beer turning into Slippery tiles, say).
///
/// <list type="bullet">
/// <item><b>Eligible squares</b>: every square of the board, or of <see cref="Squares"/> when an area is given
/// (squares off the board are ignored). Squares with a piece are skipped unless <see cref="IncludeOccupied"/>; squares
/// that already hold a tile other than plain floor are skipped unless <see cref="ReplaceTiles"/>. When fewer squares
/// are eligible than <see cref="Count"/>, every eligible square gets a tile.</item>
/// <item><b>Tile types</b>: one or several <see cref="TileChoice"/>s, each with its own duration and variables. With
/// several, each picked square draws its type by weight.</item>
/// <item><b>Randomness</b>: the game's own seeded generator (<see cref="TriggerContext.Random"/>), so a firing is
/// reproducible from the state's seed and undone with the turn. Draw order: the square shuffle, then one weight draw
/// per picked square (only when there are several types).</item>
/// <item><b>Placing a tile under a piece</b> doesn't trigger it: tiles react to pieces arriving (moves, spawns), so a
/// piece already standing there is only affected once it moves.</item>
/// </list>
///
/// The tiles change in the wave after the turn end, with <see cref="Cause"/> as the proposals' cause, so the
/// turn's <see cref="FactKinds.TileChanged"/> facts say which squares got which tile (<see cref="Placements(TurnTrace, string)"/>).
/// </summary>
public sealed class RandomTilesEffect : BoardEffect
{
    public const string DefaultCause = "random-tiles";

    public RandomTilesEffect(int count, IReadOnlyList<TileChoice> tiles, bool includeOccupied = false, bool replaceTiles = false,
        IReadOnlyList<(int X, int Y)>? squares = null, string cause = DefaultCause)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), "random tiles need a count of at least 1");
        if (tiles.Count == 0) throw new ArgumentException("random tiles need at least one tile type", nameof(tiles));
        foreach (var t in tiles)
        {
            if (t.Weight < 1) throw new ArgumentException($"tile '{t.Tile.Id}' needs a weight of at least 1", nameof(tiles));
            if (t.Duration < 0) throw new ArgumentException($"tile '{t.Tile.Id}' needs a duration of 0 or more", nameof(tiles));
        }
        if (string.IsNullOrEmpty(cause)) throw new ArgumentException("random tiles need a cause", nameof(cause));
        Count = count;
        Tiles = tiles.ToArray();
        IncludeOccupied = includeOccupied;
        ReplaceTiles = replaceTiles;
        Squares = squares?.Distinct().OrderBy(q => q.Y).ThenBy(q => q.X).ToArray();
        Cause = cause;
        TotalWeight = Tiles.Sum(t => t.Weight);
    }

    public int Count { get; }
    public IReadOnlyList<TileChoice> Tiles { get; }
    public bool IncludeOccupied { get; }
    public bool ReplaceTiles { get; }

    /// <summary>The area squares are picked from; null = the whole board.</summary>
    public IReadOnlyList<(int X, int Y)>? Squares { get; }

    /// <summary>The cause on the placements' proposals and facts ("beer-spill"), so the UI can tell spills apart.</summary>
    public string Cause { get; }

    public int TotalWeight { get; }

    public override string Key
        => $"RandomTiles({Count}, {string.Join(" | ", Tiles.Select(t => t.Key))}"
         + $"{(IncludeOccupied ? ", occupied" : "")}{(ReplaceTiles ? ", replace" : "")}"
         + $"{(Squares != null ? $", in {string.Join(" ", Squares.Select(q => $"{(char)('a' + q.X)}{q.Y + 1}"))}" : "")}, {Cause})";

    public override string Describe()
    {
        string types = string.Join(" or ", Tiles.Select(t => t.Tile.Name.ToLowerInvariant()).Distinct());
        string where = Squares != null ? $"{Squares.Count} square(s)" : "the board";
        string occupied = IncludeOccupied ? "random squares" : "random empty squares";
        return $"Places {types} tiles on {Count} {occupied} of {where}.";
    }

    public override void Apply(TriggerContext ctx, int timesFired)
    {
        var board = ctx.Board;
        var candidates = new List<int>();
        if (Squares == null)
        {
            for (int sq = 0; sq < board.Area; sq++)
                if (Eligible(board, sq)) candidates.Add(sq);
        }
        else
        {
            foreach (var (x, y) in Squares)
                if (board.InBounds(x, y) && Eligible(board, board.Sq(x, y))) candidates.Add(board.Sq(x, y));
        }

        // The order of the ctx.Random calls is part of the game's determinism: the shuffle, then the weight draws.
        int count = Math.Min(Count, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            int j = i + ctx.Random(candidates.Count - i);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        for (int i = 0; i < count; i++)
        {
            var choice = Pick(ctx);
            var vars = choice.Vars is { Count: > 0 } ? choice.Vars.ToArray() : Array.Empty<int>();
            ctx.Propose(Proposal.ChangeTimedTile(candidates[i], choice.Tile, choice.Duration, Cause, vars));
        }
    }

    private bool Eligible(IBoardView board, int sq)
        => (IncludeOccupied || board.PieceAt(sq) == null) && (ReplaceTiles || board.TileAt(sq).Def.IsDefault);

    private TileChoice Pick(TriggerContext ctx)
    {
        if (Tiles.Count == 1) return Tiles[0];
        int roll = ctx.Random(TotalWeight);
        foreach (var t in Tiles)
        {
            if (roll < t.Weight) return t;
            roll -= t.Weight;
        }
        return Tiles[^1];
    }

    /// <summary>
    /// The tiles placed with <paramref name="cause"/> in a turn, in the order they were committed (square order).
    /// This is the hook for the visual layer: play the turn with <c>ResolveOptions(Trace: true)</c>, then read
    /// what was spilled where. Read it right after the turn: <see cref="TilePlacement.TurnsLeft"/> reflects the tile
    /// as it is when read. Tiles that later run out show up as <see cref="FactKinds.TileChanged"/> facts with
    /// <see cref="Causes.TileExpired"/>.
    /// </summary>
    public static IReadOnlyList<TilePlacement> Placements(TurnTrace trace, string cause = DefaultCause)
        => Placements(trace.AllFacts, cause);

    /// <inheritdoc cref="Placements(TurnTrace, string)"/>
    public static IReadOnlyList<TilePlacement> Placements(IEnumerable<Fact> facts, string cause = DefaultCause)
        => facts.Where(f => f.Kind == FactKinds.TileChanged && f.Cause == cause && f.NewTile != null)
            .Select(f => new TilePlacement(f.From, f.NewTile!.Def, f.NewTile.TurnsLeft))
            .ToList();
}
