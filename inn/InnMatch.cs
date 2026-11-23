using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using RogueChess.Engine.AI;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;
using Side = RogueChess.Engine.Core.Side;

namespace GodotChess.Inn;

/// <summary>
/// The game played across the inn's bar: you have White, the bartender Black. Owns the engine state and the
/// bartender's AI (which thinks on a background thread), and answers the inn scene's questions about it.
/// The scene draws the board and reads input; this node only knows squares, numbered rank * Width + file
/// with rank 0 White's home rank.
/// For now the game is plain chess from the start position; encounters come later.
/// </summary>
public partial class InnMatch : Node
{
    /// <summary>The position changed: a move was played (from, to are its squares).</summary>
    [Signal] public delegate void MovedEventHandler(int from, int to, bool byBartender);
    /// <summary>The bartender started thinking about his move.</summary>
    [Signal] public delegate void ThinkingEventHandler();
    /// <summary>The game is over: "player", "bartender" or "draw".</summary>
    [Signal] public delegate void FinishedEventHandler(string winner);

    /// <summary>A beat after your move before he starts on his, so his reply never lands instantly.</summary>
    private const double ReplyDelay = 0.6;

    private readonly AgentFactory _factory = new();
    private readonly List<Move> _legal = new();
    private GameState _state = null!;
    private IChessAgent _bartender = null!;
    private IChessAgent? _autoplay;
    private Task<AgentDecision>? _thinking;
    private CancellationTokenSource? _cancel;
    private double _sinceMove;
    private bool _finished;

    /// <summary>While set, the bartender holds off starting his next move (he's busy drinking).</summary>
    public bool Busy { get; set; }

    public int Width => _state.Width;
    public int Height => _state.Height;

    /// <summary>Set up a fresh game. `autoplay` hands your side to an AI as well (for unattended test renders).</summary>
    public void Begin(bool autoplay = false)
    {
        Cancel();
        _state = Fen.Parse(Fen.StartPosition, RuleSet.Standard);
        Game.BeginGame(_state);
        _state.ClearHistory();
        _bartender = _factory.Create(AgentProfile.Casual);
        _autoplay = autoplay ? _factory.Create(AgentProfile.Casual) : null;
        _finished = false;
        _sinceMove = 0;
    }

    public bool IsPlayerTurn() => !_finished && _autoplay == null && _thinking == null && _state.SideToMove == Side.White;

    /// <summary>Every piece on the board: {id, square, symbol}, the symbol upper case for White (FEN letters).</summary>
    public Godot.Collections.Array<Godot.Collections.Dictionary> Pieces()
    {
        var list = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var piece in _state.PiecesOnBoard())
        {
            char symbol = piece.Def.Symbol;
            list.Add(new Godot.Collections.Dictionary
            {
                ["id"] = piece.Id,
                ["square"] = piece.Square,
                ["symbol"] = (piece.Side == Side.White ? char.ToUpperInvariant(symbol) : char.ToLowerInvariant(symbol)).ToString(),
            });
        }
        return list;
    }

    /// <summary>Whether `square` holds one of your pieces that you could move now.</summary>
    public bool IsMine(int square) =>
        IsPlayerTurn() && _state.PieceAt(square) is { } piece && piece.Side == Side.White;

    /// <summary>The squares your piece on `square` can move to; captures are flagged in `captures`.</summary>
    public int[] Targets(int square)
    {
        if (!IsMine(square)) return System.Array.Empty<int>();
        _legal.Clear();
        MoveGenerator.GenerateLegal(_state, _legal);
        return _legal.Where(m => m.From == square).Select(m => m.To).Distinct().ToArray();
    }

    public bool IsCapture(int from, int to)
    {
        _legal.Clear();
        MoveGenerator.GenerateLegal(_state, _legal);
        return _legal.Any(m => m.From == from && m.To == to && m.IsCapture);
    }

    /// <summary>Play your move from `from` to `to` if it's legal (a pawn reaching the end becomes a queen).</summary>
    public bool TryMove(int from, int to)
    {
        if (!IsMine(from)) return false;
        _legal.Clear();
        MoveGenerator.GenerateLegal(_state, _legal);
        foreach (var move in _legal)
            if (move.From == from && move.To == to)
                return Play(move, false);
        return false;
    }

    /// <summary>The square of the side-to-move's king if it stands in check, else -1.</summary>
    public int CheckedKing()
    {
        var side = _state.SideToMove;
        var king = _state.PiecesOnBoard(side).FirstOrDefault(p => p.Def.IsRoyal);
        if (king == null) return -1;
        var probe = _state.Clone();
        probe.SetSideToMove(side.Opponent());
        _legal.Clear();
        MoveGenerator.GenerateLegal(probe, _legal);
        return _legal.Any(m => m.To == king.Square) ? king.Square : -1;
    }

    public override void _Process(double delta)
    {
        if (_state == null || _finished) return;
        _sinceMove += delta;
        if (_thinking != null)
        {
            if (!_thinking.IsCompleted) return;
            var task = _thinking;
            _thinking = null;
            if (task.IsFaulted || task.IsCanceled)
            {
                GD.PrintErr($"[Inn] the bartender's AI failed: {task.Exception?.GetBaseException()}");
                return;
            }
            if (task.Result.Move is { } move) Play(move, _state.SideToMove == Side.Black);
            return;
        }
        var agent = _state.SideToMove == Side.Black ? _bartender : _autoplay;
        if (agent == null || _sinceMove < ReplyDelay) return;
        if (Busy && _state.SideToMove == Side.Black) return;
        var snapshot = _state.Clone();
        _cancel = new CancellationTokenSource();
        var token = _cancel.Token;
        _thinking = Task.Run(() => agent.Decide(snapshot, token));
        if (_state.SideToMove == Side.Black) EmitSignal(SignalName.Thinking);
    }

    public override void _ExitTree() => Cancel();

    private bool Play(Move move, bool byBartender)
    {
        var result = Game.PlayTurn(_state, move);
        if (!result.Accepted)
        {
            GD.PrintErr($"[Inn] move {move} rejected: {result.RejectReason}");
            return false;
        }
        _sinceMove = 0;
        EmitSignal(SignalName.Moved, move.From, move.To, byBartender);
        var outcome = _state.Rules.Evaluate(_state);
        if (outcome != GameOutcome.Ongoing)
        {
            _finished = true;
            EmitSignal(SignalName.Finished, outcome switch
            {
                GameOutcome.WhiteWins => "player",
                GameOutcome.BlackWins => "bartender",
                _ => "draw",
            });
        }
        return true;
    }

    private void Cancel()
    {
        _cancel?.Cancel();
        _cancel = null;
        _thinking = null;
    }
}
