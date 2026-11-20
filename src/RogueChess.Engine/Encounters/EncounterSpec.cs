using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RogueChess.Engine.Encounters;

/// <summary>How to build one side's army (values are in the content's own units: pawn = 1).</summary>
public sealed record ArmySpec
{
    public int PieceValue { get; init; } = 8;
    public int AbilityValue { get; init; }
    public int MinimumPawns { get; init; } = 2;
    public bool HasKing { get; init; } = true;

    /// <summary>Abilities the budget may buy (ids). Null = every ability in the catalog.</summary>
    public IReadOnlyList<string>? Abilities { get; init; }

    /// <summary>If set, the most valuable non-royal piece gets one of these for free (the player's starting perk).</summary>
    public IReadOnlyList<string>? StartingAbilities { get; init; }
}

/// <summary>One battle: board size, enemy army budget and which AI plays it.</summary>
public sealed record EncounterSpec
{
    public string Id { get; init; } = "encounter";
    public string Name { get; init; } = "Encounter";
    public string Description { get; init; } = "";
    public int Width { get; init; } = 6;
    public int Height { get; init; } = 6;
    public ArmySpec Enemy { get; init; } = new();

    /// <summary>The enemy AI: a profile id or an inline profile object, as raw JSON (see docs/AI.md).</summary>
    [JsonConverter(typeof(RawJsonConverter))]
    public string? Ai { get; init; }
}

/// <summary>A set of encounters plus the player's army, loaded from JSON (e.g. encounters/prototype.json).</summary>
public sealed record EncounterSet
{
    public ArmySpec Player { get; init; } = new() { PieceValue = 10 };
    public IReadOnlyList<EncounterSpec> Encounters { get; init; } = Array.Empty<EncounterSpec>();

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static EncounterSet Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<EncounterSet>(json, Json) ?? throw new FormatException("empty encounter set");
        }
        catch (JsonException e)
        {
            throw new FormatException($"invalid encounter set: {e.Message}", e);
        }
    }

    /// <summary>Ability ids named anywhere in the set that the catalog doesn't have (content not ported yet).</summary>
    public IReadOnlyList<string> UnknownAbilities(Content.Catalog catalog) =>
        new[] { Player }.Concat(Encounters.Select(e => e.Enemy))
            .SelectMany(a => (a.Abilities ?? Array.Empty<string>()).Concat(a.StartingAbilities ?? Array.Empty<string>()))
            .Where(id => catalog.FindAbility(id) == null)
            .Distinct()
            .ToList();
}

/// <summary>Keeps a JSON value (string or object) as its raw text.</summary>
internal sealed class RawJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return doc.RootElement.ValueKind == JsonValueKind.Null ? null : doc.RootElement.GetRawText();
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value == null) writer.WriteNullValue();
        else writer.WriteRawValue(value);
    }
}
