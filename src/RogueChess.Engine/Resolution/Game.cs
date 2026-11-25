using System;
using System.Collections.Generic;
using RogueChess.Engine.Content;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.Resolution;

/// <summary>Outcome of <see cref="Game.PlayTurn"/>.</summary>
/// <param name="Accepted">False if the action's resolution hit the watchdog; the state is then unchanged.</param>
/// <param name="RejectReason">Why the action was rejected.</param>
/// <param name="Trace">Wave-by-wave trace, when requested.</param>
public readonly record struct TurnResult(bool Accepted, string? RejectReason, TurnTrace? Trace);

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

        if (!ResolveAction(state, action, options, trace, out var reason))
        {
            state.UndoTo(mark);
            return new TurnResult(false, reason, trace);
        }

        RunTurnPhase(state, TurnEndPhase, new Fact(FactKinds.TurnEnded) { Side = side }, options, trace);
        state.SetTurn(side.Opponent(), state.Ply + 1);
        RunTurnPhase(state, TurnStartPhase, new Fact(FactKinds.TurnStarted) { Side = side.Opponent() }, options, trace);
        return new TurnResult(true, null, trace);
    }

    /// <summary>
    /// Resolve only the action (no turn-end, no turn pass). Used for legality checks. Updates en passant.
    /// </summary>
    internal static bool ResolveAction(GameState state, Move action, in ResolveOptions options, TurnTrace? trace, out string? failure)
    {
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
    /// Turn the chosen move into wave-0 proposals (RESOLUTION_RULES.md §7). A capture is just "destroy the
    /// target" plus "move in": the move succeeds because the square empties in the same wave.
    /// </summary>
    private static void BuildActionProposals(GameState s, Move m, Piece mover, List<Proposal> into)
    {
        if (m.Is(MoveFlags.Cast))
        {
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

        into.Add(Proposal.Move(mover, m.To, Causes.Action) with { Source = source });

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
