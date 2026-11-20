using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace RogueChess.Engine.AI;

/// <summary>
/// Builds agents from profiles: the injection point for levels. A level names a profile (<c>"ai": "aggressive"</c>)
/// or describes one inline (<c>"ai": { "extends": "club", "maxDepth": 2 }</c>); the game asks the factory for the
/// agent. Three things can be swapped independently:
/// <list type="bullet">
/// <item><b>Kind</b>: the agent implementation (<see cref="RegisterKind"/>). Built in: "search", "random".</item>
/// <item><b>Evaluator</b>: how positions are scored (<see cref="RegisterEvaluator"/>). Built in: "weighted".</item>
/// <item><b>Profile</b>: the parameters (<see cref="RegisterProfile"/>, <see cref="LoadProfiles"/>).</item>
/// </list>
/// </summary>
public sealed class AgentFactory
{
    public const string SearchKind = "search";
    public const string RandomKind = "random";
    public const string WeightedEvaluator = "weighted";

    private readonly Dictionary<string, Func<AgentProfile, AgentFactory, IChessAgent>> _kinds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<AgentProfile, IEvaluator>> _evaluators = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AgentProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _profileOrder = new();

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    /// <summary>A factory with the built-in kinds, evaluator and preset profiles.</summary>
    public AgentFactory()
    {
        RegisterKind(SearchKind, (p, f) => new SearchAgent(p, f.CreateEvaluator(p)));
        RegisterKind(RandomKind, (p, _) => new RandomAgent(p));
        RegisterEvaluator(WeightedEvaluator, p => new WeightedEvaluator(p.Weights));
        foreach (var preset in AgentProfile.Presets) RegisterProfile(preset);
    }

    public IReadOnlyList<AgentProfile> Profiles => _profileOrder.Select(id => _profiles[id]).ToList();

    public AgentFactory RegisterKind(string kind, Func<AgentProfile, AgentFactory, IChessAgent> create)
    {
        _kinds[kind] = create;
        return this;
    }

    public AgentFactory RegisterEvaluator(string id, Func<AgentProfile, IEvaluator> create)
    {
        _evaluators[id] = create;
        return this;
    }

    /// <summary>Add or replace a profile (by id).</summary>
    public AgentFactory RegisterProfile(AgentProfile profile)
    {
        if (!_profiles.ContainsKey(profile.Id)) _profileOrder.Add(profile.Id);
        _profiles[profile.Id] = profile;
        return this;
    }

    public AgentProfile GetProfile(string id) =>
        _profiles.TryGetValue(id, out var p) ? p : throw new AgentConfigException($"unknown AI profile '{id}' (known: {string.Join(", ", _profileOrder)})");

    public IEvaluator CreateEvaluator(AgentProfile profile) =>
        _evaluators.TryGetValue(profile.Evaluator, out var create)
            ? create(profile)
            : throw new AgentConfigException($"unknown evaluator '{profile.Evaluator}' in profile '{profile.Id}'");

    public IChessAgent Create(AgentProfile profile) =>
        _kinds.TryGetValue(profile.Kind, out var create)
            ? create(profile, this)
            : throw new AgentConfigException($"unknown agent kind '{profile.Kind}' in profile '{profile.Id}'");

    public IChessAgent Create(string profileId) => Create(GetProfile(profileId));

    /// <summary>Create an agent from a JSON profile reference: a profile id string, or an object (see <see cref="ParseProfile(JsonElement)"/>).</summary>
    public IChessAgent CreateFromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Create(ParseProfile(doc.RootElement));
    }

    /// <summary>
    /// Load and register profiles from JSON: an array of profile objects, or <c>{ "profiles": [...] }</c>.
    /// A profile may <c>"extends"</c> any profile registered before it (including earlier ones in the same file).
    /// </summary>
    public IReadOnlyList<AgentProfile> LoadProfiles(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("profiles", out var list)) root = list;
        if (root.ValueKind != JsonValueKind.Array) throw new AgentConfigException("expected an array of profiles or { \"profiles\": [...] }");

        var loaded = new List<AgentProfile>();
        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) throw new AgentConfigException("each profile must be an object");
            var profile = ParseProfile(item);
            RegisterProfile(profile);
            loaded.Add(profile);
        }
        return loaded;
    }

    /// <summary>
    /// A profile from JSON. A string is a registered profile id. An object starts from <c>"extends"</c>
    /// (default: the built-in defaults) and overrides any listed fields; <c>"weights"</c> merges field by field.
    /// Field names are the <see cref="AgentProfile"/> properties in camelCase; unknown fields are an error.
    /// </summary>
    public AgentProfile ParseProfile(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String) return GetProfile(element.GetString()!);
        if (element.ValueKind != JsonValueKind.Object) throw new AgentConfigException("an AI profile must be an id string or an object");

        var baseProfile = element.TryGetProperty("extends", out var ext) ? GetProfile(ext.GetString()!) : new AgentProfile();
        var node = JsonSerializer.SerializeToNode(baseProfile, Json)!.AsObject();
        foreach (var prop in element.EnumerateObject())
        {
            if (prop.NameEquals("extends")) continue;
            string key = JsonNamingPolicy.CamelCase.ConvertName(prop.Name);
            var value = JsonNode.Parse(prop.Value.GetRawText());
            if (key == "weights" && value is JsonObject weights && node["weights"] is JsonObject current)
                foreach (var w in weights.ToList())
                {
                    weights.Remove(w.Key);
                    current[JsonNamingPolicy.CamelCase.ConvertName(w.Key)] = w.Value;
                }
            else node[key] = value;
        }
        if (!element.TryGetProperty("id", out _) && !element.TryGetProperty("Id", out _))
            node["id"] = baseProfile.Id + "*"; // an inline variant of the base

        try
        {
            return node.Deserialize<AgentProfile>(Json) ?? throw new AgentConfigException("empty AI profile");
        }
        catch (JsonException e)
        {
            throw new AgentConfigException($"invalid AI profile: {e.Message}");
        }
    }

    /// <summary>
    /// The AI players a level asks for: the optional <c>"ai"</c> field of a map file,
    /// <c>{ "white": ..., "black": ... }</c>, where each side is a profile reference (id or object) or
    /// <c>"human"</c> / null / absent for a human player.
    /// </summary>
    public (AgentProfile? White, AgentProfile? Black) ParseMapPlayers(string mapJson)
    {
        using var doc = JsonDocument.Parse(mapJson);
        if (!doc.RootElement.TryGetProperty("ai", out var ai) || ai.ValueKind == JsonValueKind.Null) return (null, null);
        if (ai.ValueKind != JsonValueKind.Object) throw new AgentConfigException("\"ai\" must be an object: { \"white\": ..., \"black\": ... }");

        AgentProfile? white = null, black = null;
        foreach (var prop in ai.EnumerateObject())
        {
            bool human = prop.Value.ValueKind == JsonValueKind.Null
                || (prop.Value.ValueKind == JsonValueKind.String && string.Equals(prop.Value.GetString(), "human", StringComparison.OrdinalIgnoreCase));
            var profile = human ? null : ParseProfile(prop.Value);
            switch (prop.Name.ToLowerInvariant())
            {
                case "white": white = profile; break;
                case "black": black = profile; break;
                default: throw new AgentConfigException($"unknown side '{prop.Name}' in \"ai\" (use white / black)");
            }
        }
        return (white, black);
    }

    /// <summary>The profile as JSON (all fields), e.g. to start a new one from a preset.</summary>
    public static string ToJson(AgentProfile profile) => JsonSerializer.Serialize(profile, Json);
}

public sealed class AgentConfigException : Exception
{
    public AgentConfigException(string message) : base(message) { }
}
