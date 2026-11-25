using System;
using System.Collections.Generic;
using System.IO;
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

/// <summary>The caster abilities (FireMage, FreezeMage, WallPlacement), the Fire and Freeze tiles and the OnFire status.</summary>
public class CasterTests
{
    private static GameState Rogue(string fen) => Fen.Parse(fen, RuleSet.Rogue);
    private static int Sq(IBoardView s, string name) => MoveText.ParseSquare(s, name);
    private static Piece? At(GameState s, string name) => s.PieceAt(Sq(s, name));
    private static Piece Must(GameState s, string name) => At(s, name) ?? throw new InvalidOperationException($"no piece on {name}");

    private static void Grant(GameState s, string square, params AbilityDef[] abilities)
    {
        foreach (var a in abilities) s.GrantAbility(Must(s, square), a);
        s.ClearHistory();
    }

    private static void Tile(GameState s, TileDef tile, params string[] squares)
    {
        foreach (var sq in squares) s.SetTile(Sq(s, sq), tile);
        s.ClearHistory();
    }

    private static List<string> Moves(GameState s, string from)
    {
        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(s, moves);
        return moves.Where(m => m.From == Sq(s, from)).Select(m => MoveText.Format(s, m)).ToList();
    }

    private static List<string> Casts(GameState s, string from) => Moves(s, from).Where(m => m.Contains('@')).ToList();

    private static TurnResult Play(GameState s, string move)
    {
        var m = MoveText.FindLegal(s, move) ?? throw new InvalidOperationException($"{move} is not legal");
        var result = Game.PlayTurn(s, m, new ResolveOptions(Trace: true));
        Assert.True(result.Accepted, result.RejectReason);
        Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
        return result;
    }

    private static Tile TileOn(GameState s, string square) => s.TileAt(Sq(s, square));
    private static int Status(Piece p, AbilityDef status) => p.GetAbility(status)?.Var(0) ?? 0;

    // ------------------------------------------------------------------ casting

    [Fact]
    public void A_cast_targets_empty_squares_within_two_where_the_caster_cannot_move()
    {
        var s = Rogue("7k/8/8/8/3B4/8/8/K7 w - - 0 1");
        Grant(s, "d4", CasterAbility.FireMage);

        var casts = Casts(s, "d4");
        // 24 squares within range 2, minus the 8 the bishop can move to along its diagonals.
        Assert.Equal(16, casts.Count);
        Assert.Contains("d4d6@fire-mage", casts);
        Assert.Contains("d4b3@fire-mage", casts);
        Assert.DoesNotContain("d4e5@fire-mage", casts); // movement takes priority
        Assert.DoesNotContain("d4d7@fire-mage", casts); // out of range
        Assert.Contains("d4e5", Moves(s, "d4"));          // the normal moves are still there
    }

    [Fact]
    public void A_fire_cast_places_a_four_turn_fire_tile_and_the_caster_stays()
    {
        var s = Rogue("7k/8/8/8/3B4/8/8/K7 w - - 0 1");
        Grant(s, "d4", CasterAbility.FireMage);
        var bishop = Must(s, "d4");

        var result = Play(s, "d4d6@fire-mage");
        Assert.Same(bishop, At(s, "d4"));
        Assert.Equal(0, bishop.MovesMade);
        Assert.Contains(result.Trace!.AllFacts, f => f.Kind == FactKinds.TileChanged && f.Cause == Causes.Cast && f.Source.Piece == bishop);

        // 4 turn ends counting both sides, the cast turn's own end included.
        Assert.Same(FireTile.Instance, TileOn(s, "d6").Def);
        Assert.Equal(3, TileOn(s, "d6").Var(StatusTile.TurnsLeftVar));
        Assert.Equal(3, TileOn(s, "d6").Var(StatusTile.StatusTurnsVar));
        Play(s, "h8g8");
        Assert.Equal(2, TileOn(s, "d6").Var(StatusTile.TurnsLeftVar));
        Play(s, "a1b1");
        Assert.Equal(1, TileOn(s, "d6").Var(StatusTile.TurnsLeftVar));
        Play(s, "g8h8");
        Assert.True(TileOn(s, "d6").Def.IsDefault);
    }

    [Fact]
    public void Wall_placement_puts_a_wall_that_stops_sliders()
    {
        var s = Rogue("7k/8/8/8/8/8/8/RB5K w - - 0 1");
        Grant(s, "b1", CasterAbility.WallPlacement);
        Assert.Contains("a1a8", Moves(s, "a1"));

        Play(s, "b1a3@wall-placement");
        Assert.Same(WallTile.Instance, TileOn(s, "a3").Def);
        Play(s, "h8h7");
        Assert.DoesNotContain("a1a4", Moves(s, "a1"));
        Assert.Contains("a1a3", Moves(s, "a1")); // may land on the wall, which breaks it
    }

    [Fact]
    public void Casts_only_target_plain_floor_never_another_tile()
    {
        var s = Rogue("7k/8/8/8/3B4/8/8/K7 w - - 0 1");
        Grant(s, "d4", CasterAbility.FreezeMage);
        Tile(s, WallTile.Instance, "d5");
        Tile(s, GuardianTile.Instance, "d3");
        Tile(s, FogTile.Instance, "c4");
        Tile(s, FireTile.Instance, "e4");
        Tile(s, FreezeTile.Instance, "d6");

        var casts = Casts(s, "d4");
        foreach (var blocked in new[] { "d5", "d3", "c4", "e4", "d6" })
            Assert.DoesNotContain($"d4{blocked}@freeze-mage", casts);
        Assert.Equal(11, casts.Count);
        Assert.False(CasterAbility.CanTarget(s, Sq(s, "d5")));
        Assert.True(CasterAbility.CanTarget(s, Sq(s, "b4")));
    }

    [Fact]
    public void Hand_built_invalid_casts_are_rejected_and_change_nothing()
    {
        var s = Rogue("7k/8/8/8/3B4/8/8/K7 w - - 0 1");
        Grant(s, "d4", CasterAbility.FireMage);
        Tile(s, GuardianTile.Instance, "d5");
        Game.BeginGame(s);
        s.ClearHistory();
        string before = s.Dump();
        ulong hash = s.Hash;

        int d4 = Sq(s, "d4");
        foreach (var bad in new[]
        {
            new Move(d4, Sq(s, "d5"), MoveFlags.Ability | MoveFlags.Cast, Aux: 0), // B11: guardian tile
            new Move(d4, Sq(s, "d7"), MoveFlags.Ability | MoveFlags.Cast, Aux: 0), // out of range
            new Move(d4, Sq(s, "b6"), MoveFlags.Ability | MoveFlags.Cast, Aux: 3), // no such ability
            new Move(Sq(s, "a1"), Sq(s, "a2"), MoveFlags.Ability | MoveFlags.Cast, Aux: 0), // the king has no cast
        })
        {
            var result = Game.PlayTurn(s, bad);
            Assert.False(result.Accepted);
            Assert.Equal(hash, s.Hash);
            Assert.Equal(before, s.Dump());
        }
    }

    [Fact]
    public void A_piece_can_carry_two_casts_and_chooses_between_them()
    {
        var s = Rogue("7k/8/8/8/3N4/8/8/K7 w - - 0 1");
        Grant(s, "d4", ExplodingAbility.Instance, CasterAbility.FireMage, CasterAbility.FreezeMage);

        var casts = Casts(s, "d4");
        Assert.Equal(32, casts.Count); // 16 per caster: 24 in range minus the knight's 8 moves
        Assert.Contains("d4d5@fire-mage", casts);
        Assert.Contains("d4d5@freeze-mage", casts);

        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(s, moves);
        Assert.Equal(new[] { 1, 2 }, moves.Where(m => m.Is(MoveFlags.Cast)).Select(m => m.Aux).Distinct().OrderBy(a => a));

        Play(s, "d4d5@freeze-mage");
        Assert.Same(FreezeTile.Instance, TileOn(s, "d5").Def);
        Play(s, "h8g8");
        Play(s, "d4d3@fire-mage");
        Assert.Same(FireTile.Instance, TileOn(s, "d3").Def);
        Assert.DoesNotContain("d4d5@fire-mage", Casts(s, "d4")); // the freeze tile can't be overwritten
    }

    [Fact]
    public void A_frozen_caster_cannot_cast()
    {
        var s = Rogue("7k/8/8/8/3B4/8/8/K7 w - - 0 1");
        Grant(s, "d4", CasterAbility.WallPlacement);
        s.GrantAbility(Must(s, "d4"), FrozenAbility.Instance, 1);
        s.ClearHistory();
        Assert.Empty(Moves(s, "d4"));
    }

    // ------------------------------------------------------------------ fire and OnFire

    [Fact]
    public void Entering_fire_sets_the_piece_alight_and_it_burns_after_its_owners_third_turn_end()
    {
        var s = Rogue("7k/8/8/8/8/8/8/R6K w - - 0 1");
        Tile(s, FireTile.Instance, "a5");
        var rook = Must(s, "a1");

        Play(s, "a1a5");
        Assert.Equal(2, Status(rook, OnFireAbility.Instance)); // 3, minus this turn's end
        Play(s, "h8g8");
        Assert.Equal(2, Status(rook, OnFireAbility.Instance)); // the opponent's turn doesn't count
        Play(s, "a5a6");
        Assert.Equal(1, Status(rook, OnFireAbility.Instance));
        Assert.True(TileOn(s, "a5").Def.IsDefault);            // the map tile (3 turn ends) has gone out
        Play(s, "g8h8");
        var result = Play(s, "a6a7");
        Assert.False(rook.IsOnBoard);
        Assert.Contains(result.Trace!.AllFacts, f => f.Kind == FactKinds.Died && f.Piece == rook && f.Cause == Causes.Burned);
    }

    [Fact]
    public void Sliding_across_fire_catches_fire_but_a_knight_jumps_over_it()
    {
        var s = Rogue("7k/8/8/8/8/8/8/RN5K w - - 0 1");
        Tile(s, FireTile.Instance, "a4", "b2");

        Play(s, "a1a7");
        Assert.True(Must(s, "a7").HasAbility(OnFireAbility.Instance));
        Play(s, "h8g8");
        Play(s, "b1c3");
        Assert.False(Must(s, "c3").HasAbility(OnFireAbility.Instance));
    }

    [Fact]
    public void A_burning_piece_is_not_relit_by_more_fire()
    {
        var s = Rogue("7k/8/8/8/8/8/8/R6K w - - 0 1");
        Tile(s, FireTile.Instance, "a5");
        s.GrantAbility(Must(s, "a1"), OnFireAbility.Instance, 2);
        s.ClearHistory();

        Play(s, "a1a5");
        Assert.Equal(1, Status(Must(s, "a5"), OnFireAbility.Instance));
    }

    [Fact]
    public void A_burning_exploding_piece_explodes_when_it_burns()
    {
        // In the prototype the OnFire wrapper hid Exploding (A4). Here abilities are a list and both work.
        var s = Rogue("7k/8/8/8/8/1p6/1P6/K7 w - - 0 1");
        s.GrantAbility(Must(s, "b2"), ExplodingAbility.Instance);
        s.GrantAbility(Must(s, "b2"), OnFireAbility.Instance, 1);
        s.ClearHistory();

        Play(s, "a1a2");
        Assert.Null(At(s, "b2"));
        Assert.Null(At(s, "b3")); // the blast reached the black pawn
        Assert.Null(At(s, "a2")); // and the white king next to it
    }

    // ------------------------------------------------------------------ freeze

    [Fact]
    public void Entering_freeze_freezes_the_piece_for_its_owners_turns()
    {
        var s = Rogue("7k/8/8/8/8/8/8/R6K w - - 0 1");
        Tile(s, FreezeTile.Instance, "a5");

        Play(s, "a1a5");
        var rook = Must(s, "a5");
        Assert.Equal(2, Status(rook, FrozenAbility.Instance));
        Play(s, "h8g8");
        Assert.Empty(Moves(s, "a5"));
        Play(s, "h1g1");
        Assert.Equal(1, Status(rook, FrozenAbility.Instance));
        Play(s, "g8h8");
        Play(s, "g1h1");
        Assert.False(rook.HasAbility(FrozenAbility.Instance));
        Play(s, "h8g8");
        Assert.NotEmpty(Moves(s, "a5"));
    }

    [Fact]
    public void A_freeze_cast_lasts_four_turn_ends_and_freezes_who_enters()
    {
        var s = Rogue("7k/8/8/8/8/8/r7/K1B5 b - - 0 1");
        Grant(s, "c1", CasterAbility.FreezeMage);
        Play(s, "a2a3");
        Play(s, "c1a2@freeze-mage");
        Assert.Equal(3, TileOn(s, "a2").Var(StatusTile.TurnsLeftVar));
        Play(s, "a3a2"); // the rook walks into the ice
        Assert.Equal(2, Status(Must(s, "a2"), FrozenAbility.Instance));
        Assert.True(Must(s, "a2").IsImmobile);
    }

    // ------------------------------------------------------------------ maps

    private static GameState LoadLegacy(string name)
        => MapLoader.Load(File.ReadAllText(Path.Combine(MapLoaderTests.LegacyMapsDir, name + ".json")), ContentLibrary.CreateCatalog());

    [Fact]
    public void Fire_test_map_loads_its_mages_fire_and_burning_pawn()
    {
        var s = LoadLegacy("fire test");
        Assert.Same(FireTile.Instance, TileOn(s, "d4").Def);
        Assert.Equal(StatusTile.DefaultTurns, TileOn(s, "d4").Var(StatusTile.TurnsLeftVar));
        Assert.Equal(StatusTile.DefaultStatusTurns, TileOn(s, "d4").Var(StatusTile.StatusTurnsVar));
        Assert.Equal(OnFireAbility.DefaultTurns, Status(Must(s, "d4"), OnFireAbility.Instance));
        Assert.True(Must(s, "f6").HasAbility(CasterAbility.FireMage));
        Assert.True(Must(s, "b2").HasAbility(CasterAbility.WallPlacement));
        Assert.NotEmpty(Casts(s, "f6"));
        Assert.NotEmpty(Casts(s, "b2"));
    }

    [Fact]
    public void Freeze_test_map_loads_its_mage_and_freeze_tiles()
    {
        var s = LoadLegacy("freeze test");
        foreach (var sq in new[] { "e5", "d6", "f4" }) Assert.Same(FreezeTile.Instance, TileOn(s, sq).Def);
        Assert.True(Must(s, "d4").HasAbility(FrozenAbility.Instance));
        Assert.True(Must(s, "b2").HasAbility(CasterAbility.FreezeMage));
        Assert.NotEmpty(Casts(s, "b2"));
    }

    [Fact]
    public void Wall_placement_test_map_offers_wall_casts()
    {
        var s = LoadLegacy("wall placement test");
        Assert.True(Must(s, "d4").HasAbility(CasterAbility.WallPlacement));
        Assert.Equal(16, Casts(s, "d4").Count);
    }

    [Theory]
    [InlineData("fire test")]
    [InlineData("freeze test")]
    [InlineData("wall placement test")]
    public void Caster_legacy_maps_play_random_games_with_exact_hash_and_undo(string map)
    {
        var s = LoadLegacy(map);
        Game.BeginGame(s);
        s.ClearHistory();
        RandomGameUndoExact(s, new Random(map.Length), 60);
    }

    // ------------------------------------------------------------------ AI

    [Fact]
    public void The_ai_casts_when_every_move_loses()
    {
        // Zugzwang: the white king can only step next to the black king and the pawn is blocked,
        // so casting a wall is the only move that doesn't lose the king.
        var s = Rogue("8/8/8/8/8/p7/P1k5/K7 w - - 0 1");
        Grant(s, "a1", CasterAbility.WallPlacement);
        var profile = AgentProfile.Club with { Id = "sharp", ChoiceMargin = 0, RootNoise = 0, MaxDepth = 3, NodeBudget = 200_000 };
        var decision = new AgentFactory().Create(profile).Decide(s);
        Assert.True(decision.Move!.Value.Is(MoveFlags.Cast), MoveText.Format(s, decision.Move.Value));
        Assert.True(Game.PlayTurn(s, decision.Move.Value).Accepted);
    }

    // ------------------------------------------------------------------ fuzz

    [Fact]
    public void Random_games_with_casters_and_status_tiles_keep_hash_and_undo_exact()
    {
        var rng = new Random(4321);
        int casts = 0;
        for (int game = 0; game < 12; game++)
        {
            var s = Rogue("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w - - 0 1");
            s.SeedRandom((ulong)game + 1);
            s.GrantAbility(Must(s, "c1"), CasterAbility.FireMage);
            s.GrantAbility(Must(s, "f8"), CasterAbility.FreezeMage);
            s.GrantAbility(Must(s, "a1"), CasterAbility.WallPlacement);
            s.GrantAbility(Must(s, "g1"), CasterAbility.FireMage);
            s.GrantAbility(Must(s, "g1"), CasterAbility.FreezeMage);
            s.GrantAbility(Must(s, "b8"), ExplodingAbility.Instance);
            s.GrantAbility(Must(s, "b8"), CasterAbility.FireMage);
            s.GrantAbility(Must(s, "e7"), OnFireAbility.Instance, 2);
            s.GrantAbility(Must(s, "d7"), ScapegoatAbility.Instance);
            s.SetTile(Sq(s, "d4"), FireTile.Instance);
            s.SetTile(Sq(s, "e5"), FreezeTile.Instance, 6, 2);
            s.SetTile(Sq(s, "c5"), GuardianTile.Instance);
            s.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(5), new WallsPlacementEffect(1)));
            s.ClearHistory();
            casts += RandomGameUndoExact(s, rng, 80);
        }
        Assert.True(casts > 50, $"only {casts} casts were played");
    }

    /// <summary>Plays random legal actions, checking the hash after each, then unwinds and checks the exact start state.</summary>
    private static int RandomGameUndoExact(GameState s, Random rng, int plies)
    {
        string start = s.Dump();
        ulong hash = s.Hash;
        int mark = s.UndoDepth, casts = 0;
        var moves = new List<Move>();
        for (int ply = 0; ply < plies; ply++)
        {
            if (s.Rules.Evaluate(s) != GameOutcome.Ongoing) break;
            moves.Clear();
            MoveGenerator.GenerateLegal(s, moves);
            if (moves.Count == 0) break;
            var m = moves[rng.Next(moves.Count)];
            if (m.Is(MoveFlags.Cast))
            {
                casts++;
                Assert.Equal(m, MoveText.FindLegal(s, MoveText.Format(s, m))); // notation round-trips
            }
            var result = Game.PlayTurn(s, m);
            Assert.True(result.Accepted, result.RejectReason);
            Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
        }
        s.UndoTo(mark);
        Assert.Equal(hash, s.Hash);
        Assert.Equal(start, s.Dump());
        return casts;
    }
}
