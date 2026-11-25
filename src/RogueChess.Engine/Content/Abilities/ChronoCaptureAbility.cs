using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content.Abilities;

/// <summary>
/// ChronoCapture: the owner gains +5 s on the clock for every enemy piece this piece kills.
///
/// It keys off the engine's kill credit (a <see cref="FactKinds.Killed"/> fact whose source is this piece), never
/// off ability ids or causes, so every way of killing counts: a capture, a ranged attack, a cast, or this piece's own
/// death effect (e.g. an Exploding ChronoCapture piece's blast). Kill credit reaches a killer that is no longer on the
/// board, so kills made as it dies still count. This fixes the prototype's B3, where ranged kills never triggered it.
/// Several kills in one wave each add 5 s. Killing your own pieces earns nothing.
/// </summary>
public sealed class ChronoCaptureAbility : AbilityDef
{
    public const int BonusMs = 5000;
    public const string Cause = "chrono-capture";

    public static readonly ChronoCaptureAbility Instance = new();

    private ChronoCaptureAbility() : base("chrono-capture", "ChronoCapture", value: 1) { }

    public override FactKinds Triggers => FactKinds.Killed;

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        var self = ctx.SelfPiece;
        if (self == null || fact.Source.Piece != self) return;
        if (fact.Piece == null || fact.Piece.Side == self.Side) return;
        ctx.Propose(Proposal.Clock(self.Side, BonusMs, Cause));
    }
}
