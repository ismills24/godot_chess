using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GodotChess.Sandbox;
using RogueChess.Engine.Content;
using RogueChess.Engine.Content.Tiles;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Rules;

namespace GodotChess.Experiments;

/// <summary>
/// Beer spill on the sandbox / run battle board (BoardView3D), driven through the engine the way the Slippery board
/// effect will be: tiles are set to Slippery on the GameState and the view is refreshed, nothing else.
///   S  make 3 random squares Slippery (beer spills onto them)
///   D  clear the oldest Slippery square (its puddle dries off)
///   B  spill beer by hand on 2 random squares (BoardView3D.SpillBeer, no tile change)
/// Run: Godot --path . res://experiments/beer_spill/BeerSpillBoardDemo.tscn
/// Still: add `-- --capture=out.png` to make squares Slippery, wait for the puddles to settle and save the window.
/// </summary>
public partial class BeerSpillBoardDemo : Control
{
    private BoardView3D _view = null!;
    private GameState _state = null!;
    private readonly List<int> _slippery = new();
    private readonly Random _random = new(7);

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _state = Fen.Parse("r1bqkb1r/pppp1ppp/2n2n2/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 4 4", RuleSet.Rogue);
        _view = new BoardView3D { State = _state };
        _view.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_view);
        _view.RefreshVisuals();
        AddChild(new Label { Text = "S make Slippery  ·  D clear oldest  ·  B spill by hand", Position = new Vector2(16, 50) });
        string? capture = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture=", StringComparison.Ordinal));
        if (capture != null) Capture(capture["--capture=".Length..]);
    }

    private async void Capture(string path)
    {
        await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
        // A square with a piece on it, two empty ones.
        foreach (string name in new[] { "e4", "d5", "f6", "c3" }) MakeSlippery(Square(name));
        await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(path);
        GetTree().Quit();
    }

    private int Square(string name) => _state.Sq(name[0] - 'a', name[1] - '1');

    private void MakeSlippery(int sq)
    {
        if (_slippery.Contains(sq)) return;
        _state.SetTile(sq, SlipperyTile.Instance);
        _slippery.Add(sq);
        _view.RefreshVisuals();
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } key) return;
        switch (key.Keycode)
        {
            case Key.S:
                foreach (int sq in RandomSquares(3)) MakeSlippery(sq);
                break;
            case Key.D when _slippery.Count > 0:
                _state.SetTile(_slippery[0], TileDef.Standard);
                _slippery.RemoveAt(0);
                _view.RefreshVisuals();
                break;
            case Key.B:
                _view.SpillBeer(RandomSquares(2));
                break;
        }
    }

    private IEnumerable<int> RandomSquares(int count) =>
        Enumerable.Range(0, _state.Area).OrderBy(_ => _random.Next()).Take(count).ToArray();
}
