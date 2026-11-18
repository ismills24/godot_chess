using System;
using System.Collections.Generic;
using System.Linq;
using RogueChess.Engine.Content;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;
using Xunit;

namespace RogueChess.Engine.Tests;

/// <summary>Tests for the wave resolution contract (docs/RESOLUTION_RULES.md).</summary>
public class ResolverTests
{
    // ------------------------------------------------------------------ helpers

    private static GameState Rogue(string fen) => Fen.Parse(fen, RuleSet.Rogue);
    private static int Sq(IBoardView s, string name) => MoveText.ParseSquare(s, name);
    private static Piece At(GameState s, string name) => s.PieceAt(Sq(s, name)) ?? throw new InvalidOperationException($"no piece on {name}");

    private static TurnResult Play(GameState s, string move, int shuffle = 0)
    {
        var m = MoveText.FindLegal(s, move) ?? throw new InvalidOperationException($"{move} is not legal");
        var result = Game.PlayTurn(s, m, new ResolveOptions(Trace: true, ShuffleSeed: shuffle));
        Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
        return result;
    }

    private static ResolutionTrace Phase(TurnResult r, string phase) => r.Trace!.Resolutions.First(x => x.Phase == phase);

    private static List<Fact> Facts(ResolutionTrace r, FactKinds kind) => r.Waves.SelectMany(w => w.Facts).Where(f => f.Kind == kind).ToList();

    /// <summary>A rogue board with a white king on a1 (to shuffle a1a2 as a "pass") and a black king on h8.</summary>
    private static GameState WithScript(string fen, params Func<GameState, IEnumerable<Proposal>>[] scripts)
    {
        var s = Rogue(fen);
        for (int i = 0; i < scripts.Length; i++)
        {
            var script = scripts[i];
            s.AddGlobal(new ProbeScript($"script{i}", _ => script(s)));
        }
        s.ClearHistory();
        return s;
    }

    private static Proposal Move(GameState s, string from, string to) => Proposal.Move(At(s, from), Sq(s, to), "script");

    // ------------------------------------------------------------------ basics

    [Fact]
    public void Capture_is_destroy_plus_move_in_one_wave()
    {
        var s = Rogue("7k/8/8/p7/8/8/8/R6K w - - 0 1");
        int victim = At(s, "a5").Id;

        var action = Phase(Play(s, "a1a5"), Game.ActionPhase);

        Assert.Single(action.Waves);
        Assert.Equal(victim, Facts(action, FactKinds.Died).Single().Piece!.Id);
        Assert.Equal(At(s, "a5").Id, Facts(action, FactKinds.Killed).Single().Source.Piece!.Id);
        Assert.Single(Facts(action, FactKinds.Moved));
    }

    [Fact]
    public void Exploding_chain_resolves_in_rings()
    {
        // Rook takes the exploding pawn on c4; its blast kills the rook and the exploding pawn on d5,
        // whose blast then kills the pawn on e6.
        var s = Rogue("7k/8/4p3/3p4/R1p5/8/8/K7 w - - 0 1");
        s.GrantAbility(At(s, "c4"), ProbeExploding.Def);
        s.GrantAbility(At(s, "d5"), ProbeExploding.Def);
        s.ClearHistory();

        var action = Phase(Play(s, "a4c4"), Game.ActionPhase);

        var diedByWave = action.Waves.Select(w => w.Facts.Where(f => f.Kind == FactKinds.Died)
            .Select(f => MoveText.SquareName(s, f.From)).OrderBy(x => x).ToArray()).ToArray();
        Assert.Equal(new[] { "c4" }, diedByWave[0]);
        Assert.Equal(new[] { "c4", "d5" }, diedByWave[1]); // the rook (now on c4) and the d5 pawn
        Assert.Equal(new[] { "e6" }, diedByWave[2]);
        Assert.Null(s.PieceAt(Sq(s, "c4")));
        Assert.Null(s.PieceAt(Sq(s, "e6")));
    }

    /// <summary>
    /// The case that broke sequential resolution (RESOLUTION_RULES.md §11): two explosions in one wave, one hits
    /// F, the other hits Scapegoat S next to F. S saves F and dies, whatever order hooks run in.
    /// </summary>
    [Fact]
    public void Exploding_and_scapegoat_resolve_the_same_in_any_hook_order()
    {
        string? reference = null;
        for (int seed = 0; seed <= 20; seed++)
        {
            var s = Rogue("7k/8/8/4p3/4N3/2pN4/8/K7 w - - 0 1");
            foreach (var sq in new[] { "c3", "e5" })
            {
                s.GrantAbility(At(s, sq), ProbeExploding.Def);
                s.GrantAbility(At(s, sq), ProbeDoomed.Def);
            }
            s.GrantAbility(At(s, "e4"), ProbeScapegoat.Def);
            s.AddGlobal(ProbeSmite.Def);
            s.ClearHistory();
            int f = At(s, "d3").Id;

            Play(s, "a1a2", seed);

            Assert.Equal(f, s.PieceAt(Sq(s, "d3"))?.Id); // F survives
            Assert.Null(s.PieceAt(Sq(s, "e4")));          // S sacrificed itself
            Assert.Null(s.PieceAt(Sq(s, "c3")));
            Assert.Null(s.PieceAt(Sq(s, "e5")));
            reference ??= s.Dump();
            Assert.Equal(reference, s.Dump());
        }
    }

    [Fact]
    public void Guardian_cancels_the_capture_and_the_attacker_is_blocked()
    {
        var s = Rogue("7k/8/8/p7/8/8/8/R6K w - - 0 1");
        s.SetTile(Sq(s, "a5"), ProbeGuardianTile.Def);
        s.ClearHistory();
        int rook = At(s, "a1").Id, pawn = At(s, "a5").Id;

        var action = Phase(Play(s, "a1a5"), Game.ActionPhase);

        Assert.Equal(pawn, At(s, "a5").Id);
        Assert.Equal(rook, At(s, "a1").Id);
        Assert.True(s.TileAt(Sq(s, "a5")).Def.IsDefault);
        var collision = Facts(action, FactKinds.Collided).Single();
        Assert.Equal(rook, collision.Piece!.Id);
        Assert.Equal(pawn, collision.Other!.Id);
    }

    // ------------------------------------------------------------------ move conflicts (sliding blocks)

    [Fact]
    public void Two_moves_to_one_square_both_collide()
    {
        var s = WithScript("7k/8/8/8/8/N3N3/8/K7 w - - 0 1", g => new[] { Move(g, "a3", "c3"), Move(g, "e3", "c3") });
        var end = Phase(Play(s, "a1a2"), Game.TurnEndPhase);

        Assert.NotNull(s.PieceAt(Sq(s, "a3")));
        Assert.NotNull(s.PieceAt(Sq(s, "e3")));
        Assert.Null(s.PieceAt(Sq(s, "c3")));
        Assert.Equal(2, Facts(end, FactKinds.Collided).Count);
    }

    [Fact]
    public void Move_into_a_piece_that_stays_put_is_blocked()
    {
        var s = WithScript("7k/8/8/8/8/NN6/8/K7 w - - 0 1", g => new[] { Move(g, "a3", "b3") });
        int a3 = At(s, "a3").Id, b3 = At(s, "b3").Id;

        var end = Phase(Play(s, "a1a2"), Game.TurnEndPhase);

        Assert.Equal(a3, At(s, "a3").Id);
        var collision = Facts(end, FactKinds.Collided).Single();
        Assert.Equal(b3, collision.Other!.Id);
    }

    [Fact]
    public void Head_on_swap_is_blocked()
    {
        var s = WithScript("7k/8/8/8/8/NN6/8/K7 w - - 0 1", g => new[] { Move(g, "a3", "b3"), Move(g, "b3", "a3") });
        int a3 = At(s, "a3").Id, b3 = At(s, "b3").Id;

        Play(s, "a1a2");

        Assert.Equal(a3, At(s, "a3").Id);
        Assert.Equal(b3, At(s, "b3").Id);
    }

    [Fact]
    public void Rotation_of_three_succeeds()
    {
        var s = WithScript("7k/8/8/8/1N6/NN6/8/K7 w - - 0 1",
            g => new[] { Move(g, "a3", "b3"), Move(g, "b3", "b4"), Move(g, "b4", "a3") });
        int a3 = At(s, "a3").Id, b3 = At(s, "b3").Id, b4 = At(s, "b4").Id;

        Play(s, "a1a2");

        Assert.Equal(a3, At(s, "b3").Id);
        Assert.Equal(b3, At(s, "b4").Id);
        Assert.Equal(b4, At(s, "a3").Id);
    }

    [Fact]
    public void Chain_of_moves_into_vacated_squares_succeeds()
    {
        var s = WithScript("7k/8/8/8/8/NN6/8/K7 w - - 0 1", g => new[] { Move(g, "a3", "b3"), Move(g, "b3", "c3") });
        int a3 = At(s, "a3").Id, b3 = At(s, "b3").Id;

        Play(s, "a1a2");

        Assert.Equal(a3, At(s, "b3").Id);
        Assert.Equal(b3, At(s, "c3").Id);
    }

    [Fact]
    public void Destroy_beats_move_and_a_destroyed_piece_frees_its_square()
    {
        var s = WithScript("7k/8/8/8/8/NNN5/8/K7 w - - 0 1",
            g => new[]
            {
                Proposal.Destroy(At(g, "a3"), "script"), Move(g, "a3", "a4"), // a3 both destroyed and moved
                Proposal.Destroy(At(g, "b3"), "script"), Move(g, "c3", "b3"), // c3 moves into b3 as it empties
            });
        int c3 = At(s, "c3").Id;

        Play(s, "a1a2");

        Assert.Null(s.PieceAt(Sq(s, "a3")));
        Assert.Null(s.PieceAt(Sq(s, "a4")));
        Assert.Equal(c3, At(s, "b3").Id);
    }

    // ------------------------------------------------------------------ merges

    [Fact]
    public void Var_deltas_sum_and_a_set_applies_before_the_deltas()
    {
        var target = VarTarget.OfTile(27); // d4
        var s = WithScript("7k/8/8/8/8/8/8/K7 w - - 0 1",
            _ => new[] { Proposal.AddVar(target, 0, 2, "a") },
            _ => new[] { Proposal.AddVar(target, 0, 3, "b") });
        s.SetTile(27, ProbeCounterTile.Def);
        s.ClearHistory();

        Play(s, "a1a2");
        Assert.Equal(5, s.TileAt(27).Var(0));

        var t = WithScript("7k/8/8/8/8/8/8/K7 w - - 0 1",
            _ => new[] { Proposal.SetVar(target, 0, 10, "set") },
            _ => new[] { Proposal.AddVar(target, 0, 1, "add") });
        t.SetTile(27, ProbeCounterTile.Def);
        t.ClearHistory();

        Play(t, "a1a2");
        Assert.Equal(11, t.TileAt(27).Var(0));
    }

    [Fact]
    public void Conflicting_sets_go_to_the_first_source_in_canonical_order()
    {
        var target = VarTarget.OfTile(27);
        var s = WithScript("7k/8/8/8/8/8/8/K7 w - - 0 1",
            _ => new[] { Proposal.SetVar(target, 0, 7, "first") },
            _ => new[] { Proposal.SetVar(target, 0, 9, "second") });
        s.SetTile(27, ProbeCounterTile.Def);
        s.ClearHistory();

        Play(s, "a1a2", shuffle: 3);

        Assert.Equal(7, s.TileAt(27).Var(0)); // globals tie-break by index
    }

    [Theory]
    [InlineData("7k/8/3N4/8/5n2/8/8/K7 w - - 0 1", Side.White)] // white spawner north of d4, black spawner east
    [InlineData("7k/8/3n4/8/5N2/8/8/K7 w - - 0 1", Side.Black)] // swapped: north still wins
    public void Equidistant_spawn_conflict_goes_to_the_northern_source(string fen, Side expected)
    {
        for (int seed = 0; seed < 5; seed++)
        {
            var s = Rogue(fen);
            int d4 = Sq(s, "d4");
            s.GrantAbility(At(s, "d6"), ProbeSpawner.Def, d4);
            s.GrantAbility(At(s, "f4"), ProbeSpawner.Def, d4);
            s.ClearHistory();

            Play(s, "a1a2", seed);

            Assert.Equal(expected, At(s, "d4").Side);
        }
    }

    [Fact]
    public void Remove_beats_add_and_re_adding_keeps_the_larger_values()
    {
        var s = Rogue("7k/8/8/8/8/NN6/8/K7 w - - 0 1");
        var a3 = At(s, "a3");
        var b3 = At(s, "b3");
        s.GrantAbility(a3, ProbeStatus.Def, 4);
        s.AddGlobal(new ProbeScript("add-remove", _ => new[]
        {
            Proposal.AddAbility(a3, ProbeStatus.Def, "add", 9),
            Proposal.RemoveAbility(a3, ProbeStatus.Def, "remove"),
            Proposal.AddAbility(b3, ProbeStatus.Def, "add", 3),
            Proposal.AddAbility(b3, ProbeStatus.Def, "add", 5),
        }));
        s.ClearHistory();

        Play(s, "a1a2");

        Assert.False(a3.HasAbility(ProbeStatus.Def));
        Assert.Equal(5, b3.GetAbility(ProbeStatus.Def)!.Var(0));
    }

    [Fact]
    public void Every_source_that_destroys_a_piece_gets_kill_credit_but_it_dies_once()
    {
        var s = Rogue("7k/8/8/3p4/8/8/8/KR1R4 w - - 0 1");
        int victim = At(s, "d5").Id;
        foreach (var sq in new[] { "b1", "d1" })
        {
            s.GrantAbility(At(s, sq), ProbeSniper.Def, victim);
            s.GrantAbility(At(s, sq), ProbeKillCounter.Def);
        }
        s.ClearHistory();

        var end = Phase(Play(s, "a1a2"), Game.TurnEndPhase);

        Assert.Single(Facts(end, FactKinds.Died));
        Assert.Equal(2, Facts(end, FactKinds.Killed).Count);
        Assert.Equal(1, At(s, "b1").GetAbility(ProbeKillCounter.Def)!.Var(0));
        Assert.Equal(1, At(s, "d1").GetAbility(ProbeKillCounter.Def)!.Var(0));
    }

    // ------------------------------------------------------------------ canonical order

    [Fact]
    public void Bearings_sort_clockwise_from_north()
    {
        var expected = new (int, int)[] { (0, 1), (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (-1, 1) };
        var shuffled = expected.OrderBy(x => x.GetHashCode() ^ 0x5bd1e995).ToList();

        shuffled.Sort((a, b) => CanonicalOrder.CompareBearing(a.Item1, a.Item2, b.Item1, b.Item2));

        Assert.Equal(expected, shuffled);
    }

    // ------------------------------------------------------------------ watchdog

    [Fact]
    public void Oscillating_action_is_rejected_and_leaves_no_trace_on_the_state()
    {
        var s = Fen.Parse("4k3/8/8/8/8/8/8/1N2K3 w - - 0 1");
        var knight = At(s, "b1");
        s.GrantAbility(knight, ProbePingPong.Def);
        s.ClearHistory();
        string before = s.Dump();
        ulong hash = s.Hash;

        var result = Game.PlayTurn(s, new Move(Sq(s, "b1"), Sq(s, "c3")), new ResolveOptions(Trace: true));

        Assert.False(result.Accepted);
        Assert.Contains("oscillation", result.RejectReason);
        Assert.Equal(before, s.Dump());
        Assert.Equal(hash, s.Hash);
        Assert.Equal(0, s.UndoDepth);
    }

    [Fact]
    public void Standard_rules_exclude_moves_the_resolver_rejects()
    {
        var s = Fen.Parse("4k3/8/8/8/8/8/8/1N2K3 w - - 0 1");
        s.GrantAbility(At(s, "b1"), ProbePingPong.Def);
        s.ClearHistory();

        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(s, moves);

        Assert.DoesNotContain(moves, m => m.From == Sq(s, "b1"));
        Assert.Contains(moves, m => m.From == Sq(s, "e1"));
    }

    [Fact]
    public void Runaway_turn_end_is_rolled_back_but_the_turn_still_passes()
    {
        var s = Rogue("7k/8/8/8/8/8/8/K7 w - - 0 1");
        var runaway = s.AddGlobal(ProbeRunaway.Def);
        s.ClearHistory();

        var result = Play(s, "a1a2");

        Assert.True(result.Accepted);
        var end = Phase(result, Game.TurnEndPhase);
        Assert.True(end.RolledBack);
        Assert.Contains("waves", end.Failure);
        Assert.Equal(0, runaway.Var(0));
        Assert.Equal(Side.Black, s.SideToMove);
    }

    // ------------------------------------------------------------------ last-known information, timers, turn phases

    [Fact]
    public void Dead_piece_death_trigger_fires_and_can_revive_it()
    {
        var s = Rogue("7k/8/8/8/8/2N5/8/K7 w - - 0 1");
        var knight = At(s, "c3");
        s.GrantAbility(knight, ProbeRevenant.Def);
        s.GrantAbility(knight, ProbeDoomed.Def);
        s.AddGlobal(ProbeSmite.Def);
        s.ClearHistory();

        var end = Phase(Play(s, "a1a2"), Game.TurnEndPhase);

        Assert.Equal(FactKinds.Died, end.Waves[0].Facts.First().Kind);
        Assert.Equal(FactKinds.Spawned, end.Waves[1].Facts.Single().Kind);
        Assert.Same(knight, At(s, "c3"));
        Assert.True(knight.HasAbility(ProbeRevenant.Def));
    }

    [Fact]
    public void Timer_counts_down_on_its_owners_turn_end_and_expires()
    {
        var s = Rogue("7k/8/8/8/8/2N5/8/K7 w - - 0 1");
        var knight = At(s, "c3");
        s.GrantAbility(knight, ProbeTimer.Def);
        s.ClearHistory();

        Play(s, "a1a2");
        Assert.Equal(1, knight.GetAbility(ProbeTimer.Def)!.Var(0));
        Play(s, "h8h7");
        Assert.Equal(1, knight.GetAbility(ProbeTimer.Def)!.Var(0));
        Play(s, "a2a1");
        Assert.False(knight.HasAbility(ProbeTimer.Def));
    }

    [Fact]
    public void Turn_start_runs_at_game_begin_and_whenever_the_turn_passes()
    {
        var s = Rogue("7k/8/8/8/8/8/8/K7 w - - 0 1");
        var counter = s.AddGlobal(ProbeTurnCounter.Def);
        s.ClearHistory();

        Game.BeginGame(s);
        Assert.Equal(1, counter.Var(0));
        Play(s, "a1a2");
        Assert.Equal(2, counter.Var(0));
    }

    [Fact]
    public void Promotion_happens_through_the_resolver()
    {
        var s = Rogue("7k/1P6/8/8/8/8/8/K7 w - - 0 1");
        int id = At(s, "b7").Id;

        Play(s, "b7b8q");

        Assert.Equal(id, At(s, "b8").Id);
        Assert.Same(StandardPieces.Queen, At(s, "b8").Def);
    }

    [Fact]
    public void Blocked_promotion_capture_does_not_promote()
    {
        var s = Rogue("p6k/1P6/8/8/8/8/8/K7 w - - 0 1");
        s.SetTile(Sq(s, "a8"), ProbeGuardianTile.Def);
        s.ClearHistory();

        Play(s, "b7a8q");

        Assert.Same(StandardPieces.Pawn, At(s, "b7").Def);
        Assert.Equal(Side.Black, At(s, "a8").Side);
    }

    // ------------------------------------------------------------------ determinism and undo

    private static GameState ChainScenario()
    {
        var s = Rogue("7k/8/4p3/3p4/R1p5/8/8/K7 w - - 0 1");
        s.GrantAbility(At(s, "c4"), ProbeExploding.Def);
        s.GrantAbility(At(s, "d5"), ProbeExploding.Def);
        s.GrantAbility(At(s, "e6"), ProbeRevenant.Def);
        s.ClearHistory();
        return s;
    }

    [Fact]
    public void Same_input_gives_the_same_trace_and_hashes()
    {
        var a = ChainScenario();
        var b = a.Clone();

        var ta = Play(a, "a4c4").Trace!;
        var tb = Play(b, "a4c4").Trace!;

        Assert.Equal(ta.Format(a), tb.Format(b));
        Assert.Equal(
            ta.Resolutions.SelectMany(r => r.Waves).Select(w => w.HashAfter),
            tb.Resolutions.SelectMany(r => r.Waves).Select(w => w.HashAfter));
    }

    [Fact]
    public void A_resolved_turn_undoes_exactly()
    {
        var s = ChainScenario();
        string before = s.Dump();
        ulong hash = s.Hash;
        int mark = s.UndoDepth;

        Play(s, "a4c4");
        s.UndoTo(mark);

        Assert.Equal(before, s.Dump());
        Assert.Equal(hash, s.Hash);
    }

    [Fact]
    public void Random_games_with_content_keep_the_hash_and_unwind_exactly()
    {
        var rng = new Random(777);
        var moves = new List<Move>();
        for (int game = 0; game < 30; game++)
        {
            var s = TestPositions.RogueSixBySix();
            foreach (var p in s.PiecesOnBoard().Where(p => p.Def == StandardPieces.Pawn).Take(4))
                s.GrantAbility(p, ProbeExploding.Def);
            s.GrantAbility(At(s, "b1"), ProbeScapegoat.Def);
            s.GrantAbility(At(s, "e6"), ProbeRevenant.Def);
            s.GrantAbility(At(s, "a1"), ProbeTimer.Def);
            s.SetTile(Sq(s, "c4"), ProbeGuardianTile.Def);
            s.AddGlobal(ProbeTurnCounter.Def);
            s.ClearHistory();

            string start = s.Dump();
            ulong startHash = s.Hash;
            var marks = new Stack<int>();
            for (int ply = 0; ply < 80; ply++)
            {
                moves.Clear();
                MoveGenerator.GenerateLegal(s, moves);
                if (moves.Count == 0 || s.Rules.Evaluate(s) != GameOutcome.Ongoing) break;
                marks.Push(s.UndoDepth);
                Game.PlayTurn(s, moves[rng.Next(moves.Count)]);
                Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
            }
            while (marks.Count > 0) s.UndoTo(marks.Pop());

            Assert.Equal(start, s.Dump());
            Assert.Equal(startHash, s.Hash);
        }
    }
}
