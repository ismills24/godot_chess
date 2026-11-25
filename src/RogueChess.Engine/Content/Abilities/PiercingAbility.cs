using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content.Abilities;

/// <summary>
/// Piercing (redirect layer): when this piece captures as its move, it jumps over the target instead, leaving it
/// untouched, and lands on the square directly behind it, capturing whatever enemy stands there.
///
/// "Behind" is one step past the target along the attack direction (the sign of the attack vector on each axis,
/// as in the prototype, so a knight's jump continues diagonally). The capture happens normally instead when:
/// <list type="bullet">
/// <item>the square behind is off the board, or holds a friendly piece (as in the prototype);</item>
/// <item>the square behind can't be landed on, or its occupant stands in fog and can't be captured (port rules:
/// the jump would be impossible, so the piece captures as usual).</item>
/// </list>
/// The capture on the landing square is an ordinary move capture, so a Guardian tile or a Scapegoat can stop it;
/// the piercing piece is then blocked and stays where it was. Jumping over a target on a Guardian tile does not
/// use the Guardian up, because the target isn't captured. Only normal captures made as the player's action are
/// pierced (not en passant). If a piece has both Piercing and Bouncer, Piercing applies.
/// </summary>
public sealed class PiercingAbility : AbilityDef
{
    public static readonly PiercingAbility Instance = new();

    private PiercingAbility() : base("piercing", "Piercing", value: 3) { }

    public override ModifierLayer Layer => ModifierLayer.Redirect;

    public override void Modify(ModifierContext ctx)
    {
        if (!CaptureRewrite.FindOwnCapture(ctx, out int destroy, out int move, out var target)) return;

        var self = ctx.SelfPiece!;
        var board = ctx.Board;
        int landing = CaptureRewrite.SquareBehind(board, self.Square, target.Square);
        if (landing < 0) return;
        var tile = board.TileAt(landing).Def;
        if (tile.BlocksLanding) return;
        var occupant = board.PieceAt(landing);
        if (occupant != null && (occupant.Side == self.Side || tile.BlocksCapture)) return;

        var jump = Proposal.Move(self, landing, Causes.Action);
        if (occupant == null) ctx.Claim(new[] { destroy, move }, jump);
        else ctx.Claim(new[] { destroy, move }, Proposal.Destroy(occupant, Causes.Capture), jump);
    }
}
