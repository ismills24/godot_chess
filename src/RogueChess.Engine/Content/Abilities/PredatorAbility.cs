using System;
using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content.Abilities;

/// <summary>
/// Predator: each kill credited to this piece evolves it one step along Pawn → Knight → Bishop → Rook → Queen.
/// Any kill counts (a capture, or anything else whose destroy names this piece as the source); several kills
/// in one wave evolve it several steps. Kills are counted in variable 0, and the evolution happens in the
/// wave after the kill. A predator that dies in the same wave doesn't evolve. Pieces not on the chain
/// (e.g. a king) never evolve.
/// </summary>
public sealed class PredatorAbility : AbilityDef
{
    public const int KillsVar = 0;

    public static readonly PredatorAbility Instance = new();

    private static readonly PieceDef[] Chain =
    {
        StandardPieces.Pawn, StandardPieces.Knight, StandardPieces.Bishop, StandardPieces.Rook, StandardPieces.Queen,
    };

    private PredatorAbility() : base("predator", "Predator", value: 3, varCount: 1) { }

    public override FactKinds Triggers => FactKinds.Killed | FactKinds.VarChanged;

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        var self = ctx.SelfPiece!;
        var kills = VarTarget.OfAbility(self, this);

        if (fact.Kind == FactKinds.Killed && fact.Source.Piece == self)
        {
            ctx.Propose(Proposal.AddVar(kills, KillsVar, 1, "predator-kill"));
        }
        else if (fact.Kind == FactKinds.VarChanged && fact.Var == kills && fact.NewValue > fact.OldValue)
        {
            int index = Array.IndexOf(Chain, self.Def);
            if (index < 0 || index == Chain.Length - 1) return;
            int next = Math.Min(Chain.Length - 1, index + (fact.NewValue - fact.OldValue));
            ctx.Propose(Proposal.ChangeDef(self, Chain[next], "predator-evolve"));
        }
    }
}
