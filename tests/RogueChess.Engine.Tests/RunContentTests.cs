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
/// The run content ported from the prototype: the engine side of the clock, ChronoCapture, the RoyalPromotion and
/// TimeBank relics, the ExtraTurn and Fortify consumables, plus move history (B9).
/// </summary>
public class RunContentTests
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

    private static List<string> Legal(GameState s)
    {
        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(s, moves);
        return moves.Select(m => MoveText.Format(s, m)).ToList();
    }

    private static TurnResult Play(GameState s, string move)
    {
        var m = MoveText.FindLegal(s, move) ?? throw new InvalidOperationException($"{move} is not legal: {string.Join(" ", Legal(s))}");
        var result = Game.PlayTurn(s, m, new ResolveOptions(Trace: true));
        Assert.True(result.Accepted, result.RejectReason);
        Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
        return result;
    }

    private static int Walls(GameState s) => Enumerable.Range(0, s.Area).Count(sq => s.TileAt(sq).Def == WallTile.Instance);

    private static int Charges(GameState s, ConsumableDef def, Side side) => def.Charges(s, side);

    /// <summary>Test stand-in for a ranged attacker (Marksman is ported elsewhere): at the end of its side's turn it
    /// destroys the enemy piece directly in front of it. The destroy's source is this piece, as any kill credit is.</summary>
    private sealed class ProbeSniper : AbilityDef
    {
        public static readonly ProbeSniper Instance = new();
        private ProbeSniper() : base("probe-sniper", "Sniper") { }

        public override FactKinds Triggers => FactKinds.TurnEnded;

        public override void OnFact(TriggerContext ctx, in Fact fact)
        {
            var self = ctx.SelfPiece!;
            if (fact.Side != self.Side) return;
            int x = ctx.Board.FileOf(self.Square), y = ctx.Board.RankOf(self.Square) + self.Side.Forward();
            if (ctx.Board.PieceAt(x, y) is { } target && target.Side != self.Side)
                ctx.Propose(Proposal.Destroy(target, "ranged"));
        }
    }

    // ------------------------------------------------------------------ ChronoCapture

    [Fact]
    public void ChronoCapture_grants_five_seconds_to_its_side_per_capture()
    {
        var s = Rogue("k7/8/8/8/8/8/3p4/3R3K w - - 0 1");
        Grant(s, ChronoCaptureAbility.Instance, "d1");
        string start = s.Dump();
        int mark = s.UndoDepth;

        var result = Play(s, "d1d2");

        Assert.Equal(5000, s.ClockMs(Side.White));
        Assert.Equal(0, s.ClockMs(Side.Black));
        Assert.Equal(5000, result.WhiteClockDeltaMs);
        Assert.Equal(5000, result.ClockDeltaMs(Side.White));
        Assert.Equal(0, result.BlackClockDeltaMs);
        var clock = Assert.Single(result.Trace!.AllFacts, f => f.Kind == FactKinds.ClockChanged);
        Assert.Equal((Side.White, 5000), (clock.Side, clock.NewValue));

        s.UndoTo(mark);
        Assert.Equal(0, s.ClockMs(Side.White));
        Assert.Equal(start, s.Dump());
    }

    [Fact]
    public void ChronoCapture_does_nothing_for_quiet_moves_or_other_pieces_kills()
    {
        var s = Rogue("k7/8/8/8/8/8/3p4/R2R3K w - - 0 1");
        Grant(s, ChronoCaptureAbility.Instance, "a1");
        Play(s, "a1a2");  // quiet move by the ChronoCapture rook
        Play(s, "a8b8");
        Play(s, "d1d2");  // the other rook's kill
        Assert.Equal(0, s.ClockMs(Side.White));
    }

    [Fact]
    public void B3_ChronoCapture_counts_a_ranged_kill_credited_to_its_piece()
    {
        // The prototype compared the kill's source id with the ability id, so ranged kills never counted.
        var s = Rogue("k7/8/8/3p4/3R4/8/8/7K w - - 0 1");
        Grant(s, ChronoCaptureAbility.Instance, "d4");
        Grant(s, ProbeSniper.Instance, "d4");

        var result = Play(s, "h1g1"); // turn end: the sniper shoots d5
        Assert.Null(At(s, "d5"));
        Assert.Equal(5000, s.ClockMs(Side.White));
        Assert.Equal(5000, result.WhiteClockDeltaMs);
    }

    [Fact]
    public void B3_ChronoCapture_counts_kills_by_its_own_death_effect_but_not_allies()
    {
        // The White knight on e4 explodes when the rook takes it: the rook (enemy), d5 (enemy) and f4 (ally) die.
        // The kills are credited to the dead knight; only the two enemy kills earn time.
        var s = Rogue("7k/8/8/3p4/4NP2/8/8/K3r3 b - - 0 1");
        Grant(s, ChronoCaptureAbility.Instance, "e4");
        Grant(s, ExplodingAbility.Instance, "e4");

        var result = Play(s, "e1e4");
        Assert.Null(At(s, "e4"));
        Assert.Null(At(s, "d5"));
        Assert.Null(At(s, "f4"));
        Assert.Equal(10000, s.ClockMs(Side.White));
        Assert.Equal(0, s.ClockMs(Side.Black));
        Assert.Equal(10000, result.WhiteClockDeltaMs);
    }

    [Fact]
    public void ChronoCapture_is_registered_under_the_prototype_name()
    {
        var catalog = ContentLibrary.CreateCatalog();
        Assert.Same(ChronoCaptureAbility.Instance, catalog.FindAbility("ChronoCapture"));
        Assert.Same(TimeBankRelic.White, catalog.FindRelic("TimeBankRelic")!(Side.White));
        Assert.Same(RoyalPromotionRelic.Black, catalog.FindRelic("RoyalPromotionRelic")!(Side.Black));
        Assert.Same(ExtraTurnPotion.Instance, catalog.FindConsumable("ExtraTurnPotion"));
        Assert.Same(FortifyPotion.Instance, catalog.FindConsumable("FortifyPotion"));
    }

    // ------------------------------------------------------------------ clock (engine side)

    [Fact]
    public void Clock_deltas_from_one_wave_sum_and_are_not_part_of_the_hash()
    {
        var s = Rogue("k7/8/8/8/8/8/8/K7 w - - 0 1");
        s.ClearHistory();
        ulong hash = s.Hash;
        s.AddClock(Side.Black, -1500);
        s.AddClock(Side.Black, 500);
        Assert.Equal(-1000, s.ClockMs(Side.Black));
        Assert.Equal(hash, s.Hash);
        Assert.Equal(hash, s.ComputeHashFromScratch());
        var clone = s.Clone();
        Assert.Equal(-1000, clone.ClockMs(Side.Black));
        s.UndoTo(0);
        Assert.Equal(0, s.ClockMs(Side.Black));
    }

    // ------------------------------------------------------------------ TimeBank

    [Fact]
    public void TimeBank_gives_its_owner_three_seconds_after_each_of_its_moves()
    {
        var s = Rogue("k7/8/8/8/8/8/1p6/R6K w - - 0 1");
        s.AddGlobal(TimeBankRelic.White);
        s.ClearHistory();

        var r1 = Play(s, "a1a2");
        Assert.Equal(3000, r1.WhiteClockDeltaMs);
        var r2 = Play(s, "a8b8"); // Black's move: nothing
        Assert.Equal(0, r2.WhiteClockDeltaMs);
        Assert.Equal(0, r2.BlackClockDeltaMs);
        Play(s, "a2b2");          // a capture is a move too
        Assert.Equal(6000, s.ClockMs(Side.White));
        Assert.Equal(0, s.ClockMs(Side.Black));

        s.UndoTo(0);
        Assert.Equal(0, s.ClockMs(Side.White));
    }

    [Fact]
    public void TimeBank_and_ChronoCapture_stack_and_a_black_relic_pays_black()
    {
        var s = Rogue("k7/8/8/8/8/8/1P6/7K b - - 0 1");
        s.AddGlobal(TimeBankRelic.Black);
        s.AddGlobal(TimeBankRelic.White);
        Grant(s, ChronoCaptureAbility.Instance, "a8");
        Play(s, "a8b7");
        Assert.Equal(3000, s.ClockMs(Side.Black));
        Assert.Equal(0, s.ClockMs(Side.White));

        var s2 = Rogue("8/8/8/8/8/8/kP6/7K b - - 0 1");
        s2.AddGlobal(TimeBankRelic.Black);
        Grant(s2, ChronoCaptureAbility.Instance, "a2");
        var r = Play(s2, "a2b2");
        Assert.Equal(8000, r.BlackClockDeltaMs);
    }

    [Fact]
    public void TimeBank_pays_nothing_for_a_move_blocked_by_a_guardian()
    {
        var s = Rogue("k7/8/8/8/8/8/1p6/1R5K w - - 0 1");
        s.SetTile(Sq(s, "b2"), GuardianTile.Instance);
        s.AddGlobal(TimeBankRelic.White);
        s.ClearHistory();
        Play(s, "b1b2");
        Assert.Equal("rook", Must(s, "b1").Def.Id);
        Assert.Equal(0, s.ClockMs(Side.White));
    }

    // ------------------------------------------------------------------ RoyalPromotion

    [Fact]
    public void RoyalPromotion_turns_a_friendly_piece_on_the_last_rank_into_a_queen_keeping_abilities()
    {
        var s = Rogue("7k/R7/8/8/8/8/8/K7 w - - 0 1");
        s.AddGlobal(RoyalPromotionRelic.White);
        Grant(s, ChronoCaptureAbility.Instance, "a7");
        string start = s.Dump();

        var result = Play(s, "a7a8");
        var piece = Must(s, "a8");
        Assert.Same(StandardPieces.Queen, piece.Def);
        Assert.True(piece.HasAbility(ChronoCaptureAbility.Instance));
        Assert.Contains(result.Trace!.AllFacts, f => f.Kind == FactKinds.PieceChanged && f.NewDef == StandardPieces.Queen && f.Cause == RoyalPromotionRelic.Cause);

        s.UndoTo(0);
        Assert.Equal(start, s.Dump());
        Assert.Same(StandardPieces.Rook, Must(s, "a7").Def);
    }

    [Fact]
    public void RoyalPromotion_only_helps_its_owner_and_leaves_kings_alone()
    {
        // Black's rook reaches Black's last rank (rank 1): only a Black relic promotes it.
        var s = Rogue("8/8/8/8/8/8/r7/k6K b - - 0 1");
        s.AddGlobal(RoyalPromotionRelic.White);
        s.ClearHistory();
        Play(s, "a2b2");
        Play(s, "h1g1");
        Play(s, "b2b1");
        Assert.Same(StandardPieces.Rook, Must(s, "b1").Def);

        var s2 = Rogue("8/8/8/8/8/8/r7/k6K b - - 0 1");
        s2.AddGlobal(RoyalPromotionRelic.Black);
        s2.ClearHistory();
        Play(s2, "a2b2");
        Play(s2, "h1g1");
        Play(s2, "b2b1");
        Assert.Same(StandardPieces.Queen, Must(s2, "b1").Def);

        var s3 = Rogue("k7/8/8/8/8/8/8/6K1 w - - 0 1");
        s3.AddGlobal(RoyalPromotionRelic.White);
        s3.ClearHistory();
        foreach (var m in new[] { "g1g2", "a8b8", "g2g3", "b8a8", "g3g4", "a8b8", "g4g5", "b8a8", "g5g6", "a8b8", "g6g7", "b8a8", "g7g8" })
            Play(s3, m);
        Assert.Same(StandardPieces.King, Must(s3, "g8").Def);
    }

    // ------------------------------------------------------------------ ExtraTurn

    [Fact]
    public void ExtraTurn_is_a_free_action_then_the_same_side_moves_twice_with_exact_undo_and_hash()
    {
        var s = Rogue("k7/8/8/8/8/8/8/R6K w - - 0 1");
        ExtraTurnPotion.Instance.Give(s, 1, 0);
        s.ClearHistory();
        string start = s.Dump();
        ulong startHash = s.Hash;

        Assert.Contains("use:extra-turn-potion", Legal(s));
        int m0 = s.UndoDepth;
        Play(s, "use:extra-turn-potion");
        Assert.Equal(Side.White, s.SideToMove);
        Assert.Equal(0, s.Ply);
        Assert.Equal(0, Charges(s, ExtraTurnPotion.Instance, Side.White));
        Assert.Equal(1, s.ExtraTurns(Side.White));
        Assert.NotEqual(startHash, s.Hash);
        Assert.DoesNotContain("use:extra-turn-potion", Legal(s)); // no charges left
        string afterUse = s.Dump();
        ulong afterUseHash = s.Hash;

        int m1 = s.UndoDepth;
        Play(s, "h1g1");
        Assert.Equal(Side.White, s.SideToMove); // the extra turn
        Assert.Equal(1, s.Ply);
        Assert.Equal(0, s.ExtraTurns(Side.White));

        Play(s, "g1h1");
        Assert.Equal(Side.Black, s.SideToMove);
        Assert.Equal(2, s.Ply);

        s.UndoTo(m1);
        Assert.Equal(afterUse, s.Dump());
        Assert.Equal(afterUseHash, s.Hash);
        s.UndoTo(m0);
        Assert.Equal(start, s.Dump());
        Assert.Equal(startHash, s.Hash);
    }

    [Fact]
    public void Consumables_belong_to_one_side_and_the_opponent_cannot_use_them()
    {
        var s = Rogue("k7/8/8/8/8/8/8/R6K b - - 0 1");
        var g = ExtraTurnPotion.Instance.Give(s, 1, 0);
        s.ClearHistory();
        Assert.DoesNotContain(Legal(s), m => m.StartsWith("use:"));
        var r = Game.PlayTurn(s, Move.UseConsumable(s.Globals.ToList().IndexOf(g)));
        Assert.False(r.Accepted);
        Assert.Equal(0, s.UndoDepth);
        Assert.Empty(s.History);
    }

    [Fact]
    public void Extra_turns_stack_and_turn_phases_run_for_the_side_that_keeps_moving()
    {
        var s = Rogue("k7/8/8/8/8/8/8/R6K w - - 0 1");
        ExtraTurnPotion.Instance.Give(s, 2, 0);
        s.GrantAbility(Must(s, "a1"), FrozenAbility.Instance, 2);
        s.ClearHistory();
        Play(s, "use:extra-turn-potion");
        Play(s, "use:extra-turn-potion");
        Assert.Equal(2, s.ExtraTurns(Side.White));
        Play(s, "h1g1"); // white's turn ends: frozen 2 -> 1, extra turn
        Play(s, "g1h1"); // 1 -> 0, thawed, extra turn
        Assert.False(Must(s, "a1").HasAbility(FrozenAbility.Instance));
        Assert.Equal(Side.White, s.SideToMove);
        Play(s, "a1a8"); // takes the king with the third move in a row
        Assert.Equal(GameOutcome.WhiteWins, s.Rules.Evaluate(s));
    }

    [Fact]
    public void AI_drinks_the_extra_turn_potion_to_win()
    {
        // Ra8 alone lets the king step away; with an extra turn, Ra8 then Rxh8.
        var s = Rogue("7k/8/8/8/8/8/8/R6K w - - 0 1");
        ExtraTurnPotion.Instance.Give(s, 1, 0);
        s.ClearHistory();
        ulong hash = s.Hash;
        var profile = AgentProfile.Club with { Id = "sharp", ChoiceMargin = 0, RootNoise = 0, MaxDepth = 4, NodeBudget = 400_000 };

        var decision = new AgentFactory().Create(profile).Decide(s);
        Assert.Equal("use:extra-turn-potion", MoveText.Format(s, decision.Move!.Value));
        Assert.True(decision.Score >= Search.WinThreshold, decision.Format(s));
        Assert.Equal(hash, s.Hash);
        Assert.Equal(0, s.UndoDepth);
    }

    [Fact]
    public void AI_keeps_a_consumable_when_using_it_gains_nothing()
    {
        var s = Rogue("k7/pp6/8/8/8/8/PP6/K7 w - - 0 1");
        FortifyPotion.Instance.Give(s, 1, 0);
        s.ClearHistory();
        var profile = AgentProfile.Club with { Id = "sharp", ChoiceMargin = 0, RootNoise = 0, MaxDepth = 3, NodeBudget = 200_000 };
        var decision = new AgentFactory().Create(profile).Decide(s);
        Assert.False(decision.Move!.Value.IsConsumable, decision.Format(s));
    }

    // ------------------------------------------------------------------ Fortify

    [Fact]
    public void Fortify_walls_every_empty_non_wall_cell_in_the_targets_row()
    {
        var s = Rogue("k7/8/8/8/8/8/8/R6K w - - 0 1");
        FortifyPotion.Instance.Give(s, 2, 0);
        s.SetTile(Sq(s, "c1"), WallTile.Instance);
        s.ClearHistory();

        // One offer per row that would gain a wall; every row qualifies here.
        Assert.Equal(8, Legal(s).Count(m => m.StartsWith("use:fortify-potion@")));

        // Any square of the row works as the target; occupied cells and the existing wall are skipped.
        int index = s.Globals.Count - 1;
        var result = Game.PlayTurn(s, Move.UseConsumable(index, Sq(s, "e1")), new ResolveOptions(Trace: true));
        Assert.True(result.Accepted, result.RejectReason);
        Assert.Equal(Side.White, s.SideToMove); // free action
        foreach (var name in new[] { "b1", "c1", "d1", "e1", "f1", "g1" })
            Assert.Same(WallTile.Instance, s.TileAt(Sq(s, name)).Def);
        Assert.False(s.TileAt(Sq(s, "a1")).Def == WallTile.Instance);
        Assert.False(s.TileAt(Sq(s, "h1")).Def == WallTile.Instance);
        Assert.Equal(5, result.Trace!.AllFacts.Count(f => f.Kind == FactKinds.TileChanged));
        Assert.Equal(1, Charges(s, FortifyPotion.Instance, Side.White));
        Assert.DoesNotContain("use:fortify-potion@a1", Legal(s)); // nothing left to fortify in row 1
    }

    [Fact]
    public void Fortify_walls_block_sliders_last_until_broken_and_need_a_target()
    {
        var s = Rogue("k7/8/8/8/8/8/8/R6K w - - 0 1");
        FortifyPotion.Instance.Give(s, 1, 0);
        s.ClearHistory();

        Assert.False(Game.PlayTurn(s, Move.UseConsumable(0)).Accepted); // requires a target
        Assert.Equal(0, s.UndoDepth);

        Play(s, "use:fortify-potion@a4");
        Assert.Equal(8, Walls(s));
        var rookMoves = Legal(s).Where(m => m.StartsWith("a1")).ToList();
        Assert.Contains("a1a4", rookMoves);
        Assert.DoesNotContain("a1a5", rookMoves);

        // Duration: permanent, like any wall.
        for (int i = 0; i < 6; i++) Play(s, s.SideToMove == Side.White ? (At(s, "h1") != null ? "h1g1" : "g1h1") : (At(s, "a8") != null ? "a8b8" : "b8a8"));
        Assert.Equal(8, Walls(s));

        Play(s, "a1a4"); // landing breaks the wall
        Assert.Equal(7, Walls(s));
        Assert.Equal(0, Charges(s, FortifyPotion.Instance, Side.White));
    }

    // ------------------------------------------------------------------ globals, history, maps

    [Fact]
    public void Relics_and_consumable_charges_are_part_of_state_hash_and_clone()
    {
        var s = Rogue("k7/8/8/8/8/8/8/R6K w - - 0 1");
        ulong bare = s.Hash;
        s.AddGlobal(TimeBankRelic.White);
        Assert.NotEqual(bare, s.Hash);
        ulong withRelic = s.Hash;
        FortifyPotion.Instance.Give(s, 1, 2);
        Assert.NotEqual(withRelic, s.Hash);
        Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
        Assert.Throws<InvalidOperationException>(() => FortifyPotion.Instance.Give(s, 1, 0));

        var other = Rogue("k7/8/8/8/8/8/8/R6K w - - 0 1");
        other.AddGlobal(TimeBankRelic.White);
        FortifyPotion.Instance.Give(other, 1, 1);
        Assert.NotEqual(s.Hash, other.Hash); // charges differ

        var clone = s.Clone();
        Assert.Equal(s.Hash, clone.Hash);
        Assert.Equal(s.Dump(), clone.Dump());
        Assert.Equal(2, Charges(clone, FortifyPotion.Instance, Side.Black));
    }

    [Fact]
    public void B9_move_history_records_every_accepted_action_and_undoes_with_the_state()
    {
        var s = Rogue("k7/8/8/8/8/8/8/R6K w - - 0 1");
        ExtraTurnPotion.Instance.Give(s, 1, 0);
        s.ClearHistory();
        Assert.Empty(s.History);

        Play(s, "use:extra-turn-potion");
        int mark = s.UndoDepth;
        Play(s, "a1a2");
        Play(s, "a2a3");
        Play(s, "a8b8");
        Assert.False(Game.PlayTurn(s, Move.UseConsumable(0)).Accepted); // rejected: not recorded

        Assert.Equal(
            new[] { (Side.White, 0, "use:extra-turn-potion"), (Side.White, 0, "a1a2"), (Side.White, 1, "a2a3"), (Side.Black, 2, "a8b8") },
            s.History.Select(h => (h.Side, h.Ply, MoveText.Format(s, h.Move))));
        Assert.Equal(4, s.Clone().History.Count);

        s.UndoTo(mark);
        Assert.Single(s.History);
    }

    [Fact]
    public void Maps_can_give_relics_and_consumables()
    {
        const string json = """
        {
          "width": 4, "height": 4, "startingPlayer": "White",
          "pieces": [
            { "type": "King", "color": "White", "x": 0, "y": 0, "decorators": ["ChronoCapture"] },
            { "type": "King", "color": "Black", "x": 3, "y": 3, "decorators": [] }
          ],
          "tiles": [],
          "relics": [ { "id": "TimeBankRelic", "owner": "White" }, { "id": "RoyalPromotionRelic", "owner": "Black" } ],
          "consumables": [
            { "id": "ExtraTurnPotion", "owner": "White", "charges": 2 },
            { "id": "ExtraTurnPotion", "owner": "Black" },
            { "id": "FortifyPotion", "owner": "Black", "charges": 1 }
          ]
        }
        """;
        var s = MapLoader.Load(json, ContentLibrary.CreateCatalog());
        Assert.Contains(s.Globals, g => g.Def == TimeBankRelic.White);
        Assert.Contains(s.Globals, g => g.Def == RoyalPromotionRelic.Black);
        Assert.Equal(2, Charges(s, ExtraTurnPotion.Instance, Side.White));
        Assert.Equal(1, Charges(s, ExtraTurnPotion.Instance, Side.Black));
        Assert.Equal(1, Charges(s, FortifyPotion.Instance, Side.Black));
        Assert.Equal(0, Charges(s, FortifyPotion.Instance, Side.White));
        Assert.True(s.PieceAt(0)!.HasAbility(ChronoCaptureAbility.Instance));

        var bad = json.Replace("TimeBankRelic", "NoSuchRelic");
        var e = Assert.Throws<MapLoadException>(() => MapLoader.Load(bad, ContentLibrary.CreateCatalog()));
        Assert.Contains("NoSuchRelic", e.UnknownIds);
    }

    // ------------------------------------------------------------------ fuzz

    [Fact]
    public void Random_games_with_run_content_keep_hash_and_undo_exact()
    {
        int consumableUses = 0, extraTurns = 0, clockChanges = 0;
        for (int game = 0; game < 6; game++)
        {
            var rng = new Random(500 + game);
            var s = Rogue("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w - - 0 1");
            foreach (var name in new[] { "b1", "d1", "e2", "g8", "d7", "a8" }) s.GrantAbility(Must(s, name), ChronoCaptureAbility.Instance);
            foreach (var name in new[] { "c2", "f7" }) s.GrantAbility(Must(s, name), ExplodingAbility.Instance);
            s.AddGlobal(TimeBankRelic.White);
            s.AddGlobal(RoyalPromotionRelic.White);
            s.AddGlobal(RoyalPromotionRelic.Black);
            ExtraTurnPotion.Instance.Give(s, 2, 2);
            FortifyPotion.Instance.Give(s, 1, 2);
            s.ClearHistory();
            string start = s.Dump();
            ulong startHash = s.Hash;

            var moves = new List<Move>();
            int played = 0;
            for (int ply = 0; ply < 90; ply++)
            {
                moves.Clear();
                MoveGenerator.GenerateLegal(s, moves);
                if (moves.Count == 0 || s.Rules.Evaluate(s) != GameOutcome.Ongoing) break;
                // Consumables are rare among the legal moves; pick them a third of the time when offered.
                var uses = moves.Where(m => m.IsConsumable).ToList();
                var m = uses.Count > 0 && rng.Next(3) == 0 ? uses[rng.Next(uses.Count)] : moves[rng.Next(moves.Count)];
                var side = s.SideToMove;
                int clockBefore = s.ClockMs(Side.White) + s.ClockMs(Side.Black);
                var result = Game.PlayTurn(s, m);
                Assert.True(result.Accepted, result.RejectReason);
                Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
                Assert.Equal(s.ClockMs(Side.White) + s.ClockMs(Side.Black) - clockBefore, result.WhiteClockDeltaMs + result.BlackClockDeltaMs);
                if (m.IsConsumable) consumableUses++;
                else if (s.SideToMove == side) extraTurns++;
                if (result.WhiteClockDeltaMs != 0 || result.BlackClockDeltaMs != 0) clockChanges++;
                played++;
                Assert.Equal(played, s.History.Count);
            }

            Assert.True(played > 10, $"stopped after {played}\n{s.Dump()}");
            s.UndoTo(0);
            Assert.Equal(startHash, s.Hash);
            Assert.Equal(start, s.Dump());
            Assert.Empty(s.History);
        }
        Assert.True(consumableUses > 5, $"only {consumableUses} consumable uses");
        Assert.True(extraTurns > 2, $"only {extraTurns} extra turns");
        Assert.True(clockChanges > 10, $"only {clockChanges} clock changes");
    }

    [Fact]
    public void AI_self_play_with_run_content_leaves_the_state_exact()
    {
        var s = Rogue("r3k2r/pppp1ppp/8/8/8/8/PPPP1PPP/R3K2R w - - 0 1");
        s.GrantAbility(Must(s, "a1"), ChronoCaptureAbility.Instance);
        s.AddGlobal(TimeBankRelic.Black);
        ExtraTurnPotion.Instance.Give(s, 1, 1);
        FortifyPotion.Instance.Give(s, 1, 1);
        s.ClearHistory();
        var agent = new AgentFactory().Create(AgentProfile.Club with { Id = "quick", MaxDepth = 2, NodeBudget = 20_000 });

        for (int i = 0; i < 16 && s.Rules.Evaluate(s) == GameOutcome.Ongoing; i++)
        {
            ulong before = s.Hash;
            int depth = s.UndoDepth;
            var d = agent.Decide(s);
            Assert.Equal(before, s.Hash);
            Assert.Equal(depth, s.UndoDepth);
            Assert.True(Game.PlayTurn(s, d.Move!.Value).Accepted);
            Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
        }
    }
}
