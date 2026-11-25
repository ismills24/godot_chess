using System;
using System.Collections.Generic;
using System.Linq;
using RogueChess.Engine.Content;
using RogueChess.Engine.Content.Abilities;
using RogueChess.Engine.Content.BoardEffects;
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
/// Cross-family interactions: content ported by different agents (captures and ranged shots, casts, deaths and
/// tombs, board effects, relics and consumables) meeting in one position. Every Play checks the hash and that the
/// turn undoes exactly.
/// </summary>
public class Verify_InteractionTests
{
    private static GameState Rogue(string fen) => Fen.Parse(fen, RuleSet.Rogue);
    private static int Sq(IBoardView s, string name) => MoveText.ParseSquare(s, name);
    private static Piece? At(GameState s, string name) => s.PieceAt(Sq(s, name));
    private static Piece Must(GameState s, string name) => At(s, name) ?? throw new InvalidOperationException($"no piece on {name}");
    private static TileDef TileOn(GameState s, string name) => s.TileAt(Sq(s, name)).Def;

    private static void Grant(GameState s, string square, params AbilityDef[] abilities)
    {
        foreach (var a in abilities) s.GrantAbility(Must(s, square), a);
    }

    /// <summary>Put a dead piece in a tomb on <paramref name="square"/> (as if it died there with Revenant).</summary>
    private static Piece Entomb(GameState s, PieceDef def, Side side, string square, params AbilityDef[] abilities)
    {
        var p = s.Place(def, side, Sq(s, square));
        foreach (var a in abilities) s.GrantAbility(p, a);
        s.RemoveFromBoard(p);
        s.SetTile(Sq(s, square), TombTile.Instance, TombTile.VarsFor(p));
        return p;
    }

    private static List<string> Legal(GameState s)
    {
        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(s, moves);
        return moves.Select(m => MoveText.Format(s, m)).ToList();
    }

    private static List<string> LegalFrom(GameState s, string from)
    {
        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(s, moves);
        return moves.Where(m => m.From == Sq(s, from)).Select(m => MoveText.Format(s, m)).ToList();
    }

    private static Move Play(GameState s, string move)
    {
        var m = MoveText.FindLegal(s, move) ?? throw new InvalidOperationException($"{move} is not legal: {string.Join(" ", Legal(s))}");
        Assert.Equal(move, MoveText.Format(s, m));

        // The turn undoes exactly...
        string before = Verify_SoakTests.FullDump(s);
        int mark = s.UndoDepth;
        Assert.True(Game.PlayTurn(s, m).Accepted);
        s.UndoTo(mark);
        Assert.Equal(before, Verify_SoakTests.FullDump(s));

        // ...then play it for real.
        var result = Game.PlayTurn(s, m, new ResolveOptions(Trace: true));
        Assert.True(result.Accepted, result.RejectReason);
        Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
        var clone = s.Clone();
        Assert.Equal(Verify_SoakTests.FullDump(s), Verify_SoakTests.FullDump(clone));
        return m;
    }

    private static int Charges(Piece p) => p.GetAbility(MarksmanAbility.Instance)!.Var(MarksmanAbility.ChargesVar);
    private static int Walls(GameState s) => Enumerable.Range(0, s.Area).Count(sq => s.TileAt(sq).Def == WallTile.Instance);

    // ------------------------------------------------------------------ Marksman x deaths

    [Fact]
    public void Marksman_shot_kills_a_revenant_leaving_a_tomb_credits_ChronoCapture_and_the_tomb_revives()
    {
        var s = Rogue("4k3/8/8/n6r/8/8/8/R3K3 w - - 0 1");
        Grant(s, "a1", MarksmanAbility.Instance, ChronoCaptureAbility.Instance);
        Grant(s, "a5", RevenantAbility.Instance, ExplodingAbility.Instance);
        var knight = Must(s, "a5");
        var rook = Must(s, "a1");
        s.ClearHistory();

        Play(s, "a1a5@marksman");

        Assert.Same(rook, At(s, "a1"));
        Assert.Equal(0, Charges(rook));
        Assert.False(knight.IsOnBoard);
        Assert.Same(TombTile.Instance, TileOn(s, "a5"));
        Assert.Same(knight, TombTile.Entombed(s, Sq(s, "a5")));
        Assert.Equal(ChronoCaptureAbility.BonusMs, s.ClockMs(Side.White));

        // An allied rook sliding onto the empty tomb brings the knight back (without Revenant) and stays put.
        Play(s, "h5a5");
        Assert.Same(knight, At(s, "a5"));
        Assert.False(knight.HasAbility(RevenantAbility.Instance));
        Assert.True(knight.HasAbility(ExplodingAbility.Instance));
        Assert.NotNull(At(s, "h5"));
        Assert.True(TileOn(s, "a5").IsDefault);
    }

    [Fact]
    public void Marksman_shooting_an_adjacent_exploding_piece_dies_in_the_blast_but_keeps_the_kill_credit()
    {
        var s = Rogue("4k3/8/8/8/8/8/p7/R3K3 w - - 0 1");
        Grant(s, "a1", MarksmanAbility.Instance, ChronoCaptureAbility.Instance, RevenantAbility.Instance);
        Grant(s, "a2", ExplodingAbility.Instance);
        var rook = Must(s, "a1");
        s.ClearHistory();

        Play(s, "a1a2@marksman");

        Assert.Null(At(s, "a2"));
        Assert.False(rook.IsOnBoard);                         // caught in the blast
        Assert.Same(rook, TombTile.Entombed(s, Sq(s, "a1"))); // and entombed where it stood
        Assert.Equal(ChronoCaptureAbility.BonusMs, s.ClockMs(Side.White)); // the shot kill still counts
        Assert.Equal(0, s.ClockMs(Side.Black));
        Assert.NotNull(At(s, "e1"));
    }

    [Fact]
    public void Scapegoat_dies_in_place_of_a_ranged_target_and_the_shooter_gets_the_kill()
    {
        var s = Rogue("4k3/1p6/n7/8/8/8/8/R3K3 w - - 0 1");
        Grant(s, "a1", MarksmanAbility.Instance, ChronoCaptureAbility.Instance);
        Grant(s, "b7", ScapegoatAbility.Instance);
        var knight = Must(s, "a6");
        s.ClearHistory();

        Play(s, "a1a6@marksman");

        Assert.Same(knight, At(s, "a6"));
        Assert.Null(At(s, "b7"));
        Assert.Equal(0, Charges(Must(s, "a1")));
        // The shot still killed a piece, so the marksman is credited with the Scapegoat (ChronoCapture pays).
        Assert.Equal(ChronoCaptureAbility.BonusMs, s.ClockMs(Side.White));
    }

    [Fact]
    public void Scapegoat_taking_a_ranged_shot_feeds_a_predator_marksman()
    {
        var s = Rogue("4k3/1p6/n7/8/8/8/8/R3K3 w - - 0 1");
        Grant(s, "a1", MarksmanAbility.Instance, PredatorAbility.Instance);
        Grant(s, "b7", ScapegoatAbility.Instance);
        s.ClearHistory();

        Play(s, "a1a6@marksman");

        Assert.Null(At(s, "b7"));
        Assert.Same(StandardPieces.Queen, Must(s, "a1").Def); // one kill: Rook -> Queen
    }

    [Fact]
    public void Scapegoat_saving_a_piece_from_a_melee_capture_still_gives_no_kill_credit()
    {
        var s = Rogue("4k3/1p6/n7/8/8/8/8/R3K3 w - - 0 1");
        Grant(s, "a1", ChronoCaptureAbility.Instance);
        Grant(s, "b7", ScapegoatAbility.Instance);
        s.ClearHistory();

        Play(s, "a1a6");

        Assert.Null(At(s, "b7"));
        Assert.Equal(0, s.ClockMs(Side.White));
    }

    // ------------------------------------------------------------------ capture rewrites x tiles

    [Fact]
    public void Bouncer_knocks_a_piece_onto_fire_and_it_catches_fire()
    {
        var s = Rogue("4k3/8/8/8/n7/8/8/R3K3 w - - 0 1");
        Grant(s, "a1", BouncerAbility.Instance);
        s.SetTile(Sq(s, "a5"), FireTile.Instance);
        var knight = Must(s, "a4");
        s.ClearHistory();

        Play(s, "a1a4");

        Assert.Same(knight, At(s, "a5"));
        Assert.True(knight.HasAbility(OnFireAbility.Instance));
        Assert.Same(StandardPieces.Rook, Must(s, "a4").Def);
    }

    [Fact]
    public void Bouncer_knocks_a_piece_onto_slippery_and_it_slides_on()
    {
        var s = Rogue("4k3/8/8/8/n7/8/8/R3K3 w - - 0 1");
        Grant(s, "a1", BouncerAbility.Instance);
        s.SetTile(Sq(s, "a5"), SlipperyTile.Instance);
        var knight = Must(s, "a4");
        s.ClearHistory();

        Play(s, "a1a4");

        Assert.Same(knight, At(s, "a6"));
        Assert.Null(At(s, "a5"));
        Assert.Same(StandardPieces.Rook, Must(s, "a4").Def);
    }

    [Fact]
    public void Bouncer_knocking_a_piece_onto_its_allys_tomb_opens_the_tomb()
    {
        // Prototype: the knock is a MoveEvent, which the tomb intercepts like any other move onto it.
        var s = Rogue("4k3/8/8/8/n7/8/8/R3K3 w - - 0 1");
        Grant(s, "a1", BouncerAbility.Instance);
        var bishop = Entomb(s, StandardPieces.Bishop, Side.Black, "a5", RevenantAbility.Instance);
        var knight = Must(s, "a4");
        s.ClearHistory();

        Play(s, "a1a4");

        Assert.Same(bishop, At(s, "a5"));
        Assert.False(bishop.HasAbility(RevenantAbility.Instance));
        Assert.True(TileOn(s, "a5").IsDefault);
        Assert.Same(knight, At(s, "a4")); // the knock was cancelled...
        Assert.NotNull(At(s, "a1"));      // ...so the bouncer is blocked and stays
    }

    [Fact]
    public void Bouncer_knocking_a_piece_onto_an_enemy_tomb_leaves_the_tomb()
    {
        var s = Rogue("4k3/8/8/8/n7/8/8/R3K3 w - - 0 1");
        Grant(s, "a1", BouncerAbility.Instance);
        var dead = Entomb(s, StandardPieces.Bishop, Side.White, "a5", RevenantAbility.Instance);
        var knight = Must(s, "a4");
        s.ClearHistory();

        Play(s, "a1a4");

        Assert.Same(knight, At(s, "a5"));
        Assert.Same(dead, TombTile.Entombed(s, Sq(s, "a5")));
    }

    [Fact]
    public void Piercing_jumping_onto_slippery_does_not_slide()
    {
        var s = Rogue("4k3/8/8/8/n7/8/8/R3K3 w - - 0 1");
        Grant(s, "a1", PiercingAbility.Instance);
        s.SetTile(Sq(s, "a5"), SlipperyTile.Instance);
        var rook = Must(s, "a1");
        s.ClearHistory();

        Play(s, "a1a4");

        Assert.Same(rook, At(s, "a5")); // the jump over the knight is a jump: no slide
        Assert.NotNull(At(s, "a4"));
        Assert.Null(At(s, "a6"));
    }

    [Fact]
    public void Piercing_landing_on_an_allys_empty_tomb_opens_it()
    {
        var s = Rogue("4k3/8/8/8/n7/8/8/R3K3 w - - 0 1");
        Grant(s, "a1", PiercingAbility.Instance);
        var bishop = Entomb(s, StandardPieces.Bishop, Side.White, "a5", RevenantAbility.Instance);
        s.ClearHistory();

        Play(s, "a1a4");

        Assert.Same(bishop, At(s, "a5"));
        Assert.Same(StandardPieces.Rook, Must(s, "a1").Def); // the jump was cancelled
        Assert.NotNull(At(s, "a4"));
    }

    // ------------------------------------------------------------------ casts

    [Fact]
    public void Caster_never_targets_a_tomb_and_fire_next_to_a_tomb_leaves_the_revive_alone()
    {
        var s = Rogue("4k3/8/8/8/8/8/8/2B1K3 w - - 0 1");
        Grant(s, "c1", CasterAbility.FireMage);
        var knight = Entomb(s, StandardPieces.Knight, Side.White, "c2", RevenantAbility.Instance);
        s.ClearHistory();

        var casts = LegalFrom(s, "c1").Where(t => t.EndsWith("@fire-mage")).ToList();
        Assert.DoesNotContain("c1c2@fire-mage", casts);
        Assert.Contains("c1c3@fire-mage", casts);

        Play(s, "c1c3@fire-mage");
        Assert.Same(FireTile.Instance, TileOn(s, "c3"));
        Play(s, "e8e7");
        Play(s, "e1d2");
        Play(s, "e7e8");
        Play(s, "d2c2"); // opens the tomb; the king stays

        Assert.Same(knight, At(s, "c2"));
        Assert.False(knight.HasAbility(OnFireAbility.Instance));
        Assert.Same(StandardPieces.King, Must(s, "d2").Def);
    }

    [Fact]
    public void ExtraTurn_potion_then_a_cast_then_a_ranged_shot_all_in_one_turn_and_undo_to_start()
    {
        var s = Rogue("4k3/8/8/n7/8/8/8/R1B1K3 w - - 0 1");
        Grant(s, "a1", MarksmanAbility.Instance);
        Grant(s, "c1", CasterAbility.FireMage);
        ExtraTurnPotion.Instance.Give(s, 1, 0);
        s.ClearHistory();
        string start = Verify_SoakTests.FullDump(s);

        Play(s, "use:extra-turn-potion");
        Assert.Equal(Side.White, s.SideToMove);
        Assert.Equal(1, s.ExtraTurns(Side.White));
        Assert.Equal(0, s.Ply);

        Play(s, "c1c3@fire-mage");
        Assert.Equal(Side.White, s.SideToMove);
        Assert.Equal(0, s.ExtraTurns(Side.White));
        Assert.Same(FireTile.Instance, TileOn(s, "c3"));

        Play(s, "a1a5@marksman");
        Assert.Equal(Side.Black, s.SideToMove);
        Assert.Null(At(s, "a5"));
        Assert.Equal(2, s.Ply);
        Assert.Equal(3, s.History.Count);
        Assert.DoesNotContain(Legal(s), t => t.StartsWith("use:"));

        s.UndoTo(0);
        Assert.Equal(start, Verify_SoakTests.FullDump(s));
    }

    // ------------------------------------------------------------------ consumables x board effects

    [Fact]
    public void Fortify_walls_and_the_WallsPlacement_effect_never_stack_on_one_square()
    {
        var s = Rogue("4k3/8/8/8/8/8/8/4K3 w - - 0 1");
        FortifyPotion.Instance.Give(s, 1, 0);
        s.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(1), new WallsPlacementEffect(5)));
        s.SeedRandom(42);
        s.ClearHistory();

        Play(s, "use:fortify-potion@a4");
        Assert.Equal(8, Walls(s));
        Play(s, "e1e2");
        Assert.Equal(13, Walls(s));
        for (int x = 0; x < 8; x++) Assert.Same(WallTile.Instance, s.TileAt(s.Sq(x, 3)).Def);
    }

    [Fact]
    public void WindGust_pushes_one_piece_onto_slippery_and_another_onto_fire()
    {
        var s = Rogue("4k3/8/8/R1B5/8/8/8/4K3 w - - 0 1");
        s.SetTile(Sq(s, "a4"), SlipperyTile.Instance);
        s.SetTile(Sq(s, "c4"), FireTile.Instance);
        s.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(1), new WindGustEffect(Side.White)));
        var rook = Must(s, "a5");
        var bishop = Must(s, "c5");
        s.ClearHistory();

        Play(s, "e1f1");

        Assert.Same(rook, At(s, "a3"));   // pushed onto the ice, slid one more
        Assert.Same(bishop, At(s, "c4")); // pushed into the fire
        Assert.True(bishop.HasAbility(OnFireAbility.Instance));
        Assert.False(rook.HasAbility(OnFireAbility.Instance));
    }

    [Fact]
    public void CheckerboardWipe_freezes_a_marksman_and_a_caster_so_they_have_no_actions()
    {
        var s = Rogue("4k3/8/8/n7/8/8/8/R1B4K w - - 0 1");
        Grant(s, "a1", MarksmanAbility.Instance);
        Grant(s, "c1", CasterAbility.FireMage);
        s.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(1), new CheckerboardWipeEffect(SquareColor.White, 2)));
        s.ClearHistory();
        Assert.Contains("a1a5@marksman", Legal(s));
        Assert.Contains(Legal(s), t => t.EndsWith("@fire-mage"));

        Play(s, "h1g2");
        Play(s, "e8d7");

        Assert.Empty(LegalFrom(s, "a1"));
        Assert.Empty(LegalFrom(s, "c1"));
        Assert.DoesNotContain(Legal(s), t => t.Contains('@'));
        Assert.NotEmpty(LegalFrom(s, "g2"));
    }

    // ------------------------------------------------------------------ statuses, relics

    [Fact]
    public void Frozen_cannibal_has_no_moves_and_a_cannibal_can_eat_a_frozen_ally()
    {
        var s = Rogue("4k3/8/8/8/8/N7/8/R3K3 w - - 0 1");
        Grant(s, "a1", CannibalAbility.Instance);
        s.GrantAbility(Must(s, "a3"), FrozenAbility.Instance, 2);
        s.ClearHistory();
        Assert.Contains("a1a3", LegalFrom(s, "a1"));
        Play(s, "a1a3");
        Assert.Same(StandardPieces.Rook, Must(s, "a3").Def);

        var t = Rogue("4k3/8/8/8/8/N7/8/R3K3 w - - 0 1");
        Grant(t, "a1", CannibalAbility.Instance);
        t.GrantAbility(Must(t, "a1"), FrozenAbility.Instance, 2);
        t.ClearHistory();
        Assert.Empty(LegalFrom(t, "a1"));
    }

    [Fact]
    public void RoyalPromotion_of_a_predator_revenant_capturing_onto_the_last_rank_keeps_its_abilities()
    {
        var s = Rogue("4k1r1/8/5N2/8/8/8/8/4K3 w - - 0 1");
        Grant(s, "f6", PredatorAbility.Instance, RevenantAbility.Instance);
        s.AddGlobal(RoyalPromotionRelic.White);
        var knight = Must(s, "f6");
        s.ClearHistory();

        Play(s, "f6g8");

        Assert.Same(knight, At(s, "g8"));
        Assert.Same(StandardPieces.Queen, knight.Def);
        Assert.Equal(1, knight.GetAbility(PredatorAbility.Instance)!.Var(PredatorAbility.KillsVar));
        Assert.True(knight.HasAbility(RevenantAbility.Instance));
    }

    [Fact]
    public void OnFire_burns_a_revenant_into_its_tomb_and_ChronoCapture_earns_nothing_for_its_own_death()
    {
        var s = Rogue("4k3/8/8/8/8/8/8/1N2K3 w - - 0 1");
        var knight = Must(s, "b1");
        s.GrantAbility(knight, OnFireAbility.Instance, 1);
        Grant(s, "b1", RevenantAbility.Instance, ChronoCaptureAbility.Instance);
        s.ClearHistory();

        Play(s, "e1d1");

        Assert.False(knight.IsOnBoard);
        Assert.Same(knight, TombTile.Entombed(s, Sq(s, "b1")));
        Assert.Equal(0, s.ClockMs(Side.White));
    }

    // ------------------------------------------------------------------ ability indices (Move.Aux)

    [Fact]
    public void Revived_piece_without_revenant_shoots_with_the_shifted_ability_index()
    {
        var s = Rogue("4k3/8/8/n7/8/8/2N1K3/R6r b - - 0 1");
        Grant(s, "a1", RevenantAbility.Instance, MarksmanAbility.Instance); // marksman is index 1
        Grant(s, "h1", MarksmanAbility.Instance);
        var rook = Must(s, "a1");
        s.ClearHistory();

        Play(s, "h1a1@marksman");
        Assert.Same(rook, TombTile.Entombed(s, Sq(s, "a1")));
        Play(s, "c2a1");
        Assert.Same(rook, At(s, "a1"));
        Assert.Equal(0, rook.AbilityIndex(MarksmanAbility.Instance)); // Revenant is gone, Marksman moved up
        Play(s, "e8e7");

        var shot = Play(s, "a1a5@marksman");
        Assert.Equal(0, shot.Aux);
        Assert.Null(At(s, "a5"));
        Assert.Equal(0, Charges(rook));
    }

    [Fact]
    public void Thawing_shifts_the_marksman_index_and_a_stale_action_is_rejected_without_changes()
    {
        var s = Rogue("4k3/8/8/n7/8/8/8/R3K3 w - - 0 1");
        var rook = Must(s, "a1");
        s.GrantAbility(rook, FrozenAbility.Instance, 1);
        Grant(s, "a1", MarksmanAbility.Instance, CasterAbility.FireMage);
        s.ClearHistory();
        Assert.Empty(LegalFrom(s, "a1"));
        var staleShot = new Move(Sq(s, "a1"), Sq(s, "a5"), MoveFlags.Capture | MoveFlags.Ability, Aux: 1);

        Play(s, "e1e2"); // the rook thaws at the end of this turn: Frozen (index 0) is removed
        Play(s, "e8e7");
        Assert.Equal(0, rook.AbilityIndex(MarksmanAbility.Instance));
        Assert.Equal(1, rook.AbilityIndex(CasterAbility.FireMage));

        // A move generated before the thaw names index 1, now the Fire Mage: it must not fire, and nothing changes.
        string before = Verify_SoakTests.FullDump(s);
        Assert.False(Game.PlayTurn(s, staleShot).Accepted);
        Assert.Equal(before, Verify_SoakTests.FullDump(s));

        Assert.Equal(0, Play(s, "a1a5@marksman").Aux);
        Assert.Null(At(s, "a5"));
    }

    [Fact]
    public void Cannibal_marksman_has_the_same_actions_whatever_the_ability_order()
    {
        List<string> Actions(params AbilityDef[] order)
        {
            var s = Rogue("4k3/8/8/8/8/N7/8/R2nK3 w - - 0 1");
            Grant(s, "a1", order);
            s.ClearHistory();
            return LegalFrom(s, "a1").OrderBy(t => t, StringComparer.Ordinal).ToList();
        }

        var marksmanFirst = Actions(MarksmanAbility.Instance, CannibalAbility.Instance);
        var cannibalFirst = Actions(CannibalAbility.Instance, MarksmanAbility.Instance);
        Assert.Equal(marksmanFirst, cannibalFirst);
        Assert.Contains("a1a3", cannibalFirst);                 // eats the ally
        Assert.DoesNotContain("a1d1@marksman", cannibalFirst); // its movement can't take enemies, so no shot either
    }

    // ------------------------------------------------------------------ every action kind

    [Fact]
    public void Every_action_kind_round_trips_through_notation_and_undoes_exactly()
    {
        // Standard rules: castling, en passant, every promotion option, and the self-check filter resolving each
        // ability action, cast and consumable use through the full resolver.
        var s = Fen.Parse("rn2k3/P7/8/n2pP3/8/8/8/R3K2R w KQ d6 0 1", RuleSet.Standard);
        Grant(s, "a1", MarksmanAbility.Instance);
        Grant(s, "h1", CasterAbility.WallPlacement);
        ExtraTurnPotion.Instance.Give(s, 1, 0);
        FortifyPotion.Instance.Give(s, 1, 0);
        s.ClearHistory();

        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(s, moves);
        var kinds = moves.GroupBy(Verify_SoakTests.Kind).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var kind in new[] { "quiet", "capture", "en-passant", "castle", "promotion", "ability", "cast", "consumable", "consumable-targeted" })
            Assert.True(kinds.ContainsKey(kind), $"no {kind} in {string.Join(" ", Legal(s))}");

        var texts = moves.Select(m => MoveText.Format(s, m)).ToList();
        Assert.Equal(texts.Count, texts.Distinct().Count());
        Assert.Contains("e5d6", texts);
        Assert.Contains("e1g1", texts);
        Assert.Contains("a7b8n", texts);
        Assert.Contains("a1a5@marksman", texts);
        Assert.Contains("use:extra-turn-potion", texts);
        Assert.Contains("use:fortify-potion@a3", texts);

        foreach (var m in moves)
        {
            Assert.Equal(m, MoveText.FindLegal(s, MoveText.Format(s, m)));
            Verify_SoakTests.CheckPlayUndo(s, m);
        }
    }
}
