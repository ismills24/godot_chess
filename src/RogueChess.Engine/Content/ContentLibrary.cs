using RogueChess.Engine.Content.Abilities;
using RogueChess.Engine.Content.BoardEffects;
using RogueChess.Engine.Content.Consumables;
using RogueChess.Engine.Content.Relics;
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
            .AddAbility(PiercingAbility.Instance, "Piercing")
            .AddAbility(BouncerAbility.Instance, "Bouncer")
            .AddAbility(MarksmanAbility.Instance, "Marksman")
            .AddAbility(RevenantAbility.Instance, "Revenant")
            .AddAbility(CannibalAbility.Instance, "Cannibal")
            .AddAbility(ChronoCaptureAbility.Instance, "ChronoCapture")
            // tiles
            .AddTile(WallTile.Instance, "WallTile")
            .AddTile(GuardianTile.Instance, "GuardianTile")
            .AddTile(FogTile.Instance, "FogTile")
            .AddTile(FireTile.Instance, "FireTile")
            .AddTile(FreezeTile.Instance, "FreezeTile")
            .AddTile(SlipperyTile.Instance, "SlipperyTile")
            .AddTile(TombTile.Instance, "TombTile")
            // relics (built per owner) and consumables, under the prototype's ids
            .AddRelic("time-bank", TimeBankRelic.For, "TimeBankRelic")
            .AddRelic("royal-promotion", RoyalPromotionRelic.For, "RoyalPromotionRelic")
            .AddConsumable(ExtraTurnPotion.Instance, "ExtraTurnPotion")
            .AddConsumable(FortifyPotion.Instance, "FortifyPotion")
            // board effects (WallsPlacement, WindGust, CheckerboardWipe) and their conditions
            .AddBoardEffects();
    }
}
