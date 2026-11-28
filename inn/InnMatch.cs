using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using GodotChess.Run;
using GodotChess.Sandbox;
using RogueChess.Engine.AI;
using RogueChess.Engine.Content;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Rules;
using RogueChess.Runs;
using Side = RogueChess.Engine.Core.Side;

namespace GodotChess.Inn;

/// <summary>Presentation adapter. RunSession owns game state; this node plays its derived move frames.</summary>
public partial class InnMatch : Node
{
    [Signal] public delegate void MovedEventHandler(int from, int to, bool byBartender);
    [Signal] public delegate void ThinkingEventHandler();
    [Signal] public delegate void FinishedEventHandler(string winner);
    [Signal] public delegate void RefreshedEventHandler();
    [Signal] public delegate void FailedEventHandler(string reason);

    private readonly Catalog _catalog = ContentLibrary.CreateCatalog();
    private readonly Queue<BattleStep> _frames = new();
    private RunSession _session = null!;
    private RunHost? _host;
    private string _encounterId = "";
    private GameState _state = null!;
    private Task<CommandResult>? _pending;
    private CancellationTokenSource? _cancel;
    private bool _readyForInput, _finished, _autoplay, _returnAfter;
    private double _sinceFrame;
    private long _decisionStart;
    private RunPhase _before;
    public bool Busy { get; set; }
    public int Width => _state.Width;
    public int Height => _state.Height;
    public GameState DisplayState => _state;

    /// <summary>The standalone preview's game (F6 on a scene): the run's rogue battle on a 6x6 board.</summary>
    private static readonly BattleParameters PreviewBattle = new()
    {
        Width = 6, Height = 6,
        Player = new() { PieceValue = 10, MinimumPawns = 2, StartingAbilities = new[] { "exploding", "phantom", "predator" } },
        Enemy = new() { PieceValue = 8, MinimumPawns = 2, AbilityValue = 2 },
        BoardEffects = new() { System.Text.Json.JsonDocument.Parse(BeerSpill).RootElement.Clone() },
    };
    /// <summary>The bartender's beer spill (same as the inn encounter in run/prototype.json).</summary>
    private const string BeerSpill = """
        { "id": "beer-spill", "name": "Beer spill",
          "condition": { "type": "TurnInterval", "params": { "interval": 6, "startTurn": 2 } },
          "effect": { "type": "RandomTiles", "params": { "count": 3, "includeOccupied": true, "tile": "slippery", "duration": 6, "cause": "beer-spill" } } }
        """;

    /// <summary>The preview's battle, at the size `-- --board=WxH` asks for (up to 10x10).</summary>
    private static BattleParameters PreviewBoard()
    {
        string? arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--board=", StringComparison.Ordinal));
        var size = arg?["--board=".Length..].Split('x');
        if (size is { Length: 2 } && int.TryParse(size[0], out int w) && int.TryParse(size[1], out int h))
            return PreviewBattle with { Width = Math.Clamp(w, 2, BattleHandler.MaxBoardSide), Height = Math.Clamp(h, 2, BattleHandler.MaxBoardSide) };
        return PreviewBattle;
    }

    public void Begin(bool autoplay = false)
    {
        Cancel();
        _host = RunHost.Active;
        if (_host?.Session?.State.ActiveEncounterId is { } id)
        {
            _session = _host.Session;
            _encounterId = id;
        }
        else
        {
            // F6 remains a standalone preview, but follows the same command/replay path.
            _host = null;
            _encounterId = "bartender-preview";
            _session = RunSession.Start(1, new RunDefinition
            {
                StartX = 0, Encounters = new()
                {
                    new() { Id = _encounterId, Kind = "bartender", X = 1, Y = 1,
                        Parameters = RunJson.Data(PreviewBoard()) },
                },
            });
            _session.Execute(RunCommand.Create("world.move", new WorldMove(1, 1)));
        }
        // Saved positions name the encounter's board effects (e.g. the beer spill); register them before restoring.
        BattleHandler.CreateBoardEffects(_catalog, RunJson.Decode<BattleParameters>(Encounter().Definition.Parameters));
        _autoplay = autoplay;
        _finished = false;
        _returnAfter = false;
        Synchronize();
        _readyForInput = !_finished;
        _decisionStart = Stopwatch.GetTimestamp();
    }
    /// <summary>Your move, and nothing (like the bartender's drink, <see cref="Busy"/>) is holding the game up.</summary>
    public bool IsPlayerTurn() => _readyForInput && !_finished && !_autoplay && !Busy && _state.SideToMove == Side.White;
    public bool CanUndo() => _readyForInput && !_finished && Encounter().Data.As<BattleState>("battle").UndoPositions.Count > 0;
    public bool IsMine(int square) => square >= 0 && square < _state.Area && IsPlayerTurn()
        && _state.PieceAt(square) is { Side: Side.White };
    public Godot.Collections.Array<Godot.Collections.Dictionary> Pieces()
    {
        var list = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var p in _state.PiecesOnBoard())
        {
            var badges = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            foreach (var ability in p.Abilities)
                badges.Add(new Godot.Collections.Dictionary { ["code"] = BoardView.BadgeCode(ability.Def.Id, ability.Def.Name),
                    ["color"] = BoardView.BadgeColor(ability.Def.Id).Lightened(0.35f), ["name"] = ability.Def.Name });
            list.Add(new Godot.Collections.Dictionary { ["id"] = p.Id, ["square"] = p.Square, ["badges"] = badges,
                ["symbol"] = (p.Side == Side.White ? char.ToUpperInvariant(p.Def.Symbol) : char.ToLowerInvariant(p.Def.Symbol)).ToString() });
        }
        return list;
    }
    /// <summary>The board's special squares (walls, fire, tombs...): {square, id, name, color}. chess_set.gd's
    /// show_tiles draws Slippery ones ("slippery") as beer puddles.</summary>
    public Godot.Collections.Array<Godot.Collections.Dictionary> Tiles()
    {
        var list = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        for (int sq = 0; sq < _state.Area; sq++)
        {
            var tile = _state.TileAt(sq);
            if (tile.Def.IsDefault) continue;
            list.Add(new Godot.Collections.Dictionary { ["square"] = sq, ["id"] = tile.Def.Id, ["name"] = tile.Def.Name, ["color"] = TileColor(tile.Def.Id) });
        }
        return list;
    }
    private static Color TileColor(string id) => id switch
    {
        "wall" => Colors.Silver, "guardian" => Colors.Gold, "fog" => Colors.LightSteelBlue,
        "fire" => new Color(1f, 0.45f, 0.1f), "slippery" => new Color(1f, 0.72f, 0.25f), "freeze" => new Color(0.55f, 0.85f, 1f), "tomb" => new Color(0.6f, 0.55f, 0.5f),
        _ => Colors.Orchid,
    };
    /// <summary>What's on a square, for the hover line: "Your Knight: Exploding, Phantom".</summary>
    public string Describe(int square)
    {
        if (square < 0 || square >= _state.Area) return "";
        var parts = new List<string>();
        if (_state.PieceAt(square) is { } piece)
        {
            string owner = piece.Side == Side.White ? "Your" : "Their";
            parts.Add(piece.Abilities.Count == 0 ? $"{owner} {piece.Def.Name}"
                : $"{owner} {piece.Def.Name}: {string.Join(", ", piece.Abilities.Select(a => a.Def.Name))}");
        }
        var tile = _state.TileAt(square);
        if (!tile.Def.IsDefault) parts.Add(tile.Def.Name);
        return string.Join("  ·  ", parts);
    }
    private List<Move> Legal()
    {
        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(_state, moves);
        return moves;
    }
    public int[] Targets(int square) => IsMine(square)
        ? Legal().Where(m => m.From == square).Select(m => m.To).Distinct().ToArray() : Array.Empty<int>();
    public bool IsCapture(int from, int to) => Legal().Any(m => m.From == from && m.To == to && m.IsCapture);
    public int CheckedKing()
    {
        if (!MoveGenerator.IsInCheck(_state, _state.SideToMove)) return -1;
        return _state.PiecesOnBoard(_state.SideToMove).FirstOrDefault(p => p.Def.IsRoyal)?.Square ?? -1;
    }
    public bool TryMove(int from, int to)
    {
        if (!IsMine(from) || !Legal().Any(m => m.From == from && m.To == to)) return false;
        return Submit(RunCommand.Create("battle.move", new BattleMove(from, to), _encounterId));
    }
    public bool Undo() => CanUndo() && Submit(RunCommand.Create("battle.undo", new { }, _encounterId));
    public bool Leave()
    {
        if (_pending != null || _frames.Count > 0 || (!_readyForInput && !_finished)) return false;
        if (_finished) { ReturnToWorld(); return true; }
        _returnAfter = true;
        return Submit(RunCommand.Create("battle.resign", new { }, _encounterId));
    }
    private bool Submit(RunCommand command)
    {
        if (!_readyForInput || _pending != null) return false;
        long duration = (long)Stopwatch.GetElapsedTime(_decisionStart).TotalMilliseconds;
        long revision = _session.Sequence;
        _before = _session.State.Phase;
        _readyForInput = false;
        _cancel = new CancellationTokenSource();
        var token = _cancel.Token;
        _pending = Task.Run(() => _session.Execute(command, duration, revision, token), token);
        EmitSignal(SignalName.Thinking);
        return true;
    }
    public override void _Process(double delta)
    {
        if (_state == null) return;
        _sinceFrame += delta;
        if (_pending != null)
        {
            if (!_pending.IsCompleted) return;
            var task = _pending; _pending = null;
            _cancel?.Dispose(); _cancel = null;
            if (task.IsCanceled || task.IsFaulted)
            {
                _readyForInput = true;
                EmitSignal(SignalName.Failed, task.Exception?.GetBaseException().Message ?? "Move cancelled.");
                return;
            }
            var result = task.Result;
            if (!result.Accepted)
            {
                _readyForInput = true;
                EmitSignal(SignalName.Failed, result.Reason ?? "Move rejected.");
                return;
            }
            _host?.NotifyCommit(_before, result);
            foreach (var frame in result.Effects.OfType<BattleStep>()) _frames.Enqueue(frame);
            _sinceFrame = 1;
            if (_frames.Count == 0) { FinishPlayback(); return; }
        }
        if (_frames.Count > 0)
        {
            var next = _frames.Peek();
            if (_sinceFrame < 0.65 || (next.ByEnemy && Busy)) return;
            _frames.Dequeue();
            _state = GameState.Restore(next.State, _catalog);
            _sinceFrame = 0;
            EmitSignal(SignalName.Moved, next.From, next.To, next.ByEnemy);
            return;
        }
        if (!_readyForInput && !_finished && _sinceFrame >= 0.45) FinishPlayback();
        if (_autoplay && _readyForInput && !Busy && !_finished && _sinceFrame >= 0.8)
        {
            var agent = new AgentFactory().Create(AgentProfile.Novice with { TimeBudgetMs = 0, NodeBudget = 2000 });
            if (agent.Decide(_state.Clone()).Move is { } move)
                Submit(RunCommand.Create("battle.move", new BattleMove(move.From, move.To, move.Promotion), _encounterId));
        }
    }
    private EncounterState Encounter() => _session.State.Encounters.Single(e => e.Definition.Id == _encounterId);
    private void Synchronize()
    {
        var encounter = Encounter();
        _state = GameState.Restore(encounter.Data.As<BattleState>("battle").Position, _catalog);
        _finished = encounter.Status == EncounterStatus.Completed;
    }
    private void FinishPlayback()
    {
        Synchronize();
        _readyForInput = !_finished;
        _decisionStart = Stopwatch.GetTimestamp();
        EmitSignal(SignalName.Refreshed);
        if (_finished)
        {
            string winner = Encounter().Outcome ?? "draw";
            EmitSignal(SignalName.Finished, winner == "enemy" ? "bartender" : winner);
        }
        if (_returnAfter) Callable.From(ReturnToWorld).CallDeferred();
    }
    private void ReturnToWorld()
    {
        if (_host != null) _host.ShowWorld();
        else GetTree().ChangeSceneToFile("res://menu/MainMenu.tscn");
    }
    public override void _ExitTree() => Cancel();
    private void Cancel()
    {
        _cancel?.Cancel();
        _frames.Clear();
        if (_pending is { } task && _cancel is { } source) _ = task.ContinueWith(_ => source.Dispose(), TaskScheduler.Default);
        else _cancel?.Dispose();
        _cancel = null; _pending = null;
    }
}
