using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Godot;
using RogueChess.Engine;
using RogueChess.Engine.Content;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;
using Side = RogueChess.Engine.Core.Side;

namespace GodotChess.Sandbox;

/// <summary>
/// Debug sandbox: load a scenario (maps/sandbox, plus the legacy maps that load), play both sides, edit the
/// position (place pieces, set tiles, toggle abilities, erase), undo anything, and see what each turn did:
/// wave-numbered arrows on the board and the full resolution trace in the side panel.
/// </summary>
public partial class Sandbox : Control
{
    private enum Tool { Play, Place, Tile, Ability, Erase }

    private readonly Catalog _catalog = ContentLibrary.CreateCatalog();
    private readonly List<Scenario> _scenarios = new();
    private readonly List<int> _undoMarks = new();
    private readonly List<Move> _legal = new();

    private GameState _state = null!;
    private Scenario _current = null!;
    private BoardView _board = null!;
    private Label _title = null!, _description = null!, _status = null!, _hint = null!;
    private RichTextLabel _trace = null!;

    private Tool _tool = Tool.Play;
    private Side _placeSide = Side.White;
    private PieceDef _placeDef = StandardPieces.Pawn;
    private TileDef _tileDef = TileDef.Standard;
    private AbilityDef? _abilityDef;
    private readonly Dictionary<Tool, Button> _toolButtons = new();

    private sealed record Scenario(string Name, string Description, string Path);

    public override void _Ready()
    {
        DiscoverScenarios();
        BuildUi();
        LoadScenario(_scenarios[0]);
    }

    // ------------------------------------------------------------------ scenarios

    private void DiscoverScenarios()
    {
        foreach (var dir in new[] { "res://maps/sandbox", "res://maps/legacy" })
        {
            foreach (var file in DirAccess.GetFilesAt(dir).Where(f => f.EndsWith(".json")).OrderBy(f => f, StringComparer.Ordinal))
            {
                string path = $"{dir}/{file}";
                string json = FileAccess.GetFileAsString(path);
                try
                {
                    MapLoader.Load(json, _catalog); // only list maps whose content is ported
                }
                catch (MapLoadException)
                {
                    continue;
                }
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                bool legacy = dir.EndsWith("legacy");
                string name = root.TryGetProperty("name", out var n) ? n.GetString()! : file[..^5];
                string description = root.TryGetProperty("description", out var d) ? d.GetString()! : "Map from the TypeScript prototype.";
                _scenarios.Add(new Scenario(legacy ? $"Legacy: {name}" : name, description, path));
            }
        }
    }

    private void LoadScenario(Scenario scenario)
    {
        _current = scenario;
        _state = MapLoader.Load(FileAccess.GetFileAsString(scenario.Path), _catalog);
        Game.BeginGame(_state);
        _state.ClearHistory();
        _undoMarks.Clear();
        _title.Text = scenario.Name;
        _description.Text = scenario.Description;
        _trace.Text = "";
        _board.State = _state;
        _board.Markers.Clear();
        ClearSelection();
        GD.Print($"[Sandbox] loaded '{scenario.Name}' ({_state.Width}x{_state.Height}, {_state.Rules} rules)");
        Refresh();
    }

    // ------------------------------------------------------------------ clicks

    private void OnSquareClicked(int sq)
    {
        switch (_tool)
        {
            case Tool.Play:
                ClickPlay(sq);
                break;
            case Tool.Place:
                Edit(() =>
                {
                    if (_state.PieceAt(sq) is { } old) _state.RemoveFromBoard(old);
                    _state.Place(_placeDef, _placeSide, sq);
                });
                break;
            case Tool.Tile:
                Edit(() => _state.SetTile(sq, _tileDef));
                break;
            case Tool.Ability:
                if (_abilityDef != null && _state.PieceAt(sq) is { } piece)
                {
                    var def = _abilityDef;
                    Edit(() =>
                    {
                        if (piece.HasAbility(def)) _state.RevokeAbility(piece, def);
                        else _state.GrantAbility(piece, def);
                    });
                }
                break;
            case Tool.Erase:
                if (_state.PieceAt(sq) is { } victim) Edit(() => _state.RemoveFromBoard(victim));
                else if (!_state.TileAt(sq).Def.IsDefault) Edit(() => _state.SetTile(sq, TileDef.Standard));
                break;
        }
    }

    private void ClickPlay(int sq)
    {
        if (_board.Selected >= 0 && (_board.QuietTargets.Contains(sq) || _board.CaptureTargets.Contains(sq)))
        {
            // Promotion: the first option (a queen in both rule sets).
            var move = _legal.First(m => m.From == _board.Selected && m.To == sq);
            PlayMove(move);
            return;
        }

        ClearSelection();
        var piece = _state.PieceAt(sq);
        bool over = _state.Rules.Evaluate(_state) != GameOutcome.Ongoing;
        if (piece != null && piece.Side == _state.SideToMove && !over)
        {
            _board.Selected = sq;
            _legal.Clear();
            MoveGenerator.GenerateLegal(_state, _legal);
            foreach (var m in _legal.Where(m => m.From == sq))
                (m.IsCapture ? _board.CaptureTargets : _board.QuietTargets).Add(m.To);
        }
        Refresh(piece);
    }

    private void PlayMove(Move move)
    {
        string text = MoveText.Format(_state, move);
        var side = _state.SideToMove;
        int mark = _state.UndoDepth;
        var result = Game.PlayTurn(_state, move, new ResolveOptions(Trace: true));
        ClearSelection();

        if (!result.Accepted)
        {
            _trace.Text = $"{side} {text} was rejected: {result.RejectReason}";
            GD.Print($"[Sandbox] {side} {text} REJECTED: {result.RejectReason}");
            Refresh();
            return;
        }

        _undoMarks.Add(mark);
        ShowTrace(result.Trace!, $"{side} {text}");
        Refresh();
    }

    private void Edit(Action change)
    {
        _undoMarks.Add(_state.UndoDepth);
        change();
        _board.Markers.Clear();
        ClearSelection();
        Refresh();
    }

    private void Undo()
    {
        if (_undoMarks.Count == 0) return;
        _state.UndoTo(_undoMarks[^1]);
        _undoMarks.RemoveAt(_undoMarks.Count - 1);
        _board.Markers.Clear();
        _trace.Text = "(undone)";
        ClearSelection();
        Refresh();
    }

    private void ClearSelection()
    {
        _board.Selected = -1;
        _board.QuietTargets.Clear();
        _board.CaptureTargets.Clear();
    }

    // ------------------------------------------------------------------ trace + status

    private void ShowTrace(TurnTrace trace, string heading)
    {
        _board.Markers.Clear();
        int wave = 0;
        foreach (var r in trace.Resolutions.Where(r => !r.RolledBack))
        {
            foreach (var w in r.Waves)
            {
                if (w.Facts.Count == 0) continue;
                foreach (var f in w.Facts)
                {
                    BoardView.Marker? marker = f.Kind switch
                    {
                        FactKinds.Moved => new(BoardView.MarkerKind.Move, f.From, f.To, wave),
                        FactKinds.Collided => new(BoardView.MarkerKind.Collide, f.From, f.To, wave),
                        FactKinds.Died => new(BoardView.MarkerKind.Death, f.From, f.From, wave),
                        FactKinds.Spawned => new(BoardView.MarkerKind.Spawn, f.To, f.To, wave),
                        FactKinds.TileChanged => new(BoardView.MarkerKind.Tile, f.From, f.From, wave),
                        FactKinds.PieceChanged when f.Piece is { IsOnBoard: true } => new(BoardView.MarkerKind.Change, f.Piece.Square, f.Piece.Square, wave),
                        _ => null,
                    };
                    if (marker is { } m) _board.Markers.Add(m);
                }
                wave++;
            }
        }

        string text = $"{heading}\n{trace.Format(_state).TrimEnd()}";
        _trace.Text = text;
        GD.Print($"[Sandbox] {text}");
    }

    private void Refresh(Piece? inspected = null)
    {
        var outcome = _state.Rules.Evaluate(_state);
        string result = outcome switch
        {
            GameOutcome.WhiteWins => "WHITE WINS",
            GameOutcome.BlackWins => "BLACK WINS",
            GameOutcome.Draw => "DRAW",
            _ => $"{_state.SideToMove} to move",
        };
        _status.Text = $"{result}   ·   {_state.Rules} rules   ·   ply {_state.Ply}   ·   undo steps {_undoMarks.Count}";

        _hint.Text = inspected != null ? Describe(inspected) : _tool switch
        {
            Tool.Play when outcome != GameOutcome.Ongoing => "Game over. Undo, Restart, or edit the position to keep playing.",
            Tool.Play => "Click a piece of the side to move, then a highlighted square.",
            Tool.Place => $"Click a square to place a {_placeSide} {_placeDef.Name} (replaces what's there).",
            Tool.Tile => $"Click a square to make it {_tileDef.Name}.",
            Tool.Ability => _abilityDef == null ? "Pick an ability below." : $"Click a piece to toggle {_abilityDef.Name}.",
            _ => "Click a piece to remove it, or an empty special tile to reset it.",
        };

        foreach (var (tool, button) in _toolButtons) button.SetPressedNoSignal(tool == _tool);
        _board.QueueRedraw();
    }

    private string Describe(Piece p)
    {
        var sb = new StringBuilder($"{p.Side} {p.Def.Name} #{p.Id} on {MoveText.SquareName(_state, p.Square)}, value {p.Value}");
        foreach (var a in p.Abilities) sb.Append($"   ·   {a}");
        var tile = _state.TileAt(p.Square);
        if (!tile.Def.IsDefault) sb.Append($"   ·   tile: {tile}");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ UI construction

    private void BuildUi()
    {
        var bg = new ColorRect { Color = new Color(0.16f, 0.17f, 0.2f) };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var root = new HBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 16);
        AddChild(root);

        _board = new BoardView { Name = "Board" };
        _board.SquareClicked += OnSquareClicked;
        root.AddChild(_board);

        var panel = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(620, 0) };
        panel.AddThemeConstantOverride("separation", 6);
        root.AddChild(panel);

        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", 24);
        panel.AddChild(_title);
        _description = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(600, 0) };
        panel.AddChild(_description);
        _status = new Label { Name = "Status" };
        _status.AddThemeColorOverride("font_color", new Color(0.6f, 0.9f, 1f));
        panel.AddChild(_status);

        var scenarios = Section(panel, "Scenarios");
        foreach (var sc in _scenarios) AddButton(scenarios, sc.Name, () => LoadScenario(sc));

        var tools = Section(panel, "Tool");
        var toolGroup = new ButtonGroup();
        foreach (var tool in Enum.GetValues<Tool>())
        {
            var b = AddButton(tools, tool.ToString(), () => SetTool(tool), toolGroup);
            _toolButtons[tool] = b;
        }

        var pieces = Section(panel, "Place: side and piece");
        var sideGroup = new ButtonGroup();
        AddButton(pieces, "White", () => { _placeSide = Side.White; SetTool(Tool.Place); }, sideGroup).SetPressedNoSignal(true);
        AddButton(pieces, "Black", () => { _placeSide = Side.Black; SetTool(Tool.Place); }, sideGroup);
        var pieceGroup = new ButtonGroup();
        foreach (var def in _catalog.Pieces)
        {
            var d = def;
            var b = AddButton(pieces, d.Name, () => { _placeDef = d; SetTool(Tool.Place); }, pieceGroup);
            if (d == _placeDef) b.SetPressedNoSignal(true);
        }

        var tiles = Section(panel, "Tile");
        var tileGroup = new ButtonGroup();
        foreach (var def in _catalog.Tiles)
        {
            var d = def;
            AddButton(tiles, d.Name, () => { _tileDef = d; SetTool(Tool.Tile); }, tileGroup);
        }

        var abilities = Section(panel, "Ability (click a piece to toggle)");
        var abilityGroup = new ButtonGroup();
        foreach (var def in _catalog.Abilities)
        {
            var d = def;
            var b = AddButton(abilities, d.Name, () => { _abilityDef = d; SetTool(Tool.Ability); }, abilityGroup);
            b.AddThemeColorOverride("font_color", BoardView.BadgeColor(d.Id).Lightened(0.3f));
        }

        var actions = Section(panel, "Actions");
        AddButton(actions, "Undo", Undo);
        AddButton(actions, "Restart", () => LoadScenario(_current));
        AddButton(actions, "Switch side to move", () => Edit(() => _state.SetSideToMove(_state.SideToMove.Opponent())));

        _hint = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(600, 0) };
        _hint.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.6f));
        panel.AddChild(_hint);

        panel.AddChild(new Label { Text = "Last turn (wave numbers match the board markers)" });
        _trace = new RichTextLabel { Name = "Trace", SizeFlagsVertical = SizeFlags.ExpandFill, ScrollFollowing = false, SelectionEnabled = true };
        _trace.AddThemeFontOverride("normal_font", new SystemFont { FontNames = new[] { "Consolas", "DejaVu Sans Mono", "Monospace" } });
        _trace.AddThemeFontSizeOverride("normal_font_size", 13);
        panel.AddChild(_trace);
    }

    private void SetTool(Tool tool)
    {
        _tool = tool;
        ClearSelection();
        Refresh();
    }

    private static HFlowContainer Section(Container parent, string title)
    {
        var label = new Label { Text = title };
        label.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f));
        parent.AddChild(label);
        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 4);
        flow.AddThemeConstantOverride("v_separation", 4);
        parent.AddChild(flow);
        return flow;
    }

    private static Button AddButton(Container parent, string text, Action onPressed, ButtonGroup? group = null)
    {
        var b = new Button { Text = text, Name = text.Replace(":", "").Replace(" ", "_") };
        if (group != null)
        {
            b.ToggleMode = true;
            b.ButtonGroup = group;
        }
        b.Pressed += onPressed;
        parent.AddChild(b);
        return b;
    }
}
