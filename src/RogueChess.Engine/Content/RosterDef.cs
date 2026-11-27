using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RogueChess.Engine.Content;

/// <summary>
/// A character's roster: the pieces and abilities it can ever have. Rosters are handmade JSON files in
/// <c>rosters/</c> (see docs/CONTENT_GUIDE.md); effects such as Reinforcements only draw from them.
/// </summary>
public sealed record RosterDef(string Id, string Name, IReadOnlyList<PieceDef> Pieces, IReadOnlyList<AbilityDef> Abilities);

/// <summary>
/// Reads roster files: <c>{ "id": "soldier", "name": "Soldier", "pieces": ["Pawn", ...], "abilities": ["Piercing", ...] }</c>.
/// Ids resolve through the catalog (aliases and any case work). Malformed rosters throw <see cref="FormatException"/>.
/// </summary>
public static class RosterJson
{
    private const string ResourcePrefix = "rosters/";

    private static readonly JsonDocumentOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly HashSet<string> Fields = new(StringComparer.Ordinal) { "id", "name", "pieces", "abilities" };

    public static RosterDef Parse(string json, Catalog catalog)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json, Options); }
        catch (JsonException e) { throw new FormatException($"invalid roster: {e.Message}", e); }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new FormatException("a roster must be a JSON object");

            string id = root.TryGetProperty("id", out var idValue) && idValue.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(idValue.GetString())
                ? idValue.GetString()!
                : throw new FormatException("a roster needs an id");
            string where = $"roster '{id}'";

            foreach (var property in root.EnumerateObject())
                if (!Fields.Contains(property.Name)) throw new FormatException($"{where}: unknown field '{property.Name}'");

            string name = root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()! : id;

            var pieces = Ids(root, "pieces", where)
                .Select(p => catalog.FindPiece(p) ?? throw new FormatException($"{where}: unknown piece '{p}'"))
                .Distinct().ToList();
            if (pieces.Count == 0) throw new FormatException($"{where} needs a non-empty list of pieces");

            var abilities = Ids(root, "abilities", where)
                .Select(a => catalog.FindAbility(a) ?? throw new FormatException($"{where}: unknown ability '{a}'"))
                .Distinct().ToList();

            return new RosterDef(id, name, pieces, abilities);
        }
    }

    /// <summary>Register every roster file embedded in the engine (the repo's <c>rosters/*.json</c>), in name order.</summary>
    public static Catalog AddEmbeddedRosters(this Catalog catalog)
    {
        var assembly = typeof(RosterJson).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames()
                     .Where(r => r.StartsWith(ResourcePrefix, StringComparison.Ordinal) && r.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(r => r, StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            catalog.AddRoster(Parse(reader.ReadToEnd(), catalog));
        }
        return catalog;
    }

    private static List<string> Ids(JsonElement root, string field, string where)
    {
        if (!root.TryGetProperty(field, out var list) || list.ValueKind == JsonValueKind.Null) return new List<string>();
        if (list.ValueKind != JsonValueKind.Array) throw new FormatException($"{where}: '{field}' must be a list of ids");
        return list.EnumerateArray()
            .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()! : throw new FormatException($"{where}: '{field}' must be a list of ids"))
            .ToList();
    }
}
