using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using RogueChess.Engine.Content;
using RogueChess.Engine.Content.BoardEffects;
using RogueChess.Engine.Content.Tiles;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;
using Xunit;

namespace RogueChess.Engine.Tests;

/// <summary>Timed tiles (a lifetime any tile can carry) and the RandomTiles board effect that places them.</summary>
public class RandomTilesTests
{
    private static readonly Catalog Catalog = ContentLibrary.CreateCatalog();

    /// <summary>Kings on a1 (White, to move) and h8 (Black).</summary>
    private const string Kings = "7k/8/8/8/8/8/8/K7 w - - 0 1";

    private static GameState Rogue(string fen) => Fen.Parse(fen, RuleSet.Rogue);
    private static int Sq(IBoardView s, string name) => MoveText.ParseSquare(s, name);
    private static Piece? At(GameState s, string name) => s.PieceAt(Sq(s, name));

    private static TurnResult Play(GameState s, string move)
    {
        var m = MoveText.FindLegal(s, move) ?? throw new InvalidOperationException($"{move} is not legal");
        var result = Game.PlayTurn(s, m, new ResolveOptions(Trace: true));
        Assert.True(result.Accepted, result.RejectReason);
        Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
        return result;
    }

    /// <summary>Both kings step back and forth (a1↔b1, h8↔g8), one turn per move. Returns the last turn's result.</summary>
    private static TurnResult Shuffle(GameState s, int turns)
    {
        TurnResult last = default;
        for (int i = 0; i < turns; i++)
        {
            bool white = s.SideToMove == Side.White;
            string from = white ? (At(s, "a1") != null ? "a1" : "b1") : (At(s, "h8") != null ? "h8" : "g8");
            string to = white ? (from == "a1" ? "b1" : "a1") : (from == "h8" ? "g8" : "h8");
            last = Play(s, from + to);
        }
        return last;
    }

    private static GlobalInstance AddEffect(GameState s, BoardCondition condition, BoardEffect effect)
    {
        var g = s.AddGlobal(new BoardEffectDef(condition, effect));
        s.ClearHistory();
        return g;
    }

    private static List<int> SquaresWith(GameState s, TileDef def)
        => Enumerable.Range(0, s.Area).Where(sq => s.TileAt(sq).Def == def).ToList();

    private static RandomTilesEffect Spill(int count, int duration = 0, bool includeOccupied = false, bool replaceTiles = false)
        => new(count, new[] { new TileChoice(SlipperyTile.Instance, duration) }, includeOccupied, replaceTiles);

    // ------------------------------------------------------------------ Timed tiles (general lifetime)

    [Fact]
    public void A_timed_tile_counts_down_every_turn_end_and_then_turns_back_into_floor()
    {
        var s = Rogue(Kings);
        s.SetTimedTile(Sq(s, "d4"), WallTile.Instance, 2);
        s.ClearHistory();
        var tile = s.TileAt(Sq(s, "d4"));
        Assert.True(tile.IsTimed);
        Assert.Equal(2, tile.TurnsLeft);
        Assert.Equal(0, tile.VarCount - tile.Def.VarCount - 1); // the lifetime is one extra slot

        Shuffle(s, 1);
        Assert.Equal(WallTile.Instance, s.TileAt(Sq(s, "d4")).Def);
        Assert.Equal(1, s.TileAt(Sq(s, "d4")).TurnsLeft);

        var result = Shuffle(s, 1);
        Assert.True(s.TileAt(Sq(s, "d4")).Def.IsDefault);
        Assert.Contains(result.Trace!.AllFacts, f => f.Kind == FactKinds.TileChanged && f.From == Sq(s, "d4") && f.Cause == Causes.TileExpired);
    }

    [Fact]
    public void Untimed_tiles_never_expire()
    {
        var s = Rogue(Kings);
        s.SetTile(Sq(s, "d4"), SlipperyTile.Instance);
        s.ClearHistory();
        Assert.False(s.TileAt(Sq(s, "d4")).IsTimed);
        Assert.Equal(0, s.TileAt(Sq(s, "d4")).TurnsLeft);
        Shuffle(s, 6);
        Assert.Equal(SlipperyTile.Instance, s.TileAt(Sq(s, "d4")).Def);
    }

    [Fact]
    public void A_timed_status_tile_keeps_its_own_variables_and_whichever_timer_runs_out_first_wins()
    {
        var s = Rogue(Kings);
        s.SetTimedTile(Sq(s, "d4"), FireTile.Instance, 1, 5, 2); // lifetime 1; fire's own timer 5, status 2
        s.ClearHistory();
        var tile = s.TileAt(Sq(s, "d4"));
        Assert.Equal(5, tile.Var(StatusTile.TurnsLeftVar));
        Assert.Equal(2, tile.Var(StatusTile.StatusTurnsVar));

        Shuffle(s, 1);
        Assert.True(s.TileAt(Sq(s, "d4")).Def.IsDefault);
    }

    [Fact]
    public void Timed_tiles_undo_exactly_and_survive_a_snapshot()
    {
        var s = Rogue(Kings);
        s.SetTimedTile(Sq(s, "d4"), SlipperyTile.Instance, 3);
        s.ClearHistory();
        ulong before = s.Hash;
        int mark = s.UndoDepth;

        var restored = GameState.Restore(s.Capture(), Catalog);
        Assert.Equal(before, restored.Hash);
        Assert.Equal(3, restored.TileAt(Sq(s, "d4")).TurnsLeft);

        Shuffle(s, 3);
        Assert.True(s.TileAt(Sq(s, "d4")).Def.IsDefault);
        s.UndoTo(mark);
        Assert.Equal(before, s.Hash);
        Assert.Equal(3, s.TileAt(Sq(s, "d4")).TurnsLeft);
    }

    [Fact]
    public void Lifetimes_are_part_of_the_hash()
    {
        var a = Rogue(Kings);
        var b = Rogue(Kings);
        var c = Rogue(Kings);
        a.SetTimedTile(Sq(a, "d4"), SlipperyTile.Instance, 2);
        b.SetTimedTile(Sq(b, "d4"), SlipperyTile.Instance, 3);
        c.SetTile(Sq(c, "d4"), SlipperyTile.Instance);
        Assert.NotEqual(a.Hash, b.Hash);
        Assert.NotEqual(a.Hash, c.Hash);
    }

    // ------------------------------------------------------------------ RandomTiles: count and eligibility

    [Fact]
    public void Places_exactly_count_tiles_on_empty_floor_by_default()
    {
        var s = Rogue(Kings);
        s.SeedRandom(7);
        AddEffect(s, new TurnDelayCondition(1), Spill(5));

        Shuffle(s, 1);
        var placed = SquaresWith(s, SlipperyTile.Instance);
        Assert.Equal(5, placed.Count);
        Assert.DoesNotContain(Sq(s, "b1"), placed); // the white king moved there
        Assert.DoesNotContain(Sq(s, "h8"), placed);
    }

    [Fact]
    public void Excluding_occupied_tiles_never_picks_a_square_with_a_piece()
    {
        // count larger than the board: every free square is picked, no occupied one.
        var s = Rogue("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w - - 0 1");
        AddEffect(s, new TurnDelayCondition(1), Spill(64));
        Play(s, "g1f3");
        var placed = SquaresWith(s, SlipperyTile.Instance);
        Assert.Equal(32, placed.Count);
        Assert.All(placed, sq => Assert.Null(s.PieceAt(sq)));
    }

    [Fact]
    public void Including_occupied_tiles_can_pick_squares_under_pieces()
    {
        var s = Rogue("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w - - 0 1");
        AddEffect(s, new TurnDelayCondition(1), Spill(64, includeOccupied: true));
        Play(s, "g1f3");
        Assert.Equal(64, SquaresWith(s, SlipperyTile.Instance).Count);
    }

    [Fact]
    public void Beer_spilled_under_a_piece_leaves_it_alone_and_only_moving_onto_beer_slides()
    {
        var s = Rogue("7k/8/8/8/3R4/8/8/K7 w - - 0 1");
        var rook = At(s, "d4")!;
        var blackKing = At(s, "h8")!;
        AddEffect(s, new TurnDelayCondition(1), Spill(64, includeOccupied: true));
        Play(s, "a1b1");
        Assert.Equal(64, SquaresWith(s, SlipperyTile.Instance).Count);
        // Pieces standing where the beer landed don't move.
        Assert.Same(rook, At(s, "d4"));
        Assert.NotNull(At(s, "b1"));
        Assert.Same(blackKing, At(s, "h8"));

        Play(s, "h8h7"); // Black slides down the h-file
        // Leaving a slippery square doesn't slip; arriving on one does, so the rook rides the ice to d8.
        Play(s, "d4d5");
        Assert.Null(At(s, "d4"));
        Assert.Null(At(s, "d5"));
        Assert.Same(rook, At(s, "d8"));
    }

    [Fact]
    public void A_count_above_the_eligible_squares_places_on_all_of_them_and_no_more()
    {
        var s = Rogue(Kings);
        AddEffect(s, new TurnDelayCondition(1), Spill(500));
        Shuffle(s, 1);
        Assert.Equal(62, SquaresWith(s, SlipperyTile.Instance).Count);
    }

    [Fact]
    public void Existing_tiles_are_kept_unless_replace_tiles_is_set()
    {
        foreach (bool replace in new[] { false, true })
        {
            var s = Rogue(Kings);
            s.SetTile(Sq(s, "d4"), FogTile.Instance);
            AddEffect(s, new TurnDelayCondition(1), Spill(500, replaceTiles: replace));
            Shuffle(s, 1);
            Assert.Equal(replace ? SlipperyTile.Instance : FogTile.Instance, s.TileAt(Sq(s, "d4")).Def);
            Assert.Equal(replace ? 62 : 61, SquaresWith(s, SlipperyTile.Instance).Count);
        }
    }

    [Fact]
    public void An_area_limits_the_squares_that_can_be_picked()
    {
        var s = Rogue(Kings);
        var area = new[] { (2, 3), (3, 3), (4, 3), (20, 20) }; // c4 d4 e4, plus one off the board (ignored)
        AddEffect(s, new TurnDelayCondition(1),
            new RandomTilesEffect(10, new[] { new TileChoice(SlipperyTile.Instance) }, squares: area));
        Shuffle(s, 1);
        Assert.Equal(new[] { Sq(s, "c4"), Sq(s, "d4"), Sq(s, "e4") }, SquaresWith(s, SlipperyTile.Instance));
    }

    // ------------------------------------------------------------------ RandomTiles: tile types and their params

    [Fact]
    public void Every_tile_type_in_the_catalog_can_be_placed()
    {
        foreach (var def in Catalog.Tiles.Where(t => !t.IsDefault))
        {
            var s = Rogue(Kings);
            AddEffect(s, new TurnDelayCondition(1), new RandomTilesEffect(3, new[] { new TileChoice(def, 4) }));
            Shuffle(s, 1);
            var placed = SquaresWith(s, def);
            Assert.Equal(3, placed.Count);
            Assert.All(placed, sq => Assert.Equal(4, s.TileAt(sq).TurnsLeft));
        }
    }

    [Fact]
    public void Each_tile_type_keeps_its_own_params()
    {
        var s = Rogue(Kings);
        AddEffect(s, new TurnDelayCondition(1), new RandomTilesEffect(500, new[]
        {
            new TileChoice(FireTile.Instance, Duration: 0, Weight: 1, Vars: new[] { 6, 1 }),
        }));
        Shuffle(s, 1);
        var placed = SquaresWith(s, FireTile.Instance);
        Assert.Equal(62, placed.Count);
        Assert.All(placed, sq =>
        {
            Assert.Equal(6, s.TileAt(sq).Var(StatusTile.TurnsLeftVar));
            Assert.Equal(1, s.TileAt(sq).Var(StatusTile.StatusTurnsVar));
            Assert.False(s.TileAt(sq).IsTimed);
        });
    }

    [Fact]
    public void A_weighted_mix_places_several_types_each_with_its_own_duration()
    {
        var s = Rogue(Kings);
        s.SeedRandom(11);
        AddEffect(s, new TurnDelayCondition(1), new RandomTilesEffect(40, new[]
        {
            new TileChoice(SlipperyTile.Instance, Duration: 2, Weight: 3),
            new TileChoice(FogTile.Instance, Duration: 5, Weight: 1),
        }));
        Shuffle(s, 1);
        var slippery = SquaresWith(s, SlipperyTile.Instance);
        var fog = SquaresWith(s, FogTile.Instance);
        Assert.Equal(40, slippery.Count + fog.Count);
        Assert.NotEmpty(fog);
        Assert.True(slippery.Count > fog.Count, $"weight 3 vs 1 gave {slippery.Count} vs {fog.Count}");
        Assert.All(slippery, sq => Assert.Equal(2, s.TileAt(sq).TurnsLeft));
        Assert.All(fog, sq => Assert.Equal(5, s.TileAt(sq).TurnsLeft));
    }

    [Fact]
    public void A_random_tomb_holds_nobody_and_lets_allies_walk_on()
    {
        var s = Rogue(Kings);
        var area = new[] { (1, 1) }; // b2
        AddEffect(s, new TurnDelayCondition(1), new RandomTilesEffect(1, new[] { new TileChoice(TombTile.Instance) }, squares: area));
        Shuffle(s, 2); // white a1-b1, black h8-g8 (the tomb appears after white's turn)
        Assert.Equal(TombTile.Instance, s.TileAt(Sq(s, "b2")).Def);
        Play(s, "b1b2");
        Assert.NotNull(At(s, "b2"));
    }

    // ------------------------------------------------------------------ RandomTiles: duration expiry

    [Fact]
    public void Placed_tiles_last_their_duration_then_disappear()
    {
        var s = Rogue(Kings);
        AddEffect(s, new TurnDelayCondition(1), Spill(4, duration: 2));

        Shuffle(s, 1); // placed at the end of turn 1
        Assert.Equal(4, SquaresWith(s, SlipperyTile.Instance).Count);
        Shuffle(s, 1); // turn 2: 2 → 1
        Assert.Equal(4, SquaresWith(s, SlipperyTile.Instance).Count);
        Shuffle(s, 1); // turn 3: gone
        Assert.Empty(SquaresWith(s, SlipperyTile.Instance));
    }

    [Fact]
    public void Duration_zero_means_the_tiles_stay()
    {
        var s = Rogue(Kings);
        AddEffect(s, new TurnDelayCondition(1), Spill(4));
        Shuffle(s, 10);
        Assert.Equal(4, SquaresWith(s, SlipperyTile.Instance).Count);
    }

    // ------------------------------------------------------------------ RandomTiles: determinism

    private static List<int> SpillWithSeed(ulong seed)
    {
        var s = Rogue(Kings);
        s.SeedRandom(seed);
        AddEffect(s, new TurnDelayCondition(1), new RandomTilesEffect(6, new[]
        {
            new TileChoice(SlipperyTile.Instance, 3, 1), new TileChoice(FogTile.Instance, 3, 1),
        }));
        Shuffle(s, 1);
        return Enumerable.Range(0, s.Area).Select(sq => s.TileAt(sq).Def.IsDefault ? 0 : s.TileAt(sq).Def == SlipperyTile.Instance ? 1 : 2).ToList();
    }

    [Fact]
    public void The_same_seed_picks_the_same_squares_and_types()
    {
        Assert.Equal(SpillWithSeed(1234), SpillWithSeed(1234));
        Assert.NotEqual(SpillWithSeed(1234), SpillWithSeed(98765));
    }

    [Fact]
    public void A_spill_is_undone_exactly()
    {
        var s = Rogue(Kings);
        s.SeedRandom(5);
        AddEffect(s, new TurnDelayCondition(1), Spill(5, duration: 3));
        ulong before = s.Hash;
        int mark = s.UndoDepth;
        Shuffle(s, 2);
        s.UndoTo(mark);
        Assert.Equal(before, s.Hash);
        Assert.Empty(SquaresWith(s, SlipperyTile.Instance));
        Shuffle(s, 1);
        var again = SquaresWith(s, SlipperyTile.Instance);
        s.UndoTo(mark);
        Shuffle(s, 1);
        Assert.Equal(again, SquaresWith(s, SlipperyTile.Instance));
    }

    // ------------------------------------------------------------------ Hook for the visual layer

    [Fact]
    public void Placements_reads_which_tiles_were_picked_from_a_turn_trace()
    {
        var s = Rogue(Kings);
        AddEffect(s, new TurnDelayCondition(1),
            new RandomTilesEffect(3, new[] { new TileChoice(SlipperyTile.Instance, 4) }, cause: "beer-spill"));
        var result = Shuffle(s, 1);

        var placements = RandomTilesEffect.Placements(result.Trace!, "beer-spill");
        Assert.Equal(3, placements.Count);
        Assert.Equal(SquaresWith(s, SlipperyTile.Instance), placements.Select(p => p.Square).OrderBy(q => q));
        Assert.All(placements, p => { Assert.Equal(SlipperyTile.Instance, p.Tile); Assert.Equal(4, p.TurnsLeft); });
        Assert.Empty(RandomTilesEffect.Placements(result.Trace!)); // a different cause
    }

    // ------------------------------------------------------------------ JSON

    private static RandomTilesEffect Parse(string paramsJson)
    {
        using var doc = JsonDocument.Parse($$"""{ "type": "RandomTiles", "params": {{paramsJson}} }""");
        return Assert.IsType<RandomTilesEffect>(BoardEffectJson.ParseEffect(doc.RootElement, Catalog));
    }

    [Fact]
    public void Json_single_tile_shorthand()
    {
        var e = Parse("""{ "count": 3, "tile": "SlipperyTile", "duration": 4 }""");
        Assert.Equal(3, e.Count);
        Assert.False(e.IncludeOccupied);
        Assert.False(e.ReplaceTiles);
        Assert.Equal(RandomTilesEffect.DefaultCause, e.Cause);
        var choice = Assert.Single(e.Tiles);
        Assert.Equal(SlipperyTile.Instance, choice.Tile);
        Assert.Equal(4, choice.Duration);
        Assert.Equal(1, choice.Weight);
    }

    [Fact]
    public void Json_weighted_list_with_per_type_params()
    {
        var e = Parse("""
        {
          "count": 5, "includeOccupied": true, "replaceTiles": true, "cause": "beer-spill", "squares": ["c3", "d4"],
          "tiles": [
            { "tile": "slippery", "weight": 3, "duration": 2 },
            { "tile": "fire", "duration": 6, "params": { "statusTurns": 1 } }
          ]
        }
        """);
        Assert.True(e.IncludeOccupied);
        Assert.True(e.ReplaceTiles);
        Assert.Equal("beer-spill", e.Cause);
        Assert.Equal(new[] { (2, 2), (3, 3) }, e.Squares);
        Assert.Equal(2, e.Tiles.Count);
        Assert.Equal((SlipperyTile.Instance as TileDef, 3, 2), (e.Tiles[0].Tile, e.Tiles[0].Weight, e.Tiles[0].Duration));
        Assert.Equal(FireTile.Instance, e.Tiles[1].Tile);
        Assert.Equal(1, e.Tiles[1].Weight);
        Assert.Equal(new[] { StatusTile.DefaultTurns, 1 }, e.Tiles[1].Vars);
    }

    [Theory]
    [InlineData("""{ "tile": "slippery" }""")]                                    // no count
    [InlineData("""{ "count": 0, "tile": "slippery" }""")]
    [InlineData("""{ "count": 3 }""")]                                            // no tile
    [InlineData("""{ "count": 3, "tiles": [] }""")]
    [InlineData("""{ "count": 3, "tile": "lava" }""")]
    [InlineData("""{ "count": 3, "tile": "slippery", "duration": -1 }""")]
    [InlineData("""{ "count": 3, "tiles": [{ "tile": "slippery", "weight": 0 }] }""")]
    [InlineData("""{ "count": 3, "tiles": [{ "tile": "fire", "params": { "heat": 9 } }] }""")]
    [InlineData("""{ "count": 3, "tile": "slippery", "tiles": [{ "tile": "fog" }] }""")]   // both forms
    public void Bad_random_tiles_params_throw(string json)
        => Assert.Throws<FormatException>(() => Parse(json));

    [Fact]
    public void A_whole_board_effect_entry_loads_through_the_catalog()
    {
        using var doc = JsonDocument.Parse("""
        { "name": "Beer spill", "condition": { "type": "TurnDelay", "params": { "turnNumber": 1 } },
          "effect": { "type": "RandomTiles", "params": { "count": 3, "tile": "slippery", "duration": 4, "cause": "beer-spill" } } }
        """);
        var factory = Catalog.FindBoardEffect("RandomTiles");
        Assert.NotNull(factory);
        var (def, vars) = factory!(doc.RootElement);
        var s = Rogue(Kings);
        s.AddGlobal(def, vars);
        s.ClearHistory();
        Shuffle(s, 1);
        Assert.Equal(3, SquaresWith(s, SlipperyTile.Instance).Count);
    }
}
