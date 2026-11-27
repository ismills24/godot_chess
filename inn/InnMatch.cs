using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using GodotChess.Run;
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
                        Parameters = RunJson.Data(new BattleParameters { Fen = Fen.StartPosition, StandardRules = false }) },
                },
            });
            _session.Execute(RunCommand.Create("world.move", new WorldMove(1, 1)));
        }
        _autoplay = autoplay;
        _finished = false;
        _returnAfter = false;
        Synchronize();
        _readyForInput = !_finished;
        _decisionStart = Stopwatch.GetTimestamp();
    }
    public bool IsPlayerTurn() => _readyForInput && !_finished && !_autoplay && _state.SideToMove == Side.White;
    public bool CanUndo() => _readyForInput && !_finished && Encounter().Data.As<BattleState>("battle").UndoPositions.Count > 0;
    public bool IsMine(int square) => square >= 0 && square < _state.Area && IsPlayerTurn()
        && _state.PieceAt(square) is { Side: Side.White };
    public Godot.Collections.Array<Godot.Collections.Dictionary> Pieces()
    {
        var list = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var p in _state.PiecesOnBoard())
            list.Add(new Godot.Collections.Dictionary { ["id"] = p.Id, ["square"] = p.Square,
                ["symbol"] = (p.Side == Side.White ? char.ToUpperInvariant(p.Def.Symbol) : char.ToLowerInvariant(p.Def.Symbol)).ToString() });
        return list;
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
        if (_autoplay && _readyForInput && !_finished && _sinceFrame >= 0.8)
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
