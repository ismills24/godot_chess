using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content.Abilities;

/// <summary>
/// Frozen (status): the piece can't move or capture while variable 0 (turns left) is above zero. At the end of
/// each of its owner's turns the counter drops by one; when it reaches zero the status is removed.
///
/// So a piece frozen for 2 sits out its owner's next 2 turns. Freezing an already frozen piece keeps the longer
/// duration (the resolver merges a re-added ability by keeping each variable's larger value).
/// Board effects (WindGust, CheckerboardWipe) apply it with <c>Proposal.AddAbility(piece, Instance, cause, turns)</c>.
/// </summary>
public sealed class FrozenAbility : AbilityDef
{
    public const int TurnsLeftVar = 0;

    /// <summary>The prototype's default when a map gives a piece the "Frozen" decorator.</summary>
    public const int DefaultTurns = 3;

    public static readonly FrozenAbility Instance = new();

    private FrozenAbility() : base("frozen", "Frozen", value: -2, varCount: 1) { }

    public override int[] InitialVars() => new[] { DefaultTurns };

    public override bool Immobilizes(AbilityInstance self) => self.Var(TurnsLeftVar) > 0;

    public override FactKinds Triggers => FactKinds.TurnEnded | FactKinds.VarChanged;

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        var self = ctx.SelfPiece!;
        if (!self.IsOnBoard) return;
        var turnsLeft = VarTarget.OfAbility(self, this);

        if (fact.Kind == FactKinds.TurnEnded && fact.Side == self.Side)
            ctx.Propose(Proposal.AddVar(turnsLeft, TurnsLeftVar, -1, "frozen-tick"));
        else if (fact.Kind == FactKinds.VarChanged && fact.Var == turnsLeft && fact.NewValue <= 0)
            ctx.Propose(Proposal.RemoveAbility(self, this, "frozen-thaw"));
    }
}
