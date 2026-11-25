using System;
using System.Collections.Generic;
using RogueChess.Engine.Content;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.Resolution;

/// <summary>Outcome of <see cref="Game.PlayTurn"/>.</summary>
/// <param name="Accepted">False if the action's resolution hit the watchdog; the state is then unchanged.</param>
/// <param name="RejectReason">Why the action was rejected.</param>
/// <param name="Trace">Wave-by-wave trace, when requested.</param>
public readonly record struct TurnResult(bool Accepted, string? RejectReason, TurnTrace? Trace)
{
    /// <summary>Clock time (ms) the engine granted White during this turn (negative = taken). See <see cref="GameState.ClockMs"/>.</summary>
    public int WhiteClockDeltaMs { get; init; }

    /// <summary>Clock time (ms) the engine granted Black during this turn (negative = taken).</summary>
    public int BlackClockDeltaMs { get; init; }

    /// <summary>Clock time (ms) the engine granted <paramref name="side"/> during this turn.</summary>
    public int ClockDeltaMs(Side side) => side == Side.White ? WhiteClockDeltaMs : BlackClockDeltaMs;
}

/// <summary>
/// The one code path for playing turns, used by the UI, the tests and the AI alike.
///
/// A turn is: resolve the action → resolve turn-end for the mover → pass the turn → resolve turn-start
/// for the next side. Turn-start runs as the turn passes, so the next side's legal moves already
/// reflect it. Call <see cref="BeginGame"/> once after setup to run the first side's turn-start.
/// </summary>
public static class Game
{
    public const string TurnStartPhase = "turn-start";
    public const string ActionPhase = "action";
    public const string TurnEndPhase = "turn-end";

    [ThreadStatic] private static List<Proposal>? _actionBuffer;

    public static TurnTrace? BeginGame(GameState state, ResolveOptions options = default)
    {
        var trace = options.Trace ? new TurnTrace() : null;
        RunTurnPhase(state, TurnStartPhase, new Fact(FactKinds.TurnStarted) { Side = state.SideToMove }, options, trace);
        return trace;
    }

    /// <summary>
    /// Play <paramref name="action"/> for the side to move. If the action's resolution is rejected (wave cap,
    /// proposal cap or oscillation), nothing changes and <see cref="TurnResult.Accepted"/> is false.
    /// Undo the whole turn with <see cref="GameState.UndoTo"/> using the depth taken before calling.
    /// </summary>
    public static TurnResult PlayTurn(GameState state, Move action, ResolveOptions options = default)
    {
        var trace = options.Trace ? new TurnTrace() : null;
        int mark = state.UndoDepth;
        var side = state.SideToMove;
        int whiteClock = state.ClockMs(Side.White), blackClock = state.ClockMs(Side.Black);

        if (!ResolveAction(state, action, options, trace, out var reason))
        {
            state.UndoTo(mark);
            return new TurnResult(false, reason, trace);
        }
        state.RecordAction(new PlayedAction(side, state.Ply, action));

        // A consumable is a free action (as in the prototype): no turn end, and the same side acts again.
        if (!action.IsConsumable)
        {
            RunTurnPhase(state, TurnEndPhase, new Fact(FactKinds.TurnEnded) { Side = side }, options, trace);
            var next = side.Opponent();
            int extra = state.ExtraTurns(side);
            if (extra > 0)
            {
                // Extra turn: spend one and give the same side a fresh turn (it still counts as a ply).
                state.SetExtraTurns(side, extra - 1);
                next = side;
            }
            state.SetTurn(next, state.Ply + 1);
            RunTurnPhase(state, TurnStartPhase, new Fact(FactKinds.TurnStarted) { Side = next }, options, trace);
        }

        return new TurnResult(true, null, trace)
        {
            WhiteClockDeltaMs = state.ClockMs(Side.White) - whiteClock,
            BlackClockDeltaMs = state.ClockMs(Side.Black) - blackClock,
        };
    }

    /// <summary>
    /// Resolve only the action (no turn-end, no turn pass). Used for legality checks. Updates en passant.
    /// </summary>
    internal static bool ResolveAction(GameState state, Move action, in ResolveOptions options, TurnTrace? trace, out string? failure)
    {
        if (action.IsConsumable) return ResolveConsumable(state, action, options, trace, out failure);
        if (action.From < 0 || action.From >= state.Area)
        {
            failure = $"bad source square {action.From}";
            return false;
        }

        var mover = state.PieceAt(action.From);
        if (mover == null)
        {
            failure = $"no piece on square {action.From}";
            return false;
        }

        var proposals = _actionBuffer ??= new List<Proposal>(4);
        proposals.Clear();
        BuildActionProposals(state, action, mover, proposals);
        if (proposals.Count == 0)
        {
            failure = "the action does nothing";
            return false;
        }

        ResolutionTrace? rt = null;
        if (trace != null)
        {
            rt = new ResolutionTrace(ActionPhase);
            trace.Resolutions.Add(rt);
        }

        var resolver = Resolver.Rent();
        if (!resolver.Resolve(state, ActionPhase, null, proposals, new ActionInfo(mover.Id, action.Promotion), options, rt, out failure))
            return false;

        int enPassant = action.Is(MoveFlags.DoublePush) && state.Rules.EnPassant && mover.Square == action.To
            ? (action.From + action.To) / 2
            : -1;
        if (enPassant != state.EnPassantSquare) state.SetEnPassant(enPassant);
        return true;
    }

    /// <summary>
    /// Use a consumable: its definition's proposals plus one charge spent, resolved as the action. The side to
    /// move must hold a charge and the use must be valid (e.g. a target when one is required).
    /// </summary>
    private static bool ResolveConsumable(GameState state, Move action, in ResolveOptions options, TurnTrace? trace, out string? failure)
    {
        int index = action.Aux;
        var side = state.SideToMove;
        if (index < 0 || index >= state.Globals.Count || state.Globals[index].Def is not ConsumableDef def)
        {
            failure = $"no consumable in global {index}";
            return false;
        }
        if (state.Globals[index].Var(ConsumableDef.ChargesVar(side)) <= 0)
        {
            failure = $"{side} has no {def.Name} left";
            return false;
        }
        if (action.To < -1 || action.To >= state.Area)
        {
            failure = $"bad consumable target {action.To}";
            return false;
        }

        var proposals = new List<Proposal>(8);
        if (!def.Use(state, side, action.To, proposals))
        {
            failure = $"{def.Name} can't be used like that";
            return false;
        }
        var source = SourceRef.ForGlobal(def, index);
        for (int i = 0; i < proposals.Count; i++) proposals[i] = proposals[i] with { Source = source };
        proposals.Add(Proposal.AddVar(VarTarget.OfGlobal(index), ConsumableDef.ChargesVar(side), -1, ConsumableDef.UsedCause) with { Source = source });

        ResolutionTrace? rt = null;
        if (trace != null)
        {
            rt = new ResolutionTrace(ActionPhase);
            trace.Resolutions.Add(rt);
        }
        return Resolver.Rent().Resolve(state, ActionPhase, null, proposals, ActionInfo.None, options, rt, out failure);
    }

    /// <summary>
    /// Turn the chosen move into wave-0 proposals (RESOLUTION_RULES.md §7). A capture is just "destroy the
    /// target" plus "move in": the move succeeds because the square empties in the same wave.
    /// </summary>
    private static void BuildActionProposals(GameState s, Move m, Piece mover, List<Proposal> into)
    {
        if (m.Is(MoveFlags.Ability))
        {
            // An ability's own action (cast, ranged shot...): the ability builds the proposals and is their source.
            BuildAbilityActionProposals(s, m, mover, into);
            return;
        }

        var source = SourceRef.ForPiece(mover, -1, mover.Square);

        if (m.Is(MoveFlags.EnPassant))
        {
            if (s.PieceAt(m.Aux) is { } victim) into.Add(Proposal.Destroy(victim, Causes.EnPassant) with { Source = source });
        }
        else if (m.Is(MoveFlags.Capture))
        {
            if (s.PieceAt(m.To) is { } target) into.Add(Proposal.Destroy(target, Causes.Capture) with { Source = source });
        }

        into.Add(Proposal.Move(mover, m.To, Causes.Action) with { Source = source, Jump = m.Is(MoveFlags.Jump) });

        if (m.Is(MoveFlags.Castle) && s.PieceAt(m.Aux) is { } partner)
        {
            int dir = Math.Sign(m.To - m.From);
            into.Add(Proposal.Move(partner, m.From + dir, Causes.Castle) with { Source = source });
        }
    }

    /// <summary>
    /// A cast names its ability by index (<see cref="Move.Aux"/>); that ability builds the proposals and is
    /// their source. A stale index or an ability that proposes nothing leaves the list empty (rejected).
    /// </summary>
    private static void BuildAbilityActionProposals(GameState s, Move m, Piece mover, List<Proposal> into)
    {
        if ((uint)m.Aux >= (uint)mover.Abilities.Count) return;
        var ability = mover.Abilities[m.Aux];
        int first = into.Count;
        ability.Def.ProposeAction(s, mover, ability, m, into);
        var source = SourceRef.ForPiece(mover, m.Aux, mover.Square);
        for (int i = first; i < into.Count; i++) into[i] = into[i] with { Source = source };
    }

    private static void RunTurnPhase(GameState state, string phase, Fact start, in ResolveOptions options, TurnTrace? trace)
    {
        ResolutionTrace? rt = null;
        if (trace != null)
        {
            rt = new ResolutionTrace(phase);
            trace.Resolutions.Add(rt);
        }
        // A failed turn-start/turn-end resolution is rolled back and skipped; the turn still passes.
        Resolver.Rent().Resolve(state, phase, start, null, ActionInfo.None, options, rt, out _);
    }
}
