using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using RogueChess.Engine.Core;
using RogueChess.Engine.Rules;

namespace RogueChess.Engine.AI;

/// <summary>
/// The standard opponent: alpha-beta search over real turns with a pluggable evaluator. One decision at a
/// time per instance; give each AI player its own agent.
/// </summary>
public sealed class SearchAgent : IChessAgent
{
    private readonly Search _search;
    private readonly object _gate = new();

    public SearchAgent(AgentProfile profile, IEvaluator evaluator)
    {
        Profile = profile;
        Evaluator = evaluator;
        _search = new Search(profile, evaluator);
    }

    public AgentProfile Profile { get; }
    public IEvaluator Evaluator { get; }

    public AgentDecision Decide(GameState state, CancellationToken cancel = default)
    {
        lock (_gate) return _search.Decide(state, cancel);
    }
}

/// <summary>Plays a uniformly random legal move (seeded by the profile and the position).</summary>
public sealed class RandomAgent : IChessAgent
{
    public RandomAgent(AgentProfile profile) => Profile = profile;

    public AgentProfile Profile { get; }

    public AgentDecision Decide(GameState state, CancellationToken cancel = default)
    {
        var clock = Stopwatch.StartNew();
        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(state, moves);
        if (moves.Count == 0) return new AgentDecision { Reason = "none", Elapsed = clock.Elapsed };
        var rng = new SplitMix(Profile.Seed ^ state.Hash);
        return new AgentDecision { Move = moves[rng.Next(moves.Count)], Reason = "random", Elapsed = clock.Elapsed };
    }
}
