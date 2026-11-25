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

/// <summary>Revenant and its Tomb, Cannibal and the Slippery tile, plus the B6, B7, B8 and B10 regressions.</summary>
public class DeathAndMovementTests
{
    private static GameState Rogue(string fen) => Fen.Parse(fen, RuleSet.Rogue);
    private static int Sq(IBoardView s, string name) => MoveText.ParseSquare(s, name);
    private static Piece? At(GameState s, string name) => s.PieceAt(Sq(s, name));
    private static Piece Must(GameState s, string name) => At(s, name) ?? throw new InvalidOperationException($"no piece on {name}");
    private static TileDef TileOn(GameState s, string name) => s.TileAt(Sq(s, name)).Def;

    private static void Grant(GameState s, AbilityDef ability, params string[] squares)
    {
        foreach (var sq in squares) s.GrantAbility(Must(s, sq), ability);
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
        return moves.Where(m => m.From == Sq(s, from)).Select(m => MoveText.Format(s, m)).OrderBy(m => m, StringComparer.Ordinal).ToList();
    }

    private static TurnResult Play(GameState s, string move)
    {
        var m = MoveText.FindLegal(s, move) ?? throw new InvalidOperationException($"{move} is not legal\n{s.Dump()}");
        var result = Game.PlayTurn(s, m, new ResolveOptions(Trace: true));
        Assert.True(result.Accepted, result.RejectReason);
        Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
        return result;
    }

    private static string LegacyMap(string name)
        => File.ReadAllText(Path.Combine(MapLoaderTests.LegacyMapsDir, name + ".json"));

    // ------------------------------------------------------------------ Revenant + Tomb

    /// <summary>White rook f3, knight g1, king a1; black bishop d5, king h8. Black to move.</summary>
    private const string TombFen = "7k/8/8/3b4/8/5R2/8/K5N1 b - - 0 1";

    [Fact]
    public void Revenant_leaves_a_tomb_holding_the_dead_piece()
    {
        var s = Rogue(TombFen);
        var rook = Must(s, "f3");
        Grant(s, RevenantAbility.Instance, "f3");

        Play(s, "d5f3");

        Assert.False(rook.IsOnBoard);
        Assert.Same(TombTile.Instance, TileOn(s, "f3"));
        Assert.Equal(rook.Id, s.TileAt(Sq(s, "f3")).Var(TombTile.PieceVar));
        Assert.Same(rook, TombTile.Entombed(s, Sq(s, "f3")));
        Assert.Equal("bishop", Must(s, "f3").Def.Id); // the capturer stands on the tomb
    }

    [Fact]
    public void An_ally_stepping_onto_the_empty_tomb_revives_the_piece_and_stays_put()
    {
        var s = Rogue(TombFen);
        var rook = Must(s, "f3");
        var knight = Must(s, "g1");
        Grant(s, RevenantAbility.Instance, "f3");
        Grant(s, PhantomAbility.Instance, "f3"); // other abilities come back with it

        Play(s, "d5f3");
        Play(s, "a1b1");
        Play(s, "f3c6"); // an enemy leaving: the tomb stays
        Assert.Same(TombTile.Instance, TileOn(s, "f3"));

        Play(s, "g1f3");

        Assert.Same(rook, At(s, "f3"));
        Assert.Same(knight, At(s, "g1"));
        Assert.Equal(0, knight.MovesMade);
        Assert.True(TileOn(s, "f3").IsDefault);
        Assert.True(rook.HasAbility(PhantomAbility.Instance));
    }

    [Fact]
    public void B10_revenant_revives_once_by_default()
    {
        var s = Rogue(TombFen);
        var rook = Must(s, "f3");
        Grant(s, RevenantAbility.Instance, "f3");
        Play(s, "d5f3");
        Play(s, "a1b1");
        Play(s, "f3c6");
        Play(s, "g1f3");

        Assert.False(rook.HasAbility(RevenantAbility.Instance));
        Play(s, "c6f3");
        Assert.False(rook.IsOnBoard);
        Assert.True(TileOn(s, "f3").IsDefault); // no second tomb
    }

    [Fact]
    public void Forever_revenant_keeps_coming_back()
    {
        Assert.False(RevenantAbility.Instance.RevivesForever);
        Assert.True(RevenantAbility.Forever.RevivesForever);

        var s = Rogue(TombFen);
        var rook = Must(s, "f3");
        Grant(s, RevenantAbility.Forever, "f3");
        Play(s, "d5f3");
        Play(s, "a1b1");
        Play(s, "f3c6");
        Play(s, "g1f3");

        Assert.True(rook.HasAbility(RevenantAbility.Forever));
        Play(s, "c6f3");
        Assert.Same(TombTile.Instance, TileOn(s, "f3"));
        Assert.Same(rook, TombTile.Entombed(s, Sq(s, "f3")));
    }

    [Fact]
    public void A_capture_onto_the_tomb_does_not_open_it()
    {
        var s = Rogue(TombFen);
        var rook = Must(s, "f3");
        var knight = Must(s, "g1");
        Grant(s, RevenantAbility.Instance, "f3");
        Play(s, "d5f3");

        Play(s, "g1f3"); // the knight takes the bishop standing on the tomb
        Assert.Same(knight, At(s, "f3"));
        Assert.False(rook.IsOnBoard);
        Assert.Same(TombTile.Instance, TileOn(s, "f3"));

        Play(s, "h8g8");
        Play(s, "f3e5");
        Play(s, "g8h8");
        Play(s, "e5f3"); // now the tomb is empty: it opens and the knight stays on e5
        Assert.Same(rook, At(s, "f3"));
        Assert.Same(knight, At(s, "e5"));
    }

    [Fact]
    public void Exploding_blast_entombs_revenants_it_kills_including_the_capturer()
    {
        var s = Rogue("4r2k/8/8/8/4N3/3R4/8/K7 b - - 0 1");
        var whiteRook = Must(s, "d3");
        var blackRook = Must(s, "e8");
        Grant(s, ExplodingAbility.Instance, "e4");
        Grant(s, RevenantAbility.Instance, "d3", "e8");

        Play(s, "e8e4");

        Assert.False(whiteRook.IsOnBoard);
        Assert.False(blackRook.IsOnBoard);
        Assert.Same(whiteRook, TombTile.Entombed(s, Sq(s, "d3")));
        Assert.Same(blackRook, TombTile.Entombed(s, Sq(s, "e4"))); // where the capturer died
    }

    [Fact]
    public void Scapegoat_saving_a_revenant_leaves_no_tomb()
    {
        var s = Rogue("3r3k/8/8/8/8/2BR4/8/K7 b - - 0 1");
        Grant(s, RevenantAbility.Instance, "d3");
        Grant(s, ScapegoatAbility.Instance, "c3");

        Play(s, "d8d3");

        Assert.NotNull(At(s, "d3"));
        Assert.Null(At(s, "c3"));
        Assert.All(Enumerable.Range(0, s.Area), sq => Assert.NotSame(TombTile.Instance, s.TileAt(sq).Def));
    }

    [Fact]
    public void A_revenant_scapegoat_that_sacrifices_itself_is_entombed()
    {
        var s = Rogue("3r3k/8/8/8/8/2BR4/8/K7 b - - 0 1");
        var bishop = Must(s, "c3");
        Grant(s, ScapegoatAbility.Instance, "c3");
        Grant(s, RevenantAbility.Instance, "c3");

        Play(s, "d8d3");

        Assert.Equal("rook", Must(s, "d3").Def.Id);
        Assert.Same(bishop, TombTile.Entombed(s, Sq(s, "c3")));
        Assert.Equal("rook", Must(s, "d8").Def.Id); // the attacker was blocked
    }

    [Fact]
    public void A_frozen_revenant_comes_back_frozen_and_the_hash_tells_the_tombs_apart()
    {
        var plain = Rogue(TombFen);
        Grant(plain, RevenantAbility.Instance, "f3");
        var frozen = Rogue(TombFen);
        Grant(frozen, RevenantAbility.Instance, "f3");
        frozen.GrantAbility(Must(frozen, "f3"), FrozenAbility.Instance, 2);
        frozen.ClearHistory();

        Play(plain, "d5f3");
        Play(frozen, "d5f3");
        // Same board; only the entombed piece differs, and the tomb's digest variable puts that in the hash.
        Assert.NotEqual(plain.Hash, frozen.Hash);

        var rook = TombTile.Entombed(frozen, Sq(frozen, "f3"))!;
        Play(frozen, "a1b1");
        Play(frozen, "f3c6");
        Play(frozen, "g1f3");
        Assert.Same(rook, At(frozen, "f3"));
        Assert.True(rook.HasAbility(FrozenAbility.Instance));
        Assert.True(rook.IsImmobile);
    }

    [Fact]
    public void Undo_restores_tombs_and_entombed_pieces_exactly()
    {
        var s = Rogue(TombFen);
        Grant(s, RevenantAbility.Instance, "f3");
        string start = s.Dump();
        ulong startHash = s.Hash;

        Play(s, "d5f3");
        Play(s, "a1b1");
        Play(s, "f3c6");
        string beforeRevive = s.Dump();
        ulong beforeReviveHash = s.Hash;
        int mark = s.UndoDepth;

        Play(s, "g1f3");
        s.UndoTo(mark);
        Assert.Equal(beforeRevive, s.Dump());
        Assert.Equal(beforeReviveHash, s.Hash);
        var rook = TombTile.Entombed(s, Sq(s, "f3"))!;
        Assert.True(rook.HasAbility(RevenantAbility.Instance));

        s.UndoTo(0);
        Assert.Equal(start, s.Dump());
        Assert.Equal(startHash, s.Hash);
        Assert.Equal(s.ComputeHashFromScratch(), s.Hash);

        // Cloned states carry the tomb and the off-board piece too.
        Play(s, "d5f3");
        var clone = s.Clone();
        Assert.Equal(s.Dump(), clone.Dump());
        Play(clone, "a1b1");
        Play(clone, "f3c6");
        Play(clone, "g1f3");
        Assert.Equal("rook", Must(clone, "f3").Def.Id);
        Assert.Null(At(s, "c6"));
    }

    // ------------------------------------------------------------------ Cannibal

    [Fact]
    public void Cannibal_knight_from_the_legacy_map_captures_only_allies()
    {
        var s = MapLoader.Load(LegacyMap("cannibal test"), ContentLibrary.CreateCatalog());
        Assert.True(Must(s, "c3").HasAbility(CannibalAbility.Instance));
        Assert.True(Must(s, "e2").HasAbility(CannibalAbility.Instance));

        // b5, d5 and e4 hold black pawns: no captures there. b1, d1 (rooks) and e2 (king) are allies.
        Assert.Equal(new[] { "c3a2", "c3a4", "c3b1", "c3d1", "c3e2" }, Moves(s, "c3"));

        var rook = Must(s, "d1");
        Play(s, "c3d1");
        Assert.False(rook.IsOnBoard);
        Assert.True(Must(s, "d1").HasAbility(CannibalAbility.Instance));
    }

    [Fact]
    public void Cannibal_slider_takes_the_first_ally_on_a_line_but_no_enemy()
    {
        var s = Rogue("7k/8/8/8/P7/8/8/R1n4K w - - 0 1");
        Grant(s, CannibalAbility.Instance, "a1");
        Assert.Equal(new[] { "a1a2", "a1a3", "a1a4", "a1b1" }, Moves(s, "a1"));

        Tile(s, FogTile.Instance, "a4");
        Assert.Equal(new[] { "a1a2", "a1a3", "a1b1" }, Moves(s, "a1"));

        Tile(s, TileDef.Standard, "a4");
        Tile(s, WallTile.Instance, "a3"); // walls still stop the slide
        Assert.Equal(new[] { "a1a2", "a1a3", "a1b1" }, Moves(s, "a1"));
    }

    [Fact]
    public void Cannibal_pawn_captures_allies_diagonally_and_promotes()
    {
        var s = Rogue("7k/8/8/8/8/3P1p2/4P3/K7 w - - 0 1");
        Grant(s, CannibalAbility.Instance, "e2");
        Assert.Equal(new[] { "e2d3", "e2e3", "e2e4" }, Moves(s, "e2"));

        var p = Rogue("k6R/6P1/8/8/8/8/8/7K w - - 0 1");
        Grant(p, CannibalAbility.Instance, "g7");
        Assert.Contains("g7h8q", Moves(p, "g7"));
        Assert.DoesNotContain("g7h8", Moves(p, "g7"));
        Play(p, "g7h8q");
        Assert.Equal("queen", Must(p, "h8").Def.Id);
        Assert.Equal(Side.White, Must(p, "h8").Side);
    }

    [Fact]
    public void Scapegoat_protects_an_ally_from_a_cannibal()
    {
        var s = Rogue("7k/8/8/1B6/P7/8/8/R6K w - - 0 1");
        Grant(s, CannibalAbility.Instance, "a1");
        Grant(s, ScapegoatAbility.Instance, "b5");

        Play(s, "a1a4");

        Assert.Equal("pawn", Must(s, "a4").Def.Id);
        Assert.Null(At(s, "b5"));
        Assert.Equal("rook", Must(s, "a1").Def.Id); // blocked, stays put
    }

    // ------------------------------------------------------------------ Slippery

    [Fact]
    public void Slippery_legacy_map_slides_the_king_one_more_square()
    {
        var s = MapLoader.Load(LegacyMap("slippery test"), ContentLibrary.CreateCatalog());
        Assert.Same(SlipperyTile.Instance, TileOn(s, "b2"));
        Assert.Same(SlipperyTile.Instance, TileOn(s, "c3"));
        var king = Must(s, "b1");

        Play(s, "b1b2");

        Assert.Same(king, At(s, "b3"));
    }

    [Fact]
    public void Slides_chain_across_slippery_tiles_until_something_stops_them()
    {
        var s = Rogue("7k/8/8/8/8/8/8/R6K w - - 0 1");
        Tile(s, SlipperyTile.Instance, "a3", "a4");
        Play(s, "a1a3");
        Assert.Equal("rook", Must(s, "a5").Def.Id);

        // Diagonal: c3 → d4 → e5, then f6 is taken by a pawn, so it stops on e5.
        var d = Rogue("7k/8/5p2/8/8/8/8/B6K w - - 0 1");
        Tile(d, SlipperyTile.Instance, "c3", "d4", "e5");
        Play(d, "a1c3");
        Assert.Equal("bishop", Must(d, "e5").Def.Id);
        Assert.Equal("pawn", Must(d, "f6").Def.Id);

        // Sideways on the legacy map: the king steps west onto b2 and slides on to a2.
        var m = MapLoader.Load(LegacyMap("slippery test"), ContentLibrary.CreateCatalog());
        var king = Must(m, "b1");
        Play(m, "b1c2");
        Play(m, "d4d3");
        Play(m, "c2b2");
        Assert.Same(king, At(m, "a2"));
    }

    [Fact]
    public void B6_knight_slides_along_the_longer_leg_of_its_move()
    {
        var s = Rogue("7k/8/8/8/8/8/8/1N5K w - - 0 1");
        Assert.Equal((0, 1), SlipperyTile.SlideStep(s, Sq(s, "b1"), Sq(s, "c3")));
        Assert.Equal((1, 0), SlipperyTile.SlideStep(s, Sq(s, "b1"), Sq(s, "d2")));
        Assert.Equal((-1, -1), SlipperyTile.SlideStep(s, Sq(s, "h8"), Sq(s, "a1")));

        Tile(s, SlipperyTile.Instance, "c3");
        Play(s, "b1c3");
        Assert.Equal("knight", Must(s, "c4").Def.Id); // the prototype sent it to d4
        Assert.Null(At(s, "d4"));

        var t = Rogue("7k/8/8/8/8/8/8/1N5K w - - 0 1");
        Tile(t, SlipperyTile.Instance, "d2");
        Play(t, "b1d2");
        Assert.Equal("knight", Must(t, "e2").Def.Id);
    }

    [Fact]
    public void Slides_never_capture_and_stop_at_pieces_and_walls()
    {
        var s = Rogue("7k/8/8/8/p7/8/8/R6K w - - 0 1");
        Tile(s, SlipperyTile.Instance, "a3");
        Play(s, "a1a3");
        Assert.Equal("rook", Must(s, "a3").Def.Id);
        Assert.Equal("pawn", Must(s, "a4").Def.Id);

        var w = Rogue("7k/8/8/8/8/8/8/R6K w - - 0 1");
        Tile(w, SlipperyTile.Instance, "a3");
        Tile(w, WallTile.Instance, "a4");
        Play(w, "a1a3");
        Assert.Equal("rook", Must(w, "a3").Def.Id);
        Assert.Same(WallTile.Instance, TileOn(w, "a4")); // not broken: the slide didn't enter it
    }

    [Fact]
    public void A_slide_onto_a_guardian_tile_lands_and_leaves_the_guardian()
    {
        var s = Rogue("7k/8/8/8/8/8/8/R6K w - - 0 1");
        Tile(s, SlipperyTile.Instance, "a3");
        Tile(s, GuardianTile.Instance, "a4");
        Play(s, "a1a3");
        Assert.Equal("rook", Must(s, "a4").Def.Id);
        Assert.Same(GuardianTile.Instance, TileOn(s, "a4"));
    }

    [Fact]
    public void B8_a_slide_off_the_board_edge_stays_put_instead_of_wrapping()
    {
        var s = Rogue("7k/8/8/8/R7/8/8/7K w - - 0 1");
        Tile(s, SlipperyTile.Instance, "h4");
        Play(s, "a4h4");
        Assert.Equal("rook", Must(s, "h4").Def.Id);
        Assert.Null(At(s, "a5")); // h4 + 1 as a raw index is a5

        // Out-of-bounds lookups never fall back to the corner square.
        Assert.Null(s.PieceAt(-1, 0));
        Assert.Null(s.PieceAt(8, 0));
        Assert.Null(s.PieceAt(0, 8));
        Assert.Throws<IndexOutOfRangeException>(() => s.TileAt(-1));
        Assert.Throws<IndexOutOfRangeException>(() => s.TileAt(s.Area));
    }

    [Fact]
    public void Pawn_double_push_onto_ice_slides_on_and_leaves_no_en_passant_square()
    {
        var s = Rogue("7k/8/8/8/8/8/4P3/K7 w - - 0 1");
        Tile(s, SlipperyTile.Instance, "e4");
        Play(s, "e2e4");
        Assert.Equal("pawn", Must(s, "e5").Def.Id);
        Assert.Equal(-1, s.EnPassantSquare);
    }

    [Fact]
    public void A_frozen_piece_pushed_onto_ice_still_slides()
    {
        var s = Rogue("7k/8/8/R7/8/8/8/7K w - - 0 1");
        s.GrantAbility(Must(s, "a5"), FrozenAbility.Instance, 3);
        s.SetTile(Sq(s, "a4"), SlipperyTile.Instance);
        s.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(1), new WindGustEffect(Side.White, 1)));
        s.ClearHistory();

        Play(s, "h1g1"); // white's turn end: the gust pushes a5 → a4, then the ice carries it to a3

        Assert.Equal("rook", Must(s, "a3").Def.Id);
        Assert.True(Must(s, "a3").HasAbility(FrozenAbility.Instance));
    }

    [Fact]
    public void A_slide_onto_an_allys_tomb_opens_it()
    {
        var s = Rogue("7k/8/8/8/8/8/8/R6K w - - 0 1");
        var knight = s.Place(StandardPieces.Knight, Side.White, Sq(s, "a4"));
        s.GrantAbility(knight, RevenantAbility.Instance);
        s.RemoveFromBoard(knight);
        s.SetTile(Sq(s, "a4"), TombTile.Instance, TombTile.VarsFor(knight));
        s.SetTile(Sq(s, "a3"), SlipperyTile.Instance);
        s.ClearHistory();

        Play(s, "a1a3");

        Assert.Equal("rook", Must(s, "a3").Def.Id); // the slide was turned into the revive
        Assert.Same(knight, At(s, "a4"));
        Assert.False(knight.HasAbility(RevenantAbility.Instance));
        Assert.True(TileOn(s, "a4").IsDefault);
    }

    // ------------------------------------------------------------------ B7

    [Fact]
    public void B7_jumps_onto_friendly_pieces_are_not_moves_or_captures()
    {
        var s = Rogue("7k/8/8/8/8/8/3P4/1N5K w - - 0 1");
        Assert.Equal(new[] { "b1a3", "b1c3" }, Moves(s, "b1"));

        var start = Rogue("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w - - 0 1");
        var moves = new List<Move>();
        MoveGenerator.GeneratePseudoLegal(start, moves);
        Assert.DoesNotContain(moves, m => start.PieceAt(m.To) is { } target && target.Side == Side.White);
        Assert.DoesNotContain(moves, m => m.IsCapture);
    }

    // ------------------------------------------------------------------ maps + fuzz

    [Theory]
    [InlineData("Revenant Test")]
    [InlineData("cannibal test")]
    [InlineData("slippery test")]
    public void Legacy_maps_load(string name)
    {
        var s = MapLoader.Load(LegacyMap(name), ContentLibrary.CreateCatalog());
        Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
    }

    [Fact]
    public void Revenant_legacy_map_entombs_the_rook()
    {
        var s = MapLoader.Load(LegacyMap("Revenant Test"), ContentLibrary.CreateCatalog());
        var rook = Must(s, "f3");
        Assert.True(rook.HasAbility(RevenantAbility.Instance));

        Play(s, "f2e1");
        Play(s, "d5f3");

        Assert.Same(rook, TombTile.Entombed(s, Sq(s, "f3")));
    }

    [Fact]
    public void Random_games_with_death_and_movement_content_keep_hash_and_undo_exact()
    {
        var rng = new Random(4242);
        int tombsSeen = 0, revives = 0;
        for (int game = 0; game < 30; game++)
        {
            var s = Rogue("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w - - 0 1");
            foreach (var sq in new[] { "b2", "e2", "d7", "g7", "a1", "f8" }) s.GrantAbility(Must(s, sq), RevenantAbility.Instance);
            foreach (var sq in new[] { "h1", "c8" }) s.GrantAbility(Must(s, sq), RevenantAbility.Forever);
            foreach (var sq in new[] { "b1", "d1", "e7", "g8" }) s.GrantAbility(Must(s, sq), CannibalAbility.Instance);
            foreach (var sq in new[] { "c2", "f7" }) s.GrantAbility(Must(s, sq), ExplodingAbility.Instance);
            s.GrantAbility(Must(s, "g1"), ScapegoatAbility.Instance);
            s.GrantAbility(Must(s, "a8"), PhantomAbility.Instance);
            foreach (var sq in new[] { "c3", "d4", "e4", "f5", "c6", "h3", "a6" }) s.SetTile(Sq(s, sq), SlipperyTile.Instance);
            s.SetTile(Sq(s, "d5"), WallTile.Instance);
            s.SetTile(Sq(s, "e5"), GuardianTile.Instance);
            s.SeedRandom(7);
            s.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(7), new WindGustEffect(Side.Black, 1)));
            s.ClearHistory();

            string start = s.Dump();
            ulong hash = s.Hash;
            var moves = new List<Move>();
            for (int ply = 0; ply < 100; ply++)
            {
                if (s.Rules.Evaluate(s) != GameOutcome.Ongoing) break;
                moves.Clear();
                MoveGenerator.GenerateLegal(s, moves);
                if (moves.Count == 0) break;

                int tombsBefore = CountTombs(s);
                var result = Game.PlayTurn(s, moves[rng.Next(moves.Count)]);
                Assert.True(result.Accepted, result.RejectReason);
                Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
                int tombsAfter = CountTombs(s);
                if (tombsAfter > tombsBefore) tombsSeen++;
                if (tombsAfter < tombsBefore) revives++;

                // Every tomb points at a piece that is off the board.
                for (int sq = 0; sq < s.Area; sq++)
                    if (s.TileAt(sq).Def == TombTile.Instance)
                        Assert.False(s.GetPiece(s.TileAt(sq).Var(TombTile.PieceVar))!.IsOnBoard);
            }

            s.UndoTo(0);
            Assert.Equal(start, s.Dump());
            Assert.Equal(hash, s.Hash);
        }

        Assert.True(tombsSeen > 5, $"only {tombsSeen} tombs were made");
        Assert.True(revives > 0, "no tomb was ever opened or replaced");
    }

    private static int CountTombs(GameState s)
        => Enumerable.Range(0, s.Area).Count(sq => s.TileAt(sq).Def == TombTile.Instance);
}
