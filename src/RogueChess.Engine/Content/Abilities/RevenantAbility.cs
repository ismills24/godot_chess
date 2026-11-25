using RogueChess.Engine.Content.Tiles;
using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content.Abilities;

/// <summary>
/// Revenant: when this piece dies (captured, blown up, sacrificed, anything), the square it died on becomes a
/// <see cref="TombTile"/> holding it. An ally that later moves onto the empty tomb brings it back (see TombTile).
///
/// <para><see cref="RevivesForever"/> (B10): the prototype's tomb restored a piece that still had Revenant, so it
/// came back every time. The default here revives <b>once</b>: the revived piece comes back without Revenant.
/// The <see cref="Forever"/> variant keeps it.</para>
///
/// The tomb replaces whatever tile was on that square, as in the prototype. If something else changes the same
/// tile in the same wave (a tile's own trigger, say), canonical order decides and tiles come before pieces.
/// </summary>
public sealed class RevenantAbility : AbilityDef
{
    public const string TombCause = "revenant-tomb";

    /// <summary>Revives once (the default, decided for B10).</summary>
    public static readonly RevenantAbility Instance = new(revivesForever: false);

    /// <summary>
    /// Keeps Revenant when revived, so it comes back every time (the prototype's behaviour). Not registered in the
    /// catalog, so maps and generated armies use the revive-once version; register it to make it available.
    /// </summary>
    public static readonly RevenantAbility Forever = new(revivesForever: true);

    private RevenantAbility(bool revivesForever)
        : base(revivesForever ? "revenant-forever" : "revenant", revivesForever ? "Revenant (forever)" : "Revenant", value: 3)
        => RevivesForever = revivesForever;

    /// <summary>Whether the revived piece keeps this ability (and so can come back again).</summary>
    public bool RevivesForever { get; }

    public override FactKinds Triggers => FactKinds.Died;

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        var self = ctx.SelfPiece;
        if (fact.Piece != self || self == null) return;
        // The dead piece object is the snapshot: it keeps its def, abilities and variables while off the board.
        ctx.Propose(Proposal.ChangeTile(fact.From, TombTile.Instance, TombCause, TombTile.VarsFor(self)));
    }
}
