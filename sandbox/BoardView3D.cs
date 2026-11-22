using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GodotChess.Sandbox.Visuals;
using RogueChess.Engine.Core;

namespace GodotChess.Sandbox;

/// <summary>A projection of engine state. Input reports squares; only Sandbox changes the game.</summary>
public partial class BoardView3D : Control
{
    public event Action<int>? SquareClicked;
    public GameState? State { get; set; }
    public int Selected { get; set; } = -1;
    public HashSet<int> QuietTargets { get; } = new();
    public HashSet<int> CaptureTargets { get; } = new();
    public List<BoardView.Marker> Markers { get; } = new();
    private const float CellSize = 1.45f;
    private SubViewport _viewport = null!;
    private Node3D _world = null!, _board = null!;
    private Camera3D _camera = null!;
    private Control _overlay = null!;
    private PieceVisualResolver _resolver = null!;
    private readonly PieceSelectionHighlight _selectionHighlight = new();
    private GameState? _shownState;
    private readonly Dictionary<int, (Node3D Node, ResolvedPieceVisual Visual)> _pieces = new();
    private float _yaw, _elevation = 1.05f, _zoom = 1;
    private int _width, _height;
    private static readonly Color[] WaveColors = { Colors.Gold, Colors.Orange, Colors.Tomato, Colors.Orchid, Colors.MediumPurple, Colors.DeepSkyBlue };

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(880, 880);
        SizeFlagsVertical = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;
        _resolver = new PieceVisualResolver();
        _viewport = new SubViewport { Size = new Vector2I(880, 900), OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always, TransparentBg = false };
        AddChild(_viewport);
        _world = new Node3D();
        _viewport.AddChild(_world);
        _world.AddChild(new WorldEnvironment { Environment = new Godot.Environment {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("29313a"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White,
            AmbientLightEnergy = 0.7f } });
        _world.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -30, 0), LightEnergy = 1.2f });
        _camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Current = true };
        _world.AddChild(_camera);
        var texture = new TextureRect { Texture = _viewport.GetTexture(), MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
        texture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(texture);
        _overlay = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _overlay.Draw += DrawOverlay;
        AddChild(_overlay);
        var buttons = new HBoxContainer { Position = new Vector2(14, 12) };
        AddChild(buttons);
        foreach (var (label, yaw, elevation) in new[] { ("White view", 0f, 1.05f), ("Black view", Mathf.Pi, 1.05f), ("Overhead", 0f, 1.56f) })
        {
            var button = new Button { Text = label };
            button.Pressed += () => { _yaw = yaw; _elevation = elevation; _zoom = 1; UpdateCamera(); };
            buttons.AddChild(button);
        }
        foreach (var (label, angle) in new[] { ("Rotate left", -Mathf.Pi / 4), ("Rotate right", Mathf.Pi / 4) })
        {
            var button = new Button { Text = label, TooltipText = "Rotate 45° around the board. You can also right-drag or middle-drag." };
            button.Pressed += () => { _yaw += angle; UpdateCamera(); };
            buttons.AddChild(button);
        }
        Resized += ResizeViewport;
        ResizeViewport();
    }

    private void ResizeViewport()
    {
        _viewport.Size = new Vector2I(Math.Max(1, (int)Size.X), Math.Max(1, (int)Size.Y));
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        if (_camera == null) return;
        _camera.Position = new Vector3(Mathf.Sin(_yaw) * Mathf.Cos(_elevation), Mathf.Sin(_elevation), Mathf.Cos(_yaw) * Mathf.Cos(_elevation)) * 24;
        _camera.LookAt(Vector3.Zero);
        // Camera size is the vertical span; allow space for tall pieces and rectangular boards.
        float aspect = (float)_viewport.Size.X / _viewport.Size.Y;
        _camera.Size = Math.Max((_width * CellSize + 3) / aspect, _height * CellSize + 4) * _zoom;
        _overlay?.QueueRedraw();
    }

    public Vector3 SquareCenter(int sq) => new(
        (State!.FileOf(sq) - (State.Width - 1) * 0.5f) * CellSize, 0,
        ((State.Height - 1) * 0.5f - State.RankOf(sq)) * CellSize);

    public void RefreshVisuals()
    {
        if (!IsNodeReady() || State == null) return;
        _selectionHighlight.Clear();
        if (!ReferenceEquals(State, _shownState))
        {
            foreach (var entry in _pieces.Values) RemoveVisual(entry.Node);
            _pieces.Clear();
            _shownState = State;
        }
        if (_board == null || _width != State.Width || _height != State.Height)
        {
            if (_board != null) RemoveVisual(_board);
            _width = State.Width; _height = State.Height;
            _board = GD.Load<PackedScene>("res://assets/boards/wood/WoodBoard.tscn").Instantiate<Node3D>();
            _board.Set("board_width", _width); _board.Set("board_height", _height);
            _world.AddChild(_board);
            UpdateCamera();
        }
        var alive = new HashSet<int>();
        for (int sq = 0; sq < State.Area; sq++)
        {
            if (State.PieceAt(sq) is not { } piece) continue;
            alive.Add(piece.Id);
            var visual = _resolver.Resolve(piece);
            if (!_pieces.TryGetValue(piece.Id, out var entry) || entry.Visual != visual)
            {
                var node = PieceVisualAssembler.Assemble(visual);
                if (entry.Node != null)
                {
                    // Preserve the piece's visual identity when its recipe changes.
                    foreach (var child in entry.Node.GetChildren()) RemoveVisual(child);
                    foreach (var child in node.GetChildren())
                    {
                        node.RemoveChild(child);
                        entry.Node.AddChild(child);
                    }
                    node.Free();
                    node = entry.Node;
                }
                else
                {
                    node.Name = $"Piece_{piece.Id}";
                    _world.AddChild(node);
                }
                // Model bounds supply picking geometry, including custom scenes and mixed assemblies.
                AddPicking(node, node, Transform3D.Identity, piece.Id);
                entry = (node, visual);
                _pieces[piece.Id] = entry;
            }
            entry.Node.Position = SquareCenter(sq);
            entry.Node.SetMeta("square", sq);
            if (sq == Selected) _selectionHighlight.Apply(entry.Node);
        }
        foreach (int id in _pieces.Keys.Where(id => !alive.Contains(id)).ToArray())
        {
            RemoveVisual(_pieces[id].Node);
            _pieces.Remove(id);
        }
        _overlay.QueueRedraw();
    }

    private static void RemoveVisual(Node node)
    {
        node.GetParent().RemoveChild(node);
        node.QueueFree();
    }

    private static void AddPicking(Node3D root, Node node, Transform3D transform, int id)
    {
        if (node != root && node is Node3D spatial) transform *= spatial.Transform;
        // Snapshot before adding collision children.
        foreach (var child in node.GetChildren()) AddPicking(root, child, transform, id);
        if (node is not MeshInstance3D { Mesh: not null } mesh || node.Name == "ContactShadow") return;
        var bounds = mesh.GetAabb();
        if (bounds.Size.LengthSquared() < 0.00001f) return;
        var body = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0, Transform = transform };
        body.SetMeta("piece_id", id);
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = bounds.Size }, Position = bounds.GetCenter() });
        root.AddChild(body);
    }

    public int PickSquare(Vector2 point)
    {
        if (State == null) return -1;
        var from = _camera.ProjectRayOrigin(point);
        var direction = _camera.ProjectRayNormal(point);
        var query = PhysicsRayQueryParameters3D.Create(from, from + direction * 100, 1);
        var hit = _world.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count > 0 && hit["collider"].AsGodotObject() is Node collider && collider.HasMeta("piece_id"))
        {
            int id = collider.GetMeta("piece_id").AsInt32();
            if (_pieces.TryGetValue(id, out var entry)) return entry.Node.GetMeta("square").AsInt32();
        }
        if (Math.Abs(direction.Y) < 0.0001f) return -1;
        float distance = -from.Y / direction.Y;
        if (distance < 0) return -1;
        var at = from + direction * distance;
        int file = Mathf.FloorToInt(at.X / CellSize + State.Width * 0.5f);
        int rank = Mathf.FloorToInt(State.Height * 0.5f - at.Z / CellSize);
        return State.InBounds(file, rank) ? State.Sq(file, rank) : -1;
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion motion && (motion.ButtonMask & (MouseButtonMask.Right | MouseButtonMask.Middle)) != 0)
        {
            _yaw -= motion.Relative.X * 0.008f;
            _elevation = Mathf.Clamp(_elevation + motion.Relative.Y * 0.006f, 0.6f, 1.56f);
            UpdateCamera(); AcceptEvent();
        }
        if (e is not InputEventMouseButton { Pressed: true } button) return;
        if (button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            _zoom = Mathf.Clamp(_zoom * (button.ButtonIndex == MouseButton.WheelUp ? 0.9f : 1.1f), 0.7f, 1.8f);
            UpdateCamera(); AcceptEvent();
        }
        if (button.ButtonIndex == MouseButton.Left && PickSquare(button.Position) is var sq && sq >= 0)
        { SquareClicked?.Invoke(sq); AcceptEvent(); }
    }

    private Vector2 Project(Vector3 point) => _camera.UnprojectPosition(point);

    private void Outline(int sq, Color color, float inset = 0.06f)
    {
        float h = CellSize * 0.5f - inset;
        var c = SquareCenter(sq) + Vector3.Up * 0.02f;
        var points = new[] { Project(c + new Vector3(-h, 0, -h)), Project(c + new Vector3(h, 0, -h)),
            Project(c + new Vector3(h, 0, h)), Project(c + new Vector3(-h, 0, h)), Project(c + new Vector3(-h, 0, -h)) };
        _overlay.DrawPolyline(points, color, 3, true);
    }

    private void Text(Vector2 at, string text, Color color, int size = 15)
    {
        _overlay.DrawStringOutline(ThemeDB.FallbackFont, at, text, HorizontalAlignment.Left, -1, size, 5, Colors.Black);
        _overlay.DrawString(ThemeDB.FallbackFont, at, text, HorizontalAlignment.Left, -1, size, color);
    }

    private void DrawOverlay()
    {
        if (State == null) return;
        for (int sq = 0; sq < State.Area; sq++)
        {
            var c = SquareCenter(sq);
            var tile = State.TileAt(sq);
            if (!tile.Def.IsDefault)
            {
                var color = tile.Def.Id switch { "wall" => Colors.Silver, "guardian" => Colors.Gold, "fog" => Colors.LightSteelBlue, _ => Colors.Orchid };
                Outline(sq, color, 0.13f);
                Text(Project(c + new Vector3(-0.55f, 0.03f, -0.4f)), tile.Def.Name, color, 12);
            }
            if (State.PieceAt(sq) is { } piece && piece.Abilities.Count > 0)
            {
                var at = Project(c + new Vector3(-0.48f, 0.04f, 0.52f));
                foreach (var ability in piece.Abilities)
                {
                    string badge = BoardView.BadgeCode(ability.Def.Id, ability.Def.Name);
                    for (int v = 0; v < ability.VarCount; v++) if (ability.Var(v) != 0) badge += $":{ability.Var(v)}";
                    Text(at, badge, BoardView.BadgeColor(ability.Def.Id).Lightened(0.35f), 14);
                    at.X += ThemeDB.FallbackFont.GetStringSize(badge, fontSize: 14).X + 6;
                }
            }
        }
        foreach (int sq in QuietTargets) _overlay.DrawCircle(Project(SquareCenter(sq)), 7, Colors.LimeGreen);
        foreach (int sq in CaptureTargets) Outline(sq, Colors.OrangeRed);
        foreach (var marker in Markers)
        {
            if (marker.From < 0 || marker.To < 0 || marker.From >= State.Area || marker.To >= State.Area) continue;
            var color = WaveColors[marker.Wave % WaveColors.Length];
            var from = Project(SquareCenter(marker.From));
            var to = Project(SquareCenter(marker.To));
            if (marker.Kind is BoardView.MarkerKind.Move or BoardView.MarkerKind.Collide && from.DistanceTo(to) > 1)
            {
                var dir = (to - from).Normalized();
                var normal = new Vector2(-dir.Y, dir.X);
                _overlay.DrawLine(from, to, color, 3, true);
                _overlay.DrawColoredPolygon(new[] { to, to - dir * 15 + normal * 7, to - dir * 15 - normal * 7 }, color);
            }
            else Outline(marker.To, color, 0.19f);
            Text(to + new Vector2(12, -12), $"{marker.Wave}:{marker.Kind}", color, 12);
        }
        for (int file = 0; file < State.Width; file++)
            Text(Project(SquareCenter(State.Sq(file, 0)) + new Vector3(0, 0, 0.95f)), ((char)('a' + file)).ToString(), Colors.White);
        for (int rank = 0; rank < State.Height; rank++)
            Text(Project(SquareCenter(State.Sq(0, rank)) + new Vector3(-0.95f, 0, 0)), (rank + 1).ToString(), Colors.White);
        Text(new Vector2(16, Size.Y - 18), "Right / middle-drag to rotate · Scroll to zoom", Colors.LightGray, 14);
    }
}
