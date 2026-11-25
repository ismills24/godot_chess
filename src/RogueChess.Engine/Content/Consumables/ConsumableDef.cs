using System.Collections.Generic;
using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content;

/// <summary>
/// A single-use item (the prototype's <c>ConsumableDefinition</c>). One global per consumable kind holds both
/// sides' charges as variables (<see cref="ChargesVar"/>), so the AI search sees them, the hash covers them and
/// undo restores them. Using one is an action (<see cref="Move.UseConsumable"/>) that the move generator offers
/// for every useful target; it is a free action, as in the prototype: the same side acts again afterwards.
///
/// Give a side charges during setup with <see cref="Give"/>. The def itself is stateless.
/// </summary>
public abstract class ConsumableDef : GlobalDef
{
    /// <summary>Cause of the proposal that spends a charge.</summary>
    public const string UsedCause = "consumable-used";

    protected ConsumableDef(string id, string name, string description, bool requiresTarget, int value)
        : base(id, name, varCount: 2)
    {
        Description = description;
        RequiresTarget = requiresTarget;
        Value = value;
    }

    public string Description { get; }

    /// <summary>True if <see cref="Use"/> needs a target square.</summary>
    public bool RequiresTarget { get; }

    /// <summary>Material value of one unused charge for the AI (pawn = 1), so it isn't spent for nothing.</summary>
    public int Value { get; }

    /// <summary>Variable slot holding <paramref name="side"/>'s charges.</summary>
    public static int ChargesVar(Side side) => (int)side;

    /// <summary>Charges of this consumable <paramref name="side"/> holds in <paramref name="board"/> (0 if it isn't in play).</summary>
    public int Charges(IBoardView board, Side side)
    {
        foreach (var g in board.Globals)
            if (g.Def == this) return g.Var(ChargesVar(side));
        return 0;
    }

    /// <summary>Setup: add this consumable to the game with the given charges per side (once per kind).</summary>
    public GlobalInstance Give(GameState state, int whiteCharges, int blackCharges)
    {
        foreach (var g in state.Globals)
            if (g.Def == this) throw new System.InvalidOperationException($"{Id} is already in play; give all charges at once.");
        return state.AddGlobal(this, whiteCharges, blackCharges);
    }

    /// <summary>
    /// Targets worth offering to <paramref name="owner"/> (and the AI): -1 for an untargeted use. Skip targets that
    /// would do nothing, to keep the search small. <see cref="Use"/> still accepts any valid target.
    /// </summary>
    public virtual void GenerateTargets(IBoardView board, Side owner, List<int> targets)
    {
        if (!RequiresTarget) targets.Add(-1);
    }

    /// <summary>
    /// Propose the consumable's effect into <paramref name="into"/> (the engine adds the spent charge). Return false if
    /// the use is invalid (the prototype's <c>null</c>): nothing happens and no charge is spent.
    /// </summary>
    public abstract bool Use(IBoardView board, Side owner, int target, List<Proposal> into);
}
