using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using GodotChess.Sandbox.Visuals;
using RogueChess.Engine.Content;
using RogueChess.Engine.Content.BoardEffects;
using RogueChess.Engine.Content.Tiles;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;

namespace GodotChess.Experiments;

/// <summary>
/// Tidal Wave demo: the real engine plays four scripted turns with a north tidal wave on the top three ranks firing
/// every 2 turns. Each turn's trace is animated wave by wave: pieces dragged north, pieces washed off the board,
/// and pieces held back by a wall (plus the piece blocked behind them).
/// Render: Godot --path . --resolution 1280x720 --fixed-fps 30 --write-movie out\f.png res://experiments/tidal_wave/TidalWaveDemo.tscn
/// </summary>
public partial class TidalWaveDemo : Node3D
{
    private const float Cell = 1.45f;
    private const int Interval = 2;

    private static readonly Color Water = new(0.18f, 0.48f, 0.72f, 0.55f);
    private static readonly Color Foam = new(0.92f, 0.97f, 1f, 0.85f);

    private GameState _state = null!;
    private TidalWaveEffect _wave = null!;
    private readonly Dictionary<int, Node3D> _pieces = new();
    private Label _caption = null!;
    private Label _counter = null!;

    public override void _Ready()
    {
        var root = GetParent().GetParent();
        var display = root.GetNode<ColorRect>("Display");
        ((ShaderMaterial)display.Material).SetShaderParameter("stage_texture", GetViewport().GetTexture());

        var setting = GD.Load<PackedScene>("res://stages/street/StreetSetting.tscn").Instantiate<Node3D>();
        AddChild(setting);
        var board = GD.Load<PackedScene>("res://stages/street/StreetBoard.tscn").Instantiate<Node3D>();
        AddChild(board);
        setting.Call("fit", 8, 8);

        // Rank 8 is washed out, rank 7 lands on it, rank 6 moves up. The wall on e8 holds the black queen on e7,
        // which in turn blocks the white bishop behind it.
        _state = Fen.Parse("r1n3b1/pp2qp1p/2N1B1p1/8/7k/8/8/4K3 w - - 0 1", RuleSet.Rogue);
        _state.SetTile(Sq("e8"), WallTile.Instance);
        var area = Enumerable.Range(5, 3).SelectMany(y => Enumerable.Range(0, 8).Select(x => (x, y))).ToArray();
        _wave = new TidalWaveEffect(WaveDirection.North, area);
        _state.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(Interval), _wave));
        _state.ClearHistory();

        AddAreaTint();
        AddWall(Sq("e8"));
        var resolver = new PieceVisualResolver();
        foreach (var p in _state.PiecesOnBoard())
        {
            var node = PieceVisualAssembler.Assemble(resolver.Resolve(p));
            node.Position = Center(p.Square);
            AddChild(node);
            _pieces[p.Id] = node;
        }

        // From the east side, so the north edge (and everything thrown over it) is on screen right.
        var camera = new Camera3D { Fov = 46, Current = true };
        AddChild(camera);
        camera.Position = new Vector3(12.5f, 9.5f, 0.8f);
        camera.LookAt(new Vector3(0, 0, -3.0f));

        var hud = new CanvasLayer { Layer = 2 };
        root.CallDeferred(Node.MethodName.AddChild, hud);
        var box = new VBoxContainer { Position = new Vector2(28, 22) };
        hud.AddChild(box);
        box.AddChild(MakeLabel("Tidal Wave · from the north · top 3 ranks · every 2 turns", 26, Colors.White));
        _counter = MakeLabel("", 22, new Color("bfe3ff"));
        box.AddChild(_counter);
        _caption = MakeLabel("", 22, new Color("ffe08a"));
        box.AddChild(_caption);

        _ = Run();
    }

    private async Task Run()
    {
        UpdateCounter();
        await Wait(1.2);
        foreach (var move in new[] { "e1d1", "h4h5", "d1e1", "h5h4" })
        {
            _caption.Text = "";
            var m = MoveText.FindLegal(_state, move) ?? throw new InvalidOperationException($"{move} is not legal");
            var result = Game.PlayTurn(_state, m, new ResolveOptions(Trace: true));
            foreach (var resolution in result.Trace!.Resolutions.Where(r => !r.RolledBack))
                foreach (var wave in resolution.Waves)
                    await Animate(wave);
            UpdateCounter();
            await Wait(0.7);
        }
        _caption.Text = "";
        await Wait(1.5);
        GetTree().Quit();
    }

    private async Task Animate(WaveTrace wave)
    {
        var tidal = wave.Facts.Where(f => f.Cause == TidalWaveEffect.Cause).ToList();
        if (tidal.Count == 0)
        {
            foreach (var f in wave.Facts.Where(f => f.Kind == FactKinds.Moved && _pieces.ContainsKey(f.Piece!.Id)))
                Hop(_pieces[f.Piece!.Id], Center(f.To));
            if (wave.Facts.Any(f => f.Kind == FactKinds.Moved)) await Wait(0.55);
            return;
        }

        _caption.Text = "TIDAL WAVE!";
        // Pieces still on an affected square after the wave were held back (by the wall, or by a held piece).
        var moved = tidal.Where(f => f.Kind == FactKinds.Moved).Select(f => f.Piece!.Id).ToHashSet();
        var held = _wave.Squares.Select(q => _state.PieceAt(q.X, q.Y)).Where(p => p != null && !moved.Contains(p.Id)).ToList();

        var crest = MakeCrest();
        var sheet = MakeSheet();
        float north = -4 * Cell - 1.4f, areaSouth = -1.5f * Cell + 0.35f;
        crest.Position = new Vector3(0, 0.25f, north);
        var rush = CreateTween();
        rush.TweenProperty(crest, "position:z", areaSouth, 0.75).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        rush.Parallel().TweenProperty(sheet, "transparency", 0.25f, 0.6);
        await ToSignal(rush, Tween.SignalName.Finished);
        await Wait(0.15);

        // Recede: the crest pulls back north and drags everything with it.
        var recede = CreateTween();
        recede.TweenProperty(crest, "position:z", north - 1.5f, 1.1).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        recede.Parallel().TweenProperty(sheet, "transparency", 1f, 1.2);
        int saved = 0, washed = 0, pulled = 0;
        foreach (var f in tidal)
        {
            if (!_pieces.TryGetValue(f.Piece!.Id, out var node)) continue;
            if (f.Kind == FactKinds.Moved) { Slide(node, Center(f.To)); pulled++; }
            else if (f.Kind == FactKinds.Died) { WashOut(node); _pieces.Remove(f.Piece.Id); washed++; }
        }
        foreach (var p in held)
        {
            Wobble(_pieces[p!.Id]);
            saved++;
            int ahead = _state.Sq(_state.FileOf(p.Square), _state.RankOf(p.Square) + 1);
            if (_state.InBounds(_state.FileOf(p.Square), _state.RankOf(p.Square) + 1) && _state.TileAt(ahead).Def == WallTile.Instance)
                Splash(Center(ahead));
        }
        _caption.Text = $"TIDAL WAVE!  {washed} washed off · {pulled} dragged north · {saved} held back by the wall";
        await ToSignal(recede, Tween.SignalName.Finished);
        crest.QueueFree();
        sheet.QueueFree();
        await Wait(1.1);
    }

    // ------------------------------------------------------------------ piece motion

    private void Hop(Node3D node, Vector3 to)
    {
        var t = CreateTween();
        var mid = (node.Position + to) / 2 + Vector3.Up * 0.6f;
        t.TweenProperty(node, "position", mid, 0.22).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        t.TweenProperty(node, "position", to, 0.22).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
    }

    private void Slide(Node3D node, Vector3 to)
    {
        var t = CreateTween();
        t.TweenInterval(0.15);
        t.TweenProperty(node, "position", to, 0.75).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);
        t.Parallel().TweenProperty(node, "rotation:x", -0.18f, 0.35).SetTrans(Tween.TransitionType.Sine);
        t.TweenProperty(node, "rotation:x", 0f, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    /// <summary>Thrown over the north edge: an arc out past the board, a tumble, then it sinks into the foam.</summary>
    private void WashOut(Node3D node)
    {
        var start = node.Position;
        var land = start + new Vector3((GD.Randf() - 0.5f) * 0.8f, 0, -2.4f);
        var t = CreateTween();
        t.TweenInterval(0.1);
        t.TweenMethod(Callable.From<float>(k =>
        {
            node.Position = start.Lerp(land, k) + Vector3.Up * (3.2f * k * (1 - k) * 2);
            node.Rotation = new Vector3(-k * 1.57f * 1.0f, k * 0.9f, 0);
        }), 0f, 1f, 0.8).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        t.TweenProperty(node, "position:y", -1.6f, 0.9).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        t.Parallel().TweenProperty(node, "scale", new Vector3(0.7f, 0.7f, 0.7f), 0.9);
        t.TweenCallback(Callable.From(node.QueueFree));
        Splash(land + Vector3.Up * 0.1f, delay: 0.85);
    }

    private void Wobble(Node3D node)
    {
        var t = CreateTween();
        t.TweenInterval(0.2);
        foreach (float a in new[] { -0.22f, 0.14f, -0.08f, 0f })
            t.TweenProperty(node, "rotation:x", a, 0.16).SetTrans(Tween.TransitionType.Sine);
    }

    // ------------------------------------------------------------------ water

    /// <summary>The wave front: a foam roll on top of a wedge of water trailing behind it (to the north).</summary>
    private Node3D MakeCrest()
    {
        var crest = new Node3D();
        AddChild(crest);
        crest.AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.42f, Height = 8 * Cell + 1.2f },
            RotationDegrees = new Vector3(0, 0, 90),
            Scale = new Vector3(1, 1, 1.5f),
            MaterialOverride = Translucent(Foam, emission: 0.25f),
        });
        crest.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(8 * Cell + 1.2f, 0.4f, 2.2f) },
            Position = new Vector3(0, -0.1f, -1.2f),
            MaterialOverride = Translucent(Water, emission: 0.1f),
        });
        return crest;
    }

    /// <summary>A sheet of water over the affected ranks, faded in as the wave rushes over them.</summary>
    private MeshInstance3D MakeSheet()
    {
        var sheet = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(8 * Cell + 0.2f, 0.18f, 3 * Cell + 2.4f) },
            Position = new Vector3(0, 0.08f, -2.5f * Cell - 1.0f),
            MaterialOverride = Translucent(Water, emission: 0.05f),
            Transparency = 1f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(sheet);
        return sheet;
    }

    private void Splash(Vector3 at, double delay = 0.25)
    {
        for (int i = 0; i < 9; i++)
        {
            var drop = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.12f, Height = 0.24f },
                MaterialOverride = Translucent(Foam, emission: 0.3f),
                Position = at + Vector3.Up * 0.4f,
                Scale = Vector3.Zero,
            };
            AddChild(drop);
            float angle = i * Mathf.Tau / 9 + GD.Randf() * 0.4f;
            var dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (0.5f + GD.Randf() * 0.4f);
            var start = drop.Position;
            var t = CreateTween();
            t.TweenInterval(delay);
            t.TweenProperty(drop, "scale", Vector3.One, 0.05);
            t.TweenMethod(Callable.From<float>(k => drop.Position = start + dir * k + Vector3.Up * (1.4f * k * (1 - k) * 2)), 0f, 1f, 0.55);
            t.Parallel().TweenProperty(drop, "transparency", 1f, 0.55);
            t.TweenCallback(Callable.From(drop.QueueFree));
        }
    }

    // ------------------------------------------------------------------ static dressing

    private void AddAreaTint()
    {
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(8 * Cell, 3 * Cell) },
            Position = new Vector3(0, 0.012f, -2.5f * Cell),
            MaterialOverride = Translucent(new Color(0.25f, 0.6f, 0.95f, 0.16f), emission: 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        // A foam-white tide line along the area's south edge.
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(8 * Cell, 0.06f) },
            Position = new Vector3(0, 0.014f, -1 * Cell),
            MaterialOverride = Translucent(new Color(0.8f, 0.93f, 1f, 0.55f), emission: 0f),
        });
    }

    private void AddWall(int sq)
    {
        var stone = new StandardMaterial3D { AlbedoColor = new Color("5f574e"), Roughness = 0.95f };
        var wall = new Node3D { Position = Center(sq) };
        AddChild(wall);
        for (int layer = 0; layer < 4; layer++)
            for (int i = 0; i < 2; i++)
            {
                float offset = (layer % 2 == 0 ? -0.33f : -0.2f) + i * 0.66f;
                wall.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(layer % 2 == 0 ? 0.64f : 0.6f, 0.3f, 1.3f) },
                    Position = new Vector3(offset, 0.16f + layer * 0.31f, 0), RotationDegrees = new Vector3(0, (layer * 7 + i * 3) % 5 - 2, 0),
                    MaterialOverride = stone,
                });
            }
    }

    // ------------------------------------------------------------------ helpers

    private int Sq(string name) => MoveText.ParseSquare(_state, name);

    private Vector3 Center(int sq) => new((_state.FileOf(sq) - 3.5f) * Cell, 0, (3.5f - _state.RankOf(sq)) * Cell);

    private void UpdateCounter()
    {
        int turn = _state.Ply + 1;
        int next = (turn + Interval - 1) / Interval * Interval;
        _counter.Text = $"Turn {turn} · {(next == turn ? "the wave comes at the end of this turn" : $"next wave in {next - turn + 1} turns")}";
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        label.AddThemeConstantOverride("outline_size", 6);
        return label;
    }

    private static StandardMaterial3D Translucent(Color color, float emission) => new()
    {
        AlbedoColor = color,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        Roughness = 0.15f,
        Metallic = 0.1f,
        EmissionEnabled = emission > 0,
        Emission = new Color(color.R, color.G, color.B),
        EmissionEnergyMultiplier = emission,
    };

    private SignalAwaiter Wait(double seconds) => ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
}
