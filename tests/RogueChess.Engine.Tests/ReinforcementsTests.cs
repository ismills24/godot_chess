using System;
using RogueChess.Engine.Content;
using RogueChess.Engine.Content.Abilities;
using Xunit;

namespace RogueChess.Engine.Tests;

/// <summary>Handmade rosters and the Reinforcements board effect that spawns from them.</summary>
public class ReinforcementsTests
{
    // ------------------------------------------------------------------ Rosters

    [Fact]
    public void Soldier_roster_ships_with_the_content_library()
    {
        var r = ContentLibrary.CreateCatalog().FindRoster("SOLDIER");
        Assert.NotNull(r);
        Assert.Equal("Soldier", r!.Name);
        Assert.Equal(new[] { StandardPieces.Pawn, StandardPieces.Knight, StandardPieces.Bishop }, r.Pieces);
        Assert.Equal(new AbilityDef[] { MarksmanAbility.Instance, PiercingAbility.Instance }, r.Abilities);
    }

    [Fact]
    public void Roster_resolves_ids_case_insensitively_and_defaults_its_name()
    {
        var r = RosterJson.Parse("""{ "id": "scout", "pieces": ["knight"] }""", ContentLibrary.CreateCatalog());
        Assert.Equal("scout", r.Name);
        Assert.Equal(new[] { StandardPieces.Knight }, r.Pieces);
        Assert.Empty(r.Abilities);
    }

    [Theory]
    [InlineData("""{ "pieces": ["Knight"] }""")]
    [InlineData("""{ "id": "x" }""")]
    [InlineData("""{ "id": "x", "pieces": [] }""")]
    [InlineData("""{ "id": "x", "pieces": ["Dragon"] }""")]
    [InlineData("""{ "id": "x", "pieces": ["Knight"], "abilities": ["Laser"] }""")]
    [InlineData("""{ "id": "x", "pieces": ["Knight"], "colour": "red" }""")]
    [InlineData("""not json""")]
    public void Bad_rosters_throw_format_exceptions(string json)
        => Assert.Throws<FormatException>(() => RosterJson.Parse(json, ContentLibrary.CreateCatalog()));
}
