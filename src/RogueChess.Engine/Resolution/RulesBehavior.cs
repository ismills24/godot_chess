using RogueChess.Engine.Content;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.Resolution;

/// <summary>
/// Built-in rule triggers (source category Rules). Promotion: a pawn-tagged piece that ends a move on
/// its far rank becomes the promotion option the player picked (or the first option for moves the
/// player didn't make directly, e.g. a slide or a push).
///
/// Tile lifetimes: at the end of every turn (both sides' turns count) each timed tile (<see cref="Tile.IsTimed"/>)
/// counts down by one, and one with a single turn left turns back into floor (<see cref="Causes.TileExpired"/>).
/// A tile placed during a turn end isn't counted down in that same turn end (it wasn't there when the turn ended),
/// so a tile placed for N turns stays for the next N turns.
/// </summary>
internal sealed class RulesBehavior : Behavior
{
    public static readonly RulesBehavior Instance = new();

    public override FactKinds Triggers => FactKinds.Moved | FactKinds.TurnEnded;

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        if (fact.Kind == FactKinds.TurnEnded)
        {
            TickTileLifetimes(ctx);
            return;
        }

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

    private static void TickTileLifetimes(TriggerContext ctx)
    {
        var board = ctx.Board;
        for (int sq = 0; sq < board.Area; sq++)
        {
            var tile = board.TileAt(sq);
            if (!tile.IsTimed) continue;
            if (tile.TurnsLeft <= 1) ctx.Propose(Proposal.ChangeTile(sq, TileDef.Standard, Causes.TileExpired));
            else ctx.Propose(Proposal.AddVar(VarTarget.OfTile(sq), tile.LifetimeSlot, -1, Causes.TileLifetimeTick));
        }
    }

    public override string ToString() => "rules";
}
