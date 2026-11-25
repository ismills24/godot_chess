using System;
using System.Collections.Generic;
using System.Linq;
using RogueChess.Engine.AI;
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

/// <summary>
/// Independent verification of the status effects (Frozen, OnFire), the Fire/Freeze tiles, the casters and the
/// board effects against the prototype (GigaChess/rogue-chess @ 164be05). Every ply is checked for an exact
/// incremental hash, and every test that plays turns unwinds them one by one at the end, comparing the full dump.
/// </summary>
public class Verify_StatusAndCasterTests
{
    private static GameState Rogue(string fen) => Fen.Parse(fen, RuleSet.Rogue);
    private static int Sq(IBoardView s, string name) => MoveText.ParseSquare(s, name);
    private static Piece? At(GameState s, string name) => s.PieceAt(Sq(s, name));
    private static Piece Must(GameState s, string name) => At(s, name) ?? throw new InvalidOperationException($"no piece on {name}");
    private static Tile TileOn(GameState s, string square) => s.TileAt(Sq(s, square));
    private static int Frozen(Piece p) => p.GetAbility(FrozenAbility.Instance)?.Var(FrozenAbility.TurnsLeftVar) ?? 0;
    private static int Burning(Piece p) => p.GetAbility(OnFireAbility.Instance)?.Var(OnFireAbility.TurnsLeftVar) ?? 0;
    private static int Walls(GameState s) => Enumerable.Range(0, s.Area).Count(sq => s.TileAt(sq).Def == WallTile.Instance);

    private static List<string> Moves(GameState s, string from)
    {
        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(s, moves);
        return moves.Where(m => m.From == Sq(s, from)).Select(m => MoveText.Format(s, m)).ToList();
    }

    private static List<string> Casts(GameState s, string from) => Moves(s, from).Where(m => m.Contains('@')).ToList();

    private static GlobalInstance Effect(GameState s, BoardCondition condition, BoardEffect effect)
    {
        var g = s.AddGlobal(new BoardEffectDef(condition, effect));
        s.ClearHistory();
        return g;
    }

    private static void Grant(GameState s, string square, AbilityDef ability, params int[] vars)
    {
        s.GrantAbility(Must(s, square), ability, vars);
        s.ClearHistory();
    }

    private static void SetTile(GameState s, string square, TileDef tile, params int[] vars)
    {
        s.SetTile(Sq(s, square), tile, vars);
        s.ClearHistory();
    }

    /// <summary>
    /// Plays turns, checking the hash after each ply, and remembers the state before each one so
    /// <see cref="UnwindAll"/> can undo them one at a time and compare the exact dump and hash.
    /// </summary>
    private sealed class Line
    {
        private readonly GameState _s;
        private readonly Stack<(int Mark, string Dump, ulong Hash)> _before = new();

        public Line(GameState s) => _s = s;

        public TurnResult Play(string move)
        {
            var m = MoveText.FindLegal(_s, move) ?? throw new InvalidOperationException($"{move} is not legal:\n{_s.Dump()}");
            return Play(m);
        }

        public TurnResult Play(Move m)
        {
            _before.Push((_s.UndoDepth, _s.Dump(), _s.Hash));
            var result = Game.PlayTurn(_s, m, new ResolveOptions(Trace: true));
            Assert.True(result.Accepted, result.RejectReason);
            Assert.Equal(_s.ComputeHashFromScratch(), _s.Hash);
            return result;
        }

        public void UnwindAll()
        {
            while (_before.Count > 0)
            {
                var (mark, dump, hash) = _before.Pop();
                _s.UndoTo(mark);
                Assert.Equal(hash, _s.Hash);
                Assert.Equal(dump, _s.Dump());
            }
        }
    }

    // ================================================================== Frozen

    [Theory]
    [InlineData(1, 2)] // prototype: the old freeze (1) thaws this turn end, the new wrap (2) is not ticked → 2
    [InlineData(2, 2)] // old 2 → 1, new 2 → 2
    [InlineData(4, 3)] // old 4 → 3, new 2 → 3
    public void Wind_refreeze_of_an_already_frozen_piece_at_its_owners_turn_end_matches_the_prototype(int before, int expected)
    {
        // White knight c5 (already frozen) is blocked by a black pawn on c4 at the end of White's own turn.
        // In the prototype the new Frozen wrap isn't ticked by the turn end it is created in, while the old one is:
        // the piece ends up frozen for max(before - 1, freezeDuration).
        var s = Rogue("7k/8/8/2N5/2p5/8/8/7K w - - 0 1");
        Grant(s, "c5", FrozenAbility.Instance, before);
        Effect(s, new TurnIntervalCondition(1), new WindGustEffect(Side.White, freezeDuration: 2));
        var line = new Line(s);

        line.Play("h1g1");

        Assert.Equal(expected, Frozen(Must(s, "c5")));
        Assert.Equal(2, Frozen(Must(s, "c4"))); // Black's piece: no tick at White's turn end
        line.UnwindAll();
    }

    [Fact]
    public void Wind_freeze_of_a_fresh_piece_at_its_owners_turn_end_lasts_its_full_duration()
    {
        // Frozen for 2 at the end of White's turn: the knight sits out White's next 2 turns.
        var s = Rogue("7k/8/8/2N5/2p5/8/8/7K w - - 0 1");
        Effect(s, new TurnDelayCondition(1), new WindGustEffect(Side.White, freezeDuration: 2));
        var line = new Line(s);

        line.Play("h1g1");
        Assert.Equal(2, Frozen(Must(s, "c5")));
        line.Play("h8g8");
        Assert.Empty(Moves(s, "c5"));
        line.Play("g1h1");
        Assert.Equal(1, Frozen(Must(s, "c5")));
        line.Play("g8h8");
        Assert.Empty(Moves(s, "c5"));
        line.Play("h1g1");
        Assert.False(Must(s, "c5").HasAbility(FrozenAbility.Instance));
        line.Play("h8g8");
        Assert.NotEmpty(Moves(s, "c5"));
        line.UnwindAll();
    }

    [Fact]
    public void A_frozen_piece_can_still_be_captured()
    {
        var s = Rogue("r6k/8/8/8/8/8/8/R6K b - - 0 1");
        Grant(s, "a1", FrozenAbility.Instance, 3);
        var line = new Line(s);
        line.Play("a8a1");
        Assert.Equal(Side.Black, Must(s, "a1").Side);
        line.UnwindAll();
    }

    [Fact]
    public void A_side_whose_only_pieces_are_frozen_has_no_moves_and_the_game_is_a_draw()
    {
        var s = Rogue("7k/8/8/8/8/8/8/K7 w - - 0 1");
        Grant(s, "a1", FrozenAbility.Instance, 2);
        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(s, moves);
        Assert.Empty(moves);
        Assert.Equal(GameOutcome.Draw, s.Rules.Evaluate(s));
    }

    [Fact]
    public void A_zero_turn_freeze_does_not_immobilize_and_goes_away_at_the_owners_turn_end()
    {
        // Prototype: Frozen(0) returns the normal moves (0 is not > 0) and is removed at the next owner turn end.
        var s = Rogue("7k/8/8/8/8/8/8/R6K w - - 0 1");
        Grant(s, "a1", FrozenAbility.Instance, 0);
        var line = new Line(s);
        Assert.Contains("a1a8", Moves(s, "a1"));
        line.Play("a1a2");
        Assert.False(Must(s, "a2").HasAbility(FrozenAbility.Instance));
        line.UnwindAll();
    }

    [Fact]
    public void A_frozen_burning_piece_still_burns()
    {
        var s = Rogue("7k/8/8/8/8/8/8/R6K w - - 0 1");
        Grant(s, "a1", FrozenAbility.Instance, 1);
        Grant(s, "a1", OnFireAbility.Instance, 1);
        var line = new Line(s);
        var rook = Must(s, "a1");

        var result = line.Play("h1g1"); // both reach zero at this turn end: the destroy wins over the thaw
        Assert.False(rook.IsOnBoard);
        Assert.Contains(result.Trace!.AllFacts, f => f.Kind == FactKinds.Died && f.Piece == rook && f.Cause == Causes.Burned);
        line.UnwindAll();
    }

    // ================================================================== OnFire

    [Fact]
    public void A_burning_king_burns_and_its_side_loses()
    {
        var s = Rogue("7k/8/8/8/8/8/8/K7 w - - 0 1");
        Grant(s, "a1", OnFireAbility.Instance, 1);
        var line = new Line(s);
        line.Play("a1a2");
        Assert.Null(At(s, "a2"));
        Assert.Equal(GameOutcome.BlackWins, s.Rules.Evaluate(s));
        line.UnwindAll();
    }

    [Fact]
    public void A_burning_piece_captured_before_it_burns_is_simply_gone()
    {
        var s = Rogue("r6k/8/8/8/8/8/8/R6K b - - 0 1");
        Grant(s, "a1", OnFireAbility.Instance, 2);
        var line = new Line(s);
        line.Play("a8a1");
        line.Play("h1g1");
        line.Play("a1a2");
        Assert.Equal(StandardPieces.Rook, Must(s, "a2").Def);
        Assert.Equal(Side.Black, Must(s, "a2").Side);
        line.UnwindAll();
    }

    [Fact]
    public void The_on_fire_and_frozen_decorators_from_a_map_default_to_three_turns()
    {
        const string json = """
            { "width": 8, "height": 8, "startingPlayer": "White",
              "pieces": [
                { "type": "King", "color": "White", "x": 0, "y": 0, "decorators": [] },
                { "type": "King", "color": "Black", "x": 7, "y": 7, "decorators": [] },
                { "type": "Rook", "color": "White", "x": 3, "y": 3, "decorators": ["OnFire", "Frozen"] } ] }
            """;
        var s = MapLoader.Load(json, ContentLibrary.CreateCatalog());
        Assert.Equal(3, Burning(Must(s, "d4")));
        Assert.Equal(3, Frozen(Must(s, "d4")));
    }

    // ================================================================== Fire and Freeze tiles

    [Fact]
    public void Capturing_onto_fire_sets_the_capturer_alight_but_map_setup_does_not_light_the_victim()
    {
        var s = Rogue("7k/8/8/p7/8/8/8/R6K w - - 0 1");
        SetTile(s, "a5", FireTile.Instance);
        Assert.False(Must(s, "a5").HasAbility(OnFireAbility.Instance));
        var line = new Line(s);

        line.Play("a1a5");
        Assert.Equal(2, Burning(Must(s, "a5")));
        line.UnwindAll();
    }

    [Fact]
    public void A_knight_landing_on_fire_catches_fire()
    {
        var s = Rogue("7k/8/8/8/8/8/8/1N5K w - - 0 1");
        SetTile(s, "c3", FireTile.Instance);
        var line = new Line(s);
        line.Play("b1c3");
        Assert.Equal(2, Burning(Must(s, "c3")));
        line.UnwindAll();
    }

    [Fact]
    public void A_pawn_double_push_across_fire_catches_fire()
    {
        // The prototype's pawn moves are SLIDE moves, so the skipped square counts as "passed through".
        var s = Rogue("7k/8/8/8/8/8/P7/7K w - - 0 1");
        SetTile(s, "a3", FireTile.Instance);
        var line = new Line(s);
        line.Play("a2a4");
        Assert.Equal(2, Burning(Must(s, "a4")));
        line.UnwindAll();
    }

    [Fact]
    public void Sliding_across_fire_and_ice_gives_both_and_the_frozen_piece_still_burns()
    {
        var s = Rogue("7k/8/8/8/8/8/8/R6K w - - 0 1");
        SetTile(s, "a3", FireTile.Instance);
        SetTile(s, "a5", FreezeTile.Instance);
        var line = new Line(s);
        var rook = Must(s, "a1");

        line.Play("a1a7");
        Assert.Equal(2, Burning(rook));
        Assert.Equal(2, Frozen(rook));
        line.Play("h8g8");
        Assert.Empty(Moves(s, "a7"));
        line.Play("h1g1");
        Assert.Equal(1, Burning(rook));
        line.Play("g8h8");
        line.Play("g1h1");
        Assert.False(rook.IsOnBoard);
        line.UnwindAll();
    }

    [Fact]
    public void A_one_turn_fire_tile_burns_the_mover_at_the_end_of_that_same_turn()
    {
        // FireTile(duration, fireAbilityTurns = 1): OnFire(1) is applied on entry and ticks to 0 at the owner's turn end.
        var s = Rogue("7k/8/8/8/8/8/8/R6K w - - 0 1");
        SetTile(s, "a5", FireTile.Instance, 3, 1);
        var line = new Line(s);
        line.Play("a1a5");
        Assert.Null(At(s, "a5"));
        line.UnwindAll();
    }

    [Fact]
    public void A_fire_tile_nobody_enters_goes_out_after_three_turn_ends_and_then_lights_nobody()
    {
        var s = Rogue("7k/8/8/8/8/8/8/R6K w - - 0 1");
        SetTile(s, "a5", FireTile.Instance);
        var line = new Line(s);
        line.Play("h1g1");
        line.Play("h8g8");
        Assert.Same(FireTile.Instance, TileOn(s, "a5").Def);
        Assert.Equal(1, TileOn(s, "a5").Var(StatusTile.TurnsLeftVar));
        line.Play("g1h1");
        Assert.True(TileOn(s, "a5").Def.IsDefault);
        line.Play("g8h8");
        line.Play("a1a5");
        Assert.False(Must(s, "a5").HasAbility(OnFireAbility.Instance));
        line.UnwindAll();
    }

    [Fact]
    public void A_frozen_piece_entering_ice_is_not_refreshed()
    {
        // Pushed by the wind at the end of Black's turn onto a freeze tile while already frozen (White's piece, no tick).
        var s = Rogue("7k/8/8/R7/8/8/8/7K b - - 0 1");
        Grant(s, "a5", FrozenAbility.Instance, 1);
        SetTile(s, "a4", FreezeTile.Instance);
        Effect(s, new TurnIntervalCondition(1), new WindGustEffect(Side.White));
        var line = new Line(s);

        line.Play("h8g8");
        Assert.Same(StandardPieces.Rook, At(s, "a4")?.Def);
        Assert.Equal(1, Frozen(Must(s, "a4")));
        line.UnwindAll();
    }

    // ================================================================== Wind gust details

    [Fact]
    public void Wind_pushing_a_piece_onto_ice_at_its_owners_turn_end_freezes_it_for_the_full_status()
    {
        // The freeze is applied after this turn end's ticks, so it is not ticked (prototype: listeners are collected
        // at the start of the turn-end resolution).
        var s = Rogue("7k/8/8/R7/8/8/8/7K w - - 0 1");
        SetTile(s, "a4", FreezeTile.Instance);
        Effect(s, new TurnIntervalCondition(1), new WindGustEffect(Side.White));
        var line = new Line(s);

        line.Play("h1g1");
        Assert.Equal(3, Frozen(Must(s, "a4")));
        Assert.Equal(2, TileOn(s, "a4").Var(StatusTile.TurnsLeftVar));
        line.UnwindAll();
    }

    [Fact]
    public void Wind_pushing_an_enemy_onto_fire_sets_it_alight_and_it_burns_on_its_own_turn_ends()
    {
        var s = Rogue("K7/8/8/r7/8/8/8/7k w - - 0 1");
        SetTile(s, "a4", FireTile.Instance);
        Effect(s, new TurnDelayCondition(1), new WindGustEffect(Side.Black));
        var line = new Line(s);
        var rook = Must(s, "a5");

        line.Play("a8b8");
        Assert.Same(rook, At(s, "a4"));
        Assert.Equal(3, Burning(rook));
        line.Play("a4a3");
        Assert.Equal(2, Burning(rook));
        line.Play("b8a8");
        line.Play("a3a2");
        line.Play("a8b8");
        line.Play("a2a3");
        Assert.False(rook.IsOnBoard);
        line.UnwindAll();
    }

    [Fact]
    public void Wind_pushing_a_piece_onto_a_wall_breaks_the_wall()
    {
        var s = Rogue("7k/8/8/R7/8/8/8/7K w - - 0 1");
        SetTile(s, "a4", WallTile.Instance);
        Effect(s, new TurnIntervalCondition(1), new WindGustEffect(Side.White));
        var line = new Line(s);
        line.Play("h1g1");
        Assert.Same(StandardPieces.Rook, At(s, "a4")?.Def);
        Assert.True(TileOn(s, "a4").Def.IsDefault);
        line.UnwindAll();
    }

    [Fact]
    public void Wind_pushes_frozen_pieces_too()
    {
        var s = Rogue("7k/8/8/R7/8/8/8/7K w - - 0 1");
        Grant(s, "a5", FrozenAbility.Instance, 3);
        Effect(s, new TurnIntervalCondition(1), new WindGustEffect(Side.White));
        var line = new Line(s);
        line.Play("h1g1");
        Assert.Same(StandardPieces.Rook, At(s, "a4")?.Def);
        Assert.Equal(2, Frozen(Must(s, "a4")));
        line.UnwindAll();
    }

    [Fact]
    public void Wind_blocked_by_an_own_piece_freezes_both()
    {
        var s = Rogue("7k/8/8/R7/P7/8/8/7K w - - 0 1");
        Effect(s, new TurnIntervalCondition(1), new WindGustEffect(Side.White, freezeDuration: 3));
        var line = new Line(s);
        line.Play("h1g1");
        Assert.Equal(3, Frozen(Must(s, "a5")));
        Assert.Equal(3, Frozen(Must(s, "a4")));
        line.UnwindAll();
    }

    [Fact]
    public void Wind_pushing_a_black_pawn_onto_rank_one_promotes_it()
    {
        // Prototype: Pawn listens to every MoveEvent (the push included) and promotes on its last rank.
        var s = Rogue("K7/8/8/8/8/8/1p6/7k w - - 0 1");
        Effect(s, new TurnIntervalCondition(1), new WindGustEffect(Side.Black));
        var line = new Line(s);
        line.Play("a8b8");
        Assert.Equal(StandardPieces.Queen, At(s, "b1")?.Def);
        Assert.Equal(StandardPieces.King, At(s, "h1")?.Def); // on the bottom rank: stays
        line.UnwindAll();
    }

    [Fact]
    public void Two_board_effects_on_the_same_turn_read_the_same_board_whatever_their_order()
    {
        // Rook a5 is on a "white" square (x + y = 4). The wipe reads the board before the push (as in the
        // prototype, where every effect of the listener is applied to the same context), so the pushed rook is frozen.
        foreach (bool windFirst in new[] { true, false })
        {
            var s = Rogue("7k/8/8/R7/8/8/8/K7 w - - 0 1");
            var wind = new BoardEffectDef(new TurnIntervalCondition(1), new WindGustEffect(Side.White));
            var wipe = new BoardEffectDef(new TurnIntervalCondition(1), new CheckerboardWipeEffect(SquareColor.White, 2));
            s.AddGlobal(windFirst ? wind : wipe);
            s.AddGlobal(windFirst ? wipe : wind);
            s.ClearHistory();
            var line = new Line(s);

            line.Play("a1b1"); // b1 = (1,0): odd, not wiped
            Assert.Same(StandardPieces.Rook, At(s, "a4")?.Def);
            Assert.Equal(2, Frozen(Must(s, "a4")));
            Assert.False(Must(s, "b1").HasAbility(FrozenAbility.Instance));
            Assert.Equal(2, Frozen(Must(s, "h8"))); // (7,7): even
            line.UnwindAll();
        }
    }

    // ================================================================== Checkerboard wipe details

    [Fact]
    public void Checkerboard_wipe_skips_already_frozen_pieces_and_does_not_tick_fresh_ones()
    {
        // An odd-square wipe at White's turn end: black rook a8 (0,7) already frozen 5 stays 5; white rook b1 (1,0)
        // gets 2 and is not ticked by the turn end it was frozen in; the white king left a2 for a3 (0,2: even).
        var s = Rogue("r6k/8/8/8/8/8/K7/1R6 w - - 0 1");
        Grant(s, "a8", FrozenAbility.Instance, 5);
        Effect(s, new TurnIntervalCondition(1), new CheckerboardWipeEffect(SquareColor.Black, 2));
        var line = new Line(s);

        line.Play("a2a3"); // the king leaves for (0,2): even
        Assert.Equal(5, Frozen(Must(s, "a8")));
        Assert.Equal(2, Frozen(Must(s, "b1")));
        Assert.False(Must(s, "a3").HasAbility(FrozenAbility.Instance));
        line.UnwindAll();
    }

    // ================================================================== Walls placement details

    [Fact]
    public void Walls_placement_replaces_any_non_wall_tile_but_never_under_a_piece_even_a_ticking_fire_tile()
    {
        var s = Rogue("7k/8/8/8/8/8/8/K7 w - - 0 1");
        SetTile(s, "d4", FireTile.Instance);          // 3 turns: ticks (AddVar) in the same wave the wall lands
        SetTile(s, "e4", FireTile.Instance, 1, 3);    // last turn: expires (ChangeTile) in the same wave
        SetTile(s, "c5", GuardianTile.Instance);
        SetTile(s, "h7", WallTile.Instance);
        SetTile(s, "a2", FreezeTile.Instance);        // the king will stand here
        s.SeedRandom(3);
        Effect(s, new TurnIntervalCondition(1), new WallsPlacementEffect(64));
        var line = new Line(s);

        line.Play("a1a2");
        foreach (var sq in new[] { "d4", "e4", "c5", "h7", "a1" }) Assert.Same(WallTile.Instance, TileOn(s, sq).Def);
        Assert.Equal(0, TileOn(s, "d4").VarCount);
        Assert.Same(FreezeTile.Instance, TileOn(s, "a2").Def); // under the king
        Assert.True(TileOn(s, "h8").Def.IsDefault);            // under the black king
        Assert.Equal(62, Walls(s));
        line.Play("h8g8"); // a piece may land on a wall (it breaks)
        Assert.True(TileOn(s, "g8").Def.IsDefault);
        line.UnwindAll();
    }

    [Fact]
    public void Walls_placement_with_a_single_free_square_uses_it()
    {
        var s = Rogue("7k/8/8/8/8/8/8/K7 w - - 0 1");
        for (int sq = 0; sq < s.Area; sq++) if (s.PieceAt(sq) == null) s.SetTile(sq, WallTile.Instance);
        s.ClearHistory();
        var g = Effect(s, new TurnIntervalCondition(1), new WallsPlacementEffect(1));
        var line = new Line(s);
        line.Play("a1a2"); // breaks a2; a1 is now free and becomes the wall
        Assert.Same(WallTile.Instance, TileOn(s, "a1").Def);
        Assert.Equal(1, g.Var(BoardEffectDef.TimesFiredVar));
        line.UnwindAll();
    }

    [Fact]
    public void Two_walls_placements_on_one_turn_may_overlap_but_never_exceed_their_sum()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var s = Rogue("7k/8/8/8/8/8/8/K7 w - - 0 1");
            s.SeedRandom(seed);
            s.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(1), new WallsPlacementEffect(3)));
            s.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(1), new WallsPlacementEffect(3)));
            s.ClearHistory();
            var line = new Line(s);
            line.Play("a1a2");
            Assert.InRange(Walls(s), 3, 6);
            line.UnwindAll();
        }
    }

    // ================================================================== Conditions

    [Fact]
    public void Turn_interval_with_a_start_turn_before_the_interval_fires_from_it()
    {
        // new TurnIntervalCondition(3, 1): turns 1, 4, 7.
        var c = new TurnIntervalCondition(3, startTurn: 1);
        var s = Rogue("7k/8/8/8/8/8/8/K7 w - - 0 1");
        var fired = Enumerable.Range(1, 9).Where(t => c.IsMet(s, t, 0)).ToArray();
        Assert.Equal(new[] { 1, 4, 7 }, fired);
        Assert.Equal(new[] { 2, 4, 6, 8 }, Enumerable.Range(1, 9).Where(t => new TurnIntervalCondition(2).IsMet(s, t, 0)));
    }

    [Fact]
    public void Turn_delay_on_a_turn_that_never_ends_never_fires()
    {
        var s = Rogue("7k/8/8/8/8/8/8/K7 w - - 0 1");
        Effect(s, new TurnDelayCondition(0), new WallsPlacementEffect(1));
        var line = new Line(s);
        for (int i = 0; i < 4; i++) line.Play(s.SideToMove == Side.White ? (At(s, "a1") != null ? "a1b1" : "b1a1") : (At(s, "h8") != null ? "h8g8" : "g8h8"));
        Assert.Equal(0, Walls(s));
        line.UnwindAll();
    }

    [Fact]
    public void Board_value_counts_ability_values_frozen_included()
    {
        // Black rook (5) frozen (−2) = 3 < 4: fires. Without the status it would not.
        var s = Rogue("r6k/8/8/8/8/8/8/K7 w - - 0 1");
        Grant(s, "a8", FrozenAbility.Instance, 3);
        Assert.Equal(3, BoardValueCondition.BoardValue(s, Side.Black));
        Grant(s, "a8", CasterAbility.FireMage);
        Assert.Equal(5, BoardValueCondition.BoardValue(s, Side.Black));
        s.RevokeAbility(Must(s, "a8"), CasterAbility.FireMage);
        s.ClearHistory();
        var g = Effect(s, new BoardValueCondition(Side.Black, 4), new WallsPlacementEffect(1));
        var line = new Line(s);
        line.Play("a1a2");
        Assert.Equal(1, g.Var(BoardEffectDef.TimesFiredVar));
        line.UnwindAll();
    }

    [Fact]
    public void A_cast_is_a_full_turn_that_ends_with_turn_end_board_effects()
    {
        var s = Rogue("7k/8/8/8/3B4/8/8/K7 w - - 0 1");
        Grant(s, "d4", CasterAbility.FireMage);
        var g = Effect(s, new TurnDelayCondition(1), new CheckerboardWipeEffect(SquareColor.White, 2));
        var line = new Line(s);
        line.Play("d4d6@fire-mage");
        Assert.Equal(1, s.Ply);
        Assert.Equal(Side.Black, s.SideToMove);
        Assert.Equal(1, g.Var(BoardEffectDef.TimesFiredVar));
        Assert.Equal(2, Frozen(Must(s, "d4"))); // (3,3): even, a "white" square
        Assert.Equal(2, Frozen(Must(s, "a1")));
        line.UnwindAll();
    }

    // ================================================================== Casters

    [Fact]
    public void A_caster_in_the_corner_only_targets_squares_on_the_board()
    {
        var s = Rogue("7k/8/8/8/8/8/8/K7 w - - 0 1");
        Grant(s, "a1", CasterAbility.FireMage);
        var casts = Casts(s, "a1").Select(c => c[2..4]).OrderBy(x => x).ToList();
        Assert.Equal(new[] { "a3", "b3", "c1", "c2", "c3" }, casts); // a2, b1, b2 are king moves
    }

    [Fact]
    public void A_caster_on_the_h_file_does_not_wrap_to_the_a_file()
    {
        var s = Rogue("k7/8/8/8/7K/8/8/8 w - - 0 1");
        Grant(s, "h4", CasterAbility.FreezeMage);
        var casts = Casts(s, "h4").Select(c => c[2..4]).ToList();
        Assert.Equal(9, casts.Count);
        Assert.All(casts, c => Assert.True(c[0] is 'f' or 'g' or 'h', c));
    }

    [Fact]
    public void Casts_skip_squares_with_any_piece()
    {
        var s = Rogue("7k/8/8/3p4/3B4/8/3P4/K7 w - - 0 1");
        Grant(s, "d4", CasterAbility.WallPlacement);
        var casts = Casts(s, "d4");
        Assert.DoesNotContain("d4d5@wall-placement", casts);
        Assert.DoesNotContain("d4d2@wall-placement", casts);
        Assert.Equal(14, casts.Count);
    }

    [Fact]
    public void A_pawn_caster_may_cast_on_its_empty_diagonals_but_not_its_pushes()
    {
        var s = Rogue("7k/8/8/8/8/8/3P4/K7 w - - 0 1");
        Grant(s, "d2", CasterAbility.FireMage);
        var casts = Casts(s, "d2");
        Assert.Contains("d2c3@fire-mage", casts);
        Assert.Contains("d2e3@fire-mage", casts);
        Assert.DoesNotContain("d2d3@fire-mage", casts);
        Assert.DoesNotContain("d2d4@fire-mage", casts);
        Assert.Equal(17, casts.Count);
    }

    [Fact]
    public void A_caster_can_cast_after_an_earlier_status_thaws_and_shifts_its_ability_index()
    {
        var s = Rogue("7k/8/8/8/3B4/8/8/K7 w - - 0 1");
        Grant(s, "d4", FrozenAbility.Instance, 1);
        Grant(s, "d4", CasterAbility.FireMage);
        var line = new Line(s);
        Assert.Empty(Moves(s, "d4"));
        line.Play("a1a2"); // thaws: FireMage moves from index 1 to 0
        line.Play("h8g8");
        line.Play("d4d6@fire-mage");
        Assert.Same(FireTile.Instance, TileOn(s, "d6").Def);
        line.UnwindAll();
    }

    [Fact]
    public void An_enemy_walking_into_a_fresh_fire_cast_catches_fire()
    {
        var s = Rogue("r6k/8/8/8/8/8/8/1B5K w - - 0 1");
        Grant(s, "b1", CasterAbility.FireMage);
        var line = new Line(s);
        line.Play("b1a3@fire-mage");
        Assert.Equal(3, TileOn(s, "a3").Var(StatusTile.TurnsLeftVar));
        line.Play("a8a2"); // slides across a3
        Assert.Equal(2, Burning(Must(s, "a2")));
        line.UnwindAll();
    }

    // ================================================================== MapLoader JSON (params and defaults)

    private static GameState LoadEffects(string effectsJson) => MapLoader.Load($$"""
        { "width": 8, "height": 8, "startingPlayer": "White",
          "pieces": [
            { "type": "King", "color": "White", "x": 0, "y": 0, "decorators": [] },
            { "type": "King", "color": "Black", "x": 7, "y": 7, "decorators": [] } ],
          "boardEffects": [ {{effectsJson}} ] }
        """, ContentLibrary.CreateCatalog());

    [Theory]
    // defaults (prototype BoardEffectFactory)
    [InlineData("""{ "condition": { "type": "TurnInterval" }, "effect": { "type": "WallsPlacement" } }""",
        "WallsPlacement(1) when TurnInterval(every 1 from 1)")]
    [InlineData("""{ "condition": { "type": "TurnDelay" }, "effect": { "type": "WindGust" } }""",
        "WindGust(White, freeze 2) when TurnDelay(1)")]
    [InlineData("""{ "condition": { "type": "BoardValue" }, "effect": { "type": "CheckerboardWipe" } }""",
        "CheckerboardWipe(White, freeze 2) when BoardValue(White < 10, max -1)")]
    [InlineData("""{ "condition": { "type": "TurnInterval", "params": {} }, "effect": { "type": "WindGust", "params": {} } }""",
        "WindGust(White, freeze 2) when TurnInterval(every 1 from 1)")]
    // every parameter
    [InlineData("""{ "condition": { "type": "TurnInterval", "params": { "interval": 3, "startTurn": 1 } }, "effect": { "type": "WallsPlacement", "params": { "wallCount": 4, "sourceId": "x" } } }""",
        "WallsPlacement(4) when TurnInterval(every 3 from 1)")]
    [InlineData("""{ "condition": { "type": "TurnDelay", "params": { "turnNumber": 7 } }, "effect": { "type": "WindGust", "params": { "targetPlayer": "black", "freezeDuration": 4 } } }""",
        "WindGust(Black, freeze 4) when TurnDelay(7)")]
    [InlineData("""{ "condition": { "type": "BoardValue", "params": { "color": "Black", "threshold": 3, "maxTriggers": 2 } }, "effect": { "type": "CheckerboardWipe", "params": { "targetColor": "BLACK", "freezeDuration": 1, "alternating": true } } }""",
        "CheckerboardWipe(Black, freeze 1, alternating) when BoardValue(Black < 3, max 2)")]
    // prototype quirks: targetPlayer/color other than "black" mean White; a targetColor other than "white" behaves as black
    [InlineData("""{ "condition": { "type": "BoardValue", "params": { "color": "purple" } }, "effect": { "type": "WindGust", "params": { "targetPlayer": "WHITE" } } }""",
        "WindGust(White, freeze 2) when BoardValue(White < 10, max -1)")]
    [InlineData("""{ "condition": { "type": "TurnInterval" }, "effect": { "type": "CheckerboardWipe", "params": { "targetColor": "red" } } }""",
        "CheckerboardWipe(Black, freeze 2) when TurnInterval(every 1 from 1)")]
    public void Board_effect_json_params_and_defaults(string entry, string key)
    {
        var s = LoadEffects(entry);
        var g = Assert.Single(s.Globals);
        Assert.Equal("board-effect:" + key, g.Def.Id);
        Assert.Equal(0, g.Var(BoardEffectDef.TimesFiredVar));
    }

    [Fact]
    public void Board_effect_json_name_and_description_are_kept()
    {
        var s = LoadEffects("""{ "id": "e1", "priority": 5, "name": "Gale", "description": "Blows.", "condition": { "type": "TurnDelay" }, "effect": { "type": "WindGust" } }""");
        var def = Assert.IsType<BoardEffectDef>(Assert.Single(s.Globals).Def);
        Assert.Equal("Gale", def.Name);
        Assert.Equal("Blows.", def.Description);
    }

    [Fact]
    public void Json_turn_delay_default_fires_at_the_end_of_the_first_turn_only()
    {
        var s = LoadEffects("""{ "condition": { "type": "TurnDelay" }, "effect": { "type": "WallsPlacement", "params": { "wallCount": 2 } } }""");
        var line = new Line(s);
        line.Play("a1a2");
        Assert.Equal(2, Walls(s));
        line.Play("h8h7");
        line.Play("a2a3");
        Assert.InRange(Walls(s), 0, 2); // no new walls (a piece entering one breaks it)
        Assert.Equal(1, s.Globals[0].Var(BoardEffectDef.TimesFiredVar));
        line.UnwindAll();
    }

    [Fact]
    public void Json_zero_freeze_duration_is_accepted_and_harmless()
    {
        var s = LoadEffects("""{ "condition": { "type": "TurnInterval" }, "effect": { "type": "CheckerboardWipe", "params": { "freezeDuration": 0 } } }""");
        var line = new Line(s);
        line.Play("a1b1");
        line.Play("h8g8");
        Assert.NotEmpty(Moves(s, "b1"));
        line.Play("b1a1");
        line.UnwindAll();
    }

    [Fact]
    public void Json_turn_interval_of_zero_is_reported_as_a_problem()
    {
        var e = Assert.Throws<MapLoadException>(() => LoadEffects("""{ "condition": { "type": "TurnInterval", "params": { "interval": 0 } }, "effect": { "type": "WallsPlacement" } }"""));
        Assert.Contains(e.Problems, p => p.Contains("interval"));
    }

    // ================================================================== AI

    private static readonly AgentProfile Sharp = AgentProfile.Club with
    {
        Id = "sharp", ChoiceMargin = 0, RootNoise = 0, MaxDepth = 3, NodeBudget = 300_000, TimeBudgetMs = 0,
    };

    [Fact]
    public void The_ai_takes_a_free_pawn_but_not_one_standing_on_a_fire_that_would_burn_its_queen()
    {
        const string fen = "7k/8/8/3p4/8/8/8/3Q3K w - - 0 1";
        var plain = Rogue(fen);
        var decision = new AgentFactory().Create(Sharp).Decide(plain);
        Assert.Equal("d1d5", MoveText.Format(plain, decision.Move!.Value));

        var s = Rogue(fen);
        SetTile(s, "d5", FireTile.Instance, 3, 1); // entering burns at the end of the same turn
        string dump = s.Dump();
        ulong hash = s.Hash;
        decision = new AgentFactory().Create(Sharp).Decide(s);
        var chosen = MoveText.Format(s, decision.Move!.Value);
        Assert.NotEqual("d1d5", chosen);
        Assert.Equal(hash, s.Hash);
        Assert.Equal(dump, s.Dump());

        // and what it chose doesn't lose the queen
        Assert.True(Game.PlayTurn(s, decision.Move.Value).Accepted);
        Assert.Contains(s.PiecesOnBoard(Side.White), p => p.Def == StandardPieces.Queen);
    }

    [Fact]
    public void The_ai_sees_a_wind_gust_that_will_push_its_rook_onto_a_burning_square()
    {
        // At the end of White's turn the wind pushes White's furthest piece in each file down one rank. A rook left on
        // a5 is pushed onto a4 (fire that gives a one-turn burn) and burns at the end of White's next turn; moving down
        // the a-file onto or across a4 burns it at once. The AI has to move the rook off a5 another way.
        var s = Rogue("7k/8/8/R7/8/8/8/7K w - - 0 1");
        SetTile(s, "a4", FireTile.Instance, 5, 1);
        Effect(s, new TurnIntervalCondition(1), new WindGustEffect(Side.White));
        string dump = s.Dump();
        var decision = new AgentFactory().Create(Sharp).Decide(s);
        Assert.Equal(dump, s.Dump());
        var m = decision.Move!.Value;
        string chosen = MoveText.Format(s, m);
        Assert.True(Game.PlayTurn(s, m).Accepted, chosen);
        var rook = s.PiecesOnBoard(Side.White).Single(p => p.Def == StandardPieces.Rook);
        Assert.False(rook.HasAbility(OnFireAbility.Instance), chosen);
    }

    [Fact]
    public void Ai_search_over_casters_statuses_and_board_effects_leaves_the_state_exact()
    {
        var s = Rogue("r1b1k2r/ppp2ppp/2n5/3p4/3P4/2N5/PPP2PPP/R1B1K2R w - - 0 1");
        s.SeedRandom(5);
        s.GrantAbility(Must(s, "c1"), CasterAbility.FireMage);
        s.GrantAbility(Must(s, "c8"), CasterAbility.FreezeMage);
        s.GrantAbility(Must(s, "c3"), FrozenAbility.Instance, 2);
        s.GrantAbility(Must(s, "c6"), OnFireAbility.Instance, 2);
        s.SetTile(Sq(s, "e4"), FireTile.Instance);
        s.SetTile(Sq(s, "e5"), FreezeTile.Instance);
        s.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(2), new WindGustEffect(Side.Black, 1)));
        s.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(3, 1), new CheckerboardWipeEffect(SquareColor.White, 1, true)));
        s.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(1), new WallsPlacementEffect(1)));
        s.AddGlobal(new BoardEffectDef(new BoardValueCondition(Side.White, 100, 3), new WallsPlacementEffect(1)));
        s.ClearHistory();
        var line = new Line(s);

        var agent = new AgentFactory().Create(Sharp with { MaxDepth = 2, NodeBudget = 40_000 });
        for (int ply = 0; ply < 6 && s.Rules.Evaluate(s) == GameOutcome.Ongoing; ply++)
        {
            string dump = s.Dump();
            ulong hash = s.Hash;
            var decision = agent.Decide(s);
            Assert.Equal(hash, s.Hash);
            Assert.Equal(dump, s.Dump());
            if (decision.Move is not { } m) break;
            line.Play(m);
        }
        line.UnwindAll();
    }
}
