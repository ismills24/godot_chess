using RogueChess.Engine.Content.Abilities;
using RogueChess.Engine.Content.BoardEffects;
using RogueChess.Engine.Content.Tiles;

namespace RogueChess.Engine.Content;

/// <summary>
/// All ported content, registered under its own id and the prototype's map names. Adding a new piece,
/// ability or tile means writing its class and adding one line here (see docs/CONTENT_GUIDE.md).
/// </summary>
public static class ContentLibrary
{
    public static Catalog CreateCatalog()
    {
        return Catalog.CreateStandard()
            // abilities
            .AddAbility(ExplodingAbility.Instance, "Exploding")
            .AddAbility(ScapegoatAbility.Instance, "Scapegoat")
            .AddAbility(PhantomAbility.Instance, "Phantom")
            .AddAbility(PredatorAbility.Instance, "Predator")
            .AddAbility(FrozenAbility.Instance, "Frozen")
            .AddAbility(OnFireAbility.Instance, "OnFire")
            .AddAbility(CasterAbility.FireMage, "FireMage")
            .AddAbility(CasterAbility.FreezeMage, "FreezeMage")
            .AddAbility(CasterAbility.WallPlacement, "WallPlacement")
            // tiles
            .AddTile(WallTile.Instance, "WallTile")
            .AddTile(GuardianTile.Instance, "GuardianTile")
            .AddTile(FogTile.Instance, "FogTile")
            .AddTile(FireTile.Instance, "FireTile")
            .AddTile(FreezeTile.Instance, "FreezeTile")
            // board effects (WallsPlacement, WindGust, CheckerboardWipe) and their conditions
            .AddBoardEffects();
    }
}
