using System.Collections.Generic;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;

namespace RogueChess.Engine.Rules;

/// <summary>
/// Counts leaf nodes of the legal move tree to a fixed depth. Known counts for standard positions
/// make this the strongest correctness check for move generation and make/unmake, and a benchmark.
/// </summary>
public static class Perft
{
    public static long Count(GameState s, int depth)
    {
        var buffers = NewBuffers(depth);
        return Recurse(s, depth, buffers);
    }

    /// <summary>Per-root-move node counts (for diffing against a reference engine when a count is off).</summary>
    public static IReadOnlyList<(string Move, long Nodes)> Divide(GameState s, int depth)
    {
        var result = new List<(string, long)>();
        var buffers = NewBuffers(depth);
        var roots = new List<Move>();
        MoveGenerator.GenerateLegal(s, roots);
        foreach (var m in roots)
        {
            string name = MoveText.Format(s, m);
            int mark = s.UndoDepth;
            MoveApplier.Apply(s, m);
            result.Add((name, Recurse(s, depth - 1, buffers)));
            s.UndoTo(mark);
        }
        return result;
    }

    private static List<Move>[] NewBuffers(int depth)
    {
        var buffers = new List<Move>[depth + 1];
        for (int i = 0; i <= depth; i++) buffers[i] = new List<Move>(64);
        return buffers;
    }

    private static long Recurse(GameState s, int depth, List<Move>[] buffers)
    {
        if (depth == 0) return 1;
        var moves = buffers[depth];
        moves.Clear();
        MoveGenerator.GenerateLegal(s, moves);
        if (depth == 1) return moves.Count;

        long nodes = 0;
        for (int i = 0; i < moves.Count; i++)
        {
            int mark = s.UndoDepth;
            MoveApplier.Apply(s, moves[i]);
            nodes += Recurse(s, depth - 1, buffers);
            s.UndoTo(mark);
        }
        return nodes;
    }
}
