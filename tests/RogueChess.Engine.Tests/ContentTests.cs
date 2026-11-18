using System;
using System.Collections.Generic;
using System.Linq;
using RogueChess.Engine.Content;
using RogueChess.Engine.Content.Abilities;
using RogueChess.Engine.Content.Tiles;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;
using Xunit;

namespace RogueChess.Engine.Tests;

/// <summary>Behaviour of the ported content, alone and in combination.</summary>
public class ContentTests
{
    private static GameState Rogue(string fen) => Fen.Parse(fen, RuleSet.Rogue);
    private static int Sq(IBoardView s, string name) => MoveText.ParseSquare(s, name);
    private static Piece? At(GameState s, string name) => s.PieceAt(Sq(s, name));
    private static Piece Must(GameState s, string name) => At(s, name) ?? throw new InvalidOperationException($"no piece on {name}");

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
        return moves.Where(m => m.From == Sq(s, from)).Select(m => MoveText.Format(s, m)).ToList();
    }

    private static TurnResult Play(GameState s, string move)
    {
        var m = MoveText.FindLegal(s, move) ?? throw new InvalidOperationException($"{move} is not legal");
        var result = Game.PlayTurn(s, m, new ResolveOptions(Trace: true));
        Assert.True(result.Accepted, result.RejectReason);
        Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
        return result;
    }

    // ------------------------------------------------------------------ Exploding

    [Fact]
    public void Exploding_kills_the_capturer_and_every_neighbour_of_either_side()
    {
        var s = Rogue("7k/8/8/4p3/2Pp4/1N6/8/K7 w - - 0 1");
        Grant(s, ExplodingAbility.Instance, "d4");

        Play(s, "b3d4");

        Assert.Null(At(s, "d4")); // the knight that captured
        Assert.Null(At(s, "c4")); // white neighbour
        Assert.Null(At(s, "e5")); // black neighbour
        Assert.NotNull(At(s, "a1"));
    }

    [Fact]
    public void Explosion_reaches_pieces_in_fog_and_on_guardian_tiles()
    {
        var s = Rogue("7k/8/8/pp1p4/8/8/8/1R5K w - - 0 1");
        Grant(s, ExplodingAbility.Instance, "b5");
        Tile(s, FogTile.Instance, "a5");
        Tile(s, GuardianTile.Instance, "c4");
        s.Place(StandardPieces.Pawn, Side.Black, Sq(s, "c4"));
        s.ClearHistory();

        Play(s, "b1b5");

        Assert.Null(At(s, "a5"));
        Assert.Null(At(s, "c4"));
        Assert.Same(GuardianTile.Instance, s.TileAt(Sq(s, "c4")).Def); // not a capture, so the guardian wasn't used
        Assert.NotNull(At(s, "d5"));                                     // two squares away
    }

    // ------------------------------------------------------------------ Scapegoat

    [Fact]
    public void Scapegoat_dies_instead_of_an_adjacent_ally_and_the_attacker_stays_put()
    {
        var s = Rogue("7k/8/1n6/p7/8/8/8/R6K w - - 0 1");
        Grant(s, ScapegoatAbility.Instance, "b6");
        int pawn = Must(s, "a5").Id;

        Play(s, "a1a5");

        Assert.Equal(pawn, At(s, "a5")?.Id);
        Assert.Null(At(s, "b6"));
        Assert.NotNull(At(s, "a1"));
    }

    [Fact]
    public void Scapegoat_gets_no_kill_credit_for_its_own_sacrifice()
    {
        // A Predator Scapegoat must not evolve by dying for an ally (found while playtesting the sandbox).
        var s = Rogue("7k/8/1p6/p7/8/8/8/R6K w - - 0 1");
        Grant(s, ScapegoatAbility.Instance, "b6");
        Grant(s, PredatorAbility.Instance, "b6");

        var result = Play(s, "a1a5");

        var kills = result.Trace!.AllFacts.Where(f => f.Kind == FactKinds.Killed).ToList();
        Assert.Empty(kills);
        Assert.Null(At(s, "b6"));
    }

    [Fact]
    public void Scapegoat_does_not_protect_a_piece_two_squares_away()
    {
        var s = Rogue("7k/2n5/8/p7/8/8/8/R6K w - - 0 1");
        Grant(s, ScapegoatAbility.Instance, "c7");

        Play(s, "a1a5");

        Assert.Equal(Side.White, Must(s, "a5").Side);
        Assert.NotNull(At(s, "c7"));
    }

    [Fact]
    public void Scapegoat_saves_an_ally_from_an_explosion()
    {
        // White rook takes the exploding pawn on d5. The blast covers c4..e6, so it kills the rook and would kill
        // the bishop on e6, but the white Scapegoat knight on f7 (outside the blast) dies for it.
        var s = Rogue("7k/5N2/4B3/3p4/8/8/8/K2R4 w - - 0 1");
        Grant(s, ExplodingAbility.Instance, "d5");
        Grant(s, ScapegoatAbility.Instance, "f7");

        Play(s, "d1d5");

        Assert.Null(At(s, "d5"));
        Assert.NotNull(At(s, "e6"));
        Assert.Null(At(s, "f7"));
    }

    [Fact]
    public void Two_scapegoats_one_ally_the_closest_then_northern_one_steps_in()
    {
        // White knight d4 has Scapegoats north (d5) and east (e4), both adjacent. Black rook takes d4.
        var s = Rogue("7k/8/8/3N4/r2NN3/8/8/K7 b - - 0 1");
        Grant(s, ScapegoatAbility.Instance, "d5", "e4");

        Play(s, "a4d4");

        Assert.NotNull(At(s, "d4"));
        Assert.Null(At(s, "d5"));    // north wins the tie
        Assert.NotNull(At(s, "e4"));
        Assert.NotNull(At(s, "a4")); // the rook was blocked
    }

    // ------------------------------------------------------------------ Phantom

    [Fact]
    public void Phantom_slides_through_allies_and_captures_beyond()
    {
        var s = Rogue("7k/8/8/r7/8/P7/8/R6K w - - 0 1");
        Assert.DoesNotContain("a1a5", Moves(s, "a1"));

        Grant(s, PhantomAbility.Instance, "a1");
        var moves = Moves(s, "a1");

        Assert.Contains("a1a4", moves);
        Assert.Contains("a1a5", moves);
        Assert.DoesNotContain("a1a3", moves);
        Assert.DoesNotContain("a1a6", moves);
        Play(s, "a1a5");
        Assert.Equal(Side.White, Must(s, "a5").Side);
    }

    [Fact]
    public void Phantom_still_stops_at_walls()
    {
        var s = Rogue("7k/8/8/r7/8/P7/8/R6K w - - 0 1");
        Grant(s, PhantomAbility.Instance, "a1");
        Tile(s, WallTile.Instance, "a4");

        var moves = Moves(s, "a1");

        Assert.Contains("a1a4", moves);
        Assert.DoesNotContain("a1a5", moves);
    }

    // ------------------------------------------------------------------ Predator

    [Fact]
    public void Predator_evolves_one_step_per_kill()
    {
        var s = Rogue("7k/5p2/8/4p3/3P4/8/8/K7 w - - 0 1");
        Grant(s, PredatorAbility.Instance, "d4");
        var predator = Must(s, "d4");

        Play(s, "d4e5");
        Assert.Same(StandardPieces.Knight, predator.Def);

        Play(s, "h8g8");
        Play(s, "e5f7");

        Assert.Same(StandardPieces.Bishop, predator.Def);
        Assert.Equal(2, predator.GetAbility(PredatorAbility.Instance)!.Var(PredatorAbility.KillsVar));
        Assert.Equal(predator.Id, Must(s, "f7").Id);
    }

    [Fact]
    public void Predator_that_eats_an_exploding_piece_dies_unevolved()
    {
        var s = Rogue("7k/8/8/4p3/3P4/8/8/K7 w - - 0 1");
        Grant(s, PredatorAbility.Instance, "d4");
        Grant(s, ExplodingAbility.Instance, "e5");
        var predator = Must(s, "d4");

        Play(s, "d4e5");

        Assert.False(predator.IsOnBoard);
        Assert.Same(StandardPieces.Pawn, predator.Def);
    }

    [Fact]
    public void Predator_queen_stays_a_queen()
    {
        var s = Rogue("7k/8/8/p7/8/8/8/Q6K w - - 0 1");
        Grant(s, PredatorAbility.Instance, "a1");

        Play(s, "a1a5");

        Assert.Same(StandardPieces.Queen, Must(s, "a5").Def);
    }

    // ------------------------------------------------------------------ Tiles

    [Fact]
    public void Wall_stops_slides_and_breaks_when_landed_on()
    {
        var s = Rogue("7k/8/8/8/8/8/8/R6K w - - 0 1");
        Tile(s, WallTile.Instance, "a4");
        var moves = Moves(s, "a1");
        Assert.Contains("a1a4", moves);
        Assert.DoesNotContain("a1a5", moves);

        Play(s, "a1a4");

        Assert.True(s.TileAt(Sq(s, "a4")).Def.IsDefault);
    }

    [Fact]
    public void Guardian_stops_one_capture_then_is_gone()
    {
        var s = Rogue("7k/8/8/p7/8/8/8/R6K w - - 0 1");
        Tile(s, GuardianTile.Instance, "a5");
        int pawn = Must(s, "a5").Id;

        Play(s, "a1a5");
        Assert.Equal(pawn, At(s, "a5")?.Id);
        Assert.NotNull(At(s, "a1"));
        Assert.True(s.TileAt(Sq(s, "a5")).Def.IsDefault);

        Play(s, "h8h7");
        Play(s, "a1a5");
        Assert.Equal(Side.White, Must(s, "a5").Side);
    }

    [Fact]
    public void Fog_hides_its_occupant_from_captures_but_can_be_entered_when_empty()
    {
        var s = Rogue("7k/8/8/p7/8/8/8/R6K w - - 0 1");
        Tile(s, FogTile.Instance, "a5", "b1");

        Assert.DoesNotContain("a1a5", Moves(s, "a1"));
        Assert.Contains("a1a4", Moves(s, "a1"));
        Assert.Contains("a1b1", Moves(s, "a1"));
    }

    // ------------------------------------------------------------------ maps and robustness

    [Theory]
    [InlineData("chain explosions.json")]
    [InlineData("Double scapegoats.json")]
    [InlineData("Predator Test.json")]
    [InlineData("fog test.json")]
    [InlineData("guardian test.json")]
    [InlineData("phantom test.json")]
    [InlineData("scapegoat test.json")]
    [InlineData("standard chess.json")]
    public void Legacy_map_loads_and_plays_a_random_game(string file)
    {
        var s = MapLoader.Load(System.IO.File.ReadAllText(System.IO.Path.Combine(MapLoaderTests.LegacyMapsDir, file)), ContentLibrary.CreateCatalog());
        Game.BeginGame(s);
        RandomGame(s, new Random(file.Length), 60);
    }

    [Fact]
    public void Random_games_with_all_content_keep_the_hash_and_unwind_exactly()
    {
        var rng = new Random(2026);
        for (int game = 0; game < 40; game++)
        {
            var s = Rogue("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w - - 0 1");
            foreach (var sq in new[] { "c2", "f7", "d7" }) s.GrantAbility(Must(s, sq), ExplodingAbility.Instance);
            foreach (var sq in new[] { "b1", "g8" }) s.GrantAbility(Must(s, sq), ScapegoatAbility.Instance);
            foreach (var sq in new[] { "a1", "h8", "d1" }) s.GrantAbility(Must(s, sq), PhantomAbility.Instance);
            foreach (var sq in new[] { "e2", "e7" }) s.GrantAbility(Must(s, sq), PredatorAbility.Instance);
            s.SetTile(Sq(s, "d4"), WallTile.Instance);
            s.SetTile(Sq(s, "e5"), GuardianTile.Instance);
            s.SetTile(Sq(s, "c5"), FogTile.Instance);
            s.ClearHistory();

            string start = s.Dump();
            ulong hash = s.Hash;
            int mark = s.UndoDepth;
            RandomGame(s, rng, 100);
            s.UndoTo(mark);

            Assert.Equal(start, s.Dump());
            Assert.Equal(hash, s.Hash);
        }
    }

    private static void RandomGame(GameState s, Random rng, int plies)
    {
        var moves = new List<Move>();
        for (int ply = 0; ply < plies; ply++)
        {
            if (s.Rules.Evaluate(s) != GameOutcome.Ongoing) return;
            moves.Clear();
            MoveGenerator.GenerateLegal(s, moves);
            if (moves.Count == 0) return;
            Assert.True(Game.PlayTurn(s, moves[rng.Next(moves.Count)]).Accepted);
            Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
        }
    }
}
