using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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

public class AiTests
{
    private readonly ITestOutputHelper _out;
    public AiTests(ITestOutputHelper output) => _out = output;

    private static readonly AgentFactory Factory = new();

    private static GameState Rogue(string fen) => Fen.Parse(fen, RuleSet.Rogue);
    private static int Sq(IBoardView s, string name) => MoveText.ParseSquare(s, name);

    private string Choose(GameState s, AgentProfile profile)
    {
        var agent = Factory.Create(profile);
        var decision = agent.Decide(s);
        _out.WriteLine($"[{profile.Id}] " + decision.Format(s));
        return decision.Move is { } m ? MoveText.Format(s, m) : "(none)";
    }

    /// <summary>Strong, noise-free, deterministic: for tactical tests.</summary>
    private static readonly AgentProfile Sharp = AgentProfile.Club with
    {
        Id = "sharp", ChoiceMargin = 0, RootNoise = 0, MaxDepth = 4, NodeBudget = 400_000,
    };

    // ------------------------------------------------------------------ tactics

    [Fact]
    public void Takes_a_hanging_king()
    {
        foreach (var profile in AgentProfile.Presets.Where(p => p.Kind == AgentFactory.SearchKind))
            Assert.Equal("a1a8", Choose(Rogue("k7/8/8/8/8/8/8/R6K w - - 0 1"), profile));
    }

    [Fact]
    public void Finds_a_forced_king_capture_two_moves_deep()
    {
        // Back rank: after Ra8 the black king has nowhere to go.
        var s = Rogue("6k1/5ppp/8/8/8/8/8/R5K1 w - - 0 1");
        var decision = Factory.Create(Sharp).Decide(s);
        _out.WriteLine(decision.Format(s));
        Assert.Equal("a1a8", MoveText.Format(s, decision.Move!.Value));
        Assert.True(decision.Score >= Search.WinThreshold);
    }

    [Fact]
    public void Finds_checkmate_under_standard_rules()
    {
        var s = Fen.Parse(Fen.StartPosition, RuleSet.Standard);
        foreach (var m in new[] { "f2f3", "e7e5", "g2g4" }) TestPositions.Play(s, m);
        Assert.Equal("d8h4", Choose(s, Sharp));
    }

    [Fact]
    public void Saves_its_own_king()
    {
        // The white queen attacks the black king; only taking it saves the game.
        Assert.Equal("a8b7", Choose(Rogue("k7/1Q6/8/8/8/8/8/7K b - - 0 1"), AgentProfile.Casual with { BlunderChance = 0 }));
    }

    [Fact]
    public void Does_not_capture_an_exploding_piece_next_to_its_own_king()
    {
        var s = Rogue("4k3/8/8/8/3Q4/8/3p4/4K3 w - - 0 1");
        s.GrantAbility(s.PieceAt(Sq(s, "d2"))!, ExplodingAbility.Instance);
        s.ClearHistory();

        string move = Choose(s, Sharp);

        Assert.NotEqual("d4d2", move);
        Assert.NotEqual("e1d2", move);
        TestPositions.Play(s, move);
        // Whatever black replies, white still has its king.
        var reply = Factory.Create(Sharp).Decide(s).Move!.Value;
        Game.PlayTurn(s, reply);
        Assert.NotEqual(GameOutcome.BlackWins, s.Rules.Evaluate(s));
    }

    [Fact]
    public void Uses_an_exploding_enemy_piece_to_kill_the_enemy_king()
    {
        var s = Rogue("8/8/8/4k3/3p4/8/8/3R3K w - - 0 1");
        s.GrantAbility(s.PieceAt(Sq(s, "d4"))!, ExplodingAbility.Instance);
        s.ClearHistory();
        Assert.Equal("d1d4", Choose(s, Sharp));
    }

    [Fact]
    public void Knows_a_guardian_tile_blocks_a_capture_even_without_lookahead()
    {
        // The rook can "take" the queen (on a guardian tile: nothing happens) or the knight.
        var s = Rogue("k7/8/8/8/q2R3n/8/8/7K w - - 0 1");
        s.SetTile(Sq(s, "a4"), GuardianTile.Instance);
        s.ClearHistory();
        var shallow = AgentProfile.Club with { Id = "shallow", MaxDepth = 1, QuiescenceDepth = 0, ChoiceMargin = 0, RootNoise = 0 };
        Assert.Equal("d4h4", Choose(s, shallow));
    }

    // ------------------------------------------------------------------ determinism and safety

    [Fact]
    public void Decisions_are_deterministic_and_leave_the_state_untouched()
    {
        var s = Rogue("r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w - - 0 1");
        string before = s.Dump();
        // Node budgets are the reproducible limit; the wall-clock cap is off here (it is only a safety net).
        foreach (var preset in AgentProfile.Presets)
        {
            var profile = preset with { TimeBudgetMs = 0, NodeBudget = Math.Min(preset.NodeBudget, 100_000) };
            var a = Factory.Create(profile).Decide(s);
            Assert.Equal(before, s.Dump());
            var b = Factory.Create(profile).Decide(s.Clone());
            Assert.Equal(a.Move, b.Move);
            Assert.Equal(a.Nodes, b.Nodes);
        }
    }

    [Fact]
    public void Cancellation_stops_the_search_and_still_returns_a_move()
    {
        var s = Rogue(Fen.StartPosition);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var decision = Factory.Create(AgentProfile.Master).Decide(s, cts.Token);
        Assert.NotNull(decision.Move);
    }

    [Fact]
    public void Randomness_varies_by_seed_but_never_skips_a_forced_win()
    {
        var s = Rogue(Fen.StartPosition);
        var moves = Enumerable.Range(1, 12)
            .Select(seed => Factory.Create(AgentProfile.Chaotic with { Seed = (ulong)seed }).Decide(s).Move)
            .Distinct().Count();
        Assert.True(moves > 1, "chaotic profiles should vary their opening move with the seed");

        for (ulong seed = 1; seed <= 12; seed++)
            Assert.Equal("a1a8", Choose(Rogue("k7/8/8/8/8/8/8/R6K w - - 0 1"), AgentProfile.Novice with { Seed = seed, BlunderChance = 1 }));
    }

    [Fact]
    public void Personality_weights_change_the_evaluation()
    {
        // A white queen next to the black king, white king alone.
        var s = Rogue("4k3/3Q4/8/8/8/8/8/4K3 b - - 0 1");
        int aggressive = new WeightedEvaluator(AgentProfile.Aggressive.Weights).Evaluate(s);
        int defensive = new WeightedEvaluator(AgentProfile.Defensive.Weights).Evaluate(s);
        // Black to move: the aggressive evaluator fears white's pressure more.
        Assert.True(aggressive < defensive, $"aggressive {aggressive} vs defensive {defensive}");
    }

    // ------------------------------------------------------------------ factory / injection

    [Fact]
    public void Profiles_load_from_json_with_extends_and_weight_overrides()
    {
        var factory = new AgentFactory();
        var loaded = factory.LoadProfiles("""
            { "profiles": [
                { "id": "berserker", "extends": "aggressive", "name": "Berserker", "maxDepth": 2,
                  "weights": { "kingPressure": 50 } },
                { "id": "berserker-lite", "extends": "berserker", "blunderChance": 0.3 }
            ] }
            """);

        Assert.Equal(2, loaded.Count);
        var b = factory.GetProfile("berserker-lite");
        Assert.Equal(2, b.MaxDepth);
        Assert.Equal(50, b.Weights.KingPressure);
        Assert.Equal(AgentProfile.Aggressive.Weights.Threats, b.Weights.Threats);
        Assert.Equal(0.3, b.BlunderChance);
        Assert.Equal(AgentProfile.Aggressive.Contempt, b.Contempt);
    }

    [Fact]
    public void Level_json_can_name_a_profile_or_describe_one_inline()
    {
        Assert.Equal("greedy", Factory.CreateFromJson("\"greedy\"").Profile.Id);

        var agent = Factory.CreateFromJson("""{ "extends": "club", "maxDepth": 1, "contempt": 25 }""");
        Assert.Equal(1, agent.Profile.MaxDepth);
        Assert.Equal(25, agent.Profile.Contempt);
        Assert.Equal("club*", agent.Profile.Id);
    }

    [Fact]
    public void Map_files_choose_the_ai_players()
    {
        var (white, black) = Factory.ParseMapPlayers("""
            { "width": 8, "height": 8, "pieces": [],
              "ai": { "white": "human", "black": { "extends": "aggressive", "maxDepth": 2 } } }
            """);
        Assert.Null(white);
        Assert.Equal(2, black!.MaxDepth);
        Assert.Equal(AgentProfile.Aggressive.Weights, black.Weights);

        Assert.Equal((null, null), Factory.ParseMapPlayers("""{ "width": 8, "height": 8, "pieces": [] }"""));
        Assert.Throws<AgentConfigException>(() => Factory.ParseMapPlayers("""{ "ai": { "red": "club" } }"""));
    }

    [Fact]
    public void Bad_profiles_are_reported()
    {
        Assert.Throws<AgentConfigException>(() => Factory.CreateFromJson("\"grandmaster\""));
        Assert.Throws<AgentConfigException>(() => Factory.CreateFromJson("""{ "extends": "club", "maxDeph": 2 }"""));
        Assert.Throws<AgentConfigException>(() => Factory.CreateFromJson("""{ "kind": "neural" }"""));
    }

    private sealed class FirstMoveAgent : IChessAgent
    {
        public FirstMoveAgent(AgentProfile p) => Profile = p;
        public AgentProfile Profile { get; }
        public AgentDecision Decide(GameState state, CancellationToken cancel = default)
        {
            var moves = new List<Move>();
            MoveGenerator.GenerateLegal(state, moves);
            return new AgentDecision { Move = moves.Count > 0 ? moves[0] : null, Reason = "first" };
        }
    }

    private sealed class MaterialOnly : IEvaluator
    {
        public int Evaluate(GameState s) => s.PiecesOnBoard().Sum(p => (p.Side == s.SideToMove ? 1 : -1) * p.Value * 100);
    }

    [Fact]
    public void Custom_agent_kinds_and_evaluators_can_be_injected()
    {
        var factory = new AgentFactory()
            .RegisterKind("first", (p, _) => new FirstMoveAgent(p))
            .RegisterEvaluator("material", _ => new MaterialOnly());

        Assert.IsType<FirstMoveAgent>(factory.CreateFromJson("""{ "kind": "first" }"""));

        var agent = (SearchAgent)factory.CreateFromJson("""{ "extends": "club", "evaluator": "material" }""");
        Assert.IsType<MaterialOnly>(agent.Evaluator);
        Assert.Equal("a1a8", MoveText.Format(Rogue("k7/8/8/8/8/8/8/R6K w - - 0 1"),
            agent.Decide(Rogue("k7/8/8/8/8/8/8/R6K w - - 0 1")).Move!.Value));
    }

    // ------------------------------------------------------------------ strength and speed

    private static GameOutcome PlayGame(GameState s, IChessAgent white, IChessAgent black, int maxPlies)
    {
        for (int ply = 0; ply < maxPlies; ply++)
        {
            var outcome = s.Rules.Evaluate(s);
            if (outcome != GameOutcome.Ongoing) return outcome;
            var agent = s.SideToMove == Side.White ? white : black;
            var move = agent.Decide(s).Move;
            if (move == null) return GameOutcome.Draw;
            Game.PlayTurn(s, move.Value);
        }
        return s.Rules.Evaluate(s);
    }

    [Fact]
    public void A_searching_agent_beats_random_play()
    {
        var quick = AgentProfile.Casual with { Id = "quick", BlunderChance = 0, NodeBudget = 15_000 };
        int wins = 0;
        for (ulong seed = 1; seed <= 4; seed++)
        {
            var s = Rogue(Fen.StartPosition);
            var result = PlayGame(s, Factory.Create(quick with { Seed = seed }), Factory.Create(AgentProfile.Random with { Seed = seed }), 160);
            _out.WriteLine($"game {seed}: {result} after {s.Ply} plies");
            if (result == GameOutcome.WhiteWins) wins++;
        }
        Assert.True(wins >= 3, $"won {wins}/4");
    }

    [Fact]
    public void Benchmark_nodes_per_second()
    {
        var positions = new[]
        {
            ("start", Rogue(Fen.StartPosition)),
            ("kiwipete", Rogue("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w - - 0 1")),
            ("exploding mix", WithContent(Rogue(Fen.StartPosition))),
        };
        foreach (var (name, s) in positions)
        {
            var d = Factory.Create(AgentProfile.Club with { ChoiceMargin = 0, RootNoise = 0, MaxDepth = 4, NodeBudget = 200_000, TimeBudgetMs = 0 }).Decide(s);
            _out.WriteLine($"{name,-14} depth {d.Depth}{(d.Truncated ? "+" : "")} {d.Nodes,8:N0} nodes {d.Elapsed.TotalMilliseconds,6:0} ms {d.NodesPerSecond,9:N0} n/s  best {MoveText.Format(s, d.Move!.Value)}");
            Assert.True(d.Nodes > 0);
        }
    }

    private static GameState WithContent(GameState s)
    {
        foreach (var sq in new[] { "d2", "e7" }) s.GrantAbility(s.PieceAt(Sq(s, sq))!, ExplodingAbility.Instance);
        s.GrantAbility(s.PieceAt(Sq(s, "f2"))!, ScapegoatAbility.Instance);
        s.GrantAbility(s.PieceAt(Sq(s, "d8"))!, PhantomAbility.Instance);
        s.GrantAbility(s.PieceAt(Sq(s, "b1"))!, PredatorAbility.Instance);
        s.SetTile(Sq(s, "d4"), GuardianTile.Instance);
        s.SetTile(Sq(s, "e5"), FogTile.Instance);
        s.ClearHistory();
        return s;
    }
}
