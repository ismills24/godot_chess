using System;
using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content.Abilities;

/// <summary>
/// Scapegoat (prevent layer): if an adjacent ally would be destroyed (captured, blown up, anything), this
/// piece dies in its place.
///
/// Rules that follow from the resolution contract:
/// <list type="bullet">
/// <item>One Scapegoat can save several adjacent allies in the same wave; it still only dies once.</item>
/// <item>A Scapegoat that is itself being destroyed this wave still protects its neighbours (it reads the snapshot).</item>
/// <item>If two Scapegoats could save the same ally, the one first in canonical order does (closest, then clockwise from north).</item>
/// <item>When it saves an ally from a capture, the attacker's move is blocked and the attacker stays where it was.</item>
/// </list>
/// </summary>
public sealed class ScapegoatAbility : AbilityDef
{
    public static readonly ScapegoatAbility Instance = new();

    private ScapegoatAbility() : base("scapegoat", "Scapegoat", value: 1) { }

    public override ModifierLayer Layer => ModifierLayer.Prevent;

    public override void Modify(ModifierContext ctx)
    {
        var self = ctx.SelfPiece!;
        var board = ctx.Board;
        for (int i = 0; i < ctx.Batch.Count; i++)
        {
            var p = ctx.Batch[i];
            if (p.Kind != ProposalKind.Destroy) continue;
            var target = board.GetPiece(p.PieceId);
            if (target == null || target == self || target.Side != self.Side || !target.IsOnBoard) continue;

            int dx = Math.Abs(board.FileOf(target.Square) - board.FileOf(ctx.SelfSquare));
            int dy = Math.Abs(board.RankOf(target.Square) - board.RankOf(ctx.SelfSquare));
            if (Math.Max(dx, dy) == 1)
                ctx.Claim(i, Proposal.Destroy(self, Causes.Scapegoat));
        }
    }
}
