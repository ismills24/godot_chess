using System;
using System.Collections.Generic;
using System.Text.Json;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.Content.BoardEffects;

/// <summary>
/// Reads the prototype's board-effect entries:
/// <c>{ "id", "name", "description", "priority", "condition": { "type", "params" }, "effect": { "type", "params" } }</c>.
/// Parameter names and defaults match the prototype's BoardEffectFactory. "id", "priority" and the effects'
/// "sourceId" are accepted and ignored (ordering and sources come from the resolver).
/// Malformed entries throw <see cref="FormatException"/>, which the map loader reports as a problem.
/// </summary>
public static class BoardEffectJson
{
    /// <summary>Register the effect types (the catalog looks entries up by their effect type).</summary>
    public static Catalog AddBoardEffects(this Catalog catalog) => catalog
        .AddBoardEffect("WallsPlacement", Create)
        .AddBoardEffect("WindGust", Create)
        .AddBoardEffect("CheckerboardWipe", Create)
        .AddBoardEffect("TidalWave", Create)
        .AddBoardEffect("Reinforcements", entry => Create(entry, catalog));

    public static (GlobalDef Def, int[] Vars) Create(JsonElement entry) => Create(entry, null);

    /// <param name="catalog">Needed by effects that name content (Reinforcements looks up its roster); may be null otherwise.</param>
    public static (GlobalDef Def, int[] Vars) Create(JsonElement entry, Catalog? catalog)
    {
        if (!entry.TryGetProperty("condition", out var condition)) throw new FormatException("board effect has no condition");
        if (!entry.TryGetProperty("effect", out var effect)) throw new FormatException("board effect has no effect");

        var def = new BoardEffectDef(ParseCondition(condition), ParseEffect(effect, catalog),
            OptionalString(entry, "name"), OptionalString(entry, "description"));
        return (def, Array.Empty<int>());
    }

    public static BoardCondition ParseCondition(JsonElement condition)
    {
        var p = Params(condition);
        switch (Type(condition))
        {
            case "TurnInterval":
                int interval = Int(p, "interval", 1);
                if (interval < 1) throw new FormatException("TurnInterval needs an interval of at least 1");
                return new TurnIntervalCondition(interval, p is { } q && q.TryGetProperty("startTurn", out var st) ? st.GetInt32() : null);
            case "TurnDelay":
                return new TurnDelayCondition(Int(p, "turnNumber", 1));
            case "BoardValue":
                return new BoardValueCondition(SideParam(p, "color"), Int(p, "threshold", 10), Int(p, "maxTriggers", -1));
            case var other:
                throw new FormatException($"unknown board-effect condition '{other}'");
        }
    }

    public static BoardEffect ParseEffect(JsonElement effect) => ParseEffect(effect, null);

    public static BoardEffect ParseEffect(JsonElement effect, Catalog? catalog)
    {
        var p = Params(effect);
        switch (Type(effect))
        {
            case "WallsPlacement":
                return new WallsPlacementEffect(Int(p, "wallCount", 1));
            case "WindGust":
                return new WindGustEffect(SideParam(p, "targetPlayer"), Int(p, "freezeDuration", 2));
            case "CheckerboardWipe":
                // Prototype: missing means "white"; any value other than "white" (any case) behaves as black.
                var colorText = String(p, "targetColor");
                var color = colorText == null || string.Equals(colorText, "white", StringComparison.OrdinalIgnoreCase)
                    ? SquareColor.White
                    : SquareColor.Black;
                return new CheckerboardWipeEffect(color, Int(p, "freezeDuration", 2), Bool(p, "alternating", false));
            case "TidalWave":
                return ParseTidalWave(p);
            case "Reinforcements":
                return ParseReinforcements(p, catalog ?? throw new FormatException("Reinforcements needs a catalog"));
            case var other:
                throw new FormatException($"unknown board effect '{other}'");
        }
    }

    /// <summary>
    /// <c>{ "direction": "north|south|east|west", "squares": ["a6", ...], "strength": 1 }</c>. Square names aren't
    /// bounds-checked here (the board isn't known yet); squares off the board are ignored when the wave fires.
    /// </summary>
    private static TidalWaveEffect ParseTidalWave(JsonElement? p)
    {
        if (!Enum.TryParse<WaveDirection>(String(p, "direction"), ignoreCase: true, out var direction)
            || !Enum.IsDefined(direction) || int.TryParse(String(p, "direction"), out _))
            throw new FormatException("TidalWave needs a direction of north, south, east or west");

        var squares = ParseSquares(p, "TidalWave");
        int strength = Int(p, "strength", 1);
        if (strength < 1 || strength > TidalWaveEffect.MaxStrength)
            throw new FormatException($"TidalWave needs a strength from 1 to {TidalWaveEffect.MaxStrength}");
        return new TidalWaveEffect(direction, squares, strength);
    }

    /// <summary>
    /// <c>{ "side": "white|black", "roster": "soldier", "pieceValue": 6, "abilityValue": 3, "squares": ["a7", ...] }</c>.
    /// <c>side</c> defaults to White and <c>abilityValue</c> to 0; both budgets are at most
    /// <see cref="ReinforcementsEffect.MaxValue"/>; the roster must be in the catalog.
    /// </summary>
    private static ReinforcementsEffect ParseReinforcements(JsonElement? p, Catalog catalog)
    {
        string rosterId = String(p, "roster") ?? throw new FormatException("Reinforcements needs a roster");
        var roster = catalog.FindRoster(rosterId) ?? throw new FormatException($"Reinforcements: unknown roster '{rosterId}'");
        if (p is not { } o || !o.TryGetProperty("pieceValue", out var pv) || pv.ValueKind != JsonValueKind.Number)
            throw new FormatException("Reinforcements needs a pieceValue");
        int pieceValue = pv.GetInt32();
        if (pieceValue < 1 || pieceValue > ReinforcementsEffect.MaxValue)
            throw new FormatException($"Reinforcements needs a pieceValue from 1 to {ReinforcementsEffect.MaxValue}");
        int abilityValue = Int(p, "abilityValue", 0);
        if (abilityValue < 0 || abilityValue > ReinforcementsEffect.MaxValue)
            throw new FormatException($"Reinforcements needs an abilityValue from 0 to {ReinforcementsEffect.MaxValue}");
        // Strict, unlike the prototype's lenient sides: a typo here would hand the enemy's reinforcements to the player.
        var sideText = String(p, "side");
        if (sideText != null && !sideText.Equals("white", StringComparison.OrdinalIgnoreCase) && !sideText.Equals("black", StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"Reinforcements: side must be white or black, not '{sideText}'");
        return new ReinforcementsEffect(SideParam(p, "side"), roster, pieceValue, abilityValue, ParseSquares(p, "Reinforcements"), catalog);
    }

    /// <summary>A non-empty <c>"squares"</c> list of names like "a6". Not bounds-checked (the board isn't known yet).</summary>
    private static List<(int X, int Y)> ParseSquares(JsonElement? p, string effect)
    {
        if (p is not { } o || !o.TryGetProperty("squares", out var list) || list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
            throw new FormatException($"{effect} needs a non-empty list of squares");
        var squares = new List<(int X, int Y)>();
        foreach (var item in list.EnumerateArray())
        {
            string name = item.ValueKind == JsonValueKind.String ? item.GetString()! : item.ToString();
            if (name.Length < 2 || name[0] < 'a' || name[0] > 'z' || !int.TryParse(name.AsSpan(1), out int rank) || rank < 1)
                throw new FormatException($"{effect}: bad square name '{name}'");
            squares.Add((name[0] - 'a', rank - 1));
        }
        return squares;
    }

    private static string Type(JsonElement e)
        => e.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()! : "";

    private static JsonElement? Params(JsonElement e)
        => e.TryGetProperty("params", out var p) && p.ValueKind == JsonValueKind.Object ? p : null;

    private static int Int(JsonElement? p, string name, int fallback)
        => p is { } o && o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : fallback;

    private static bool Bool(JsonElement? p, string name, bool fallback)
        => p is { } o && o.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : fallback;

    private static string? String(JsonElement? p, string name)
        => p is { } o && o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>"White" / "Black" in any case; anything else (or missing) means White, as in the prototype.</summary>
    private static Side SideParam(JsonElement? p, string name)
        => string.Equals(String(p, name), "black", StringComparison.OrdinalIgnoreCase) ? Side.Black : Side.White;

    private static string? OptionalString(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
