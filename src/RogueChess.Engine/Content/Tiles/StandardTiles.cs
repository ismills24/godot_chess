using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content.Tiles;

/// <summary>
/// Wall: sliding pieces stop here. A piece may still move onto the wall, which breaks it (the tile becomes
/// standard in the next wave). Knights leap over it; pawns can't push into it.
/// </summary>
public sealed class WallTile : TileDef
{
    public static readonly WallTile Instance = new();

    private WallTile() : base("wall", "Wall", blocksPassage: true) { }

    public override FactKinds Triggers => FactKinds.Moved;

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        if (fact.To == ctx.SelfSquare)
            ctx.Propose(Proposal.ChangeTile(ctx.SelfSquare, Standard, "wall-broken"));
    }
}

/// <summary>
/// Guardian (prevent layer): the first capture of the piece standing here is cancelled and the tile is used
/// up. The attacker's move is then blocked and it stays put. Only move captures are stopped; explosions and
/// other destruction go through, as in the prototype.
/// </summary>
public sealed class GuardianTile : TileDef
{
    public static readonly GuardianTile Instance = new();

    private GuardianTile() : base("guardian", "Guardian") { }

    public override ModifierLayer Layer => ModifierLayer.Prevent;

    public override void Modify(ModifierContext ctx)
    {
        for (int i = 0; i < ctx.Batch.Count; i++)
        {
            var p = ctx.Batch[i];
            if (p.Kind != ProposalKind.Destroy || !Causes.IsMoveCapture(p.Cause)) continue;
            if (ctx.Board.GetPiece(p.PieceId)?.Square != ctx.SelfSquare) continue;
            ctx.Claim(i, Proposal.ChangeTile(ctx.SelfSquare, Standard, "guardian-consumed"));
            return;
        }
    }
}

/// <summary>
/// Fog: a piece standing in fog can't be captured by a move (no move is even offered). Empty fog can be
/// entered, and explosions still reach pieces in fog.
/// </summary>
public sealed class FogTile : TileDef
{
    public static readonly FogTile Instance = new();

    private FogTile() : base("fog", "Fog", blocksCapture: true) { }
}
