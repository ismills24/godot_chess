using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content.Abilities;

/// <summary>
/// Bouncer (redirect layer): when this piece captures as its move, the target isn't destroyed. It is knocked
/// one square further along the attack direction, and the bouncer moves onto the target's old square. An enemy
/// standing on the square the target is knocked into is destroyed ("bounced into", credited to the bouncer).
///
/// The target always moves exactly <b>one</b> square, the square directly behind it (the sign of the attack
/// vector on each axis). The prototype's doc said so, but its code knocked the target as far again as the
/// attacker had travelled (a rook capturing from 5 away sent it 5 further): bug B5, fixed here.
///
/// The capture happens normally instead when (as in the prototype) the square behind is off the board or holds
/// a piece friendly to the bouncer, or (port rule) that square can't be landed on.
/// <list type="bullet">
/// <item>"Bounced into" is not a move capture: a Guardian tile doesn't stop it, and fog doesn't hide from it.</item>
/// <item>If a Scapegoat dies in place of the piece bounced into, that piece stays, so the target can't move and
/// the bouncer is blocked too: only the Scapegoat dies.</item>
/// <item>The target isn't captured, so a Guardian under it is not used up and an Exploding target doesn't explode.</item>
/// <item>Only normal captures made as the player's action bounce (not en passant). With Piercing too, Piercing applies.</item>
/// </list>
/// </summary>
public sealed class BouncerAbility : AbilityDef
{
    public const string BounceCause = "bounce";
    public const string BouncedIntoCause = "bounced-into";

    public static readonly BouncerAbility Instance = new();

    private BouncerAbility() : base("bouncer", "Bouncer", value: 1) { }

    public override ModifierLayer Layer => ModifierLayer.Redirect;

    public override void Modify(ModifierContext ctx)
    {
        var self = ctx.SelfPiece!;
        // Both abilities would claim the same capture; decide by rule, not by the order of the abilities.
        if (self.HasAbility(PiercingAbility.Instance)) return;
        if (!CaptureRewrite.FindOwnCapture(ctx, out int destroy, out int move, out var target)) return;

        var board = ctx.Board;
        int bounce = CaptureRewrite.SquareBehind(board, self.Square, target.Square);
        if (bounce < 0 || board.TileAt(bounce).Def.BlocksLanding) return;
        var occupant = board.PieceAt(bounce);
        if (occupant != null && occupant.Side == self.Side) return;

        var knock = Proposal.Move(target, bounce, BounceCause);
        var advance = Proposal.Move(self, target.Square, Causes.Action);
        if (occupant == null) ctx.Claim(new[] { destroy, move }, knock, advance);
        else ctx.Claim(new[] { destroy, move }, Proposal.Destroy(occupant, BouncedIntoCause), knock, advance);
    }
}
