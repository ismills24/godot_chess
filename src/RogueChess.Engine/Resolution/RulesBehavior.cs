using RogueChess.Engine.Content;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.Resolution;

/// <summary>
/// Built-in rule triggers (source category Rules). Promotion: a pawn-tagged piece that ends a move on
/// its far rank becomes the promotion option the player picked (or the first option for moves the
/// player didn't make directly, e.g. a slide or a push).
/// </summary>
internal sealed class RulesBehavior : Behavior
{
    public static readonly RulesBehavior Instance = new();

    public override FactKinds Triggers => FactKinds.Moved;

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        var p = fact.Piece;
        if (p == null || !p.Def.IsPawn || p.Square != fact.To) return;

        var board = ctx.Board;
        var options = board.Rules.PromotionOptions;
        if (options.Count == 0) return;

        int lastRank = p.Side == Side.White ? board.Height - 1 : 0;
        if (board.RankOf(fact.To) != lastRank) return;

        int choice = ctx.Action.PieceId == p.Id && ctx.Action.PromotionIndex >= 0 ? ctx.Action.PromotionIndex : 0;
        ctx.Propose(Proposal.ChangeDef(p, options[choice], Causes.Promotion));
    }

    public override string ToString() => "rules";
}
