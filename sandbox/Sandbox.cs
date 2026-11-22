using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using RogueChess.Engine;
using RogueChess.Engine.AI;
using RogueChess.Engine.Content;
using RogueChess.Engine.Core;
using RogueChess.Engine.Encounters;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;
using Side = RogueChess.Engine.Core.Side;

namespace GodotChess.Sandbox;

/// <summary>
/// Debug sandbox: load a scenario (maps/sandbox, plus the legacy maps that load), play both sides, edit the
/// position (place pieces, set tiles, toggle abilities, erase), undo anything, and see what each turn did:
/// wave-numbered arrows on the board and the full resolution trace in the side panel.
/// Either side can be played by an AI profile (built-in presets plus res://ai/profiles.json); a map's
/// "ai" field picks the players when it loads. The AI thinks on a background thread on a cloned state.
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
    private BoardView3D _board = null!;
    private Label _title = null!, _description = null!, _status = null!, _hint = null!;
    private RichTextLabel _trace = null!;

    private Tool _tool = Tool.Play;
    private Side _placeSide = Side.White;
    private PieceDef _placeDef = StandardPieces.Pawn;
    private TileDef _tileDef = TileDef.Standard;
    private AbilityDef? _abilityDef;
    private readonly Dictionary<Tool, Button> _toolButtons = new();

    // AI players
    private const double AiMoveDelay = 0.35;
    private readonly AgentFactory _factory = new();
    private readonly Dictionary<Side, IChessAgent?> _players = new() { [Side.White] = null, [Side.Black] = null };
    private readonly Dictionary<Side, OptionButton> _playerPickers = new();
    private CheckBox _aiPaused = null!;
    private Task<AgentDecision>? _thinking;
    private CancellationTokenSource? _thinkCancel;
    private IChessAgent? _thinkingAgent;
    private ulong _thinkHash;
    private int _thinkDepth;
    private bool _forceAiMove;
    private double _sinceLastMove;

    // Roguelike encounters (res://encounters/prototype.json)
    private EncounterSet? _encounters;
    private (EncounterSpec Spec, ulong Seed)? _encounter;
    private ulong _encounterSeed = 1;

    private sealed record Scenario(string Name, string Description, string Path);

    public override void _Ready()
    {
        LoadAiProfiles();
        LoadEncounters();
        DiscoverScenarios();
        BuildUi();
        ApplyCommandLine();
    }

    /// <summary>
    /// Optional user args (after "--"): --encounter=&lt;id or name&gt; --seed=&lt;n&gt; --scenario=&lt;file name or title&gt; --white=&lt;profile|human&gt; --black=&lt;profile|human&gt;.
    /// Lets a headless run play AI-vs-AI games, e.g. godot --headless --path . --quit-after 600 -- --scenario=07 --white=club --black=aggressive
    /// </summary>
    private void ApplyCommandLine()
    {
        var args = OS.GetCmdlineUserArgs()
            .Select(a => a.TrimStart('-').Split('=', 2))
            .Where(kv => kv.Length == 2)
            .ToDictionary(kv => kv[0].ToLowerInvariant(), kv => kv[1]);

        var scenario = args.TryGetValue("scenario", out var wanted)
            ? _scenarios.FirstOrDefault(s => s.Path.Contains(wanted, StringComparison.OrdinalIgnoreCase)
                || s.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase)) ?? _scenarios[0]
            : _scenarios[0];
        if (args.TryGetValue("seed", out var seedText) && ulong.TryParse(seedText, out var seed)) _encounterSeed = seed;
        var encounter = args.TryGetValue("encounter", out var encId)
            ? _encounters?.Encounters.FirstOrDefault(e => e.Id.Equals(encId, StringComparison.OrdinalIgnoreCase)
                || e.Name.Equals(encId, StringComparison.OrdinalIgnoreCase))
            : null;
        if (encounter != null) LoadEncounter(encounter, _encounterSeed);
        else LoadScenario(scenario);

        foreach (var side in new[] { Side.White, Side.Black })
        {
            if (!args.TryGetValue(side.ToString().ToLowerInvariant(), out var id)) continue;
            try
            {
                SetPlayer(side, id.Equals("human", StringComparison.OrdinalIgnoreCase) ? null : _factory.GetProfile(id));
                GD.Print($"[Sandbox] {side} played by {id}");
            }
            catch (AgentConfigException e)
            {
                GD.PrintErr($"[Sandbox] {e.Message}");
            }
        }
        Refresh();
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

    private void LoadAiProfiles()
    {
        const string path = "res://ai/profiles.json";
        if (!FileAccess.FileExists(path)) return;
        try
        {
            var loaded = _factory.LoadProfiles(FileAccess.GetFileAsString(path));
            GD.Print($"[Sandbox] loaded {loaded.Count} AI profiles from {path}");
        }
        catch (Exception e) when (e is AgentConfigException or JsonException)
        {
            GD.PrintErr($"[Sandbox] {path}: {e.Message}");
        }
    }

    private void LoadEncounters()
    {
        const string path = "res://encounters/prototype.json";
        if (!FileAccess.FileExists(path)) return;
        try
        {
            _encounters = EncounterSet.Parse(FileAccess.GetFileAsString(path));
            var unported = _encounters.UnknownAbilities(_catalog);
            GD.Print($"[Sandbox] loaded {_encounters.Encounters.Count} encounters from {path}"
                + (unported.Count > 0 ? $" (not ported yet, skipped: {string.Join(", ", unported)})" : ""));
        }
        catch (FormatException e)
        {
            GD.PrintErr($"[Sandbox] {path}: {e.Message}");
        }
    }

    /// <summary>Generate a battle from the encounter set: the prototype's 6x6 board and value-budgeted armies.</summary>
    private void LoadEncounter(EncounterSpec spec, ulong seed)
    {
        CancelThinking();
        _encounter = (spec, seed);
        var g = EncounterGenerator.Generate(_encounters!, spec, _catalog, seed);
        _state = g.State;
        SetPlayer(Side.White, null);
        try
        {
            AgentProfile? ai = null;
            if (spec.Ai != null)
            {
                using var doc = JsonDocument.Parse(spec.Ai);
                ai = _factory.ParseProfile(doc.RootElement);
            }
            SetPlayer(Side.Black, ai);
        }
        catch (Exception e) when (e is AgentConfigException or JsonException)
        {
            GD.PrintErr($"[Sandbox] encounter {spec.Id}: ai: {e.Message}");
            SetPlayer(Side.Black, null);
        }
        string description = $"{spec.Description} Enemy AI: {_players[Side.Black]?.Profile.Name ?? "none"}.\n"
            + $"You: {string.Join(", ", g.Player)}\nEnemy: {string.Join(", ", g.Enemy)}";
        StartGame($"{spec.Name}  (seed {seed})", description);
    }

    private void LoadScenario(Scenario scenario)
    {
        CancelThinking();
        _encounter = null;
        _current = scenario;
        string json = FileAccess.GetFileAsString(scenario.Path);
        _state = MapLoader.Load(json, _catalog);
        try
        {
            var (white, black) = _factory.ParseMapPlayers(json);
            SetPlayer(Side.White, white);
            SetPlayer(Side.Black, black);
        }
        catch (Exception e) when (e is AgentConfigException or JsonException)
        {
            GD.PrintErr($"[Sandbox] {scenario.Path}: ai: {e.Message}");
            SetPlayer(Side.White, null);
            SetPlayer(Side.Black, null);
        }
        StartGame(scenario.Name, scenario.Description);
    }

    private void StartGame(string title, string description)
    {
        Game.BeginGame(_state);
        _state.ClearHistory();
        _undoMarks.Clear();
        _title.Text = title;
        _description.Text = description;
        _trace.Text = "";
        _board.State = _state;
        _board.Markers.Clear();
        ClearSelection();
        _sinceLastMove = 0;
        GD.Print($"[Sandbox] loaded '{title}' ({_state.Width}x{_state.Height}, {_state.Rules} rules)");
        Refresh();
    }

    private void Restart()
    {
        if (_encounter is { } e) LoadEncounter(e.Spec, e.Seed);
        else LoadScenario(_current);
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
        bool human = _players[_state.SideToMove] == null;
        if (piece != null && piece.Side == _state.SideToMove && !over && human)
        {
            _board.Selected = sq;
            _legal.Clear();
            MoveGenerator.GenerateLegal(_state, _legal);
            foreach (var m in _legal.Where(m => m.From == sq))
                (m.IsCapture ? _board.CaptureTargets : _board.QuietTargets).Add(m.To);
        }
        Refresh(piece);
    }

    private void PlayMove(Move move, string? aiSummary = null)
    {
        _sinceLastMove = 0;
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
        ShowTrace(result.Trace!, aiSummary != null ? $"{aiSummary}\n\n{side} {text}" : $"{side} {text}");
        Refresh();
    }

    private void Edit(Action change)
    {
        CancelThinking();
        _undoMarks.Add(_state.UndoDepth);
        change();
        _board.Markers.Clear();
        ClearSelection();
        Refresh();
    }

    private void Undo()
    {
        CancelThinking();
        if (_undoMarks.Count == 0) return;
        _state.UndoTo(_undoMarks[^1]);
        _undoMarks.RemoveAt(_undoMarks.Count - 1);
        // Against an AI, keep undoing until it's the human's turn again (otherwise the AI just replays).
        while (_undoMarks.Count > 0 && _players[_state.SideToMove] != null && _players[_state.SideToMove.Opponent()] == null)
        {
            _state.UndoTo(_undoMarks[^1]);
            _undoMarks.RemoveAt(_undoMarks.Count - 1);
        }
        _sinceLastMove = 0;
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
            _ when _thinkingAgent != null => $"{_state.SideToMove} ({_thinkingAgent.Profile.Name}) is thinking...",
            _ => $"{_state.SideToMove} to move" + (_players[_state.SideToMove] is { } ai ? $" ({ai.Profile.Name})" : ""),
        };
        _status.Text = $"{result}   ·   {_state.Rules} rules   ·   ply {_state.Ply}   ·   undo steps {_undoMarks.Count}";

        _hint.Text = inspected != null ? Describe(inspected) : _tool switch
        {
            Tool.Play when outcome != GameOutcome.Ongoing => "Game over. Undo, Restart, or edit the position to keep playing.",
            Tool.Play when _players[_state.SideToMove] is { } ai && _aiPaused.ButtonPressed =>
                $"AI paused. Press 'AI move' to let {ai.Profile.Name} play one move.",
            Tool.Play when _players[_state.SideToMove] is { } ai => $"{ai.Profile.Name}: {ai.Profile.Description}",
            Tool.Play => "Click a piece of the side to move, then a highlighted square.",
            Tool.Place => $"Click a square to place a {_placeSide} {_placeDef.Name} (replaces what's there).",
            Tool.Tile => $"Click a square to make it {_tileDef.Name}.",
            Tool.Ability => _abilityDef == null ? "Pick an ability below." : $"Click a piece to toggle {_abilityDef.Name}.",
            _ => "Click a piece to remove it, or an empty special tile to reset it.",
        };

        foreach (var (tool, button) in _toolButtons) button.SetPressedNoSignal(tool == _tool);
        _board.RefreshVisuals();
    }

    private string Describe(Piece p)
    {
        var sb = new StringBuilder($"{p.Side} {p.Def.Name} #{p.Id} on {MoveText.SquareName(_state, p.Square)}, value {p.Value}");
        foreach (var a in p.Abilities) sb.Append($"   ·   {a}");
        var tile = _state.TileAt(p.Square);
        if (!tile.Def.IsDefault) sb.Append($"   ·   tile: {tile}");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ AI players

    public override void _Process(double delta)
    {
        _sinceLastMove += delta;

        if (_thinking != null)
        {
            if (!_thinking.IsCompleted) return;
            var task = _thinking;
            var agent = _thinkingAgent!;
            _thinking = null;
            _thinkingAgent = null;
            if (task.IsFaulted)
            {
                GD.PrintErr($"[Sandbox] AI {agent.Profile.Id} failed: {task.Exception?.GetBaseException()}");
                _aiPaused.ButtonPressed = true;
                Refresh();
                return;
            }
            // Ignore stale results (the position changed while the AI was thinking).
            if (task.IsCanceled || _state.Hash != _thinkHash || _state.UndoDepth != _thinkDepth) { Refresh(); return; }

            var decision = task.Result;
            string summary = $"AI {agent.Profile.Name} ({agent.Profile.Id}): {decision.Format(_state).TrimEnd()}";
            if (decision.Move is { } move) PlayMove(move, summary);
            else Refresh();
            return;
        }

        if (_tool != Tool.Play) return;
        var player = _players[_state.SideToMove];
        bool force = _forceAiMove;
        _forceAiMove = false;
        if (!force && (player == null || _aiPaused.ButtonPressed || _sinceLastMove < AiMoveDelay)) return;
        if (_state.Rules.Evaluate(_state) != GameOutcome.Ongoing) return;

        // "AI move" on a human's turn: the Club profile suggests a move and plays it.
        StartThinking(player ?? _factory.Create(AgentProfile.Club));
    }

    private void StartThinking(IChessAgent agent)
    {
        ClearSelection();
        var snapshot = _state.Clone();
        _thinkHash = _state.Hash;
        _thinkDepth = _state.UndoDepth;
        _thinkCancel = new CancellationTokenSource();
        var token = _thinkCancel.Token;
        _thinkingAgent = agent;
        _thinking = Task.Run(() => agent.Decide(snapshot, token));
        Refresh();
    }

    private void CancelThinking()
    {
        _thinkCancel?.Cancel();
        _thinkCancel = null;
        _thinking = null;
        _thinkingAgent = null;
    }

    private void SetPlayer(Side side, AgentProfile? profile)
    {
        CancelThinking();
        if (profile != null && !_factory.Profiles.Any(p => p.Id == profile.Id))
        {
            _factory.RegisterProfile(profile); // an inline profile from a map
            RebuildPickers();
        }
        _players[side] = profile == null ? null : _factory.Create(profile);
        if (_playerPickers.TryGetValue(side, out var picker))
        {
            int index = 0;
            for (int i = 0; i < picker.ItemCount; i++)
                if (picker.GetItemMetadata(i).AsString() == (profile?.Id ?? "")) index = i;
            picker.Select(index);
        }
        _sinceLastMove = 0;
    }

    private void RebuildPickers()
    {
        foreach (var (side, picker) in _playerPickers)
        {
            string selected = _players[side]?.Profile.Id ?? "";
            picker.Clear();
            picker.AddItem("Human");
            picker.SetItemMetadata(0, "");
            foreach (var p in _factory.Profiles)
            {
                picker.AddItem($"AI: {p.Name}");
                picker.SetItemMetadata(picker.ItemCount - 1, p.Id);
                picker.SetItemTooltip(picker.ItemCount - 1, p.Description);
                if (p.Id == selected) picker.Select(picker.ItemCount - 1);
            }
            if (selected == "") picker.Select(0);
        }
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

        _board = new BoardView3D { Name = "Board" };
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

        if (_encounters is { Encounters.Count: > 0 } set)
        {
            var encounters = Section(panel, "Roguelike encounters (prototype balance, random armies)");
            foreach (var enc in set.Encounters)
            {
                var spec = enc;
                AddButton(encounters, spec.Name, () => LoadEncounter(spec, _encounterSeed));
            }
            AddButton(encounters, "Reroll", () =>
            {
                _encounterSeed++;
                LoadEncounter(_encounter?.Spec ?? set.Encounters[0], _encounterSeed);
            });
        }

        var scenarios = Section(panel, "Scenarios");
        foreach (var sc in _scenarios) AddButton(scenarios, sc.Name, () => LoadScenario(sc));

        var players = Section(panel, "Players");
        foreach (var side in new[] { Side.White, Side.Black })
        {
            var s = side;
            players.AddChild(new Label { Text = $"{s}:" });
            var picker = new OptionButton { Name = $"{s}Player", CustomMinimumSize = new Vector2(170, 0) };
            picker.ItemSelected += index =>
            {
                string id = picker.GetItemMetadata((int)index).AsString();
                SetPlayer(s, id == "" ? null : _factory.GetProfile(id));
                Refresh();
            };
            _playerPickers[s] = picker;
            players.AddChild(picker);
        }
        RebuildPickers();
        _aiPaused = new CheckBox { Text = "Pause AI", Name = "PauseAI" };
        _aiPaused.Toggled += _ => Refresh();
        players.AddChild(_aiPaused);
        AddButton(players, "AI move", () => { _forceAiMove = true; });

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
        AddButton(actions, "Restart", Restart);
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
