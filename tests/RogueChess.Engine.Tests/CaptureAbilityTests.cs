using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RogueChess.Engine.AI;
using RogueChess.Engine.Content;
using RogueChess.Engine.Content.Abilities;
using RogueChess.Engine.Content.Tiles;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;
using Xunit;
using Xunit.Abstractions;

namespace RogueChess.Engine.Tests;

/// <summary>Capture rewrites (Piercing, Bouncer) and the ranged attack (Marksman) ported from the prototype.</summary>
public class CaptureAbilityTests
{
    private readonly ITestOutputHelper _out;
    public CaptureAbilityTests(ITestOutputHelper output) => _out = output;

    private static GameState Rogue(string fen) => Fen.Parse(fen, RuleSet.Rogue);
    private static int Sq(IBoardView s, string name) => MoveText.ParseSquare(s, name);
    private static Piece? At(GameState s, string name) => s.PieceAt(Sq(s, name));
    private static Piece Must(GameState s, string name) => At(s, name) ?? throw new InvalidOperationException($"no piece on {name}");

    private static GameState Setup(string fen, params (string Square, AbilityDef Ability)[] abilities)
    {
        var s = Rogue(fen);
        foreach (var (sq, ability) in abilities) s.GrantAbility(Must(s, sq), ability);
        s.ClearHistory();
        return s;
    }

    private static List<string> Moves(GameState s, string from)
    {
        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(s, moves);
        return moves.Where(m => m.From == Sq(s, from)).Select(m => MoveText.Format(s, m)).ToList();
    }

    private static TurnResult Play(GameState s, string move, int shuffleSeed = 0)
    {
        var m = MoveText.FindLegal(s, move) ?? throw new InvalidOperationException($"{move} is not legal");
        var result = Game.PlayTurn(s, m, new ResolveOptions(Trace: true, ShuffleSeed: shuffleSeed));
        Assert.True(result.Accepted, result.RejectReason);
        Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
        return result;
    }

    private static void AssertPiece(GameState s, string square, PieceDef def, Side side)
    {
        var p = Must(s, square);
        Assert.Same(def, p.Def);
        Assert.Equal(side, p.Side);
    }

    private static int Charges(Piece p) => p.GetAbility(MarksmanAbility.Instance)!.Var(MarksmanAbility.ChargesVar);

    private static GameState LoadLegacy(string file)
        => MapLoader.Load(File.ReadAllText(Path.Combine(MapLoaderTests.LegacyMapsDir, file)), ContentLibrary.CreateCatalog());

    // ------------------------------------------------------------------ Piercing

    [Fact]
    public void Piercing_jumps_over_the_target_and_captures_the_piece_behind_it()
    {
        var s = Setup("4k3/8/8/q7/b7/8/8/R3K3 w - - 0 1", ("a1", PiercingAbility.Instance));
        var queen = Must(s, "a5");
        Play(s, "a1a4");

        AssertPiece(s, "a5", StandardPieces.Rook, Side.White);
        AssertPiece(s, "a4", StandardPieces.Bishop, Side.Black); // jumped over, untouched
        Assert.False(queen.IsOnBoard);
        Assert.Null(At(s, "a1"));
    }

    [Fact]
    public void Piercing_lands_on_an_empty_square_behind_the_target_without_capturing()
    {
        var s = Setup("4k3/8/8/8/p7/8/8/R3K3 w - - 0 1", ("a1", PiercingAbility.Instance));
        Play(s, "a1a4");
        AssertPiece(s, "a5", StandardPieces.Rook, Side.White);
        AssertPiece(s, "a4", StandardPieces.Pawn, Side.Black);
    }

    [Theory]
    [InlineData("p3k3/8/8/8/8/8/8/R3K3 w - - 0 1", "a1a8")]  // target on the board edge
    [InlineData("4k3/8/8/N7/p7/8/8/R3K3 w - - 0 1", "a1a4")] // friendly piece behind the target
    public void Piercing_captures_normally_when_it_cannot_jump(string fen, string move)
    {
        var s = Setup(fen, ("a1", PiercingAbility.Instance));
        var target = s.PieceAt(Sq(s, move[2..]))!;
        Play(s, move);
        Assert.False(target.IsOnBoard);
        AssertPiece(s, move[2..], StandardPieces.Rook, Side.White);
    }

    [Fact]
    public void Piercing_captures_normally_when_the_square_behind_cannot_be_landed_on_or_hides_in_fog()
    {
        var pit = Setup("4k3/8/8/8/p7/8/8/R3K3 w - - 0 1", ("a1", PiercingAbility.Instance));
        pit.SetTile(Sq(pit, "a5"), TestPositions.Pit);
        pit.ClearHistory();
        Play(pit, "a1a4");
        AssertPiece(pit, "a4", StandardPieces.Rook, Side.White);

        var fog = Setup("4k3/8/8/q7/b7/8/8/R3K3 w - - 0 1", ("a1", PiercingAbility.Instance));
        fog.SetTile(Sq(fog, "a5"), FogTile.Instance);
        fog.ClearHistory();
        Play(fog, "a1a4");
        AssertPiece(fog, "a4", StandardPieces.Rook, Side.White);
        AssertPiece(fog, "a5", StandardPieces.Queen, Side.Black);
    }

    [Fact]
    public void Piercing_after_a_knight_jump_continues_diagonally_as_in_the_prototype()
    {
        var s = Setup("4k3/8/8/8/3q4/2p5/8/1N2K3 w - - 0 1", ("b1", PiercingAbility.Instance));
        Play(s, "b1c3");
        AssertPiece(s, "d4", StandardPieces.Knight, Side.White);
        AssertPiece(s, "c3", StandardPieces.Pawn, Side.Black);
    }

    [Fact]
    public void Guardian_on_the_landing_square_stops_the_pierce_and_the_attacker_stays()
    {
        var s = Setup("4k3/8/8/q7/b7/8/8/R3K3 w - - 0 1", ("a1", PiercingAbility.Instance));
        s.SetTile(Sq(s, "a5"), GuardianTile.Instance);
        s.ClearHistory();
        Play(s, "a1a4");

        AssertPiece(s, "a1", StandardPieces.Rook, Side.White);
        AssertPiece(s, "a4", StandardPieces.Bishop, Side.Black);
        AssertPiece(s, "a5", StandardPieces.Queen, Side.Black);
        Assert.True(s.TileAt(Sq(s, "a5")).Def.IsDefault); // used up
    }

    [Fact]
    public void Jumping_over_a_guarded_target_does_not_use_the_guardian_up()
    {
        var s = Setup("4k3/8/8/8/p7/8/8/R3K3 w - - 0 1", ("a1", PiercingAbility.Instance));
        s.SetTile(Sq(s, "a4"), GuardianTile.Instance);
        s.ClearHistory();
        Play(s, "a1a4");
        AssertPiece(s, "a5", StandardPieces.Rook, Side.White);
        Assert.Same(GuardianTile.Instance, s.TileAt(Sq(s, "a4")).Def);
    }

    [Fact]
    public void Scapegoat_saves_the_piece_on_the_landing_square()
    {
        var s = Setup("4k3/8/1n6/q7/b7/8/8/R3K3 w - - 0 1", ("a1", PiercingAbility.Instance), ("b6", ScapegoatAbility.Instance));
        var goat = Must(s, "b6");
        Play(s, "a1a4");

        Assert.False(goat.IsOnBoard);
        AssertPiece(s, "a5", StandardPieces.Queen, Side.Black);
        AssertPiece(s, "a4", StandardPieces.Bishop, Side.Black);
        AssertPiece(s, "a1", StandardPieces.Rook, Side.White);
    }

    [Fact]
    public void Piercing_an_exploding_piece_behind_the_target_blows_up_the_attacker_and_the_target()
    {
        var s = Setup("4k3/8/8/q7/b7/8/8/R3K3 w - - 0 1", ("a1", PiercingAbility.Instance), ("a5", ExplodingAbility.Instance));
        var rook = Must(s, "a1");
        var bishop = Must(s, "a4");
        Play(s, "a1a4");
        Assert.False(rook.IsOnBoard);
        Assert.False(bishop.IsOnBoard);
        Assert.Null(At(s, "a5"));
    }

    [Fact]
    public void Jumping_over_an_exploding_target_does_not_set_it_off()
    {
        var s = Setup("4k3/8/8/8/p7/8/8/R3K3 w - - 0 1", ("a1", PiercingAbility.Instance), ("a4", ExplodingAbility.Instance));
        Play(s, "a1a4");
        AssertPiece(s, "a5", StandardPieces.Rook, Side.White);
        AssertPiece(s, "a4", StandardPieces.Pawn, Side.Black);
    }

    [Fact]
    public void Piercing_works_in_any_ability_order_and_after_being_frozen_and_thawed()
    {
        // A4: identity doesn't depend on ability order (here Frozen is added after Piercing, then removed).
        var s = Setup("4k3/8/8/q7/b7/8/8/R3K3 w - - 0 1", ("a1", PiercingAbility.Instance));
        var rook = Must(s, "a1");
        s.GrantAbility(rook, FrozenAbility.Instance, 1);
        s.ClearHistory();
        Assert.Empty(Moves(s, "a1"));
        Play(s, "e1d1");   // white's turn ends: thawed
        Play(s, "e8d8");
        Play(s, "a1a4");
        AssertPiece(s, "a5", StandardPieces.Rook, Side.White);
    }

    // ------------------------------------------------------------------ Bouncer

    [Fact]
    public void Bouncer_knocks_the_target_one_square_back_and_takes_its_square()
    {
        var s = Setup("4k3/8/8/8/p7/8/8/R3K3 w - - 0 1", ("a1", BouncerAbility.Instance));
        Play(s, "a1a4");
        AssertPiece(s, "a4", StandardPieces.Rook, Side.White);
        AssertPiece(s, "a5", StandardPieces.Pawn, Side.Black);
    }

    [Fact]
    public void B5_bouncer_knocks_one_square_not_the_attackers_distance()
    {
        // The prototype sent the pawn as far again as the rook travelled (a4 + 3 = a7) and destroyed the rook there.
        var s = Setup("4k3/r7/8/8/p7/8/8/R3K3 w - - 0 1", ("a1", BouncerAbility.Instance));
        Play(s, "a1a4");
        AssertPiece(s, "a5", StandardPieces.Pawn, Side.Black);
        AssertPiece(s, "a7", StandardPieces.Rook, Side.Black);
        AssertPiece(s, "a4", StandardPieces.Rook, Side.White);
    }

    [Fact]
    public void Bouncer_destroys_an_enemy_on_the_square_behind_and_gets_the_kill()
    {
        var s = Setup("4k3/8/8/n7/p7/8/8/R3K3 w - - 0 1", ("a1", BouncerAbility.Instance), ("a1", PredatorAbility.Instance));
        var knight = Must(s, "a5");
        Play(s, "a1a4");
        Assert.False(knight.IsOnBoard);
        AssertPiece(s, "a5", StandardPieces.Pawn, Side.Black);
        AssertPiece(s, "a4", StandardPieces.Queen, Side.White); // Predator: rook → queen for the kill
    }

    [Theory]
    [InlineData("p3k3/8/8/8/8/8/8/R3K3 w - - 0 1", "a1a8")]  // target on the edge
    [InlineData("4k3/8/8/N7/p7/8/8/R3K3 w - - 0 1", "a1a4")] // friendly piece behind
    public void Bouncer_captures_normally_when_the_target_cannot_be_knocked_back(string fen, string move)
    {
        var s = Setup(fen, ("a1", BouncerAbility.Instance));
        var target = s.PieceAt(Sq(s, move[2..]))!;
        Play(s, move);
        Assert.False(target.IsOnBoard);
        AssertPiece(s, move[2..], StandardPieces.Rook, Side.White);
    }

    [Fact]
    public void Bouncer_captures_normally_when_the_square_behind_cannot_be_landed_on()
    {
        var s = Setup("4k3/8/8/8/p7/8/8/R3K3 w - - 0 1", ("a1", BouncerAbility.Instance));
        s.SetTile(Sq(s, "a5"), TestPositions.Pit);
        s.ClearHistory();
        var pawn = Must(s, "a4");
        Play(s, "a1a4");
        Assert.False(pawn.IsOnBoard);
        AssertPiece(s, "a4", StandardPieces.Rook, Side.White);
    }

    [Fact]
    public void Legacy_bouncer_map_captures_normally_because_its_own_king_is_behind_the_target()
    {
        // Rook b1 takes d1; e1 holds the white king, so there's no bounce. The prototype bounced to f1 instead.
        var s = LoadLegacy("bouncer test.json");
        Assert.True(Must(s, "b1").HasAbility(BouncerAbility.Instance));
        Game.BeginGame(s);
        Play(s, "b1d1");
        AssertPiece(s, "d1", StandardPieces.Rook, Side.White);
        AssertPiece(s, "f1", StandardPieces.Rook, Side.Black);
        AssertPiece(s, "e1", StandardPieces.King, Side.White);
        Assert.Equal(5, Enumerable.Range(0, s.Area).Count(sq => s.PieceAt(sq) != null));
    }

    [Fact]
    public void Bounced_exploding_target_does_not_explode_but_one_bounced_into_does()
    {
        var push = Setup("4k3/8/8/8/p7/8/8/R3K3 w - - 0 1", ("a1", BouncerAbility.Instance), ("a4", ExplodingAbility.Instance));
        Play(push, "a1a4");
        AssertPiece(push, "a5", StandardPieces.Pawn, Side.Black);
        AssertPiece(push, "a4", StandardPieces.Rook, Side.White);

        var into = Setup("4k3/8/8/n7/p7/8/8/R3K3 w - - 0 1", ("a1", BouncerAbility.Instance), ("a5", ExplodingAbility.Instance));
        var rook = Must(into, "a1");
        var pawn = Must(into, "a4");
        Play(into, "a1a4");
        Assert.False(rook.IsOnBoard); // the knight blew up next to its new square
        Assert.False(pawn.IsOnBoard);
    }

    [Fact]
    public void Guardian_does_not_stop_a_bounce()
    {
        // Under the target: it isn't captured, so the guardian stays. Under the piece bounced into: not a move capture.
        var s = Setup("4k3/8/8/n7/p7/8/8/R3K3 w - - 0 1", ("a1", BouncerAbility.Instance));
        s.SetTile(Sq(s, "a4"), GuardianTile.Instance);
        s.SetTile(Sq(s, "a5"), GuardianTile.Instance);
        s.ClearHistory();
        Play(s, "a1a4");
        AssertPiece(s, "a4", StandardPieces.Rook, Side.White);
        AssertPiece(s, "a5", StandardPieces.Pawn, Side.Black);
        Assert.Same(GuardianTile.Instance, s.TileAt(Sq(s, "a4")).Def);
        Assert.Same(GuardianTile.Instance, s.TileAt(Sq(s, "a5")).Def);
    }

    [Fact]
    public void Scapegoat_saving_the_piece_bounced_into_blocks_the_whole_bounce()
    {
        var s = Setup("4k3/8/1b6/n7/p7/8/8/R3K3 w - - 0 1", ("a1", BouncerAbility.Instance), ("b6", ScapegoatAbility.Instance));
        var goat = Must(s, "b6");
        Play(s, "a1a4");
        Assert.False(goat.IsOnBoard);
        AssertPiece(s, "a5", StandardPieces.Knight, Side.Black);
        AssertPiece(s, "a4", StandardPieces.Pawn, Side.Black);
        AssertPiece(s, "a1", StandardPieces.Rook, Side.White);
    }

    [Fact]
    public void Frozen_target_is_knocked_back_and_stays_frozen()
    {
        var s = Setup("4k3/8/8/8/p7/8/8/R3K3 w - - 0 1", ("a1", BouncerAbility.Instance));
        s.GrantAbility(Must(s, "a4"), FrozenAbility.Instance, 3);
        s.ClearHistory();
        Play(s, "a1a4");
        Assert.True(Must(s, "a5").HasAbility(FrozenAbility.Instance));
    }

    [Fact]
    public void Piercing_wins_over_bouncer_whatever_the_ability_order()
    {
        const string fen = "4k3/8/8/n7/p7/8/8/R3K3 w - - 0 1";
        var a = Setup(fen, ("a1", PiercingAbility.Instance), ("a1", BouncerAbility.Instance));
        var b = Setup(fen, ("a1", BouncerAbility.Instance), ("a1", PiercingAbility.Instance));
        Play(a, "a1a4");
        Play(b, "a1a4");
        AssertPiece(a, "a5", StandardPieces.Rook, Side.White);
        AssertPiece(a, "a4", StandardPieces.Pawn, Side.Black);
        Assert.Equal(a.Hash, b.Hash);
    }

    [Fact]
    public void Bounce_into_an_explosion_is_the_same_in_any_hook_order()
    {
        const string fen = "4k3/8/1b6/n7/p7/8/8/R3K3 w - - 0 1";
        GameState Make() => Setup(fen, ("a1", BouncerAbility.Instance), ("a5", ExplodingAbility.Instance), ("b6", ScapegoatAbility.Instance));
        var reference = Make();
        Play(reference, "a1a4");
        for (int seed = 1; seed <= 12; seed++)
        {
            var s = Make();
            Play(s, "a1a4", seed);
            Assert.Equal(reference.Dump(), s.Dump());
        }
    }

    // ------------------------------------------------------------------ Marksman

    [Fact]
    public void B4_marksman_offers_a_ranged_shot_next_to_the_normal_capture()
    {
        var s = Setup("4k3/8/8/8/r7/8/8/R3K3 w - - 0 1", ("a1", MarksmanAbility.Instance));
        var moves = Moves(s, "a1");
        Assert.Contains("a1a4", moves);
        Assert.Contains("a1a4@marksman", moves);
        Assert.Single(moves, m => m.EndsWith("@marksman"));
        Assert.Equal(moves.Count, moves.Distinct().Count());
    }

    [Fact]
    public void B4_a_normal_capture_moves_the_marksman_and_spends_no_charge()
    {
        var s = Setup("4k3/8/8/8/r7/8/8/R3K3 w - - 0 1", ("a1", MarksmanAbility.Instance));
        var target = Must(s, "a4");
        Play(s, "a1a4");
        Assert.False(target.IsOnBoard);
        var rook = Must(s, "a4");
        Assert.Equal(1, Charges(rook));
    }

    [Fact]
    public void Ranged_shot_destroys_the_target_without_moving_and_spends_a_charge()
    {
        var s = Setup("4k3/8/8/8/r7/8/8/R3K3 w - - 0 1", ("a1", MarksmanAbility.Instance));
        var target = Must(s, "a4");
        var result = Play(s, "a1a4@marksman");
        Assert.False(target.IsOnBoard);
        var rook = Must(s, "a1");
        Assert.Equal(0, Charges(rook));
        Assert.Contains(Causes.Ranged, result.Trace!.Format(s));

        Play(s, "e8d8");
        Assert.DoesNotContain(Moves(s, "a1"), m => m.Contains('@'));
    }

    [Fact]
    public void Charges_come_from_the_ability_variable_and_undo_restores_them_exactly()
    {
        // A2: charges are a piece variable, so undo (and AI search) see them.
        var s = Rogue("4k3/8/8/8/r7/8/8/R3K3 w - - 0 1");
        var rook = Must(s, "a1");
        s.GrantAbility(rook, MarksmanAbility.Instance, 2);
        s.ClearHistory();
        string start = s.Dump();
        ulong hash = s.Hash;

        Play(s, "a1a4@marksman");
        Assert.Equal(1, Charges(rook));
        Assert.NotEqual(hash, s.Hash); // charges are hashed

        s.UndoTo(0);
        Assert.Equal(2, Charges(rook));
        Assert.Equal(hash, s.Hash);
        Assert.Equal(start, s.Dump());
    }

    [Fact]
    public void Ranged_targets_are_the_pieces_own_capture_squares_not_through_fog()
    {
        var s = Setup("4k3/8/8/8/r7/8/8/R1n1K3 w - - 0 1", ("a1", MarksmanAbility.Instance));
        s.SetTile(Sq(s, "a4"), FogTile.Instance);
        s.ClearHistory();
        var shots = Moves(s, "a1").Where(m => m.EndsWith("@marksman")).ToList();
        Assert.Equal(new[] { "a1c1@marksman" }, shots);
    }

    [Fact]
    public void Guardian_does_not_stop_a_ranged_shot()
    {
        var s = Setup("4k3/8/8/8/r7/8/8/R3K3 w - - 0 1", ("a1", MarksmanAbility.Instance));
        s.SetTile(Sq(s, "a4"), GuardianTile.Instance);
        s.ClearHistory();
        var target = Must(s, "a4");
        Play(s, "a1a4@marksman");
        Assert.False(target.IsOnBoard);
        Assert.Same(GuardianTile.Instance, s.TileAt(Sq(s, "a4")).Def);
    }

    [Fact]
    public void Scapegoat_dies_for_the_shot_target_and_the_charge_is_spent()
    {
        var s = Setup("4k3/8/1n6/r7/8/8/8/R3K3 w - - 0 1", ("a1", MarksmanAbility.Instance), ("b6", ScapegoatAbility.Instance));
        var goat = Must(s, "b6");
        Play(s, "a1a5@marksman");
        Assert.False(goat.IsOnBoard);
        AssertPiece(s, "a5", StandardPieces.Rook, Side.Black);
        Assert.Equal(0, Charges(Must(s, "a1")));
    }

    [Fact]
    public void Shooting_an_exploding_piece_keeps_the_marksman_out_of_the_blast()
    {
        var s = Setup("4k3/8/1p6/n7/8/8/8/R3K3 w - - 0 1", ("a1", MarksmanAbility.Instance), ("a5", ExplodingAbility.Instance));
        var pawn = Must(s, "b6");
        Play(s, "a1a5@marksman");
        Assert.False(pawn.IsOnBoard);
        AssertPiece(s, "a1", StandardPieces.Rook, Side.White);
    }

    [Fact]
    public void Frozen_marksman_cannot_shoot()
    {
        var s = Setup("4k3/8/8/8/r7/8/8/R3K3 w - - 0 1", ("a1", MarksmanAbility.Instance));
        s.GrantAbility(Must(s, "a1"), FrozenAbility.Instance, 2);
        s.ClearHistory();
        Assert.Empty(Moves(s, "a1"));
    }

    [Fact]
    public void Ranged_kill_is_credited_to_the_marksman_piece()
    {
        // B3's root cause: the kill names the piece, so Predator (and later ChronoCapture) see it.
        var s = Setup("4k3/8/8/8/8/1r6/P7/4K3 w - - 0 1", ("a2", MarksmanAbility.Instance), ("a2", PredatorAbility.Instance));
        Play(s, "a2b3@marksman");
        AssertPiece(s, "a2", StandardPieces.Knight, Side.White);
        Assert.Null(At(s, "b3"));
    }

    [Fact]
    public void Ranged_shot_on_the_king_wins()
    {
        var s = Setup("k7/8/8/8/8/8/8/R6K w - - 0 1", ("a1", MarksmanAbility.Instance));
        Play(s, "a1a8@marksman");
        Assert.Equal(GameOutcome.WhiteWins, s.Rules.Evaluate(s));
    }

    [Fact]
    public void Ai_chooses_the_ranged_shot_when_melee_would_lose_the_piece()
    {
        // Taking the exploding knight by moving onto a5 gets the rook blown up; shooting it doesn't.
        var s = Setup("k7/8/8/n7/8/8/8/R6K w - - 0 1", ("a1", MarksmanAbility.Instance), ("a5", ExplodingAbility.Instance));
        var profile = AgentProfile.Club with { Id = "sharp", ChoiceMargin = 0, RootNoise = 0, MaxDepth = 4, NodeBudget = 400_000 };
        var decision = new AgentFactory().Create(profile).Decide(s);
        _out.WriteLine(decision.Format(s));
        Assert.Equal("a1a5@marksman", MoveText.Format(s, decision.Move!.Value));
    }

    // ------------------------------------------------------------------ legacy maps and fuzz

    [Fact]
    public void Legacy_piercing_map_loads_and_pierces()
    {
        var s = LoadLegacy("piercing test.json");
        Assert.True(Must(s, "a1").HasAbility(PiercingAbility.Instance));
        Game.BeginGame(s);
        Play(s, "a1a4"); // over the bishop, onto the queen
        AssertPiece(s, "a5", StandardPieces.Rook, Side.White);
        AssertPiece(s, "a4", StandardPieces.Bishop, Side.Black);
    }

    [Fact]
    public void Legacy_marksman_map_loads_with_one_charge_each()
    {
        var s = LoadLegacy("marksman test.json");
        Assert.Equal(1, Charges(Must(s, "a1")));
        Assert.Equal(1, Charges(Must(s, "a4")));
        Game.BeginGame(s);
        Play(s, "a1a4@marksman");
        Assert.Null(At(s, "a4"));
        AssertPiece(s, "a1", StandardPieces.Rook, Side.White);
    }

    [Theory]
    [InlineData("piercing test.json")]
    [InlineData("bouncer test.json")]
    [InlineData("marksman test.json")]
    [InlineData("exploding decorator.json")]
    public void Legacy_map_plays_random_games_with_exact_undo(string file)
    {
        var rng = new Random(file.Length);
        for (int game = 0; game < 10; game++)
        {
            var s = LoadLegacy(file);
            Game.BeginGame(s);
            s.ClearHistory();
            string start = s.Dump();
            ulong hash = s.Hash;
            RandomGame(s, rng, 60, out _);
            s.UndoTo(0);
            Assert.Equal(start, s.Dump());
            Assert.Equal(hash, s.Hash);
        }
    }

    [Fact]
    public void Random_games_with_capture_abilities_keep_hash_and_undo_exact()
    {
        var rng = new Random(4242);
        int shots = 0, played = 0;
        for (int game = 0; game < 30; game++)
        {
            var s = Rogue("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w - - 0 1");
            foreach (var sq in new[] { "a1", "g1", "d8", "b7" }) s.GrantAbility(Must(s, sq), PiercingAbility.Instance);
            foreach (var sq in new[] { "h1", "d1", "a8", "e7" }) s.GrantAbility(Must(s, sq), BouncerAbility.Instance);
            foreach (var sq in new[] { "c1", "f8", "d2", "b8" }) s.GrantAbility(Must(s, sq), MarksmanAbility.Instance, 2);
            s.GrantAbility(Must(s, "d1"), PiercingAbility.Instance); // both: Piercing applies
            foreach (var sq in new[] { "c2", "f7", "h8" }) s.GrantAbility(Must(s, sq), ExplodingAbility.Instance);
            foreach (var sq in new[] { "b1", "g8" }) s.GrantAbility(Must(s, sq), ScapegoatAbility.Instance);
            foreach (var sq in new[] { "e2", "c7" }) s.GrantAbility(Must(s, sq), PredatorAbility.Instance);
            s.GrantAbility(Must(s, "h2"), FrozenAbility.Instance, 2);
            s.SetTile(Sq(s, "d4"), WallTile.Instance);
            s.SetTile(Sq(s, "e5"), GuardianTile.Instance);
            s.SetTile(Sq(s, "c5"), FogTile.Instance);
            s.SetTile(Sq(s, "f4"), GuardianTile.Instance);
            s.ClearHistory();

            string start = s.Dump();
            ulong hash = s.Hash;
            played += RandomGame(s, rng, 100, out int gameShots);
            shots += gameShots;
            s.UndoTo(0);
            Assert.Equal(start, s.Dump());
            Assert.Equal(hash, s.Hash);
        }
        Assert.True(shots > 0, "no ranged shot was played");
        Assert.True(played > 300, $"only {played} plies played");
    }

    private static int RandomGame(GameState s, Random rng, int plies, out int abilityActions)
    {
        abilityActions = 0;
        var moves = new List<Move>();
        int ply = 0;
        for (; ply < plies; ply++)
        {
            if (s.Rules.Evaluate(s) != GameOutcome.Ongoing) break;
            moves.Clear();
            MoveGenerator.GenerateLegal(s, moves);
            if (moves.Count == 0) break;
            var m = moves[rng.Next(moves.Count)];
            if (m.Is(MoveFlags.Ability)) abilityActions++;
            var result = Game.PlayTurn(s, m);
            Assert.True(result.Accepted, result.RejectReason);
            Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
        }
        return ply;
    }
}
