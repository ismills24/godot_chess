using System;
using System.Collections.Generic;
using System.Linq;
using RogueChess.Engine.AI;
using RogueChess.Engine.Content;
using RogueChess.Engine.Content.Abilities;
using RogueChess.Engine.Content.Consumables;
using RogueChess.Engine.Content.Relics;
using RogueChess.Engine.Content.Tiles;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;
using Xunit;

namespace RogueChess.Engine.Tests;

/// <summary>
/// Independent verification of the death/movement content (Revenant + Tomb, Cannibal, Slippery) and the run
/// content (ChronoCapture, relics, consumables, clock, history) against the prototype, focusing on the gaps left by
/// DeathAndMovementTests and RunContentTests. Every ply played through <see cref="Step"/> is checked for hash
/// exactness, undo exactness (undo then replay gives the same state) and Clone equivalence.
/// </summary>
public class Verify_DeathMovementRunTests
{
    private static GameState Rogue(string fen) => Fen.Parse(fen, RuleSet.Rogue);
    private static int Sq(IBoardView s, string name) => MoveText.ParseSquare(s, name);
    private static Piece? At(GameState s, string name) => s.PieceAt(Sq(s, name));
    private static Piece Must(GameState s, string name) => At(s, name) ?? throw new InvalidOperationException($"no piece on {name}\n{s.Dump()}");
    private static TileDef TileOn(GameState s, string name) => s.TileAt(Sq(s, name)).Def;

    private static List<string> Legal(GameState s)
    {
        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(s, moves);
        return moves.Select(m => MoveText.Format(s, m)).OrderBy(m => m, StringComparer.Ordinal).ToList();
    }

    private static List<string> MovesFrom(GameState s, string from) => Legal(s).Where(m => m.StartsWith(from, StringComparison.Ordinal)).ToList();

    /// <summary>Put a tomb holding a fresh <paramref name="def"/> of <paramref name="side"/> on <paramref name="square"/>.</summary>
    private static Piece Entomb(GameState s, PieceDef def, Side side, string square, params AbilityDef[] abilities)
    {
        var piece = s.Place(def, side, Sq(s, square));
        foreach (var a in abilities) s.GrantAbility(piece, a);
        s.RemoveFromBoard(piece);
        s.SetTile(Sq(s, square), TombTile.Instance, TombTile.VarsFor(piece));
        return piece;
    }

    /// <summary>
    /// Play one turn and verify: accepted, incremental hash equals the from-scratch hash, a Clone is identical, undo
    /// restores the exact prior state (dump, hash, history), and replaying gives the exact same result.
    /// </summary>
    private static TurnResult Step(GameState s, string move)
    {
        var m = MoveText.FindLegal(s, move) ?? throw new InvalidOperationException($"{move} is not legal: {string.Join(" ", Legal(s))}\n{s.Dump()}");
        string before = s.Dump();
        ulong hashBefore = s.Hash;
        int mark = s.UndoDepth, history = s.History.Count;

        var first = Game.PlayTurn(s, m, new ResolveOptions(Trace: true));
        Assert.True(first.Accepted, first.RejectReason);
        Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
        string after = s.Dump();
        ulong hashAfter = s.Hash;
        int wClock = s.ClockMs(Side.White), bClock = s.ClockMs(Side.Black);

        var clone = s.Clone();
        Assert.Equal(after, clone.Dump());
        Assert.Equal(hashAfter, clone.Hash);
        Assert.Equal(clone.ComputeHashFromScratch(), clone.Hash);
        Assert.Equal(s.History.Count, clone.History.Count);

        s.UndoTo(mark);
        Assert.Equal(before, s.Dump());
        Assert.Equal(hashBefore, s.Hash);
        Assert.Equal(history, s.History.Count);

        var again = Game.PlayTurn(s, m, new ResolveOptions(Trace: true));
        Assert.True(again.Accepted, again.RejectReason);
        Assert.Equal(after, s.Dump());
        Assert.Equal(hashAfter, s.Hash);
        Assert.Equal((wClock, bClock), (s.ClockMs(Side.White), s.ClockMs(Side.Black)));
        Assert.Equal(history + 1, s.History.Count);
        Assert.Equal(m, s.History[^1].Move);
        return again;
    }

    private static AgentProfile Exact(int depth) => AgentProfile.Club with
    {
        Id = "verify", ChoiceMargin = 0, RootNoise = 0, BlunderChance = 0, Contempt = 0,
        MaxDepth = depth, NodeBudget = 0, TimeBudgetMs = 0, QuiescenceDepth = 0,
    };

    // ================================================================== Revenant + Tomb

    [Fact]
    public void An_enemy_moving_onto_an_empty_tomb_just_lands_and_the_tomb_stays()
    {
        var s = Rogue("7k/8/8/3b4/8/5R2/8/K5N1 b - - 0 1");
        var rook = Must(s, "f3");
        s.GrantAbility(rook, RevenantAbility.Instance);
        s.ClearHistory();

        Step(s, "d5f3");
        Step(s, "a1b1");
        Step(s, "f3c6");
        Step(s, "b1a1");
        Step(s, "c6f3"); // the bishop comes back onto the empty tomb: no revive (enemy)

        Assert.Equal("bishop", Must(s, "f3").Def.Id);
        Assert.Same(TombTile.Instance, TileOn(s, "f3"));
        Assert.Same(rook, TombTile.Entombed(s, Sq(s, "f3")));
    }

    [Fact]
    public void A_revive_cancels_the_move_so_time_bank_pays_nothing_and_the_revived_piece_is_not_crowned()
    {
        // Tomb on c8 (White's last rank) holding a knight. The rook's move onto it becomes the revive.
        var s = Rogue("7k/8/8/8/8/8/8/K1R5 w - - 0 1");
        var knight = Entomb(s, StandardPieces.Knight, Side.White, "c8", RevenantAbility.Instance);
        s.AddGlobal(TimeBankRelic.White);
        s.AddGlobal(RoyalPromotionRelic.White);
        s.ClearHistory();
        var rook = Must(s, "c1");
        int rookMoves = rook.MovesMade;

        var r = Step(s, "c1c8");

        Assert.Same(knight, At(s, "c8"));
        Assert.Same(StandardPieces.Knight, knight.Def);   // a revive is a Spawned, not a Moved: no crown (as in the prototype; open question vs "any arrival")
        Assert.Same(rook, At(s, "c1"));
        Assert.Equal(rookMoves, rook.MovesMade);
        Assert.Equal(0, r.WhiteClockDeltaMs);              // no Moved fact for the action: no TimeBank payment
        Assert.False(knight.HasAbility(RevenantAbility.Instance));
        Assert.True(TileOn(s, "c8").IsDefault);
        Assert.Equal(Side.Black, s.SideToMove);            // the turn still passes
        Assert.Contains(r.Trace!.AllFacts, f => f.Kind == FactKinds.Spawned && f.Piece == knight && f.Cause == TombTile.RestoreCause);
    }

    [Fact]
    public void A_revenant_dying_on_another_tomb_replaces_it_and_the_old_piece_stays_dead()
    {
        // f3: a White knight's tomb with a Black revenant bishop standing on it. The White rook takes the bishop.
        var s = Rogue("7k/8/8/8/8/8/8/K4R2 w - - 0 1");
        var knight = Entomb(s, StandardPieces.Knight, Side.White, "f3");
        var bishop = s.Place(StandardPieces.Bishop, Side.Black, Sq(s, "f3"));
        s.GrantAbility(bishop, RevenantAbility.Instance);
        s.ClearHistory();
        ulong startHash = s.Hash;

        Step(s, "f1f3");

        Assert.Same(bishop, TombTile.Entombed(s, Sq(s, "f3")));
        Assert.False(knight.IsOnBoard);
        Assert.Equal("rook", Must(s, "f3").Def.Id);
        Assert.NotEqual(startHash, s.Hash);

        // White leaving and coming back never opens Black's tomb; a Black piece would.
        Step(s, "h8g8");
        Step(s, "f3f1");
        Step(s, "g8h8");
        Step(s, "f1f3");
        Assert.Equal("rook", Must(s, "f3").Def.Id);
        Assert.Same(bishop, TombTile.Entombed(s, Sq(s, "f3")));
    }

    [Fact]
    public void A_cannibal_eating_a_revenant_ally_stands_on_its_tomb_and_can_reopen_it_later_without_clock_time()
    {
        var s = Rogue("7k/8/8/8/8/8/8/R1R4K w - - 0 1");
        var cannibal = Must(s, "a1");
        var victim = Must(s, "c1");
        s.GrantAbility(cannibal, CannibalAbility.Instance);
        s.GrantAbility(cannibal, ChronoCaptureAbility.Instance);
        s.GrantAbility(victim, RevenantAbility.Instance);
        s.ClearHistory();

        var eat = Step(s, "a1c1");
        Assert.Same(cannibal, At(s, "c1"));
        Assert.Same(victim, TombTile.Entombed(s, Sq(s, "c1")));
        Assert.Equal(0, eat.WhiteClockDeltaMs); // ChronoCapture counts enemy kills only

        Step(s, "h8g8");
        Step(s, "c1c4");
        Step(s, "g8h8");
        Step(s, "c4c1"); // the cannibal is an ally of the entombed rook: it opens the tomb and stays on c4

        Assert.Same(victim, At(s, "c1"));
        Assert.Same(cannibal, At(s, "c4"));
        Assert.True(TileOn(s, "c1").IsDefault);
    }

    [Fact]
    public void A_revenant_killed_on_ice_leaves_a_tomb_in_place_of_the_ice_and_its_killer_still_slides()
    {
        // The rook stops on the ice at f3 (f4 is taken). The bishop takes it there: in the next wave the tomb
        // replaces the ice and the ice (still there when triggers gather) slides the bishop on to g2.
        var s = Rogue("7k/8/8/3b4/5p2/8/8/K4RN1 w - - 0 1");
        s.SetTile(Sq(s, "f3"), SlipperyTile.Instance);
        var rook = Must(s, "f1");
        var knight = Must(s, "g1");
        s.GrantAbility(rook, RevenantAbility.Instance);
        s.ClearHistory();

        Step(s, "f1f3");
        Assert.Same(rook, At(s, "f3"));
        Step(s, "d5f3");
        Assert.Equal("bishop", Must(s, "g2").Def.Id);
        Assert.Null(At(s, "f3"));
        Assert.Same(rook, TombTile.Entombed(s, Sq(s, "f3")));

        Step(s, "g1f3"); // revive: the square comes back plain, not icy
        Assert.Same(rook, At(s, "f3"));
        Assert.Same(knight, At(s, "g1"));
        Assert.True(TileOn(s, "f3").IsDefault);
    }

    [Fact]
    public void Fortify_walls_over_an_empty_tomb_destroying_it_as_in_the_prototype()
    {
        var s = Rogue("7k/8/8/8/8/8/8/K7 w - - 0 1");
        var knight = Entomb(s, StandardPieces.Knight, Side.White, "d4");
        FortifyPotion.Instance.Give(s, 1, 0);
        s.ClearHistory();

        Step(s, "use:fortify-potion@a4");
        Assert.Same(WallTile.Instance, TileOn(s, "d4"));
        Assert.Null(TombTile.Entombed(s, Sq(s, "d4")));
        Assert.False(knight.IsOnBoard);
    }

    // ================================================================== Cannibal

    [Fact]
    public void Cannibal_with_no_reachable_allies_has_only_quiet_moves()
    {
        var s = Rogue("7k/8/2p1p3/8/3N4/8/8/7K w - - 0 1");
        s.GrantAbility(Must(s, "d4"), CannibalAbility.Instance);
        s.ClearHistory();
        var moves = MovesFrom(s, "d4");
        Assert.Equal(new[] { "d4b3", "d4b5", "d4c2", "d4e2", "d4f3", "d4f5" }, moves);

        var all = new List<Move>();
        MoveGenerator.GenerateLegal(s, all);
        Assert.DoesNotContain(all, m => m.From == Sq(s, "d4") && m.IsCapture);
    }

    [Fact]
    public void Cannibal_king_takes_adjacent_allies_but_not_enemies()
    {
        var s = Rogue("7k/8/8/8/8/2pP4/2K5/8 w - - 0 1");
        s.GrantAbility(Must(s, "c2"), CannibalAbility.Instance);
        s.ClearHistory();
        var moves = MovesFrom(s, "c2");
        Assert.Contains("c2d3", moves);
        Assert.DoesNotContain("c2c3", moves);
        Step(s, "c2d3");
        Assert.Equal("king", Must(s, "d3").Def.Id);
        Assert.Equal("pawn", Must(s, "c3").Def.Id);
    }

    [Fact]
    public void Cannibal_pawn_never_captures_enemies_even_en_passant_style()
    {
        var s = Fen.Parse("7k/8/8/3pP3/8/8/8/K7 w - d6 0 1", RuleSet.Standard);
        s.GrantAbility(Must(s, "e5"), CannibalAbility.Instance);
        s.ClearHistory();
        Assert.Equal(new[] { "e5e6" }, MovesFrom(s, "e5"));
    }

    // ================================================================== Slippery

    [Fact]
    public void A_slide_onto_an_enemys_tomb_lands_and_keeps_the_tomb()
    {
        var s = Rogue("7k/8/8/8/8/8/8/R6K w - - 0 1");
        var knight = Entomb(s, StandardPieces.Knight, Side.Black, "a4");
        s.SetTile(Sq(s, "a3"), SlipperyTile.Instance);
        s.ClearHistory();

        Step(s, "a1a3");
        Assert.Equal("rook", Must(s, "a4").Def.Id);
        Assert.Same(knight, TombTile.Entombed(s, Sq(s, "a4")));
    }

    [Fact]
    public void A_pawn_sliding_onto_the_last_rank_promotes()
    {
        var s = Rogue("k7/8/4P3/8/8/8/8/7K w - - 0 1");
        s.SetTile(Sq(s, "e7"), SlipperyTile.Instance);
        s.ClearHistory();
        Step(s, "e6e7");
        Assert.Same(StandardPieces.Queen, Must(s, "e8").Def);
    }

    [Fact]
    public void A_slide_onto_the_last_rank_is_crowned_and_time_bank_pays_once_for_the_whole_move()
    {
        var s = Rogue("7k/8/R7/8/8/8/8/7K w - - 0 1");
        s.SetTile(Sq(s, "a7"), SlipperyTile.Instance);
        s.AddGlobal(RoyalPromotionRelic.White);
        s.AddGlobal(TimeBankRelic.White);
        s.ClearHistory();

        var r = Step(s, "a6a7");
        Assert.Same(StandardPieces.Queen, Must(s, "a8").Def);
        // The prototype's slide was another player-action MoveEvent (+3 s more); here only the action move pays.
        Assert.Equal(TimeBankRelic.BonusMs, r.WhiteClockDeltaMs);
    }

    [Fact]
    public void A_knight_jumping_two_files_onto_ice_stays_put()
    {
        var s = Rogue("7k/8/8/8/8/8/8/1N5K w - - 0 1");
        s.SetTile(Sq(s, "d2"), SlipperyTile.Instance);
        s.ClearHistory();
        Step(s, "b1d2");
        Assert.Equal("knight", Must(s, "d2").Def.Id); // a jump doesn't slide, even with e2 free
        Assert.Null(At(s, "e2"));
    }

    [Fact]
    public void Slides_chain_in_the_move_direction_and_stop_at_the_board_edge()
    {
        var s = Rogue("7k/8/8/8/8/8/8/R1R4K w - - 0 1");
        s.SetTile(Sq(s, "a2"), SlipperyTile.Instance);
        s.SetTile(Sq(s, "a3"), SlipperyTile.Instance);
        s.SetTile(Sq(s, "b3"), SlipperyTile.Instance);
        s.ClearHistory();
        Step(s, "a1a2"); // a2 → a3 → a4
        Assert.Equal("rook", Must(s, "a4").Def.Id);
        Step(s, "h8g8");
        Step(s, "c1c3");
        Step(s, "g8h8");
        Step(s, "c3b3"); // west onto ice: slides to a3 (ice too), whose next square west is off the board
        Assert.Equal("rook", Must(s, "a3").Def.Id);
    }

    // ================================================================== consumables

    [Fact]
    public void A_consumable_with_zero_charges_is_never_offered_and_cannot_be_used()
    {
        var s = Rogue("k7/8/8/8/8/8/8/R6K w - - 0 1");
        ExtraTurnPotion.Instance.Give(s, 0, 0);
        s.ClearHistory();
        Assert.DoesNotContain(Legal(s), m => m.StartsWith("use:"));
        var r = Game.PlayTurn(s, Move.UseConsumable(0));
        Assert.False(r.Accepted);
        Assert.Equal(0, s.UndoDepth);
        Assert.Equal(0, s.ExtraTurns(Side.White));
    }

    [Fact]
    public void Fortify_on_a_full_row_is_not_offered_but_a_manual_use_spends_the_charge_like_the_prototype()
    {
        var s = Rogue("k7/8/8/8/8/8/8/RRRRRRRK w - - 0 1");
        FortifyPotion.Instance.Give(s, 1, 0);
        s.ClearHistory();
        Assert.DoesNotContain(Legal(s), m => m.StartsWith("use:fortify-potion@a1", StringComparison.Ordinal));
        Assert.Contains(Legal(s), m => m.StartsWith("use:fortify-potion@a2", StringComparison.Ordinal));

        Assert.False(Game.PlayTurn(s, Move.UseConsumable(0, -2)).Accepted);
        Assert.False(Game.PlayTurn(s, Move.UseConsumable(0, s.Area)).Accepted);
        Assert.Equal(0, s.UndoDepth);

        // The prototype returned an empty (non-null) result for a full row, which still consumed the potion.
        var r = Game.PlayTurn(s, Move.UseConsumable(0, Sq(s, "d1")));
        Assert.True(r.Accepted, r.RejectReason);
        Assert.Equal(0, FortifyPotion.Instance.Charges(s, Side.White));
        Assert.Equal(Side.White, s.SideToMove);
        Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
    }

    [Fact]
    public void An_extra_turn_runs_turn_end_then_turn_start_for_the_same_side_and_each_move_counts_a_ply()
    {
        var s = Rogue("k7/8/8/8/8/8/8/R6K w - - 0 1");
        ExtraTurnPotion.Instance.Give(s, 1, 0);
        s.AddGlobal(TimeBankRelic.White);
        s.ClearHistory();

        var use = Step(s, "use:extra-turn-potion");
        Assert.Equal(0, use.WhiteClockDeltaMs); // a consumable is not a move
        Assert.DoesNotContain(use.Trace!.Resolutions, r => r.Phase != Game.ActionPhase);

        var first = Step(s, "a1a2");
        var phases = first.Trace!.Resolutions.Select(r => r.Phase).ToArray();
        Assert.Equal(new[] { Game.ActionPhase, Game.TurnEndPhase, Game.TurnStartPhase }, phases);
        Assert.Equal(TimeBankRelic.BonusMs, first.WhiteClockDeltaMs);
        Assert.Equal((Side.White, 1), (s.SideToMove, s.Ply));

        var second = Step(s, "a2a3");
        Assert.Equal(TimeBankRelic.BonusMs, second.WhiteClockDeltaMs);
        Assert.Equal((Side.Black, 2), (s.SideToMove, s.Ply));
        Assert.Equal(2 * TimeBankRelic.BonusMs, s.ClockMs(Side.White));
    }

    [Fact]
    public void A_frozen_piece_stays_frozen_during_an_extra_turn_until_its_count_runs_out()
    {
        var s = Rogue("k7/8/8/8/8/8/8/R6K w - - 0 1");
        ExtraTurnPotion.Instance.Give(s, 1, 0);
        s.GrantAbility(Must(s, "a1"), FrozenAbility.Instance, 2);
        s.ClearHistory();

        Step(s, "use:extra-turn-potion");
        Assert.Empty(MovesFrom(s, "a1"));
        Step(s, "h1g1"); // turn end: 2 → 1, extra turn
        Assert.Equal(Side.White, s.SideToMove);
        Assert.Empty(MovesFrom(s, "a1"));
        Step(s, "g1h1"); // 1 → 0: thawed, turn passes
        Assert.Equal(Side.Black, s.SideToMove);
        Step(s, "a8b8");
        Assert.NotEmpty(MovesFrom(s, "a1"));
    }

    [Fact]
    public void Black_can_drink_haste_too_and_a_banked_turn_is_spent_on_its_next_turn_end_only()
    {
        var s = Rogue("k7/8/8/8/8/8/8/R6K b - - 0 1");
        ExtraTurnPotion.Instance.Give(s, 1, 1);
        s.ClearHistory();

        Step(s, "use:extra-turn-potion");
        Assert.Equal(1, s.ExtraTurns(Side.Black));
        Assert.Equal(0, ExtraTurnPotion.Instance.Charges(s, Side.Black));
        Assert.Equal(1, ExtraTurnPotion.Instance.Charges(s, Side.White));
        Step(s, "a8b8");
        Assert.Equal((Side.Black, 0), (s.SideToMove, s.ExtraTurns(Side.Black)));
        Step(s, "b8c8");
        Assert.Equal(Side.White, s.SideToMove);
        Assert.Contains("use:extra-turn-potion", Legal(s));
    }

    [Fact]
    public void Consumable_values_and_the_relic_wrong_side_cases()
    {
        // TimeBank owned by Black pays nothing for White's moves.
        var s = Rogue("k7/8/8/8/8/8/8/R6K w - - 0 1");
        s.AddGlobal(TimeBankRelic.Black);
        s.ClearHistory();
        var r = Step(s, "a1a2");
        Assert.Equal((0, 0), (r.WhiteClockDeltaMs, r.BlackClockDeltaMs));
        r = Step(s, "a8b8");
        Assert.Equal((0, TimeBankRelic.BonusMs), (r.WhiteClockDeltaMs, r.BlackClockDeltaMs));

        // RoyalPromotion owned by White ignores a Black knight on rank 1 and a White knight on rank 1.
        var p = Rogue("k7/8/8/8/8/8/2n5/N6K b - - 0 1");
        p.AddGlobal(RoyalPromotionRelic.White);
        p.ClearHistory();
        Step(p, "c2e1");
        Assert.Same(StandardPieces.Knight, Must(p, "e1").Def);
        Step(p, "a1b3");
        Step(p, "a8b8");
        Step(p, "b3c1");
        Assert.Same(StandardPieces.Knight, Must(p, "c1").Def);
    }

    [Fact]
    public void ChronoCapture_pays_for_killing_an_enemy_revenant_and_the_tomb_and_clock_undo_together()
    {
        var s = Rogue("7k/8/8/3r4/8/8/8/K2R4 w - - 0 1");
        s.GrantAbility(Must(s, "d1"), ChronoCaptureAbility.Instance);
        s.GrantAbility(Must(s, "d5"), RevenantAbility.Instance);
        s.ClearHistory();
        var r = Step(s, "d1d5");
        Assert.Equal(ChronoCaptureAbility.BonusMs, r.WhiteClockDeltaMs);
        Assert.Same(TombTile.Instance, TileOn(s, "d5"));
        s.UndoTo(0);
        Assert.Equal(0, s.ClockMs(Side.White));
        Assert.True(TileOn(s, "d5").IsDefault);
    }

    // ================================================================== history + clone

    [Fact]
    public void History_follows_undo_then_a_different_line()
    {
        var s = Rogue("k7/8/8/8/8/8/8/R6K w - - 0 1");
        ExtraTurnPotion.Instance.Give(s, 1, 0);
        s.ClearHistory();
        Step(s, "use:extra-turn-potion");
        int mark = s.UndoDepth;
        Step(s, "a1a5");
        Step(s, "a5a6");
        s.UndoTo(mark);
        Assert.Single(s.History);
        Assert.Equal(1, s.ExtraTurns(Side.White));
        Step(s, "h1g1");
        Step(s, "g1f1");
        Step(s, "a8b8");
        Assert.Equal(new[] { "use:extra-turn-potion", "h1g1", "g1f1", "a8b8" }, s.History.Select(h => MoveText.Format(s, h.Move)));
        Assert.Equal(new[] { Side.White, Side.White, Side.White, Side.Black }, s.History.Select(h => h.Side));
    }

    [Fact]
    public void A_clone_taken_mid_run_plays_on_identically_and_independently()
    {
        var s = Rogue("7k/8/8/3b4/8/5R2/8/K5N1 b - - 0 1");
        s.GrantAbility(Must(s, "f3"), RevenantAbility.Instance);
        s.GrantAbility(Must(s, "g1"), ChronoCaptureAbility.Instance);
        s.SetTile(Sq(s, "e2"), SlipperyTile.Instance);
        ExtraTurnPotion.Instance.Give(s, 1, 1);
        s.AddGlobal(TimeBankRelic.White);
        s.ClearHistory();

        Step(s, "d5f3");                  // tomb under the bishop
        Step(s, "use:extra-turn-potion"); // White banks a turn
        var clone = s.Clone();
        string snapshot = s.Dump();

        foreach (var state in new[] { s, clone })
        {
            Step(state, "g1f3");  // ChronoCapture knight takes the bishop on the tomb
            Step(state, "f3e5");  // extra turn
            Step(state, "h8g8");
            Step(state, "e5f3");  // revive
        }
        Assert.Equal(s.Dump(), clone.Dump());
        Assert.Equal(s.Hash, clone.Hash);
        Assert.Equal(s.ClockMs(Side.White), clone.ClockMs(Side.White));
        Assert.Equal(ChronoCaptureAbility.BonusMs + 2 * TimeBankRelic.BonusMs, s.ClockMs(Side.White));
        Assert.Equal(s.History.Select(h => h.Move), clone.History.Select(h => h.Move));
        Assert.Equal("rook", Must(clone, "f3").Def.Id);

        // Undoing the original back to the snapshot doesn't touch the clone.
        s.UndoTo(0);
        Assert.Equal("rook", Must(clone, "f3").Def.Id);
        Assert.NotEqual(snapshot, clone.Dump());
    }

    // ================================================================== AI with extra turns

    [Fact]
    public void AI_as_black_drinks_haste_to_win()
    {
        var s = Rogue("r6k/8/8/8/8/8/8/7K b - - 0 1");
        ExtraTurnPotion.Instance.Give(s, 0, 1);
        s.ClearHistory();
        ulong hash = s.Hash;
        var d = new AgentFactory().Create(Exact(3)).Decide(s);
        Assert.True(d.Score >= Search.WinThreshold, d.Format(s));
        Assert.Equal("use:extra-turn-potion", MoveText.Format(s, d.Move!.Value));
        Assert.Equal(hash, s.Hash);
        Assert.Equal(0, s.UndoDepth);
    }

    [Fact]
    public void AI_defends_against_the_opponents_haste_threat_instead_of_grabbing_material()
    {
        // Black's knight on d4 is two knight moves from a1; with Haste, Black plays Nc2/Nb3 then Nxa1.
        // Rxe5 wins a bishop but loses the king; Ka2/Kb1 (light squares, out of the knight's two-move reach) hold.
        var s = Rogue("k7/8/8/4b2R/3n4/8/8/K7 w - - 0 1");
        ExtraTurnPotion.Instance.Give(s, 0, 1);
        s.ClearHistory();

        var agent = new AgentFactory().Create(Exact(4));
        var d = agent.Decide(s);
        string chosen = MoveText.Format(s, d.Move!.Value);
        Assert.True(chosen is "a1a2" or "a1b1", d.Format(s));
        Assert.True(d.Score > -Search.WinThreshold, d.Format(s));
        var grab = d.RootMoves.Single(r => MoveText.Format(s, r.Move) == "h5e5");
        Assert.True(grab.Score <= -Search.WinThreshold, d.Format(s));

        // Without the potion the bishop is free.
        var calm = Rogue("k7/8/8/4b2R/3n4/8/8/K7 w - - 0 1");
        Assert.Equal("h5e5", MoveText.Format(calm, agent.Decide(calm).Move!.Value));
    }

    /// <summary>
    /// Exhaustive solver for the side to move: +1 forced royal capture within <paramref name="depth"/> plies,
    /// -1 every line loses within that many plies, 0 otherwise. Extra turns and free actions keep the same side
    /// to move, so the child's value is used as is instead of negated.
    /// </summary>
    private static int Solve(GameState s, int depth)
    {
        int w = 0, b = 0;
        for (int sq = 0; sq < s.Area; sq++)
            if (s.PieceAt(sq) is { Def.IsRoyal: true } k) { if (k.Side == Side.White) w++; else b++; }
        if (w == 0 && b == 0) return 0;
        if (w == 0 || b == 0) return (w == 0) == (s.SideToMove == Side.White) ? -1 : 1;
        if (depth == 0) return 0;

        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(s, moves);
        if (moves.Count == 0) return 0;
        var mover = s.SideToMove;
        int best = -1;
        foreach (var m in moves)
        {
            int mark = s.UndoDepth;
            if (!Game.PlayTurn(s, m).Accepted) continue;
            int v = Solve(s, depth - 1);
            if (s.SideToMove != mover) v = -v;
            s.UndoTo(mark);
            if (v > best) best = v;
            if (best == 1) break;
        }
        return best;
    }

    [Theory]
    [InlineData(3, 2024, 70, false)]
    [InlineData(3, 77, 50, true)]
    [InlineData(4, 9001, 20, false)]
    public void Search_agrees_with_an_exhaustive_solver_on_small_positions_with_haste_potions(int depth, int seed, int count, bool fortify)
    {
        var rng = new Random(seed);
        var defs = new[] { StandardPieces.Rook, StandardPieces.Knight, StandardPieces.Bishop, StandardPieces.Pawn };
        int tested = 0, decisive = 0, potionWins = 0;
        for (int attempt = 0; attempt < 6 * count && tested < count; attempt++)
        {
            var s = new GameState(5, 5, RuleSet.Rogue);
            var free = Enumerable.Range(0, 25).OrderBy(_ => rng.Next()).ToList();
            s.Place(StandardPieces.King, Side.White, free[0]);
            s.Place(StandardPieces.King, Side.Black, free[1]);
            int extras = 1 + rng.Next(3), used = 2;
            for (int i = 0; i < extras; i++)
            {
                var def = defs[rng.Next(defs.Length)];
                int sq = free[used++];
                if (def.IsPawn && (s.RankOf(sq) == 0 || s.RankOf(sq) == 4)) def = StandardPieces.Knight;
                s.Place(def, rng.Next(2) == 0 ? Side.White : Side.Black, sq);
            }
            ExtraTurnPotion.Instance.Give(s, rng.Next(2), rng.Next(2));
            if (fortify) FortifyPotion.Instance.Give(s, rng.Next(2), rng.Next(2));
            if (rng.Next(2) == 0) s.SetTurn(Side.Black, 0);
            s.ClearHistory();

            var moves = new List<Move>();
            MoveGenerator.GenerateLegal(s, moves);
            if (moves.Count < 2) continue;

            int truth = Solve(s, depth);
            Assert.Equal(0, s.UndoDepth);
            var d = new AgentFactory().Create(Exact(depth)).Decide(s);
            Assert.Equal(0, s.UndoDepth);
            int found = d.Score >= Search.WinThreshold ? 1 : d.Score <= -Search.WinThreshold ? -1 : 0;
            Assert.True(truth == found, $"solver {truth}, search {found} ({d.Format(s)})\n{s.Dump()}");
            if (truth == 1)
            {
                // The chosen move itself keeps the forced win.
                var mover = s.SideToMove;
                Assert.True(Game.PlayTurn(s, d.Move!.Value).Accepted);
                int after = Solve(s, depth - 1);
                if (s.SideToMove != mover) after = -after;
                s.UndoTo(0);
                Assert.True(after == 1, $"{MoveText.Format(s, d.Move.Value)} doesn't keep the win\n{s.Dump()}");
                if (d.Move.Value.IsConsumable) potionWins++;
            }
            tested++;
            if (truth != 0) decisive++;
        }
        Assert.True(tested >= count * 3 / 4, $"only {tested} positions");
        Assert.True(decisive >= count / 7, $"only {decisive} decisive positions");
        Assert.True(potionWins >= 1, "no position where the win starts with the potion");
    }

    /// <summary>A random 5x5 position: two kings plus 1-3 other pieces, Haste potions at random. Null if it has fewer than two moves.</summary>
    private static GameState? RandomSmall(Random rng)
    {
        var defs = new[] { StandardPieces.Rook, StandardPieces.Knight, StandardPieces.Bishop, StandardPieces.Pawn };
        var s = new GameState(5, 5, RuleSet.Rogue);
        var free = Enumerable.Range(0, 25).OrderBy(_ => rng.Next()).ToList();
        s.Place(StandardPieces.King, Side.White, free[0]);
        s.Place(StandardPieces.King, Side.Black, free[1]);
        int extras = 1 + rng.Next(3);
        for (int i = 0; i < extras; i++)
        {
            var def = defs[rng.Next(defs.Length)];
            int sq = free[2 + i];
            if (def.IsPawn && (s.RankOf(sq) == 0 || s.RankOf(sq) == 4)) def = StandardPieces.Knight;
            s.Place(def, rng.Next(2) == 0 ? Side.White : Side.Black, sq);
        }
        ExtraTurnPotion.Instance.Give(s, rng.Next(2), rng.Next(2));
        if (rng.Next(2) == 0) s.SetTurn(Side.Black, 0);
        s.ClearHistory();
        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(s, moves);
        return moves.Count < 2 ? null : s;
    }

    [Fact]
    public void Quiescence_with_extra_turns_never_claims_a_result_that_is_not_forced()
    {
        // With quiescence on, a claimed win/loss must be real within depth + quiescence plies, and anything the
        // full-width part can prove must still be found.
        var rng = new Random(31337);
        const int depth = 2, q = 2;
        int tested = 0, claims = 0;
        for (int attempt = 0; attempt < 300 && tested < 60; attempt++)
        {
            var s = RandomSmall(rng);
            if (s == null) continue;
            var d = new AgentFactory().Create(Exact(depth) with { QuiescenceDepth = q }).Decide(s);
            Assert.Equal(0, s.UndoDepth);
            int found = d.Score >= Search.WinThreshold ? 1 : d.Score <= -Search.WinThreshold ? -1 : 0;
            int shallow = Solve(s, depth);
            if (shallow != 0) Assert.True(found == shallow, $"solver {shallow}, search {found}\n{d.Format(s)}\n{s.Dump()}");
            if (found != 0)
            {
                claims++;
                Assert.True(Solve(s, depth + q) == found, $"search claims {found}\n{d.Format(s)}\n{s.Dump()}");
            }
            tested++;
        }
        Assert.True(tested >= 40 && claims >= 5, $"{tested} positions, {claims} claims");
    }

    // ================================================================== odd moves onto tombs (standard rules)

    [Fact]
    public void Castling_rook_onto_an_allys_tomb_revives_it_and_the_state_stays_exact()
    {
        // Standard rules: O-O moves the rook h1 → f1; f1 holds a White knight's tomb. The rook's move is the one that
        // lands on the tomb, so it is cancelled and the knight comes back; the king still castles.
        var s = Fen.Parse("4k3/8/8/8/8/8/8/4K2R w K - 0 1", RuleSet.Standard);
        var knight = Entomb(s, StandardPieces.Knight, Side.White, "f1");
        s.ClearHistory();
        Assert.Contains("e1g1", Legal(s));
        Step(s, "e1g1");
        Assert.Equal("king", Must(s, "g1").Def.Id);
        Assert.Same(knight, At(s, "f1"));
        Assert.Equal("rook", Must(s, "h1").Def.Id);
    }

    [Fact]
    public void En_passant_onto_an_allys_tomb_takes_the_pawn_and_revives_instead_of_moving()
    {
        // Black's d7-d5 passed over d6, a White knight's tomb. exd6 e.p.: the victim dies, the move onto the empty
        // tomb becomes the revive, so the capturing pawn stays on e5. Ambiguous in the prototype; pinned for exactness.
        var s = Fen.Parse("4k3/3p4/8/4P3/8/8/8/4K3 b - - 0 1", RuleSet.Standard);
        var knight = Entomb(s, StandardPieces.Knight, Side.White, "d6");
        s.ClearHistory();
        Step(s, "d7d5");
        Assert.Equal(Sq(s, "d6"), s.EnPassantSquare);
        Step(s, "e5d6");
        Assert.Null(At(s, "d5"));
        Assert.Same(knight, At(s, "d6"));
        Assert.Equal("pawn", Must(s, "e5").Def.Id);
    }

    // ================================================================== combined fuzz

    [Fact]
    public void Random_games_mixing_death_movement_and_run_content_stay_exact_every_ply()
    {
        int tombs = 0, slides = 0, extraTurns = 0, uses = 0;
        for (int game = 0; game < 8; game++)
        {
            var rng = new Random(9100 + game);
            var s = Rogue("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w - - 0 1");
            foreach (var sq in new[] { "b1", "e2", "d7", "g8", "a1", "h8" }) s.GrantAbility(Must(s, sq), RevenantAbility.Instance);
            foreach (var sq in new[] { "c1", "f8", "d2" }) s.GrantAbility(Must(s, sq), CannibalAbility.Instance);
            foreach (var sq in new[] { "g1", "b8", "c7", "d1" }) s.GrantAbility(Must(s, sq), ChronoCaptureAbility.Instance);
            foreach (var sq in new[] { "c3", "d4", "e5", "f6", "a5", "h4", "e4", "d5" }) s.SetTile(Sq(s, sq), SlipperyTile.Instance);
            s.AddGlobal(TimeBankRelic.White);
            s.AddGlobal(TimeBankRelic.Black);
            s.AddGlobal(RoyalPromotionRelic.White);
            s.AddGlobal(RoyalPromotionRelic.Black);
            ExtraTurnPotion.Instance.Give(s, 2, 2);
            FortifyPotion.Instance.Give(s, 1, 1);
            s.ClearHistory();
            string start = s.Dump();
            ulong startHash = s.Hash;

            var moves = new List<Move>();
            for (int ply = 0; ply < 70; ply++)
            {
                if (s.Rules.Evaluate(s) != GameOutcome.Ongoing) break;
                moves.Clear();
                MoveGenerator.GenerateLegal(s, moves);
                if (moves.Count == 0) break;
                var consumables = moves.Where(m => m.IsConsumable).ToList();
                var move = consumables.Count > 0 && rng.Next(4) == 0 ? consumables[rng.Next(consumables.Count)] : moves[rng.Next(moves.Count)];
                var side = s.SideToMove;
                var r = Step(s, MoveText.Format(s, move));
                var facts = r.Trace!.AllFacts.ToList();
                if (facts.Any(f => f.Kind == FactKinds.TileChanged && f.Cause == RevenantAbility.TombCause)) tombs++;
                if (facts.Any(f => f.Kind == FactKinds.Moved && f.Cause == SlipperyTile.Cause)) slides++;
                if (move.IsConsumable) uses++;
                else if (s.SideToMove == side) extraTurns++;
            }
            s.UndoTo(0);
            Assert.Equal(start, s.Dump());
            Assert.Equal(startHash, s.Hash);
            Assert.Empty(s.History);
        }
        Assert.True(tombs > 3, $"only {tombs} tombs");
        Assert.True(slides > 5, $"only {slides} slides");
        Assert.True(extraTurns > 1, $"only {extraTurns} extra turns");
        Assert.True(uses > 3, $"only {uses} consumable uses");
    }
}
