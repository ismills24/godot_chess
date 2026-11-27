using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RogueChess.Engine.Content;
using RogueChess.Engine.Content.Abilities;
using RogueChess.Engine.Content.BoardEffects;
using RogueChess.Engine.Content.Tiles;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;
using Xunit;

namespace RogueChess.Engine.Tests;

/// <summary>The Frozen status, the board-effect conditions and the board effects ported from the prototype.</summary>
public class BoardEffectTests
{
    private static GameState Rogue(string fen) => Fen.Parse(fen, RuleSet.Rogue);
    private static int Sq(IBoardView s, string name) => MoveText.ParseSquare(s, name);
    private static Piece? At(GameState s, string name) => s.PieceAt(Sq(s, name));
    private static Piece Must(GameState s, string name) => At(s, name) ?? throw new InvalidOperationException($"no piece on {name}");

    private static List<string> Moves(GameState s, string from)
    {
        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(s, moves);
        return moves.Where(m => m.From == Sq(s, from)).Select(m => MoveText.Format(s, m)).ToList();
    }

    private static void Play(GameState s, string move)
    {
        var m = MoveText.FindLegal(s, move) ?? throw new InvalidOperationException($"{move} is not legal");
        var result = Game.PlayTurn(s, m, new ResolveOptions(Trace: true));
        Assert.True(result.Accepted, result.RejectReason);
        Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
    }

    /// <summary>Both kings step back and forth (a1↔b1, h8↔g8), one turn per move.</summary>
    private static void Shuffle(GameState s, int turns)
    {
        for (int i = 0; i < turns; i++)
        {
            bool white = s.SideToMove == Side.White;
            string from = white ? (At(s, "a1") != null ? "a1" : "b1") : (At(s, "h8") != null ? "h8" : "g8");
            string to = white ? (from == "a1" ? "b1" : "a1") : (from == "h8" ? "g8" : "h8");
            Play(s, from + to);
        }
    }

    private static GlobalInstance AddEffect(GameState s, BoardCondition condition, BoardEffect effect)
    {
        var g = s.AddGlobal(new BoardEffectDef(condition, effect));
        s.ClearHistory();
        return g;
    }

    private static int Walls(GameState s) => Enumerable.Range(0, s.Area).Count(sq => s.TileAt(sq).Def == WallTile.Instance);
    private static int TurnsLeft(Piece p) => p.GetAbility(FrozenAbility.Instance)?.Var(FrozenAbility.TurnsLeftVar) ?? 0;

    // ------------------------------------------------------------------ Frozen

    [Fact]
    public void Frozen_piece_has_no_moves_and_thaws_after_its_owners_turns()
    {
        var s = Rogue("7k/8/8/8/8/8/8/R6K w - - 0 1");
        s.GrantAbility(Must(s, "a1"), FrozenAbility.Instance, 2);
        s.ClearHistory();

        Assert.Empty(Moves(s, "a1"));
        Play(s, "h1g1"); // white turn 1 ends: 2 → 1
        Assert.Equal(1, TurnsLeft(Must(s, "a1")));
        Play(s, "h8g8"); // black's turn doesn't tick a white piece
        Assert.Equal(1, TurnsLeft(Must(s, "a1")));
        Assert.Empty(Moves(s, "a1"));
        Play(s, "g1h1"); // 1 → 0, then the status is removed
        Assert.False(Must(s, "a1").HasAbility(FrozenAbility.Instance));
        Play(s, "g8h8");
        Assert.NotEmpty(Moves(s, "a1"));
    }

    [Fact]
    public void Frozen_wins_over_phantom_whatever_order_they_were_granted()
    {
        foreach (bool frozenFirst in new[] { true, false })
        {
            var s = Rogue("7k/8/8/8/8/8/P7/R6K w - - 0 1");
            var rook = Must(s, "a1");
            if (frozenFirst) s.GrantAbility(rook, FrozenAbility.Instance, 2);
            s.GrantAbility(rook, PhantomAbility.Instance);
            if (!frozenFirst) s.GrantAbility(rook, FrozenAbility.Instance, 2);
            s.ClearHistory();

            Assert.Empty(Moves(s, "a1"));
        }
    }

    [Fact]
    public void Frozen_piece_does_not_attack_under_standard_rules()
    {
        var s = Fen.Parse("4k3/8/8/8/8/8/8/K3R3 b - - 0 1", RuleSet.Standard);
        Assert.True(MoveGenerator.IsInCheck(s, Side.Black));
        s.GrantAbility(Must(s, "e1"), FrozenAbility.Instance, 2);
        Assert.False(MoveGenerator.IsInCheck(s, Side.Black));
    }

    // ------------------------------------------------------------------ Conditions

    [Fact]
    public void Turn_interval_fires_on_every_nth_turn_counting_both_sides()
    {
        var s = Rogue("7k/8/8/8/8/8/8/K7 w - - 0 1");
        AddEffect(s, new TurnIntervalCondition(3), new WallsPlacementEffect(1));

        Shuffle(s, 2);
        Assert.Equal(0, Walls(s));
        Shuffle(s, 1); // turn 3
        Assert.Equal(1, Walls(s));
        Shuffle(s, 3); // turn 6
        Assert.Equal(2, Walls(s));
    }

    [Fact]
    public void Turn_interval_with_a_start_turn_fires_from_that_turn()
    {
        var s = Rogue("7k/8/8/8/8/8/8/K7 w - - 0 1");
        AddEffect(s, new TurnIntervalCondition(5, startTurn: 2), new WallsPlacementEffect(1));

        Shuffle(s, 1);
        Assert.Equal(0, Walls(s));
        Shuffle(s, 1); // turn 2
        Assert.Equal(1, Walls(s));
        Shuffle(s, 4);
        Assert.Equal(1, Walls(s));
        Shuffle(s, 1); // turn 7
        Assert.Equal(2, Walls(s));
    }

    [Fact]
    public void Turn_delay_fires_once()
    {
        var s = Rogue("7k/8/8/8/8/8/8/K7 w - - 0 1");
        var g = AddEffect(s, new TurnDelayCondition(2), new WallsPlacementEffect(2));

        Shuffle(s, 1);
        Assert.Equal(0, Walls(s));
        Shuffle(s, 1);
        Assert.Equal(2, Walls(s));
        Shuffle(s, 6);
        Assert.Equal(2, Walls(s));
        Assert.Equal(1, g.Var(BoardEffectDef.TimesFiredVar));
    }

    [Fact]
    public void Board_value_fires_below_the_threshold_up_to_max_triggers_and_ignores_kings()
    {
        // Black: king (ignored) + rook (5). Threshold 6 → below; max 2 triggers.
        var s = Rogue("r6k/8/8/8/8/8/8/K7 w - - 0 1");
        s.GrantAbility(Must(s, "h8"), PredatorAbility.Instance); // a king's abilities don't count either
        AddEffect(s, new BoardValueCondition(Side.Black, 6, maxTriggers: 2), new WallsPlacementEffect(1));
        Assert.Equal(5, BoardValueCondition.BoardValue(s, Side.Black));

        Shuffle(s, 1);
        Assert.Equal(1, Walls(s));
        Shuffle(s, 1);
        Assert.Equal(2, Walls(s));
        Shuffle(s, 4);
        Assert.Equal(2, Walls(s));
    }

    [Fact]
    public void Board_value_does_not_fire_at_or_above_the_threshold()
    {
        var s = Rogue("r6k/8/8/8/8/8/8/K7 w - - 0 1");
        AddEffect(s, new BoardValueCondition(Side.Black, 5), new WallsPlacementEffect(1));
        Shuffle(s, 4);
        Assert.Equal(0, Walls(s));
    }

    // ------------------------------------------------------------------ Effects

    [Fact]
    public void Walls_placement_only_uses_empty_squares_and_is_undone_exactly()
    {
        var s = Rogue("7k/8/8/8/8/8/8/K7 w - - 0 1");
        s.SetTile(Sq(s, "d4"), WallTile.Instance);
        s.SeedRandom(42);
        AddEffect(s, new TurnIntervalCondition(1), new WallsPlacementEffect(5));
        ulong before = s.Hash;
        int mark = s.UndoDepth;

        Shuffle(s, 1);
        Assert.Equal(6, Walls(s));
        foreach (var p in s.PiecesOnBoard()) Assert.NotEqual(WallTile.Instance, s.TileAt(p.Square).Def);

        s.UndoTo(mark);
        Assert.Equal(1, Walls(s));
        Assert.Equal(before, s.Hash);
    }

    [Fact]
    public void Walls_placement_is_reproducible_from_the_seed()
    {
        string Layout(ulong seed)
        {
            var s = Rogue("7k/8/8/8/8/8/8/K7 w - - 0 1");
            s.SeedRandom(seed);
            AddEffect(s, new TurnIntervalCondition(1), new WallsPlacementEffect(4));
            Shuffle(s, 1);
            return string.Join(",", Enumerable.Range(0, s.Area).Where(sq => s.TileAt(sq).Def == WallTile.Instance));
        }

        Assert.Equal(Layout(7), Layout(7));
        Assert.NotEqual(Layout(7), Layout(8));
    }

    [Fact]
    public void Wind_gust_pushes_the_furthest_piece_of_each_file_down_and_freezes_blocked_pairs()
    {
        // a-file: white rook a5 is pushed to a4 (the a2 pawn is not the furthest).
        // c-file: white knight c5 is blocked by a black pawn on c4 → both frozen.
        // e-file: white pawn e1 is on the bottom rank → stays (so does the king, which moved along rank 1).
        var s = Rogue("7k/8/8/R1N5/2p5/8/P7/4P2K w - - 0 1");
        AddEffect(s, new TurnIntervalCondition(1), new WindGustEffect(Side.White, freezeDuration: 2));

        Play(s, "h1g1");

        Assert.Equal(StandardPieces.Rook, At(s, "a4")?.Def);
        Assert.NotNull(At(s, "a2"));
        Assert.Equal(2, TurnsLeft(Must(s, "c5")));
        Assert.Equal(2, TurnsLeft(Must(s, "c4")));
        Assert.Equal(StandardPieces.Pawn, At(s, "e1")?.Def);
        Assert.Equal(StandardPieces.King, At(s, "g1")?.Def);
    }

    [Fact]
    public void Wind_gust_on_black_blows_the_same_way()
    {
        var s = Rogue("k7/8/8/8/3r4/8/8/7K w - - 0 1");
        AddEffect(s, new TurnIntervalCondition(1), new WindGustEffect(Side.Black));

        Play(s, "h1g1");

        Assert.Equal(StandardPieces.Rook, At(s, "d3")?.Def);
        Assert.Equal(StandardPieces.King, At(s, "a7")?.Def);
    }

    [Fact]
    public void Refreezing_keeps_the_longer_duration()
    {
        var s = Rogue("7k/8/8/2N5/2p5/8/8/7K w - - 0 1");
        s.GrantAbility(Must(s, "c4"), FrozenAbility.Instance, 5);
        AddEffect(s, new TurnIntervalCondition(1), new WindGustEffect(Side.White, freezeDuration: 2));

        Play(s, "h1g1");

        Assert.Equal(5, TurnsLeft(Must(s, "c4")));
        Assert.Equal(2, TurnsLeft(Must(s, "c5")));
    }

    [Fact]
    public void Checkerboard_wipe_freezes_one_colour_and_alternates()
    {
        // a1 (0,0) is a prototype "white" square (x + y even); a2, b1 and a8 are "black"; h8 is "white".
        var s = Rogue("p6k/8/8/8/8/8/8/KN6 w - - 0 1");
        AddEffect(s, new TurnIntervalCondition(1), new CheckerboardWipeEffect(SquareColor.White, freezeDuration: 1, alternating: true));

        Play(s, "a1a2"); // first wipe: "white" squares → only the black king on h8 is frozen
        Assert.False(Must(s, "a2").HasAbility(FrozenAbility.Instance));
        Assert.False(Must(s, "b1").HasAbility(FrozenAbility.Instance));
        Assert.False(Must(s, "a8").HasAbility(FrozenAbility.Instance));
        Assert.True(Must(s, "h8").HasAbility(FrozenAbility.Instance));
        Assert.Empty(Moves(s, "h8"));

        Play(s, "a8a7"); // second wipe: "black" squares → a2 and b1 freeze, a7 (even) doesn't; h8 thaws at the end of Black's turn
        Assert.True(Must(s, "a2").HasAbility(FrozenAbility.Instance));
        Assert.True(Must(s, "b1").HasAbility(FrozenAbility.Instance));
        Assert.False(Must(s, "a7").HasAbility(FrozenAbility.Instance));
        Assert.False(Must(s, "h8").HasAbility(FrozenAbility.Instance));
    }

    [Fact]
    public void Checkerboard_wipe_colour_sequence()
    {
        var wipe = new CheckerboardWipeEffect(SquareColor.Black, alternating: true);
        Assert.Equal(SquareColor.Black, wipe.ColorFor(0));
        Assert.Equal(SquareColor.White, wipe.ColorFor(1));
        Assert.Equal(SquareColor.Black, wipe.ColorFor(2));
        Assert.Equal(SquareColor.White, new CheckerboardWipeEffect(SquareColor.White).ColorFor(1));
    }

    // ------------------------------------------------------------------ Multi-step effects

    /// <summary>Each step turns the first non-wall square of rank 1 (from a1) into a wall.</summary>
    private sealed class WallPerStepEffect(int steps) : BoardEffect
    {
        public override string Key => $"WallPerStep({steps})";
        public override string Describe() => "test";
        public override int Steps => steps;

        public override void Apply(TriggerContext ctx, int timesFired)
        {
            for (int x = 0; x < ctx.Board.Width; x++)
                if (ctx.Board.TileAt(ctx.Board.Sq(x, 0)).Def != WallTile.Instance)
                {
                    ctx.Propose(Proposal.ChangeTile(ctx.Board.Sq(x, 0), WallTile.Instance, "test"));
                    return;
                }
        }
    }

    [Fact]
    public void Multi_step_effect_applies_once_per_wave_within_one_firing()
    {
        var s = Rogue("7k/8/8/8/8/8/8/7K w - - 0 1");
        var g = AddEffect(s, new TurnDelayCondition(1), new WallPerStepEffect(3));

        Play(s, "h1g1");
        Assert.Equal(3, Walls(s));
        Assert.Equal(1, g.Var(BoardEffectDef.TimesFiredVar));
        Assert.Equal(0, g.Var(BoardEffectDef.StepsLeftVar));

        Play(s, "h8g8"); // TurnDelay fires once only
        Assert.Equal(3, Walls(s));
    }

    [Fact]
    public void Single_step_effects_keep_one_variable()
    {
        Assert.Equal(1, new BoardEffectDef(new TurnIntervalCondition(1), new WindGustEffect(Side.White)).VarCount);
        Assert.Equal(2, new BoardEffectDef(new TurnIntervalCondition(1), new WallPerStepEffect(2)).VarCount);
    }

    // ------------------------------------------------------------------ Tidal wave

    /// <summary>Every square of the given (1-based) ranks.</summary>
    private static (int X, int Y)[] Rows(int width, params int[] ranks)
        => ranks.SelectMany(r => Enumerable.Range(0, width).Select(x => (x, r - 1))).ToArray();

    private static (int X, int Y) At(string name) => (name[0] - 'a', int.Parse(name.AsSpan(1)) - 1);

    private static TidalWaveEffect Wave(WaveDirection d, int strength, params string[] squares)
        => new(d, squares.Select(At).ToArray(), strength);

    [Fact] // the user's example: a wave from the north over the top three rows
    public void Tidal_wave_pulls_the_top_rows_north_and_washes_out_the_edge()
    {
        var s = Rogue("r6k/Q7/N7/B7/8/8/8/7K w - - 0 1");
        AddEffect(s, new TurnDelayCondition(1), new TidalWaveEffect(WaveDirection.North, Rows(8, 6, 7, 8)));

        Play(s, "h1g1");

        Assert.Equal(StandardPieces.Queen, At(s, "a8")?.Def);  // a7 → a8, the rook on a8 is washed out
        Assert.Equal(StandardPieces.Knight, At(s, "a7")?.Def); // a6 → a7
        Assert.Null(At(s, "a6"));
        Assert.Equal(StandardPieces.Bishop, At(s, "a5")?.Def); // outside the area: untouched
        Assert.DoesNotContain(s.PiecesOnBoard(), p => p.Def == StandardPieces.Rook);
    }

    [Theory]
    [InlineData(WaveDirection.North, "d5")]
    [InlineData(WaveDirection.South, "d3")]
    [InlineData(WaveDirection.East, "e4")]
    [InlineData(WaveDirection.West, "c4")]
    public void Tidal_wave_works_in_every_direction(WaveDirection direction, string landing)
    {
        var s = Rogue("7k/8/8/8/3R4/8/8/7K w - - 0 1");
        AddEffect(s, new TurnDelayCondition(1), Wave(direction, 1, "d4"));

        Play(s, "h1g1");

        Assert.Null(At(s, "d4"));
        Assert.Equal(StandardPieces.Rook, At(s, landing)?.Def);
    }

    [Fact]
    public void Tidal_wave_destroys_pieces_pulled_off_the_board_without_crediting_anyone()
    {
        var s = Rogue("N6k/8/8/8/8/8/8/7K w - - 0 1");
        AddEffect(s, new TurnDelayCondition(1), Wave(WaveDirection.North, 1, "a8"));
        var knight = Must(s, "a8");

        var m = MoveText.FindLegal(s, "h1g1")!.Value;
        var result = Game.PlayTurn(s, m, new ResolveOptions(Trace: true));

        Assert.Null(At(s, "a8"));
        Assert.False(knight.IsOnBoard);
        var facts = result.Trace!.AllFacts.ToList();
        Assert.Contains(facts, f => f.Kind == FactKinds.Died && f.Piece?.Id == knight.Id && f.Cause == TidalWaveEffect.Cause);
        Assert.DoesNotContain(facts, f => f.Kind == FactKinds.Killed && f.Source.Category == SourceCategory.Piece);
    }

    [Fact]
    public void Tidal_wave_is_held_back_by_walls()
    {
        var s = Rogue("7k/P7/N7/8/8/8/8/7K w - - 0 1");
        s.SetTile(Sq(s, "a8"), WallTile.Instance);
        AddEffect(s, new TurnDelayCondition(1), new TidalWaveEffect(WaveDirection.North, Rows(8, 6, 7, 8)));

        Play(s, "h1g1");

        Assert.Equal(WallTile.Instance, s.TileAt(Sq(s, "a8")).Def);
        Assert.Null(At(s, "a8"));
        Assert.Equal(StandardPieces.Pawn, At(s, "a7")?.Def);
        Assert.Equal(StandardPieces.Knight, At(s, "a6")?.Def);
    }

    [Fact]
    public void Tidal_wave_is_blocked_by_a_piece_outside_the_area()
    {
        var s = Rogue("7k/8/P7/N7/8/8/8/7K w - - 0 1");
        AddEffect(s, new TurnDelayCondition(1), Wave(WaveDirection.North, 1, "a5"));

        Play(s, "h1g1");

        Assert.Equal(StandardPieces.Pawn, At(s, "a6")?.Def);
        Assert.Equal(StandardPieces.Knight, At(s, "a5")?.Def);
    }

    [Fact]
    public void Tidal_wave_strength_two_rechecks_the_area_each_step()
    {
        // Area: ranks 6 and 7. The knight goes a6 → a7 (still in the area) → a8. The bishop on a5 is never pulled.
        var s = Rogue("7k/8/N7/B7/8/8/8/7K w - - 0 1");
        AddEffect(s, new TurnDelayCondition(1), new TidalWaveEffect(WaveDirection.North, Rows(8, 6, 7), strength: 2));

        Play(s, "h1g1");

        Assert.Equal(StandardPieces.Knight, At(s, "a8")?.Def);
        Assert.Equal(StandardPieces.Bishop, At(s, "a5")?.Def);
    }

    [Fact]
    public void Tidal_wave_strength_two_washes_out_two_rows()
    {
        var s = Rogue("N6k/N7/8/8/8/8/8/7K w - - 0 1");
        AddEffect(s, new TurnDelayCondition(1), Wave(WaveDirection.North, 2, "a7", "a8"));

        Play(s, "h1g1");

        Assert.Null(At(s, "a7"));
        Assert.Null(At(s, "a8"));
        Assert.DoesNotContain(s.PiecesOnBoard(), p => p.Def == StandardPieces.Knight);
    }

    [Fact]
    public void Tidal_wave_ignores_affected_squares_off_the_board()
    {
        var s = Rogue("7k/8/8/8/3R4/8/8/7K w - - 0 1");
        AddEffect(s, new TurnDelayCondition(1), new TidalWaveEffect(WaveDirection.North, new[] { (10, 10), (3, 3) }));

        Play(s, "h1g1");

        Assert.Equal(StandardPieces.Rook, At(s, "d5")?.Def);
    }

    [Fact]
    public void Tidal_wave_washing_out_a_king_ends_the_game()
    {
        var s = Rogue("7k/8/8/8/8/8/8/K7 w - - 0 1");
        AddEffect(s, new TurnDelayCondition(1), Wave(WaveDirection.North, 1, "h8"));

        Play(s, "a1b1");

        Assert.Null(At(s, "h8"));
        Assert.NotEqual(GameOutcome.Ongoing, s.Rules.Evaluate(s));
    }

    [Fact]
    public void Tidal_wave_rejects_a_strength_below_one()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Wave(WaveDirection.North, 0, "a1"));

    [Fact] // a long chain would hit the resolver's wave limit and silently roll back the whole turn end
    public void Tidal_wave_strength_is_capped()
    {
        Assert.Equal(32, Wave(WaveDirection.North, TidalWaveEffect.MaxStrength, "a1").Strength);
        Assert.Throws<ArgumentOutOfRangeException>(() => Wave(WaveDirection.North, TidalWaveEffect.MaxStrength + 1, "a1"));
    }

    // ------------------------------------------------------------------ Maps

    private static string LegacyMap(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "maps", "legacy"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "maps", "legacy", name + ".json"));
    }

    [Theory]
    [InlineData("wind gust effect test", "WindGust(White, freeze 2) when TurnInterval(every 4 from 4)")]
    [InlineData("walls placement effect test", "WallsPlacement(3) when TurnInterval(every 2 from 2)")]
    [InlineData("checkerboard wipe test", "CheckerboardWipe(White, freeze 2, alternating) when TurnInterval(every 3 from 3)")]
    public void Legacy_board_effect_maps_load_their_effect(string map, string key)
    {
        var s = MapLoader.Load(LegacyMap(map), ContentLibrary.CreateCatalog());
        var g = Assert.Single(s.Globals);
        Assert.Equal("board-effect:" + key, g.Def.Id);
    }

    [Fact]
    public void Bad_board_effect_entries_are_reported()
    {
        const string json = """
            { "width": 4, "height": 4, "pieces": [], "boardEffects": [
              { "condition": { "type": "Moon" }, "effect": { "type": "WindGust" } } ] }
            """;
        var e = Assert.Throws<MapLoadException>(() => MapLoader.Load(json, ContentLibrary.CreateCatalog()));
        Assert.Contains(e.Problems, p => p.Contains("unknown board-effect condition 'Moon'"));
    }

    private static string TidalMap(string effectParams) => $$"""
        { "width": 8, "height": 8, "pieces": [
            { "type": "King", "color": "White", "x": 0, "y": 0 }, { "type": "King", "color": "Black", "x": 7, "y": 7 } ],
          "boardEffects": [ { "condition": { "type": "TurnInterval", "params": { "interval": 2 } },
                              "effect": { "type": "TidalWave", "params": {{effectParams}} } } ] }
        """;

    [Fact]
    public void Tidal_wave_loads_from_a_map()
    {
        var s = MapLoader.Load(TidalMap("""{ "direction": "North", "squares": ["c6", "a6", "b6"] }"""), ContentLibrary.CreateCatalog());
        var g = Assert.Single(s.Globals);
        Assert.Equal("board-effect:TidalWave(North, a6 b6 c6, strength 1) when TurnInterval(every 2 from 2)", g.Def.Id);

        var strong = MapLoader.Load(TidalMap("""{ "direction": "west", "squares": ["a1"], "strength": 3 }"""), ContentLibrary.CreateCatalog());
        Assert.Equal(3, ((TidalWaveEffect)((BoardEffectDef)Assert.Single(strong.Globals).Def).Effect).Strength);
    }

    [Theory]
    [InlineData("""{ "squares": ["a1"] }""")]
    [InlineData("""{ "direction": "up", "squares": ["a1"] }""")]
    [InlineData("""{ "direction": "north" }""")]
    [InlineData("""{ "direction": "north", "squares": [] }""")]
    [InlineData("""{ "direction": "north", "squares": ["9z"] }""")]
    [InlineData("""{ "direction": "north", "squares": ["a1"], "strength": 0 }""")]
    [InlineData("""{ "direction": "north", "squares": ["a1"], "strength": 100 }""")]
    public void Bad_tidal_wave_entries_are_reported(string effectParams)
    {
        var e = Assert.Throws<MapLoadException>(() => MapLoader.Load(TidalMap(effectParams), ContentLibrary.CreateCatalog()));
        Assert.Contains(e.Problems, p => p.Contains("TidalWave") && !p.Contains("unknown board effect"));
    }

    [Fact]
    public void Random_games_with_board_effects_keep_hash_and_undo_exact()
    {
        var rng = new Random(1234);
        var s = Rogue("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w - - 0 1");
        s.SeedRandom(99);
        // Mild settings: a heavy freeze can leave a side with no legal moves, which ends the game as a draw.
        s.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(6), new WindGustEffect(Side.White, 1)));
        s.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(8, 4), new CheckerboardWipeEffect(SquareColor.Black, 1, alternating: true)));
        s.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(5), new WallsPlacementEffect(2)));
        s.AddGlobal(new BoardEffectDef(new BoardValueCondition(Side.Black, 30, 2), new WallsPlacementEffect(1)));
        s.AddGlobal(new BoardEffectDef(new TurnDelayCondition(9), new WindGustEffect(Side.Black, 1)));
        s.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(7), new TidalWaveEffect(WaveDirection.West, Rows(8, 3, 4), strength: 2)));
        s.ClearHistory();
        string start = s.Dump();
        ulong startHash = s.Hash;

        var moves = new List<Move>();
        int played = 0;
        for (int ply = 0; ply < 80; ply++)
        {
            moves.Clear();
            MoveGenerator.GenerateLegal(s, moves);
            if (moves.Count == 0 || s.Rules.Evaluate(s) != GameOutcome.Ongoing) break;
            var result = Game.PlayTurn(s, moves[rng.Next(moves.Count)]);
            Assert.True(result.Accepted, result.RejectReason);
            Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
            played++;
        }

        Assert.True(played > 10, $"stopped after {played}: {s.Rules.Evaluate(s)}\n{s.Dump()}");
        s.UndoTo(0);
        Assert.Equal(startHash, s.Hash);
        Assert.Equal(start, s.Dump());
    }
}
