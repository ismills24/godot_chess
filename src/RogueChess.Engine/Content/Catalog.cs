using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace RogueChess.Engine.Content;

/// <summary>Builds a global effect from a map file's board-effect entry (the whole JSON object).</summary>
public delegate (GlobalDef Def, int[] Vars) BoardEffectFactory(JsonElement entry);

/// <summary>
/// Lookup of content by the string ids used in map files. Ids are case-insensitive, and one def can be
/// registered under several aliases (e.g. "Pawn" and "pawn", or "WallTile" and "wall").
/// </summary>
public sealed class Catalog
{
    private readonly Dictionary<string, PieceDef> _pieces = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TileDef> _tiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AbilityDef> _abilities = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BoardEffectFactory> _boardEffects = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<Core.Side, RelicDef>> _relics = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ConsumableDef> _consumables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GlobalDef> _globals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RosterDef> _rosters = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The standard pieces and the standard tile, under both their def ids and the prototype's names.</summary>
    public static Catalog CreateStandard()
    {
        var c = new Catalog();
        foreach (var def in StandardPieces.All) c.AddPiece(def, def.Name);
        c.AddTile(TileDef.Standard, "StandardTile");
        return c;
    }

    public Catalog AddPiece(PieceDef def, params string[] aliases) => Add(_pieces, def, def.Id, aliases);
    public Catalog AddTile(TileDef def, params string[] aliases) => Add(_tiles, def, def.Id, aliases);
    public Catalog AddAbility(AbilityDef def, params string[] aliases) => Add(_abilities, def, def.Id, aliases);
    public Catalog AddGlobal(GlobalDef def, params string[] aliases) => Add(_globals, def, def.Id, aliases);

    /// <summary>Register a board-effect factory under the effect type used in map files (e.g. "WindGust").</summary>
    public Catalog AddBoardEffect(string effectType, BoardEffectFactory factory)
    {
        _boardEffects[effectType] = factory;
        return this;
    }

    /// <summary>Register a relic under its id and aliases (e.g. "TimeBankRelic"). Relics are built per owner.</summary>
    public Catalog AddRelic(string id, Func<Core.Side, RelicDef> factory, params string[] aliases) => Add(_relics, factory, id, aliases);

    public Catalog AddRoster(RosterDef def) => Add(_rosters, def, def.Id, Array.Empty<string>());
    public Catalog AddConsumable(ConsumableDef def, params string[] aliases) => Add(_consumables, def, def.Id, aliases);

    /// <summary>Every registered def once (aliases collapsed), in registration order. For palettes and pickers.</summary>
    public IReadOnlyList<PieceDef> Pieces => _pieces.Values.Distinct().ToList();
    public IReadOnlyList<TileDef> Tiles => _tiles.Values.Distinct().ToList();
    public IReadOnlyList<AbilityDef> Abilities => _abilities.Values.Distinct().ToList();
    public IReadOnlyList<ConsumableDef> Consumables => _consumables.Values.Distinct().ToList();

    public PieceDef? FindPiece(string id) => _pieces.GetValueOrDefault(id);
    public TileDef? FindTile(string id) => _tiles.GetValueOrDefault(id);
    public AbilityDef? FindAbility(string id) => _abilities.GetValueOrDefault(id);
    public GlobalDef? FindGlobal(string id) => _globals.GetValueOrDefault(id);
    public BoardEffectFactory? FindBoardEffect(string type) => _boardEffects.GetValueOrDefault(type);
    public Func<Core.Side, RelicDef>? FindRelic(string id) => _relics.GetValueOrDefault(id);
    public ConsumableDef? FindConsumable(string id) => _consumables.GetValueOrDefault(id);
    public RosterDef? FindRoster(string id) => _rosters.GetValueOrDefault(id);
    public IEnumerable<RosterDef> Rosters => _rosters.Values.Distinct();

    private Catalog Add<T>(Dictionary<string, T> map, T def, string id, string[] aliases)
    {
        map[id] = def;
        foreach (var alias in aliases) map[alias] = def;
        return this;
    }
}
