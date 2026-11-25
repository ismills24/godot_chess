using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content.Abilities;

/// <summary>
/// OnFire (status): the piece burns to death. Variable 0 counts its owner's turns left; at the end of each of
/// its owner's turns it drops by one, and when it reaches zero the piece is destroyed (cause
/// <see cref="Causes.Burned"/>, source this ability). The piece moves normally until then.
///
/// A Fire tile sets it to 3. A piece that enters fire during its own turn loses one at that turn's end, so it
/// gets two more moves and burns at the end of the second, as in the prototype. The burn is an ordinary
/// destroy: a Scapegoat can take it, a Guardian tile (captures only) can't stop it.
/// </summary>
public sealed class OnFireAbility : AbilityDef
{
    public const int TurnsLeftVar = 0;

    /// <summary>The prototype's default when a map gives a piece the "OnFire" decorator.</summary>
    public const int DefaultTurns = 3;

    public static readonly OnFireAbility Instance = new();

    private OnFireAbility() : base("on-fire", "On Fire", value: -2, varCount: 1) { }

    public override int[] InitialVars() => new[] { DefaultTurns };

    public override FactKinds Triggers => FactKinds.TurnEnded | FactKinds.VarChanged;

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        var self = ctx.SelfPiece!;
        if (!self.IsOnBoard) return;
        var turnsLeft = VarTarget.OfAbility(self, this);

        if (fact.Kind == FactKinds.TurnEnded && fact.Side == self.Side)
            ctx.Propose(Proposal.AddVar(turnsLeft, TurnsLeftVar, -1, "fire-tick"));
        else if (fact.Kind == FactKinds.VarChanged && fact.Var == turnsLeft && fact.NewValue <= 0)
            ctx.Propose(Proposal.Destroy(self, Causes.Burned));
    }
}
