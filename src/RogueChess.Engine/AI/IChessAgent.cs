using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;

namespace RogueChess.Engine.AI;

/// <summary>
/// An AI opponent. Implementations are created from an <see cref="AgentProfile"/> by an
/// <see cref="AgentFactory"/>, so a level only names (or describes) the opponent it wants.
/// </summary>
public interface IChessAgent
{
    AgentProfile Profile { get; }

    /// <summary>
    /// Choose a move for the side to move. The agent may play moves on <paramref name="state"/> while thinking
    /// but always restores it before returning. To think on another thread, pass <c>state.Clone()</c>.
    /// Returns a decision with <see cref="AgentDecision.Move"/> null when there is no move.
    /// </summary>
    AgentDecision Decide(GameState state, CancellationToken cancel = default);
}

/// <summary>One root move as the agent saw it.</summary>
/// <param name="Score">Centipawns from the mover's point of view, before noise.</param>
/// <param name="Exact">False when the search only proved the move is worse than <see cref="Score"/>.</param>
/// <param name="Noise">The personality's fixed noise for this move.</param>
public readonly record struct RootMove(Move Move, int Score, bool Exact, int Noise);

/// <summary>What the agent chose and why. Drives the sandbox's AI panel and tests.</summary>
public sealed class AgentDecision
{
    public Move? Move { get; init; }
    public int Score { get; init; }
    public int Depth { get; init; }
    public long Nodes { get; init; }
    public TimeSpan Elapsed { get; init; }

    /// <summary>How the move was picked: "best", "candidate" (within the choice margin), "blunder", "random", "only move", "none".</summary>
    public string Reason { get; init; } = "";

    /// <summary>Root moves of the last completed iteration, best first.</summary>
    public IReadOnlyList<RootMove> RootMoves { get; init; } = Array.Empty<RootMove>();

    /// <summary>Expected continuation, starting with <see cref="Move"/> when it was the search's best move.</summary>
    public IReadOnlyList<Move> PrincipalVariation { get; init; } = Array.Empty<Move>();

    /// <summary>True if a budget, the time cap or cancellation cut the last iteration short.</summary>
    public bool Truncated { get; init; }

    public long NodesPerSecond => Elapsed.TotalSeconds > 0 ? (long)(Nodes / Elapsed.TotalSeconds) : 0;

    public static string FormatScore(int score)
    {
        if (score >= Search.WinThreshold) return $"wins in {(Search.Win - score + 1) / 2}";
        if (score <= -Search.WinThreshold) return $"loses in {(Search.Win + score + 1) / 2}";
        return (score / 100.0).ToString("+0.00;-0.00;0.00", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Human-readable summary: the choice, the search stats and the top root moves.</summary>
    public string Format(GameState board, int top = 5)
    {
        var sb = new System.Text.StringBuilder();
        string move = Move is { } m ? MoveText.Format(board, m) : "(none)";
        sb.Append($"{move}  [{Reason}]  eval {FormatScore(Score)}  depth {Depth}{(Truncated ? "+" : "")}  "
            + $"{Nodes:N0} nodes  {Elapsed.TotalMilliseconds:0} ms  {NodesPerSecond / 1000:N0}k n/s\n");
        if (PrincipalVariation.Count > 0)
            sb.Append("  line: ").Append(string.Join(" ", PrincipalVariation.Select(x => MoveText.Format(board, x)))).Append('\n');
        foreach (var r in RootMoves.Take(top))
            sb.Append($"  {MoveText.Format(board, r.Move),-7} {(r.Exact ? "" : "≤")}{FormatScore(r.Score)}{(r.Noise != 0 ? $" (noise {r.Noise:+0;-0})" : "")}\n");
        return sb.ToString();
    }
}
