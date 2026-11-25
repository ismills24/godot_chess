using System;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.Content.BoardEffects;

/// <summary>
/// Fires at the end of turn <c>start</c>, then every <c>interval</c> turns after it (start defaults to interval):
/// interval 3 fires on turns 3, 6, 9...; interval 5 from turn 2 fires on 2, 7, 12...
/// </summary>
public sealed class TurnIntervalCondition : BoardCondition
{
    public TurnIntervalCondition(int interval, int? startTurn = null)
    {
        if (interval < 1) throw new ArgumentOutOfRangeException(nameof(interval), "interval must be at least 1");
        Interval = interval;
        StartTurn = startTurn ?? interval;
    }

    public int Interval { get; }
    public int StartTurn { get; }

    public override string Key => $"TurnInterval(every {Interval} from {StartTurn})";

    public override bool IsMet(IBoardView board, int turn, int timesFired)
        => turn >= StartTurn && (turn - StartTurn) % Interval == 0;
}

/// <summary>Fires once, at the end of turn <c>turnNumber</c>.</summary>
public sealed class TurnDelayCondition : BoardCondition
{
    public TurnDelayCondition(int turnNumber) => TurnNumber = turnNumber;

    public int TurnNumber { get; }

    public override string Key => $"TurnDelay({TurnNumber})";

    public override bool IsMet(IBoardView board, int turn, int timesFired)
        => timesFired == 0 && turn == TurnNumber;
}

/// <summary>
/// Fires at the end of any turn where <c>side</c>'s board value is below <c>threshold</c>, at most
/// <c>maxTriggers</c> times (-1 = no limit). Board value is the sum of piece values plus their abilities' values,
/// leaving out royal pieces (and their abilities) entirely.
/// </summary>
public sealed class BoardValueCondition : BoardCondition
{
    public BoardValueCondition(Side side, int threshold, int maxTriggers = -1)
    {
        Side = side;
        Threshold = threshold;
        MaxTriggers = maxTriggers;
    }

    public Side Side { get; }
    public int Threshold { get; }
    public int MaxTriggers { get; }

    public override string Key => $"BoardValue({Side} < {Threshold}, max {MaxTriggers})";

    public override bool IsMet(IBoardView board, int turn, int timesFired)
    {
        if (MaxTriggers >= 0 && timesFired >= MaxTriggers) return false;
        return BoardValue(board, Side) < Threshold;
    }

    public static int BoardValue(IBoardView board, Side side)
    {
        int total = 0;
        foreach (var p in board.PiecesOnBoard(side))
            if (!p.Def.IsRoyal) total += p.Value;
        return total;
    }
}
