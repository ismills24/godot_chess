using Godot;
using GodotChess.Inn;
using GodotChess.Sandbox;

namespace GodotChess.Run;

/// <summary>Battle presentation for configured encounters without a bespoke scene.</summary>
public partial class BattleEncounterView : Control
{
    private InnMatch _match = null!;
    private BoardView3D _board = null!;
    private Label _status = null!;
    private Button _undo = null!, _leave = null!;
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var layout = new VBoxContainer();
        layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(layout);
        var bar = new HBoxContainer();
        layout.AddChild(bar);
        _status = new Label { Text = "Your move.", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        bar.AddChild(_status);
        _undo = new Button { Text = "Undo move" };
        _undo.Pressed += () => _match.Undo();
        bar.AddChild(_undo);
        _leave = new Button { Text = "Concede and leave" };
        _leave.Pressed += () => _match.Leave();
        bar.AddChild(_leave);
        _board = new BoardView3D();
        layout.AddChild(_board);
        _board.CustomMinimumSize = new Vector2(600, 500);
        _board.SizeFlagsVertical = SizeFlags.ExpandFill;
        _match = new InnMatch();
        AddChild(_match);
        _match.Moved += (_, _, _) => Refresh();
        _match.Refreshed += Refresh;
        _match.Thinking += () => { _status.Text = "Resolving turn..."; _undo.Disabled = true; _leave.Disabled = true; };
        _match.Failed += message => { Refresh(); _status.Text = message; };
        _match.Finished += winner => { _status.Text = winner == "player" ? "Victory." : winner == "draw" ? "Draw." : "Defeat.";
            _leave.Text = "Return to map"; _leave.Disabled = false; };
        _match.Begin();
        _board.SquareClicked += square =>
        {
            if (_board.Selected >= 0 && (_board.QuietTargets.Contains(square) || _board.CaptureTargets.Contains(square)))
            { _match.TryMove(_board.Selected, square); _board.Selected = -1; }
            else _board.Selected = _match.IsMine(square) ? square : -1;
            _board.QuietTargets.Clear(); _board.CaptureTargets.Clear();
            if (_board.Selected >= 0)
                foreach (int target in _match.Targets(_board.Selected))
                    (_match.IsCapture(_board.Selected, target) ? _board.CaptureTargets : _board.QuietTargets).Add(target);
            _board.RefreshVisuals();
        };
        Refresh();
    }
    private void Refresh()
    {
        _board.State = _match.DisplayState;
        _board.Selected = -1; _board.QuietTargets.Clear(); _board.CaptureTargets.Clear();
        _board.RefreshVisuals();
        _undo.Disabled = !_match.CanUndo();
        _leave.Disabled = false;
        if (_match.IsPlayerTurn()) _status.Text = _match.CheckedKing() >= 0 ? "Check. Your move." : "Your move.";
    }
}
