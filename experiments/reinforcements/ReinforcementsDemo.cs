using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using GodotChess.Sandbox.Visuals;
using RogueChess.Engine.Content;
using RogueChess.Engine.Content.BoardEffects;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;
using Side = RogueChess.Engine.Core.Side;

namespace GodotChess.Experiments;

/// <summary>
/// Reinforcements demo: the real engine plays four scripted turns while Black's Soldier roster
/// (Pawn, Knight, Bishop; Marksman, Piercing) sends reinforcements every 2 turns onto b6–g7. Each spawned piece drops
/// in from above, and a tag names any ability it came with. The second wave only fills the squares left free.
/// Render: Godot --path . --resolution 1280x720 --fixed-fps 30 --write-movie out\f.png res://experiments/reinforcements/ReinforcementsDemo.tscn
/// </summary>
public partial class ReinforcementsDemo : Node3D
{
    private const float Cell = 1.45f;
    private const int Interval = 2;
    private const ulong Seed = 11;

    private static readonly Color Gold = new(1f, 0.82f, 0.35f);

    private GameState _state = null!;
    private ReinforcementsEffect _effect = null!;
    private readonly Dictionary<int, Node3D> _pieces = new();
    private readonly PieceVisualResolver _resolver = new();
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

        // A lone black king facing a white army; a black pawn already holds d6, so the drop skips it.
        _state = Fen.Parse("4k3/8/3p4/8/8/8/PPP2PPP/R3K2R w - - 0 1", RuleSet.Rogue);
        _state.SeedRandom(Seed);
        var catalog = ContentLibrary.CreateCatalog();
        var area = Enumerable.Range(5, 2).SelectMany(y => Enumerable.Range(1, 6).Select(x => (x, y))).ToArray();
        _effect = new ReinforcementsEffect(Side.Black, catalog.FindRoster("soldier")!, 8, 7, area, catalog);
        _state.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(Interval), _effect));
        _state.ClearHistory();

        AddAreaTint();
        foreach (var p in _state.PiecesOnBoard()) AddPiece(p, Center(p.Square));

        var camera = new Camera3D { Fov = 44, Current = true };
        AddChild(camera);
        camera.Position = new Vector3(0, 12.6f, 10.2f);
        camera.LookAt(new Vector3(0, 0, -0.2f));

        var hud = new CanvasLayer { Layer = 2 };
        root.CallDeferred(Node.MethodName.AddChild, hud);
        var box = new VBoxContainer { Position = new Vector2(28, 22) };
        hud.AddChild(box);
        box.AddChild(MakeLabel("Reinforcements · Soldier roster · Black · every 2 turns", 26, Colors.White));
        box.AddChild(MakeLabel("Pawn · Knight · Bishop  —  Marksman · Piercing  ·  piece value 8, ability value 7", 18, new Color("d8d8d8")));
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
        foreach (var move in new[] { "e1d1", "e8d8", "d1e1", "d8e8" })
        {
            _caption.Text = "";
            var m = MoveText.FindLegal(_state, move) ?? throw new InvalidOperationException($"{move} is not legal");
            var result = Game.PlayTurn(_state, m, new ResolveOptions(Trace: true));
            foreach (var resolution in result.Trace!.Resolutions.Where(r => !r.RolledBack))
                foreach (var wave in resolution.Waves)
                    await Animate(wave);
            UpdateCounter();
            await Wait(0.6);
        }
        _caption.Text = "";
        await Wait(1.6);
        GetTree().Quit();
    }

    private async Task Animate(WaveTrace wave)
    {
        var spawns = wave.Facts.Where(f => f.Kind == FactKinds.Spawned && f.Cause == ReinforcementsEffect.Cause).ToList();
        if (spawns.Count == 0)
        {
            foreach (var f in wave.Facts.Where(f => f.Kind == FactKinds.Moved && _pieces.ContainsKey(f.Piece!.Id)))
                Hop(_pieces[f.Piece!.Id], Center(f.To));
            if (wave.Facts.Any(f => f.Kind == FactKinds.Moved)) await Wait(0.55);
            return;
        }

        _caption.Text = "REINFORCEMENTS!";
        Flash();
        await Wait(0.35);

        // Most valuable first, the order the effect placed them in.
        var ordered = spawns.OrderByDescending(f => f.Piece!.Value).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            var f = ordered[i];
            var node = AddPiece(f.Piece!, Center(f.To) + Vector3.Up * 7f);
            Drop(node, Center(f.To), delay: i * 0.22);
        }
        await Wait(ordered.Count * 0.22 + 0.9);

        int free = _effect.Squares.Count(q => _state.PieceAt(q.X, q.Y) == null);
        string list = string.Join(", ", ordered.Select(f => Describe(f.Piece!)));
        _caption.Text = $"REINFORCEMENTS!  {ordered.Count} arrive: {list}" + (free == 0 ? "  ·  the area is full" : "");
        await Wait(2.0);
    }

    private static string Describe(Piece p) =>
        p.Abilities.Count == 0 ? p.Def.Name : $"{p.Def.Name} [{string.Join(", ", p.Abilities.Select(a => a.Def.Name))}]";

    // ------------------------------------------------------------------ pieces

    private Node3D AddPiece(Piece p, Vector3 at)
    {
        var node = PieceVisualAssembler.Assemble(_resolver.Resolve(p));
        node.Position = at;
        AddChild(node);
        _pieces[p.Id] = node;
        if (p.Abilities.Count > 0)
        {
            node.AddChild(new Label3D
            {
                Text = string.Join("\n", p.Abilities.Select(a => a.Def.Name)),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                // Staggered by file so neighbouring tags don't overlap.
                Position = new Vector3(0, _state.FileOf(p.Square) % 2 == 0 ? 2.1f : 2.75f, 0),
                FontSize = 40,
                OutlineSize = 12,
                Modulate = Gold,
                OutlineModulate = new Color(0, 0, 0, 0.85f),
                PixelSize = 0.006f,
                NoDepthTest = true,
            });
        }
        return node;
    }

    private void Hop(Node3D node, Vector3 to)
    {
        var t = CreateTween();
        var mid = (node.Position + to) / 2 + Vector3.Up * 0.6f;
        t.TweenProperty(node, "position", mid, 0.22).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        t.TweenProperty(node, "position", to, 0.22).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
    }

    /// <summary>Falls from above with a small bounce, kicks up a dust ring, and squashes a little on landing.</summary>
    private void Drop(Node3D node, Vector3 to, double delay)
    {
        node.Visible = false;
        var t = CreateTween();
        t.TweenInterval(delay);
        t.TweenCallback(Callable.From(() => node.Visible = true));
        t.TweenProperty(node, "position", to, 0.5).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        t.TweenCallback(Callable.From(() => Dust(to)));
        t.TweenProperty(node, "scale", new Vector3(1.15f, 0.82f, 1.15f), 0.07);
        t.TweenProperty(node, "scale", Vector3.One, 0.22).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    private void Dust(Vector3 at)
    {
        var ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.32f, OuterRadius = 0.5f },
            Position = at + Vector3.Up * 0.05f,
            MaterialOverride = Translucent(new Color(0.95f, 0.9f, 0.78f, 0.8f), emission: 0.3f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(ring);
        var t = CreateTween();
        t.TweenProperty(ring, "scale", new Vector3(2.1f, 0.4f, 2.1f), 0.45).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        t.Parallel().TweenProperty(ring, "transparency", 1f, 0.45);
        t.TweenCallback(Callable.From(ring.QueueFree));
    }

    // ------------------------------------------------------------------ the area

    private MeshInstance3D _tint = null!;

    private void AddAreaTint()
    {
        _tint = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(6 * Cell, 2 * Cell) },
            Position = new Vector3(0, 0.012f, -2 * Cell),
            MaterialOverride = Translucent(new Color(Gold.R, Gold.G, Gold.B, 0.16f), emission: 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_tint);
        // A thin gold outline around the drop zone.
        var line = Translucent(new Color(Gold.R, Gold.G, Gold.B, 0.7f), emission: 0.2f);
        foreach (var (pos, size) in new[]
                 {
                     (new Vector3(0, 0.014f, -1 * Cell), new Vector2(6 * Cell, 0.05f)),
                     (new Vector3(0, 0.014f, -3 * Cell), new Vector2(6 * Cell, 0.05f)),
                     (new Vector3(-3 * Cell, 0.014f, -2 * Cell), new Vector2(0.05f, 2 * Cell)),
                     (new Vector3(3 * Cell, 0.014f, -2 * Cell), new Vector2(0.05f, 2 * Cell)),
                 })
            AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = size }, Position = pos, MaterialOverride = line });
    }

    /// <summary>The drop zone glows while the reinforcements arrive.</summary>
    private void Flash()
    {
        var material = (StandardMaterial3D)_tint.MaterialOverride;
        var t = CreateTween();
        t.TweenProperty(material, "albedo_color:a", 0.5f, 0.3);
        t.TweenInterval(1.2);
        t.TweenProperty(material, "albedo_color:a", 0.16f, 0.6);
    }

    // ------------------------------------------------------------------ helpers

    private Vector3 Center(int sq) => new((_state.FileOf(sq) - 3.5f) * Cell, 0, (3.5f - _state.RankOf(sq)) * Cell);

    private void UpdateCounter()
    {
        int turn = _state.Ply + 1;
        int next = (turn + Interval - 1) / Interval * Interval;
        _counter.Text = $"Turn {turn} · {(next == turn ? "reinforcements arrive at the end of this turn" : $"next reinforcements in {next - turn + 1} turns")}";
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
