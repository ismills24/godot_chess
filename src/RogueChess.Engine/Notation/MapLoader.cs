using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using RogueChess.Engine.Content;
using RogueChess.Engine.Core;
using RogueChess.Engine.Rules;

namespace RogueChess.Engine.Notation;

/// <summary>Problems found while loading a map. Every problem is reported, not just the first.</summary>
public sealed class MapLoadException : Exception
{
    public MapLoadException(IReadOnlyList<string> problems, IReadOnlyCollection<string> unknownIds)
        : base("Map could not be loaded:\n  " + string.Join("\n  ", problems))
    {
        Problems = problems;
        UnknownIds = unknownIds;
    }

    public IReadOnlyList<string> Problems { get; }

    /// <summary>Content ids the catalog doesn't know (pieces, tiles, abilities, board effects).</summary>
    public IReadOnlyCollection<string> UnknownIds { get; }
}

/// <summary>
/// Loads the prototype's map format (assets/maps/*.json):
/// <c>{ width, height, startingPlayer, pieces: [{type, color, x, y, decorators[]}], tiles: [{type, x, y}], boardEffects: [...] }</c>,
/// plus optional extensions: <c>"rules"</c> ("rogue" or "standard"), and <c>"name"</c> / <c>"description"</c>, which are ignored here
/// and used by the sandbox.
/// </summary>
public static class MapLoader
{
    public static GameState Load(string json, Catalog catalog, RuleSet? rules = null)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var problems = new List<string>();
        var unknown = new SortedSet<string>(StringComparer.Ordinal);

        int width = root.GetProperty("width").GetInt32();
        int height = root.GetProperty("height").GetInt32();
        var s = new GameState(width, height, rules ?? ParseRules(root, problems));

        if (root.TryGetProperty("tiles", out var tiles))
        {
            foreach (var t in tiles.EnumerateArray())
            {
                string type = t.GetProperty("type").GetString() ?? "";
                int x = t.GetProperty("x").GetInt32(), y = t.GetProperty("y").GetInt32();
                var def = catalog.FindTile(type);
                if (def == null) { Unknown("tile", type); continue; }
                if (!s.InBounds(x, y)) { problems.Add($"tile {type} at ({x},{y}) is off the board"); continue; }
                s.SetTile(s.Sq(x, y), def);
            }
        }

        if (root.TryGetProperty("pieces", out var pieces))
        {
            foreach (var p in pieces.EnumerateArray())
            {
                string type = p.GetProperty("type").GetString() ?? "";
                int x = p.GetProperty("x").GetInt32(), y = p.GetProperty("y").GetInt32();
                var side = ParseSide(p.GetProperty("color").GetString(), problems);
                var def = catalog.FindPiece(type);
                if (def == null) { Unknown("piece", type); continue; }
                if (!s.InBounds(x, y)) { problems.Add($"{type} at ({x},{y}) is off the board"); continue; }
                if (s.PieceAt(x, y) != null) { problems.Add($"two pieces on ({x},{y})"); continue; }
                var piece = s.Place(def, side, s.Sq(x, y));

                if (p.TryGetProperty("decorators", out var decorators))
                {
                    foreach (var d in decorators.EnumerateArray())
                    {
                        string id = d.GetString() ?? "";
                        var ability = catalog.FindAbility(id);
                        if (ability == null) { Unknown("ability", id); continue; }
                        if (!piece.HasAbility(ability)) s.GrantAbility(piece, ability);
                    }
                }
            }
        }

        if (root.TryGetProperty("boardEffects", out var effects))
        {
            foreach (var e in effects.EnumerateArray())
            {
                string type = e.TryGetProperty("effect", out var effect) && effect.TryGetProperty("type", out var et)
                    ? et.GetString() ?? ""
                    : "";
                var factory = catalog.FindBoardEffect(type);
                if (factory == null) { Unknown("board effect", type); continue; }
                var (def, vars) = factory(e);
                s.AddGlobal(def, vars);
            }
        }

        if (root.TryGetProperty("startingPlayer", out var sp))
            s.SetSideToMove(ParseSide(sp.GetString(), problems));

        if (problems.Count > 0) throw new MapLoadException(problems, unknown);
        s.ClearHistory();
        return s;

        void Unknown(string kind, string id)
        {
            problems.Add($"unknown {kind} '{id}'");
            unknown.Add(id);
        }
    }

    /// <summary>Optional <c>"rules": "rogue" | "standard"</c>; the prototype's maps have none and use rogue rules.</summary>
    private static RuleSet ParseRules(JsonElement root, List<string> problems)
    {
        if (!root.TryGetProperty("rules", out var r)) return RuleSet.Rogue;
        switch (r.GetString())
        {
            case "rogue": return RuleSet.Rogue;
            case "standard": return RuleSet.Standard;
            default:
                problems.Add($"unknown rules '{r.GetString()}'");
                return RuleSet.Rogue;
        }
    }

    private static Side ParseSide(string? color, List<string> problems)
    {
        switch (color)
        {
            case "White": return Side.White;
            case "Black": return Side.Black;
            default:
                problems.Add($"unknown color '{color}'");
                return Side.White;
        }
    }
}
