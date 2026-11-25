using System;
using System.Linq;
using RogueChess.Engine.Content;
using RogueChess.Engine.Rules;

namespace RogueChess.Engine.Core;

/// <summary>Portable complete state, independent of the transient inverse log and search hash.</summary>
public sealed record GameStateSnapshot
{
    public int Version { get; init; } = 2;
    public int Width { get; init; }
    public int Height { get; init; }
    public RuleSnapshot Rules { get; init; } = new();
    public Side SideToMove { get; init; }
    public int Ply { get; init; }
    public int EnPassantSquare { get; init; } = -1;
    public int NextPieceId { get; init; }
    public ulong RandomState { get; init; }
    public PieceSnapshot[] Pieces { get; init; } = Array.Empty<PieceSnapshot>();
    public DefinitionState[] Tiles { get; init; } = Array.Empty<DefinitionState>();
    public DefinitionState[] Globals { get; init; } = Array.Empty<DefinitionState>();
    public int[] ExtraTurns { get; init; } = new int[2];
    public int[] ClockMs { get; init; } = new int[2];
    public PlayedAction[] History { get; init; } = Array.Empty<PlayedAction>();
}

public sealed record DefinitionState(string Id, int[] Vars);
public sealed record PieceSnapshot(int Id, Side Side, string Definition, int Square, int MovesMade, DefinitionState[] Abilities);
public sealed record RuleSnapshot
{
    public string Name { get; init; } = "rogue";
    public bool ForbidSelfCheck { get; init; }
    public bool Castling { get; init; }
    public bool EnPassant { get; init; }
    public WinCondition WinCondition { get; init; }
    public string[] Promotions { get; init; } = Array.Empty<string>();
}

public sealed partial class GameState
{
    public GameStateSnapshot Capture() => new()
    {
        Width = Width, Height = Height, SideToMove = SideToMove, Ply = Ply,
        EnPassantSquare = EnPassantSquare, NextPieceId = _nextPieceId, RandomState = RandomState,
        Rules = new RuleSnapshot
        {
            Name = Rules.Name, ForbidSelfCheck = Rules.ForbidSelfCheck, Castling = Rules.Castling,
            EnPassant = Rules.EnPassant, WinCondition = Rules.WinCondition,
            Promotions = Rules.PromotionOptions.Select(p => p.Id).ToArray(),
        },
        Pieces = _pieces.Values.OrderBy(p => p.Id).Select(p => new PieceSnapshot(p.Id, p.Side,
            p.Def.Id, p.Square, p.MovesMade, p.Abilities.Select(a => new DefinitionState(a.Def.Id, (int[])a.Vars.Clone())).ToArray())).ToArray(),
        Tiles = _tiles.Select(t => new DefinitionState(t.Def.Id, (int[])t.Vars.Clone())).ToArray(),
        Globals = _globals.Select(g => new DefinitionState(g.Def.Id, (int[])g.Vars.Clone())).ToArray(),
        ExtraTurns = (int[])_extraTurns.Clone(), ClockMs = (int[])_clockMs.Clone(), History = _history.ToArray(),
    };

    public static GameState Restore(GameStateSnapshot snapshot, Catalog catalog)
    {
        if (snapshot.Version != 2) throw new FormatException($"Unsupported battle snapshot version {snapshot.Version}.");
        if (snapshot.Width < 1 || snapshot.Width > 26 || snapshot.Height < 1 || snapshot.Height > 128
            || snapshot.Tiles.Length != snapshot.Width * snapshot.Height || snapshot.Ply < 0
            || !Enum.IsDefined(snapshot.SideToMove) || !Enum.IsDefined(snapshot.Rules.WinCondition))
            throw new FormatException("Invalid battle snapshot dimensions or turn.");
        if (snapshot.ExtraTurns.Length != 2 || snapshot.ClockMs.Length != 2 || snapshot.ExtraTurns.Any(n => n < 0)
            || snapshot.History.Any(h => !Enum.IsDefined(h.Side) || h.Ply < 0 || h.Ply > snapshot.Ply))
            throw new FormatException("Invalid battle snapshot turn counters or history.");
        PieceDef PieceDef(string id) => catalog.FindPiece(id) ?? throw new FormatException($"Unknown piece '{id}'.");
        var rules = snapshot.Rules;
        var state = new GameState(snapshot.Width, snapshot.Height, new RuleSet(rules.Name,
            rules.ForbidSelfCheck, rules.Castling, rules.EnPassant, rules.Promotions.Select(PieceDef).ToArray(), rules.WinCondition));
        foreach (var saved in snapshot.Pieces)
        {
            if (saved.Id < 1 || saved.Square < -1 || saved.Square >= state.Area || saved.MovesMade < 0
                || !Enum.IsDefined(saved.Side) || state._pieces.ContainsKey(saved.Id))
                throw new FormatException("Invalid piece in battle snapshot.");
            var piece = new Piece(saved.Id, saved.Side, PieceDef(saved.Definition))
                { Square = saved.Square, MovesMade = saved.MovesMade };
            foreach (var ability in saved.Abilities)
            {
                var def = catalog.FindAbility(ability.Id) ?? throw new FormatException($"Unknown ability '{ability.Id}'.");
                if (ability.Vars.Length != def.VarCount || piece.HasAbility(def)) throw new FormatException("Invalid ability state.");
                piece.AbilityList.Add(new AbilityInstance(def, ability.Vars));
            }
            state._pieces.Add(piece.Id, piece);
            if (piece.Square >= 0)
            {
                if (state._board[piece.Square] != null) throw new FormatException("Two pieces occupy a square.");
                state._board[piece.Square] = piece;
            }
        }
        for (int i = 0; i < state.Area; i++)
        {
            var tile = snapshot.Tiles[i];
            var def = catalog.FindTile(tile.Id) ?? throw new FormatException($"Unknown tile '{tile.Id}'.");
            if (tile.Vars.Length != def.VarCount) throw new FormatException("Invalid tile state.");
            state._tiles[i] = def.IsDefault ? Tile.Standard : new Tile(def, tile.Vars);
        }
        foreach (var global in snapshot.Globals)
        {
            var def = catalog.FindGlobal(global.Id) ?? throw new FormatException($"Unknown global '{global.Id}'; register its definition in the catalog.");
            if (global.Vars.Length != def.VarCount) throw new FormatException("Invalid global state.");
            state._globals.Add(new GlobalInstance(def, global.Vars));
        }
        if (snapshot.NextPieceId <= state._pieces.Keys.DefaultIfEmpty(0).Max()
            || snapshot.EnPassantSquare < -1 || snapshot.EnPassantSquare >= state.Area)
            throw new FormatException("Invalid battle ID counter or en passant square.");
        state._nextPieceId = snapshot.NextPieceId;
        state.SideToMove = snapshot.SideToMove;
        state.Ply = snapshot.Ply;
        state.EnPassantSquare = snapshot.EnPassantSquare;
        state.RandomState = snapshot.RandomState;
        Array.Copy(snapshot.ExtraTurns, state._extraTurns, 2);
        Array.Copy(snapshot.ClockMs, state._clockMs, 2);
        state._history.AddRange(snapshot.History);
        state.Hash = state.ComputeHashFromScratch();
        return state;
    }
}
