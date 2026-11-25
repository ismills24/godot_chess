using System;
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
    /// <summary>Register the three effect types (the catalog looks entries up by their effect type).</summary>
    public static Catalog AddBoardEffects(this Catalog catalog) => catalog
        .AddBoardEffect("WallsPlacement", Create)
        .AddBoardEffect("WindGust", Create)
        .AddBoardEffect("CheckerboardWipe", Create);

    public static (GlobalDef Def, int[] Vars) Create(JsonElement entry)
    {
        if (!entry.TryGetProperty("condition", out var condition)) throw new FormatException("board effect has no condition");
        if (!entry.TryGetProperty("effect", out var effect)) throw new FormatException("board effect has no effect");

        var def = new BoardEffectDef(ParseCondition(condition), ParseEffect(effect),
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

    public static BoardEffect ParseEffect(JsonElement effect)
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
            case var other:
                throw new FormatException($"unknown board effect '{other}'");
        }
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
