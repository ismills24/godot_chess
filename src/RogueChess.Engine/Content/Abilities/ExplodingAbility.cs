using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content.Abilities;

/// <summary>
/// Exploding: when this piece dies (any cause), every piece within <see cref="Radius"/> of the square it died
/// on is destroyed, friend or foe, including a capturer that has just moved onto that square.
///
/// The blast happens in the wave after the death, so a chain of exploding pieces spreads outward one ring
/// per wave.
/// </summary>
public sealed class ExplodingAbility : AbilityDef
{
    public static readonly ExplodingAbility Instance = new();

    private ExplodingAbility() : base("exploding", "Exploding", value: 2) { }

    public int Radius => 1;

    public override FactKinds Triggers => FactKinds.Died;

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        if (fact.Piece != ctx.SelfPiece) return;

        var board = ctx.Board;
        int cx = board.FileOf(fact.From), cy = board.RankOf(fact.From);
        for (int dy = -Radius; dy <= Radius; dy++)
            for (int dx = -Radius; dx <= Radius; dx++)
                if (board.PieceAt(cx + dx, cy + dy) is { } victim)
                    ctx.Propose(Proposal.Destroy(victim, Causes.Explosion));
    }
}
