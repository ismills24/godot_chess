using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content.BoardEffects;

/// <summary>
/// When a board effect fires. Conditions are checked at the end of every turn (both sides' turns count).
/// They keep no state of their own: what they need to remember (how often the effect already fired) is the
/// owning global's variable, so it is hashed and undone with the rest of the game.
/// </summary>
public abstract class BoardCondition
{
    /// <summary>Stable text describing the condition and its parameters (part of the effect's id and hash).</summary>
    public abstract string Key { get; }

    /// <param name="turn">The turn that is ending, counted from 1 and going up by one every move (the prototype's turnNumber).</param>
    /// <param name="timesFired">How many times this board effect has fired before.</param>
    public abstract bool IsMet(IBoardView board, int turn, int timesFired);
}

/// <summary>What a board effect does when its condition is met: proposals for the next wave of the turn-end resolution.</summary>
public abstract class BoardEffect
{
    /// <summary>Stable text describing the effect and its parameters (part of the effect's id and hash).</summary>
    public abstract string Key { get; }

    /// <summary>Plain-language description for the UI.</summary>
    public abstract string Describe();

    /// <param name="timesFired">How many times this board effect has fired before (0 the first time).</param>
    public abstract void Apply(TriggerContext ctx, int timesFired);
}

/// <summary>
/// A board effect in play: a condition plus an effect, as in the prototype's map files
/// (<c>boardEffects: [{ id, name, description, condition: {type, params}, effect: {type, params} }]</c>).
///
/// It is a global trigger on <see cref="FactKinds.TurnEnded"/>. When the condition is met, the effect's proposals
/// and a +1 to <see cref="TimesFiredVar"/> go into the next wave. The def is immutable and built per map entry;
/// two entries with identical parameters share an id.
/// </summary>
public sealed class BoardEffectDef : GlobalDef
{
    /// <summary>Global variable 0: how many times this board effect has fired.</summary>
    public const int TimesFiredVar = 0;

    public BoardEffectDef(BoardCondition condition, BoardEffect effect, string? name = null, string? description = null)
        : base($"board-effect:{effect.Key} when {condition.Key}", name ?? effect.Key, varCount: 1)
    {
        Condition = condition;
        Effect = effect;
        Description = description ?? effect.Describe();
    }

    public BoardCondition Condition { get; }
    public BoardEffect Effect { get; }
    public string Description { get; }

    public override FactKinds Triggers => FactKinds.TurnEnded;

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        int index = ctx.Self.GlobalIndex;
        int timesFired = ctx.Board.Globals[index].Var(TimesFiredVar);
        if (!Condition.IsMet(ctx.Board, ctx.Board.Ply + 1, timesFired)) return;

        Effect.Apply(ctx, timesFired);
        ctx.Propose(Proposal.AddVar(VarTarget.OfGlobal(index), TimesFiredVar, 1, "board-effect-fired"));
    }
}
