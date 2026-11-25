using System;
using System.Collections.Generic;
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

/// <summary>
/// Independent verification of the capture abilities (Piercing, Bouncer, Marksman), the earlier content
/// (Exploding, Scapegoat, Phantom, Predator, Wall/Fog/Guardian, standard pieces and promotion) against the
/// prototype, plus regressions for PORT_PLAN.md §1 bugs that had no explicit test. Every multi-ply test checks
/// the incremental hash after each ply and unwinds ply by ply back to the start.
/// </summary>
public class Verify_CaptureAndCoreTests
{
    private readonly ITestOutputHelper _out;
    public Verify_CaptureAndCoreTests(ITestOutputHelper output) => _out = output;

    /// <summary>Roguelike rules with en passant switched on, to test abilities against en passant.</summary>
    private static readonly RuleSet RogueWithEnPassant = new("rogue-ep", forbidSelfCheck: false, castling: false,
        enPassant: true, new[] { StandardPieces.Queen }, WinCondition.CaptureRoyal);

    private static readonly AgentProfile Sharp = AgentProfile.Club with
    {
        Id = "sharp", ChoiceMargin = 0, RootNoise = 0, MaxDepth = 4, NodeBudget = 400_000, TimeBudgetMs = 0,
    };

    private static int Sq(IBoardView s, string name) => MoveText.ParseSquare(s, name);
    private static Piece? At(GameState s, string name) => s.PieceAt(Sq(s, name));
    private static Piece Must(GameState s, string name) => At(s, name) ?? throw new InvalidOperationException($"no piece on {name}");

    private static GameState Setup(string fen, RuleSet? rules, params (string Square, AbilityDef Ability)[] abilities)
    {
        var s = Fen.Parse(fen, rules ?? RuleSet.Rogue);
        foreach (var (sq, ability) in abilities) s.GrantAbility(Must(s, sq), ability);
        s.ClearHistory();
        return s;
    }

    private static GameState Setup(string fen, params (string Square, AbilityDef Ability)[] abilities)
        => Setup(fen, null, abilities);

    private static void Tile(GameState s, TileDef tile, params string[] squares)
    {
        foreach (var sq in squares) s.SetTile(Sq(s, sq), tile);
        s.ClearHistory();
    }

    private static List<string> Moves(GameState s, string? from = null)
    {
        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(s, moves);
        return moves.Where(m => from == null || m.From == Sq(s, from)).Select(m => MoveText.Format(s, m)).ToList();
    }

    private static void AssertPiece(GameState s, string square, PieceDef def, Side side)
    {
        var p = Must(s, square);
        Assert.Same(def, p.Def);
        Assert.Equal(side, p.Side);
    }

    private static int Charges(Piece p) => p.GetAbility(MarksmanAbility.Instance)!.Var(MarksmanAbility.ChargesVar);
    private static int Kills(Piece p) => p.GetAbility(PredatorAbility.Instance)!.Var(PredatorAbility.KillsVar);

    /// <summary>
    /// Plays moves one ply at a time, checking the incremental hash and a clone after every ply, and remembering
    /// each ply's start so <see cref="Unwind"/> can check that undo restores every intermediate state exactly.
    /// </summary>
    private sealed class Line
    {
        private readonly List<(int Mark, string Dump, ulong Hash)> _plies = new();
        public Line(GameState s) => S = s;
        public GameState S { get; }

        public TurnResult Play(string move, int shuffleSeed = 0)
        {
            var m = MoveText.FindLegal(S, move) ?? throw new InvalidOperationException($"{move} is not legal; legal: {string.Join(' ', Moves(S))}");
            return Play(m, shuffleSeed);
        }

        public TurnResult Play(Move m, int shuffleSeed = 0)
        {
            _plies.Add((S.UndoDepth, S.Dump(), S.Hash));
            var result = Game.PlayTurn(S, m, new ResolveOptions(Trace: true, ShuffleSeed: shuffleSeed));
            Assert.True(result.Accepted, result.RejectReason);
            Assert.Equal(S.ComputeHashFromScratch(), S.Hash);
            var clone = S.Clone();
            Assert.Equal(S.Dump(), clone.Dump());
            Assert.Equal(S.Hash, clone.Hash);
            return result;
        }

        public void Unwind()
        {
            for (int i = _plies.Count - 1; i >= 0; i--)
            {
                S.UndoTo(_plies[i].Mark);
                Assert.Equal(_plies[i].Dump, S.Dump());
                Assert.Equal(_plies[i].Hash, S.Hash);
                Assert.Equal(S.ComputeHashFromScratch(), S.Hash);
            }
            _plies.Clear();
        }
    }

    // ================================================================== Piercing

    [Fact]
    public void Piercing_landing_on_a_wall_breaks_the_wall()
    {
        var s = Setup("4k3/8/8/8/p7/8/8/R3K3 w - - 0 1", ("a1", PiercingAbility.Instance));
        Tile(s, WallTile.Instance, "a5");
        var line = new Line(s);
        line.Play("a1a4");
        AssertPiece(s, "a5", StandardPieces.Rook, Side.White);
        AssertPiece(s, "a4", StandardPieces.Pawn, Side.Black);
        Assert.True(s.TileAt(Sq(s, "a5")).Def.IsDefault);
        line.Unwind();
    }

    [Fact]
    public void Piercing_landing_on_an_empty_guardian_tile_lands_and_keeps_the_guardian()
    {
        var s = Setup("4k3/8/8/8/p7/8/8/R3K3 w - - 0 1", ("a1", PiercingAbility.Instance));
        Tile(s, GuardianTile.Instance, "a5");
        var line = new Line(s);
        line.Play("a1a4");
        AssertPiece(s, "a5", StandardPieces.Rook, Side.White);
        Assert.Same(GuardianTile.Instance, s.TileAt(Sq(s, "a5")).Def);
        line.Unwind();
    }

    [Fact]
    public void Piercing_jumped_target_is_not_destroyed_so_a_scapegoat_next_to_it_stays()
    {
        // The redirect layer removes the capture before the prevent layer, so the scapegoat has nothing to save.
        var s = Setup("4k3/8/8/8/pn6/8/8/R3K3 w - - 0 1", ("a1", PiercingAbility.Instance), ("b4", ScapegoatAbility.Instance));
        var line = new Line(s);
        line.Play("a1a4");
        AssertPiece(s, "a5", StandardPieces.Rook, Side.White);
        AssertPiece(s, "a4", StandardPieces.Pawn, Side.Black);
        AssertPiece(s, "b4", StandardPieces.Knight, Side.Black);
        line.Unwind();
    }

    [Fact]
    public void Piercing_jumps_over_a_king_and_takes_a_king_behind_the_target()
    {
        // Over the king: as in the prototype the king is untouched and the game goes on.
        var over = Setup("8/8/8/8/k7/8/8/R3K3 w - - 0 1", ("a1", PiercingAbility.Instance));
        new Line(over).Play("a1a4");
        AssertPiece(over, "a4", StandardPieces.King, Side.Black);
        AssertPiece(over, "a5", StandardPieces.Rook, Side.White);
        Assert.Equal(GameOutcome.Ongoing, over.Rules.Evaluate(over));

        // A king standing behind the target is captured on the landing square.
        var behind = Setup("8/8/8/k7/p7/8/8/R3K3 w - - 0 1", ("a1", PiercingAbility.Instance));
        new Line(behind).Play("a1a4");
        Assert.Equal(GameOutcome.WhiteWins, behind.Rules.Evaluate(behind));
    }

    [Fact]
    public void Piercing_works_for_black_attacking_down_the_board()
    {
        var s = Setup("r3k3/8/8/8/P7/8/8/4K3 b - - 0 1", ("a8", PiercingAbility.Instance));
        var line = new Line(s);
        line.Play("a8a4");
        AssertPiece(s, "a3", StandardPieces.Rook, Side.Black);
        AssertPiece(s, "a4", StandardPieces.Pawn, Side.White);
        line.Unwind();
    }

    [Fact]
    public void Piercing_pawn_jumps_diagonally_and_promotes_on_the_landing_square()
    {
        var s = Setup("4k3/2p5/1P6/8/8/8/8/4K3 w - - 0 1", ("b6", PiercingAbility.Instance));
        var pawn = Must(s, "b6");
        var line = new Line(s);
        line.Play("b6c7");
        Assert.Equal(Sq(s, "d8"), pawn.Square);
        Assert.Same(StandardPieces.Queen, pawn.Def); // promotion follows the Moved fact, wherever it lands
        Assert.True(pawn.HasAbility(PiercingAbility.Instance));
        AssertPiece(s, "c7", StandardPieces.Pawn, Side.Black);
        line.Unwind();
        Assert.Same(StandardPieces.Pawn, pawn.Def);
    }

    [Fact]
    public void Piercing_and_bouncer_leave_en_passant_as_a_normal_capture()
    {
        foreach (var ability in new AbilityDef[] { PiercingAbility.Instance, BouncerAbility.Instance })
        {
            var s = Setup("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1", RogueWithEnPassant, ("e5", ability));
            var victim = Must(s, "d5");
            var line = new Line(s);
            line.Play("e5d6");
            Assert.False(victim.IsOnBoard);
            AssertPiece(s, "d6", StandardPieces.Pawn, Side.White);
            Assert.Null(At(s, "d7"));
            line.Unwind();
        }
    }

    [Fact]
    public void Piercing_kill_on_the_landing_square_feeds_a_predator_but_a_plain_jump_does_not()
    {
        var kill = Setup("4k3/8/8/q7/b7/8/8/R3K3 w - - 0 1", ("a1", PiercingAbility.Instance), ("a1", PredatorAbility.Instance));
        var rook = Must(kill, "a1");
        var line = new Line(kill);
        line.Play("a1a4");
        Assert.Same(StandardPieces.Queen, rook.Def);
        Assert.Equal(1, Kills(rook));
        line.Unwind();

        var jump = Setup("4k3/8/8/8/b7/8/8/R3K3 w - - 0 1", ("a1", PiercingAbility.Instance), ("a1", PredatorAbility.Instance));
        var rook2 = Must(jump, "a1");
        new Line(jump).Play("a1a4");
        Assert.Same(StandardPieces.Rook, rook2.Def);
        Assert.Equal(0, Kills(rook2));
    }

    // ================================================================== Bouncer

    [Fact]
    public void Bouncer_knocks_the_target_into_the_enemy_king_and_wins()
    {
        var s = Setup("8/8/8/k7/p7/8/8/R3K3 w - - 0 1", ("a1", BouncerAbility.Instance));
        var line = new Line(s);
        line.Play("a1a4");
        AssertPiece(s, "a5", StandardPieces.Pawn, Side.Black);
        AssertPiece(s, "a4", StandardPieces.Rook, Side.White);
        Assert.Equal(GameOutcome.WhiteWins, s.Rules.Evaluate(s));
        line.Unwind();
    }

    [Fact]
    public void Bouncer_cannot_kill_a_king_it_hits_the_king_is_only_knocked_back()
    {
        var s = Setup("8/8/8/8/k7/8/8/R3K3 w - - 0 1", ("a1", BouncerAbility.Instance));
        var line = new Line(s);
        line.Play("a1a4");
        AssertPiece(s, "a5", StandardPieces.King, Side.Black);
        AssertPiece(s, "a4", StandardPieces.Rook, Side.White);
        Assert.Equal(GameOutcome.Ongoing, s.Rules.Evaluate(s));
        line.Unwind();
    }

    [Fact]
    public void Bouncer_knocking_a_pawn_onto_its_last_rank_promotes_it()
    {
        // The prototype's Pawn promoted after any MoveEvent onto its last rank, a knock-back included.
        var s = Setup("R3k3/8/8/8/8/8/p7/7K w - - 0 1", ("a8", BouncerAbility.Instance));
        var pawn = Must(s, "a2");
        var line = new Line(s);
        line.Play("a8a2");
        Assert.Equal(Sq(s, "a1"), pawn.Square);
        Assert.Same(StandardPieces.Queen, pawn.Def);
        AssertPiece(s, "a2", StandardPieces.Rook, Side.White);
        line.Unwind();
    }

    [Fact]
    public void B5_bouncing_knight_knocks_the_target_one_diagonal_step()
    {
        var s = Setup("4k3/8/8/8/8/2p5/8/1N2K3 w - - 0 1", ("b1", BouncerAbility.Instance));
        var line = new Line(s);
        line.Play("b1c3");
        AssertPiece(s, "c3", StandardPieces.Knight, Side.White);
        AssertPiece(s, "d4", StandardPieces.Pawn, Side.Black); // not d5 (the prototype's full L again)
        line.Unwind();
    }

    [Fact]
    public void Bouncer_target_is_not_destroyed_so_its_scapegoat_neighbour_stays()
    {
        var s = Setup("4k3/8/8/8/pn6/8/8/R3K3 w - - 0 1", ("a1", BouncerAbility.Instance), ("b4", ScapegoatAbility.Instance));
        var line = new Line(s);
        line.Play("a1a4");
        AssertPiece(s, "a5", StandardPieces.Pawn, Side.Black);
        AssertPiece(s, "b4", StandardPieces.Knight, Side.Black);
        AssertPiece(s, "a4", StandardPieces.Rook, Side.White);
        line.Unwind();
    }

    [Fact]
    public void Bouncer_knocks_the_target_onto_a_wall_which_breaks()
    {
        var s = Setup("4k3/8/8/8/p7/8/8/R3K3 w - - 0 1", ("a1", BouncerAbility.Instance));
        Tile(s, WallTile.Instance, "a5");
        var line = new Line(s);
        line.Play("a1a4");
        AssertPiece(s, "a5", StandardPieces.Pawn, Side.Black);
        Assert.True(s.TileAt(Sq(s, "a5")).Def.IsDefault);
        line.Unwind();
    }

    [Fact]
    public void Bounced_into_is_not_a_move_capture_so_fog_and_guardian_do_not_stop_it()
    {
        foreach (var tile in new TileDef[] { FogTile.Instance, GuardianTile.Instance })
        {
            var s = Setup("4k3/8/8/q7/p7/8/8/R3K3 w - - 0 1", ("a1", BouncerAbility.Instance));
            Tile(s, tile, "a5");
            var queen = Must(s, "a5");
            var line = new Line(s);
            line.Play("a1a4");
            Assert.False(queen.IsOnBoard);
            AssertPiece(s, "a5", StandardPieces.Pawn, Side.Black);
            line.Unwind();
        }
    }

    [Fact]
    public void Bounced_into_kill_feeds_a_predator_bouncer_but_a_plain_bounce_does_not()
    {
        var s = Setup("4k3/8/8/q7/p7/8/8/R3K3 w - - 0 1", ("a1", PredatorAbility.Instance), ("a1", BouncerAbility.Instance));
        var rook = Must(s, "a1");
        var line = new Line(s);
        line.Play("a1a4");
        Assert.Equal(1, Kills(rook));
        Assert.Same(StandardPieces.Queen, rook.Def);
        line.Unwind();

        var plain = Setup("4k3/8/8/8/p7/8/8/R3K3 w - - 0 1", ("a1", PredatorAbility.Instance), ("a1", BouncerAbility.Instance));
        var rook2 = Must(plain, "a1");
        new Line(plain).Play("a1a4");
        Assert.Equal(0, Kills(rook2));
        Assert.Same(StandardPieces.Rook, rook2.Def);
    }

    [Fact]
    public void Bouncer_works_for_black_and_in_any_ability_order()
    {
        // A4: the prototype only bounced while Bouncer was the outermost decorator.
        var orders = new[]
        {
            new AbilityDef[] { BouncerAbility.Instance, ExplodingAbility.Instance, MarksmanAbility.Instance },
            new AbilityDef[] { MarksmanAbility.Instance, ExplodingAbility.Instance, BouncerAbility.Instance },
            new AbilityDef[] { ExplodingAbility.Instance, BouncerAbility.Instance },
        };
        string? reference = null;
        foreach (var order in orders)
        {
            var s = Setup("r3k3/8/8/8/P7/8/8/4K3 b - - 0 1", order.Select(a => ("a8", a)).ToArray());
            var line = new Line(s);
            line.Play("a8a4");
            AssertPiece(s, "a4", StandardPieces.Rook, Side.Black);
            AssertPiece(s, "a3", StandardPieces.Pawn, Side.White);
            string board = string.Join(",", s.PiecesOnBoard().OrderBy(p => p.Square).Select(p => $"{p.Square}{p.Def.Symbol}{p.Side}"));
            reference ??= board;
            Assert.Equal(reference, board);
            line.Unwind();
        }
    }

    // ================================================================== Marksman

    [Fact]
    public void Marksman_hand_built_shots_that_are_not_offered_are_rejected_and_change_nothing()
    {
        // Like hand-built casts: the action must name a target the marksman could shoot, or nothing happens.
        var s = Setup("4k3/7p/8/8/r7/8/8/R1n1K3 w - - 0 1", ("a1", MarksmanAbility.Instance));
        Tile(s, WallTile.Instance, "a3");
        Tile(s, FogTile.Instance, "c1");
        Game.BeginGame(s);
        s.ClearHistory();
        string before = s.Dump();
        ulong hash = s.Hash;
        int a1 = Sq(s, "a1");
        int index = Must(s, "a1").AbilityIndex(MarksmanAbility.Instance);

        foreach (var to in new[] { "a2", "a4", "c1", "h7", "e1" }) // empty, behind a wall, in fog, not on a line, own king
        {
            var bad = new Move(a1, Sq(s, to), MoveFlags.Capture | MoveFlags.Ability, Aux: index);
            var result = Game.PlayTurn(s, bad);
            Assert.False(result.Accepted, $"shot at {to} was accepted");
            Assert.Equal(hash, s.Hash);
            Assert.Equal(before, s.Dump());
        }
        Assert.DoesNotContain(Moves(s, "a1"), m => m.EndsWith("@marksman"));
    }

    [Fact]
    public void Marksman_with_two_charges_shoots_twice_then_only_melee_and_every_ply_undoes()
    {
        var s = Fen.Parse("4k3/8/8/8/r7/7K/8/R6r w - - 0 1", RuleSet.Rogue);
        var rook = Must(s, "a1");
        s.GrantAbility(rook, MarksmanAbility.Instance, 2);
        s.ClearHistory();
        var line = new Line(s);

        line.Play("a1a4@marksman");
        Assert.Equal(1, Charges(rook));
        line.Play("e8d8");
        Assert.Contains("a1h1@marksman", Moves(s, "a1"));
        line.Play("a1h1@marksman");
        Assert.Equal(0, Charges(rook));
        Assert.Equal(Sq(s, "a1"), rook.Square);
        line.Play("d8c8");
        Assert.DoesNotContain(Moves(s, "a1"), m => m.Contains('@'));

        line.Unwind();
        Assert.Equal(2, Charges(rook));
    }

    [Fact]
    public void Marksman_shot_ignores_piercing_and_bouncer_on_the_same_piece()
    {
        foreach (var other in new AbilityDef[] { PiercingAbility.Instance, BouncerAbility.Instance })
        {
            var s = Setup("4k3/8/8/q7/p7/8/8/R3K3 w - - 0 1", ("a1", other), ("a1", MarksmanAbility.Instance));
            var pawn = Must(s, "a4");
            var line = new Line(s);
            line.Play("a1a4@marksman");
            Assert.False(pawn.IsOnBoard);
            AssertPiece(s, "a1", StandardPieces.Rook, Side.White);
            AssertPiece(s, "a5", StandardPieces.Queen, Side.Black);
            line.Unwind();

            // The melee capture still gets rewritten.
            line.Play("a1a4");
            Assert.Equal(other == PiercingAbility.Instance ? StandardPieces.Pawn : StandardPieces.Rook, Must(s, "a4").Def);
            Assert.Equal(1, Charges(Must(s, other == PiercingAbility.Instance ? "a5" : "a4")));
            line.Unwind();
        }
    }

    [Fact]
    public void Marksman_shooting_an_adjacent_exploding_piece_is_caught_in_the_blast()
    {
        var s = Setup("4k3/8/8/8/8/8/n7/R3K3 w - - 0 1", ("a1", MarksmanAbility.Instance), ("a2", ExplodingAbility.Instance));
        var rook = Must(s, "a1");
        var line = new Line(s);
        line.Play("a1a2@marksman");
        Assert.False(rook.IsOnBoard);
        line.Unwind();
        Assert.True(rook.IsOnBoard);
        Assert.Equal(1, Charges(rook));
    }

    [Fact]
    public void Marksman_targets_follow_the_pieces_own_movement()
    {
        // Pawn: diagonals only, not the square ahead. Knight: over pieces. Rook: not through an ally, even with Phantom.
        var pawn = Setup("4k3/8/8/8/1pp5/2P5/8/4K3 w - - 0 1", ("c3", MarksmanAbility.Instance));
        Assert.Equal(new[] { "c3b4@marksman" }, Moves(pawn, "c3").Where(m => m.Contains('@')).ToArray());

        var knight = Setup("4k3/8/8/8/8/1pP5/PP6/N3K3 w - - 0 1", ("a1", MarksmanAbility.Instance));
        Assert.Equal(new[] { "a1b3@marksman" }, Moves(knight, "a1").Where(m => m.Contains('@')).ToArray());

        var phantom = Setup("4k3/8/8/r7/8/P7/8/R3K3 w - - 0 1", ("a1", MarksmanAbility.Instance), ("a1", PhantomAbility.Instance));
        var moves = Moves(phantom, "a1");
        Assert.Contains("a1a5", moves); // phantom melee capture through the ally
        Assert.DoesNotContain("a1a5@marksman", moves);
    }

    [Fact]
    public void Ai_search_leaves_marksman_charges_and_the_state_untouched()
    {
        var s = Setup("k7/8/8/n7/8/8/8/R6K w - - 0 1", ("a1", MarksmanAbility.Instance), ("a5", ExplodingAbility.Instance));
        string before = s.Dump();
        ulong hash = s.Hash;
        var decision = new AgentFactory().Create(Sharp).Decide(s);
        Assert.NotNull(decision.Move);
        Assert.Equal(before, s.Dump());
        Assert.Equal(hash, s.Hash);
        Assert.Equal(1, Charges(Must(s, "a1")));
    }

    // ================================================================== Exploding

    [Fact]
    public void Explosion_in_a_corner_only_reaches_the_squares_on_the_board()
    {
        var s = Setup("n6k/1P6/1N6/8/8/8/8/7K w - - 0 1", ("a8", ExplodingAbility.Instance));
        var line = new Line(s);
        line.Play("b6a8");
        Assert.Null(At(s, "a8"));
        Assert.Null(At(s, "b7"));
        AssertPiece(s, "h8", StandardPieces.King, Side.Black);
        line.Unwind();
    }

    [Fact]
    public void Exploding_piece_on_a_guardian_is_not_captured_and_does_not_explode()
    {
        var s = Setup("4k3/8/8/8/n7/8/8/R3K3 w - - 0 1", ("a4", ExplodingAbility.Instance));
        Tile(s, GuardianTile.Instance, "a4");
        var line = new Line(s);
        line.Play("a1a4");
        AssertPiece(s, "a4", StandardPieces.Knight, Side.Black);
        AssertPiece(s, "a1", StandardPieces.Rook, Side.White);
        Assert.True(s.TileAt(Sq(s, "a4")).Def.IsDefault);
        line.Unwind();
    }

    [Fact]
    public void Exploding_piece_in_fog_is_set_off_by_a_neighbouring_blast()
    {
        var s = Setup("4k3/8/8/8/bn6/8/8/1R2K3 w - - 0 1", ("b4", ExplodingAbility.Instance), ("a4", ExplodingAbility.Instance));
        Tile(s, FogTile.Instance, "a4");
        var line = new Line(s);
        line.Play("b1b4");
        Assert.Null(At(s, "a4")); // b4's blast reached it in fog, then it went off too
        Assert.Null(At(s, "b4"));
        line.Unwind();
    }

    [Fact]
    public void Exploding_pawn_taken_en_passant_explodes_on_its_own_square_and_kills_the_capturer()
    {
        var s = Setup("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1", RogueWithEnPassant, ("d5", ExplodingAbility.Instance));
        var capturer = Must(s, "e5");
        var line = new Line(s);
        line.Play("e5d6");
        Assert.False(capturer.IsOnBoard);
        line.Unwind();
    }

    [Fact]
    public void Exploding_scapegoat_that_dies_for_an_ally_still_explodes_next_to_it()
    {
        // Port behaviour (prototype ambiguous because of A4): the sacrifice is a death, so the blast follows and
        // takes the ally it just saved. The attacker was blocked two squares away and survives.
        var s = Setup("4k3/8/8/8/nn6/8/8/R3K3 w - - 0 1", ("b4", ScapegoatAbility.Instance), ("b4", ExplodingAbility.Instance));
        var line = new Line(s);
        line.Play("a1a4");
        Assert.Null(At(s, "b4"));
        Assert.Null(At(s, "a4"));
        AssertPiece(s, "a1", StandardPieces.Rook, Side.White);
        line.Unwind();
    }

    [Fact]
    public void Promoted_exploding_pawn_keeps_exploding()
    {
        var s = Setup("4k3/P7/8/8/8/8/8/r3K3 w - - 0 1", ("a7", ExplodingAbility.Instance));
        var line = new Line(s);
        line.Play("a7a8q");
        var queen = Must(s, "a8");
        Assert.Same(StandardPieces.Queen, queen.Def);
        Assert.True(queen.HasAbility(ExplodingAbility.Instance));
        line.Play("a1a8");
        Assert.Null(At(s, "a8")); // the capturing rook died in the blast
        line.Unwind();
    }

    // ================================================================== Scapegoat

    [Fact]
    public void Scapegoat_next_to_a_scapegoat_dies_for_it_and_the_target_lives()
    {
        var s = Setup("4k3/8/8/8/nn6/8/8/R3K3 w - - 0 1", ("a4", ScapegoatAbility.Instance), ("b4", ScapegoatAbility.Instance));
        var target = Must(s, "a4");
        var other = Must(s, "b4");
        var line = new Line(s);
        line.Play("a1a4");
        Assert.True(target.IsOnBoard);
        Assert.False(other.IsOnBoard);
        AssertPiece(s, "a1", StandardPieces.Rook, Side.White);
        line.Unwind();
    }

    [Fact]
    public void Two_scapegoats_in_one_blast_both_die()
    {
        // A ranged shot sets off the bishop, whose blast hits both white scapegoats. Each claims the destroy aimed
        // at the other, the claims don't overlap, so both stand and both die (in any hook order).
        // (Scapegoats on the exploding piece's own side would instead die for it in wave 0.)
        var s = Setup("4k3/8/8/1N6/1Nb5/8/8/2R1K3 w - - 0 1", ("c1", MarksmanAbility.Instance),
            ("b5", ScapegoatAbility.Instance), ("b4", ScapegoatAbility.Instance), ("c4", ExplodingAbility.Instance));
        var line = new Line(s);
        for (int seed = 0; seed <= 6; seed++)
        {
            line.Play("c1c4@marksman", seed);
            Assert.Null(At(s, "b4"));
            Assert.Null(At(s, "b5"));
            line.Unwind();
        }
    }

    [Fact]
    public void Scapegoat_does_not_protect_enemies()
    {
        var s = Setup("4k3/8/8/8/nN6/8/8/R3K3 w - - 0 1", ("b4", ScapegoatAbility.Instance));
        var line = new Line(s);
        line.Play("a1a4");
        AssertPiece(s, "a4", StandardPieces.Rook, Side.White);
        AssertPiece(s, "b4", StandardPieces.Knight, Side.White);
        line.Unwind();
    }

    [Fact]
    public void A1_a_scapegoat_killed_by_the_first_blast_cannot_save_an_ally_from_the_second()
    {
        // Wave 0: the white rook takes E1 (c4). Wave 1: E1's blast kills E2 (d5) and the white scapegoat (d3),
        // which dies for the rook (now on c4). Wave 2: E2's blast reaches the rook again and the white knight on e4,
        // both next to the dead scapegoat's square: a dead scapegoat no longer reacts, so both die.
        var s = Setup("7k/8/8/3n4/2n1N3/3N4/8/2R4K w - - 0 1",
            ("c4", ExplodingAbility.Instance), ("d5", ExplodingAbility.Instance), ("d3", ScapegoatAbility.Instance));
        var ally = Must(s, "e4");
        var rook = Must(s, "c1");
        var line = new Line(s);
        line.Play("c1c4");
        Assert.False(ally.IsOnBoard);
        Assert.False(rook.IsOnBoard);
        Assert.Null(At(s, "d3"));
        line.Unwind();
    }

    [Fact]
    public void Scapegoat_saves_a_pawn_from_en_passant_and_the_capturer_still_moves()
    {
        // Port behaviour (the prototype had no en passant): the capture is cancelled, but the capturer's move
        // goes to the empty en passant square, so nothing blocks it.
        var s = Setup("4k3/8/8/2npP3/8/8/8/4K3 w - d6 0 1", RogueWithEnPassant, ("c5", ScapegoatAbility.Instance));
        var victim = Must(s, "d5");
        var line = new Line(s);
        line.Play("e5d6");
        Assert.True(victim.IsOnBoard);
        Assert.Null(At(s, "c5"));
        AssertPiece(s, "d6", StandardPieces.Pawn, Side.White);
        line.Unwind();
    }

    // ================================================================== Phantom

    [Fact]
    public void Phantom_passes_several_allies_but_not_into_fog_or_onto_a_pit_and_offers_no_duplicates()
    {
        var s = Setup("4k3/r7/8/8/P7/8/P7/R3K3 w - - 0 1", ("a1", PhantomAbility.Instance));
        Tile(s, TestPositions.Pit, "a3");
        var moves = Moves(s, "a1");
        Assert.Equal(moves.Count, moves.Distinct().Count());
        Assert.Contains("a1a5", moves);
        Assert.Contains("a1a6", moves);
        Assert.Contains("a1a7", moves);
        Assert.DoesNotContain("a1a3", moves);
        Assert.DoesNotContain("a1a8", moves);

        Tile(s, FogTile.Instance, "a7");
        Assert.DoesNotContain("a1a7", Moves(s, "a1"));
    }

    [Fact]
    public void Phantom_does_not_change_leapers_or_pawns()
    {
        // The prototype extended any move "blocked" by an aligned ally, so a phantom king slid on without limit
        // past an adjacent ally. The port follows the documented rule: only sliding moves pass through.
        var start = Fen.Parse("4k3/8/8/8/8/8/1PP5/NK6 w - - 0 1", RuleSet.Rogue);
        var plain = Moves(start).OrderBy(x => x).ToList();
        foreach (var sq in new[] { "a1", "b1", "b2" }) start.GrantAbility(Must(start, sq), PhantomAbility.Instance);
        start.ClearHistory();
        Assert.Equal(plain, Moves(start).OrderBy(x => x).ToList());
    }

    [Fact]
    public void Phantom_capture_beyond_an_ally_is_stopped_by_a_guardian()
    {
        var s = Setup("4k3/8/8/r7/8/P7/8/R3K3 w - - 0 1", ("a1", PhantomAbility.Instance));
        Tile(s, GuardianTile.Instance, "a5");
        var line = new Line(s);
        line.Play("a1a5");
        AssertPiece(s, "a5", StandardPieces.Rook, Side.Black);
        AssertPiece(s, "a1", StandardPieces.Rook, Side.White);
        line.Unwind();
    }

    [Fact]
    public void Perft_counts_phantom_and_marksman_moves()
    {
        var s = Fen.Parse(Fen.StartPosition, RuleSet.Rogue);
        Assert.Equal(20, Perft.Count(s, 1));
        s.GrantAbility(Must(s, "a1"), PhantomAbility.Instance);
        s.ClearHistory();
        Assert.Equal(25, Perft.Count(s, 1)); // a3..a6 through the pawn, and a7

        // Marksman: one extra action per enemy its own movement attacks.
        var m = Setup("4k3/8/8/8/r1n5/8/8/R1Q1K3 w - - 0 1", ("a1", MarksmanAbility.Instance), ("c1", MarksmanAbility.Instance));
        var moves = Moves(m);
        Assert.Equal(new[] { "a1a4@marksman", "c1c4@marksman" }.OrderBy(x => x), moves.Where(x => x.Contains('@')).OrderBy(x => x));
    }

    [Fact]
    public void Perft_with_every_capture_ability_leaves_the_state_unchanged_and_is_deterministic()
    {
        GameState Make()
        {
            var s = Fen.Parse(PerftTests.Kiwipete, RuleSet.Standard);
            void G(string sq, AbilityDef a) => s.GrantAbility(Must(s, sq), a);
            G("e5", PiercingAbility.Instance);
            G("f3", BouncerAbility.Instance);
            G("d5", MarksmanAbility.Instance);
            G("e7", ScapegoatAbility.Instance);
            G("f6", ExplodingAbility.Instance);
            G("b4", PredatorAbility.Instance);
            G("a1", PhantomAbility.Instance);
            s.SetTile(Sq(s, "c6"), FogTile.Instance);
            s.SetTile(Sq(s, "g6"), GuardianTile.Instance);
            s.ClearHistory();
            return s;
        }

        var a = Make();
        string before = a.Dump();
        ulong hash = a.Hash;
        long count = Perft.Count(a, 3);
        Assert.Equal(before, a.Dump());
        Assert.Equal(hash, a.Hash);
        Assert.Equal(count, Perft.Count(Make(), 3));
        Assert.Equal(count, Perft.Count(a.Clone(), 3));
        _out.WriteLine($"perft(3) = {count}");
    }

    // ================================================================== Predator

    [Fact]
    public void Predator_climbs_the_whole_chain_and_stops_at_queen_while_still_counting()
    {
        var s = Setup("k7/8/8/8/8/1p6/P7/7K w - - 0 1", ("a2", PredatorAbility.Instance));
        var p = Must(s, "a2");
        var line = new Line(s);
        line.Play("a2b3");
        Assert.Same(StandardPieces.Knight, p.Def);
        line.Play("a8a7");

        // Hand the predator one more victim per turn; the black king shuffles in between.
        string king = "a7";
        foreach (var (victim, expected) in new[] { ("c5", StandardPieces.Bishop), ("e3", StandardPieces.Rook), ("e8", StandardPieces.Queen), ("e1", StandardPieces.Queen) })
        {
            s.Place(StandardPieces.Pawn, Side.Black, Sq(s, victim));
            line.Play(MoveText.SquareName(s, p.Square) + victim);
            Assert.Same(expected, p.Def);
            string next = king == "a7" ? "a8" : "a7";
            line.Play(king + next);
            king = next;
        }
        Assert.Equal(5, Kills(p));
        line.Unwind();
        Assert.Same(StandardPieces.Pawn, p.Def);
        Assert.Equal(0, Kills(p));
    }

    [Fact]
    public void Predator_king_never_evolves()
    {
        // The prototype's chain lookup returned -1 for a king and evolved it into a Pawn; the port keeps it a king.
        var s = Setup("7k/8/8/8/8/8/p7/K7 w - - 0 1", ("a1", PredatorAbility.Instance));
        var king = Must(s, "a1");
        var line = new Line(s);
        line.Play("a1a2");
        Assert.Same(StandardPieces.King, king.Def);
        Assert.Equal(1, Kills(king));
        line.Unwind();
    }

    [Fact]
    public void Predator_blocked_by_a_guardian_gets_no_kill()
    {
        var s = Setup("7k/8/8/8/8/1p6/P7/K7 w - - 0 1", ("a2", PredatorAbility.Instance));
        Tile(s, GuardianTile.Instance, "b3");
        var p = Must(s, "a2");
        var line = new Line(s);
        line.Play("a2b3");
        Assert.Same(StandardPieces.Pawn, p.Def);
        Assert.Equal(0, Kills(p));
        Assert.Equal(Sq(s, "a2"), p.Square);
        line.Unwind();
    }

    [Fact]
    public void Predator_pawn_capturing_onto_the_last_rank_promotes_and_then_evolves_from_the_promoted_piece()
    {
        // Rogue: promotion to queen, the evolution has nowhere to go.
        var rogue = Setup("1n2k3/P7/8/8/8/8/8/4K3 w - - 0 1", ("a7", PredatorAbility.Instance));
        var p = Must(rogue, "a7");
        var line = new Line(rogue);
        line.Play("a7b8q");
        Assert.Same(StandardPieces.Queen, p.Def);
        line.Unwind();

        // Standard, promoting to a knight: the kill (counted in the same wave) then evolves the knight (port rule).
        var std = Setup("1n2k3/P7/8/8/8/8/8/4K3 w - - 0 1", RuleSet.Standard, ("a7", PredatorAbility.Instance));
        var q = Must(std, "a7");
        var line2 = new Line(std);
        line2.Play("a7b8n");
        Assert.Same(StandardPieces.Bishop, q.Def);
        Assert.Equal(1, Kills(q));
        line2.Unwind();
        Assert.Same(StandardPieces.Pawn, q.Def);
    }

    // ================================================================== Tiles

    [Fact]
    public void Guardian_stops_a_pawn_capture_once()
    {
        var s = Setup("4k3/8/8/8/2p5/1P6/8/N3K3 w - - 0 1");
        Tile(s, GuardianTile.Instance, "c4");
        var line = new Line(s);
        line.Play("b3c4");
        AssertPiece(s, "c4", StandardPieces.Pawn, Side.Black);
        AssertPiece(s, "b3", StandardPieces.Pawn, Side.White);
        Assert.True(s.TileAt(Sq(s, "c4")).Def.IsDefault);
        line.Play("e8d8");
        line.Play("b3c4");
        AssertPiece(s, "c4", StandardPieces.Pawn, Side.White);
        line.Unwind();
    }

    [Fact]
    public void Guardian_stops_en_passant_but_the_capturer_still_moves()
    {
        // Port behaviour (no en passant in the prototype): same shape as Scapegoat vs en passant.
        var s = Setup("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1", RogueWithEnPassant);
        Tile(s, GuardianTile.Instance, "d5");
        var line = new Line(s);
        line.Play("e5d6");
        AssertPiece(s, "d5", StandardPieces.Pawn, Side.Black);
        AssertPiece(s, "d6", StandardPieces.Pawn, Side.White);
        Assert.True(s.TileAt(Sq(s, "d5")).Def.IsDefault);
        line.Unwind();
    }

    [Fact]
    public void Fog_hides_its_occupant_from_every_kind_of_move_capture()
    {
        var s = Setup("4k3/8/8/3pP3/8/1p6/P7/4K3 w - d6 0 1", RogueWithEnPassant, ("a2", MarksmanAbility.Instance));
        Assert.Contains("e5d6", Moves(s));
        Assert.Contains("a2b3", Moves(s));
        Tile(s, FogTile.Instance, "d5", "b3");
        var moves = Moves(s);
        Assert.DoesNotContain("e5d6", moves);   // en passant victim in fog
        Assert.DoesNotContain("a2b3", moves);   // pawn capture
        Assert.DoesNotContain("a2b3@marksman", moves);
        Assert.Contains("a2a3", moves);
    }

    [Fact]
    public void Wall_blocks_a_pawn_double_push_and_pawns_cannot_take_an_empty_wall_diagonally()
    {
        // Open question 6: the prototype let pawns "capture" walls diagonally. The port does not (no target).
        var s = Setup("4k3/8/8/8/8/8/P7/4K3 w - - 0 1");
        Tile(s, WallTile.Instance, "a3", "b3");
        Assert.Empty(Moves(s, "a2"));

        var two = Setup("4k3/8/8/8/8/8/P7/4K3 w - - 0 1");
        Tile(two, WallTile.Instance, "a4");
        Assert.Equal(new[] { "a2a3" }, Moves(two, "a2"));
    }

    [Fact]
    public void Knight_landing_on_a_wall_breaks_it()
    {
        var s = Setup("4k3/8/8/8/8/8/8/1N2K3 w - - 0 1");
        Tile(s, WallTile.Instance, "c3");
        var line = new Line(s);
        line.Play("b1c3");
        Assert.True(s.TileAt(Sq(s, "c3")).Def.IsDefault);
        line.Unwind();
        Assert.Same(WallTile.Instance, s.TileAt(Sq(s, "c3")).Def);
    }

    // ================================================================== promotion, B2

    [Fact]
    public void Promotion_keeps_every_ability_and_its_variables_for_each_option()
    {
        for (int option = 0; option < RuleSet.Standard.PromotionOptions.Count; option++)
        {
            var s = Fen.Parse("4k3/P7/8/8/8/8/8/4K3 w - - 0 1", RuleSet.Standard);
            var pawn = Must(s, "a7");
            s.GrantAbility(pawn, MarksmanAbility.Instance, 3);
            s.GrantAbility(pawn, ScapegoatAbility.Instance);
            s.ClearHistory();
            var def = RuleSet.Standard.PromotionOptions[option];
            var line = new Line(s);
            line.Play("a7a8" + def.Symbol);
            Assert.Same(def, pawn.Def);
            Assert.Equal(3, Charges(pawn));
            Assert.True(pawn.HasAbility(ScapegoatAbility.Instance));
            line.Unwind();
            Assert.Same(StandardPieces.Pawn, pawn.Def);
        }
    }

    [Fact]
    public void B2_a_king_carrying_abilities_is_still_the_king_for_check_and_mate()
    {
        var s = Fen.Parse(Fen.StartPosition, RuleSet.Standard);
        s.GrantAbility(Must(s, "e1"), PhantomAbility.Instance);
        s.GrantAbility(Must(s, "e1"), ScapegoatAbility.Instance);
        s.ClearHistory();
        foreach (var m in new[] { "f2f3", "e7e5", "g2g4", "d8h4" }) TestPositions.Play(s, m);
        Assert.True(MoveGenerator.IsInCheck(s, Side.White));
        Assert.Equal(GameOutcome.BlackWins, s.Rules.Evaluate(s));
    }

    [Fact]
    public void B2_check_detection_sees_tiles_and_statuses()
    {
        // Fog hides the king from the rook. (Frozen attackers: BoardEffectTests.Frozen_piece_does_not_attack_under_standard_rules.)
        var onFile = Fen.Parse("4k3/8/8/8/8/8/8/4K2r w - - 0 1", RuleSet.Standard);
        Assert.True(MoveGenerator.IsInCheck(onFile, Side.White));
        onFile.SetTile(Sq(onFile, "e1"), FogTile.Instance);
        Assert.False(MoveGenerator.IsInCheck(onFile, Side.White));
    }

    // ================================================================== AI

    [Fact]
    public void Ai_knows_piercing_jumps_over_a_king_instead_of_taking_it()
    {
        var s = Setup("8/8/8/8/k7/8/8/R6K w - - 0 1", ("a1", PiercingAbility.Instance));
        var shallow = Sharp with { Id = "shallow", MaxDepth = 1, QuiescenceDepth = 0 };
        var decision = new AgentFactory().Create(shallow).Decide(s);
        _out.WriteLine(decision.Format(s));
        var jump = decision.RootMoves.FirstOrDefault(r => MoveText.Format(s, r.Move) == "a1a4");
        Assert.Equal("a1a4", MoveText.Format(s, jump.Move));
        Assert.True(jump.Score < Search.WinThreshold, "the jump over the king was scored as a win");
    }

    [Fact]
    public void Ai_bounces_a_piece_into_the_enemy_king()
    {
        var s = Setup("8/8/8/k7/p7/8/8/R6K w - - 0 1", ("a1", BouncerAbility.Instance));
        var decision = new AgentFactory().Create(Sharp).Decide(s);
        _out.WriteLine(decision.Format(s));
        Assert.Equal("a1a4", MoveText.Format(s, decision.Move!.Value));
        Assert.True(decision.Score >= Search.WinThreshold);
    }

    [Fact]
    public void C2_ai_scores_a_faster_win_higher()
    {
        var factory = new AgentFactory();
        var now = Fen.Parse("k7/8/8/8/8/8/8/R6K w - - 0 1", RuleSet.Rogue);
        var later = Fen.Parse("6k1/5ppp/8/8/8/8/8/R5K1 w - - 0 1", RuleSet.Rogue); // back rank: wins a move later
        var a = factory.Create(Sharp).Decide(now);
        var b = factory.Create(Sharp).Decide(later);
        Assert.True(a.Score >= Search.WinThreshold && b.Score >= Search.WinThreshold);
        Assert.True(a.Score > b.Score, $"win now {a.Score} vs later {b.Score}");
    }

    [Fact]
    public void C1_ai_search_runs_turn_end_so_it_sees_a_freeze_wear_off()
    {
        // The rook on a8 is frozen for one more white turn end. After any white move it thaws; black cannot get its
        // king off the eighth rank or block it, so the thawed rook takes the king. Only a search that runs turn end
        // (where the freeze ticks) sees this forced win.
        var s = Fen.Parse("R6k/6pp/8/8/8/8/8/7K w - - 0 1", RuleSet.Rogue);
        s.GrantAbility(Must(s, "a8"), FrozenAbility.Instance, 1);
        s.ClearHistory();
        Assert.Empty(Moves(s, "a8"));
        var decision = new AgentFactory().Create(Sharp).Decide(s);
        _out.WriteLine(decision.Format(s));
        Assert.True(decision.Score >= Search.WinThreshold, "search did not see the freeze wear off");
    }
}
