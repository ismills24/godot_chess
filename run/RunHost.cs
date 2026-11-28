using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using GodotChess.Sandbox;
using RogueChess.Engine.Content;
using RogueChess.Engine.Core;
using RogueChess.Runs;
using FileAccess = Godot.FileAccess;
using RandomNumberGenerator = System.Security.Cryptography.RandomNumberGenerator;

namespace GodotChess.Run;

/// <summary>Survives encounter view changes. Godot owns presentation/storage adapters, never simulation rules.</summary>
public partial class RunHost : Control
{
    [Export] public bool SkipEncounterIntro { get; set; }
    [Export] public string StoragePath { get; set; } = "user://runs/current.json";
    public static RunHost? Active { get; private set; }
    public RunSession? Session { get; private set; }
    private FileRunStore _store = null!;
    private RunPersistence _persistence = null!;
    private Node? _view;
    private Label _error = null!;
    private long _decisionStart;
    private CancellationTokenSource? _replayCancellation;
    private int _viewGeneration;
    private sealed record Presentation(string? ScenePath, Func<Node>? Create = null);
    private readonly Dictionary<string, Presentation> _presentations = new(StringComparer.Ordinal)
    {
        ["bartender"] = new("res://inn/Inn.tscn"),
        ["battle"] = new(null, () => new BattleEncounterView()),
        ["apothecary"] = new("res://places/apothecary/Apothecary.tscn"),
        ["beach"] = new("res://places/beach/Beach.tscn"),
        ["mountain"] = new("res://places/mountain/Mountain.tscn"),
        ["sewer"] = new("res://places/sewer/Sewer.tscn"),
        ["town_gate"] = new("res://places/town_gate/TownGate.tscn"),
    };
    private static readonly Dictionary<string, string> MapLabels = new(StringComparer.Ordinal)
    {
        ["bartender"] = "The inn", ["battle"] = "Battle", ["apothecary"] = "Apothecary",
        ["beach"] = "Beach", ["mountain"] = "Mountain", ["sewer"] = "Sewer", ["town_gate"] = "Town gate",
    };
    // Keep the PackedScene alive across visits; Godot's resource cache uses weak references.
    // Only resources load on a worker. Instantiation and scene-tree work stay on the main thread.
    private readonly Dictionary<string, PackedScene> _loadedScenes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _loadingScenes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _sceneLoadErrors = new(StringComparer.Ordinal);
    public override void _Ready()
    {
        Active = this;
        MouseFilter = MouseFilterEnum.Ignore;
        _store = new FileRunStore(ProjectSettings.GlobalizePath(StoragePath));
        _persistence = new RunPersistence(_store);
        var preferences = new ConfigFile();
        if (preferences.Load(StoragePath + ".settings.cfg") == Error.Ok)
        {
            int policy = preferences.GetValue("saving", "policy", 0).AsInt32();
            if (Enum.IsDefined(typeof(SavePolicy), policy)) _persistence.Policy = (SavePolicy)policy;
        }
        var warnings = new CanvasLayer { Layer = 90 };
        AddChild(warnings);
        _error = new Label { Position = new Vector2(18, 8), MouseFilter = MouseFilterEnum.Ignore };
        _error.AddThemeColorOverride("font_color", new Color("#ffc8a0"));
        _error.AddThemeColorOverride("font_outline_color", Colors.Black);
        _error.AddThemeConstantOverride("outline_size", 6);
        warnings.AddChild(_error);
        string? seedArg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--run-seed=", StringComparison.Ordinal));
        if (seedArg != null && ulong.TryParse(seedArg.Split('=')[1], out var seed)) NewRun(seed);
        else ShowLobby();
    }
    public override void _ExitTree()
    {
        _viewGeneration++;
        _replayCancellation?.Cancel();
        _loadedScenes.Clear();
        if (Active == this) Active = null;
        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
    }
    private async void RequestScene(string path)
    {
        if (_loadedScenes.ContainsKey(path) || _loadingScenes.Contains(path) || _sceneLoadErrors.ContainsKey(path)) return;
        _loadingScenes.Add(path);
        try
        {
            var error = ResourceLoader.LoadThreadedRequest(path, "PackedScene");
            if (error != Error.Ok) throw new IOException($"Could not load encounter scene '{path}' ({error}).");
            var tree = GetTree();
            // Use the tree's lifetime: finish collecting an outstanding request even if the
            // player leaves this host during preload. Never block _ExitTree waiting on I/O.
            while (ResourceLoader.LoadThreadedGetStatus(path) == ResourceLoader.ThreadLoadStatus.InProgress)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            var scene = ResourceLoader.LoadThreadedGet(path) as PackedScene;
            if (scene == null) throw new IOException($"Could not load encounter scene '{path}'.");
            if (IsInstanceValid(this) && IsInsideTree()) _loadedScenes[path] = scene;
        }
        catch (Exception e) { _sceneLoadErrors[path] = e.Message; }
        finally { _loadingScenes.Remove(path); }
    }
    private void SetView(Node node)
    {
        _viewGeneration++;
        _replayCancellation?.Cancel();
        if (_view != null) { RemoveChild(_view); _view.QueueFree(); }
        _view = node;
        AddChild(node);
    }
    private VBoxContainer Page(string title, string subtitle)
    {
        var background = new ColorRect { Color = new Color("#142126"), MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (string edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 42);
        background.AddChild(margin);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 14);
        margin.AddChild(box);
        var heading = new Label { Text = title };
        heading.AddThemeFontSizeOverride("font_size", 32);
        heading.AddThemeColorOverride("font_color", new Color("#ebd5aa"));
        box.AddChild(heading);
        box.AddChild(new Label { Text = subtitle, AutowrapMode = TextServer.AutowrapMode.WordSmart });
        SetView(background);
        return box;
    }
    private static Button Button(Container parent, string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 42) };
        button.Pressed += () => { try { action(); } catch (Exception e) { Active?.Report(e.Message); GD.PrintErr(e); } };
        parent.AddChild(button);
        return button;
    }
    private void Report(string text) => _error.Text = text;
    private void ShowLobby()
    {
        var page = Page("A night in the district", "Start a seeded run, or return to the one you left behind.");
        var row = new HBoxContainer();
        page.AddChild(row);
        var seed = new LineEdit { PlaceholderText = "Seed (leave blank for a new one)", CustomMinimumSize = new Vector2(370, 42) };
        row.AddChild(seed);
        Button(row, "New run", () =>
        {
            ulong chosen;
            if (string.IsNullOrWhiteSpace(seed.Text)) chosen = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));
            else if (!ulong.TryParse(seed.Text, NumberStyles.None, CultureInfo.InvariantCulture, out chosen))
            { Report("Enter a whole-number seed from 0 to 18446744073709551615."); return; }
            NewRun(chosen);
        });
        if (Session != null) Button(page, "Return to current run", ShowCurrent);
        if (System.IO.File.Exists(_store.Path)) Button(page, "Continue saved run", ContinueRun);
        Button(page, "Import trace for replay", ImportTrace);
        Button(page, "Main menu", () => GetTree().ChangeSceneToFile("res://menu/MainMenu.tscn"));
    }
    public void NewRun(ulong seed)
    {
        try
        {
            var definition = RunJson.Read<RunDefinition>(FileAccess.GetFileAsString("res://run/prototype.json"));
            Session = RunSession.Start(seed, definition);
            _decisionStart = 0;
            _persistence.Flush(Session);
            Report(_persistence.LastError ?? "");
            ShowWorld();
        }
        catch (Exception e) { Report(e.Message); GD.PrintErr(e); }
    }
    private void ContinueRun()
    {
        try { Session = RunSession.Restore(_store.Read()); Report(""); }
        catch (Exception e) when (e is IOException or FormatException or System.Text.Json.JsonException)
        {
            if (!System.IO.File.Exists(_store.Path + ".previous")) throw;
            Session = RunSession.Restore(RunJson.Read<RunSave>(System.IO.File.ReadAllText(_store.Path + ".previous")));
            Report("Recovered the previous save. The latest save could not be read: " + e.Message);
        }
        _decisionStart = 0;
        _persistence.MarkLoaded(Session!.Sequence);
        ShowCurrent();
    }
    private async void ShowCurrent()
    {
        var state = Session?.State;
        if (state?.Phase != RunPhase.Encounter) { ShowWorld(); return; }
        var encounter = state.Encounters.Single(e => e.Definition.Id == state.ActiveEncounterId);
        var started = Stopwatch.GetTimestamp();
        Page($"Entering {encounter.Definition.Name}...", "Preparing the encounter.");
        int generation = _viewGeneration;
        bool IsCurrent() => IsInstanceValid(this) && IsInsideTree() && generation == _viewGeneration;
        string? path = null;
        try
        {
            if (!_presentations.TryGetValue(encounter.Definition.Kind, out var presentation))
                throw new InvalidOperationException($"No view registered for encounter kind '{encounter.Definition.Kind}'.");
            path = presentation.ScenePath;
            if (path != null) RequestScene(path);
            // ProcessFrame fires before drawing. Two boundaries guarantee the feedback has
            // rendered before synchronous instantiation/_Ready, even when called from input.
            var tree = GetTree();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (!IsCurrent()) return;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (!IsCurrent()) return;
            while (path != null && !_loadedScenes.ContainsKey(path))
            {
                if (_sceneLoadErrors.TryGetValue(path, out var error)) throw new IOException(error);
                await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                if (!IsCurrent()) return;
            }
            var loadedAt = Stopwatch.GetTimestamp();
            var view = path != null ? _loadedScenes[path].Instantiate() : presentation.Create!();
            if (path != null) view.Set("skip_intro", SkipEncounterIntro);
            SetView(view);
            _decisionStart = Stopwatch.GetTimestamp();
            GD.Print($"[Run] enter {encounter.Definition.Kind}: {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1} ms total, " +
                $"{Stopwatch.GetElapsedTime(loadedAt).TotalMilliseconds:F1} ms instantiate/ready");
        }
        catch (Exception e)
        {
            if (!IsCurrent()) return;
            var page = Page("Could not enter the encounter", e.Message);
            // The world command was already committed. Retry presentation without recording it again.
            Button(page, "Retry", () => { if (path != null) _sceneLoadErrors.Remove(path); ShowCurrent(); });
            Button(page, "Save and return to menu", () => { if (SaveNow()) GetTree().ChangeSceneToFile("res://menu/MainMenu.tscn"); });
            GD.PrintErr(e);
        }
    }
    public void ShowWorld()
    {
        if (Session == null) { ShowLobby(); return; }
        var state = Session.State;
        if (state.Phase == RunPhase.Encounter) { ShowCurrent(); return; }
        // Prepare only reachable encounters, not the whole map. Simulation is untouched.
        foreach (var next in state.Encounters.Where(e => e.Status != EncounterStatus.Completed &&
                     Math.Max(Math.Abs(state.X - e.Definition.X), Math.Abs(state.Y - e.Definition.Y)) == 1))
            if (_presentations.TryGetValue(next.Definition.Kind, out var presentation) && presentation.ScenePath is { } path)
                RequestScene(path);
        var page = Page("The district", state.Phase == RunPhase.Ended
            ? "The night's encounters are complete." : "Move one square in any direction. Capture an encounter to enter.");
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 32);
        page.AddChild(row);
        var grid = new GridContainer { Columns = state.Width };
        grid.AddThemeConstantOverride("h_separation", 3);
        grid.AddThemeConstantOverride("v_separation", 3);
        row.AddChild(grid);
        for (int y = state.Height - 1; y >= 0; y--)
            for (int x = 0; x < state.Width; x++)
            {
                int targetX = x, targetY = y;
                var encounter = state.Encounters.SingleOrDefault(e => e.Definition.X == x && e.Definition.Y == y);
                bool player = state.X == x && state.Y == y;
                bool completed = encounter?.Status == EncounterStatus.Completed;
                string name = encounter == null ? "" : completed ? "Cleared" : MapLabels.GetValueOrDefault(encounter.Definition.Kind, "Battle");
                var square = Button(grid, (player ? "YOU" : name) + $"\n{(char)('a' + x)}{y + 1}", () => MoveWorld(targetX, targetY));
                square.Name = $"Square_{x}_{y}";
                square.CustomMinimumSize = new Vector2(98, 98);
                square.Disabled = state.Phase == RunPhase.Ended || Math.Max(Math.Abs(state.X - x), Math.Abs(state.Y - y)) != 1;
                square.TooltipText = encounter?.Definition.Name ?? "An empty square";
                var fill = new StyleBoxFlat { BgColor = player ? new Color("#9f814b") :
                    ((x + y) % 2 == 0 ? new Color("#425558") : new Color("#283a40")) };
                square.AddThemeStyleboxOverride("normal", fill);
                square.AddThemeStyleboxOverride("disabled", fill);
                square.AddThemeColorOverride("font_color", new Color("#f4e7cd"));
                square.AddThemeColorOverride("font_disabled_color", new Color("#aeb9b8"));
            }
        var details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        details.AddThemeConstantOverride("separation", 14);
        row.AddChild(details);
        details.AddChild(new Label { Text = $"Seed {Session.Trace.Header.Seed}", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        foreach (var encounter in state.Encounters)
            details.AddChild(new Label { Text = $"{encounter.Definition.Name}\n{(char)('a' + encounter.Definition.X)}{encounter.Definition.Y + 1} · {encounter.Status}" });
        Button(details, "Save and return to menu", () => { if (SaveNow()) GetTree().ChangeSceneToFile("res://menu/MainMenu.tscn"); });
        Button(details, "Run tools", ShowTools);
        Button(details, "New run...", ShowLobby);
        if (_decisionStart == 0) _decisionStart = Stopwatch.GetTimestamp();
    }
    private void MoveWorld(int x, int y)
    {
        if (Session == null) return;
        var before = Session.State.Phase;
        var result = Session.Execute(RunCommand.Create("world.move", new WorldMove(x, y)),
            (long)Stopwatch.GetElapsedTime(_decisionStart).TotalMilliseconds, Session.Sequence);
        if (!result.Accepted) { Report(result.Reason ?? "Move rejected."); return; }
        NotifyCommit(before, result);
        ShowCurrent();
    }
    public void NotifyCommit(RunPhase before, CommandResult result)
    {
        if (Session == null || !result.Accepted) return;
        _persistence.OnCommand(Session, before);
        _decisionStart = Stopwatch.GetTimestamp();
        Report(_persistence.LastError ?? "");
        GD.Print($"[Run] command {result.Sequence}: {Session.Trace.Commands[^1].Command.Type}");
    }
    public bool SaveNow()
    {
        if (Session == null) return false;
        bool saved = _persistence.Flush(Session);
        Report(saved ? "Run saved." : "Could not save: " + _persistence.LastError);
        return saved;
    }
    // Called by the inn if Escape is pressed during its introductory walk.
    public void LeaveEncounter()
    {
        if (Session?.State.ActiveEncounterId is not { } id) { ShowWorld(); return; }
        var result = Session.Execute(RunCommand.Create("battle.resign", new { }, id),
            (long)Stopwatch.GetElapsedTime(_decisionStart).TotalMilliseconds, Session.Sequence);
        NotifyCommit(RunPhase.Encounter, result);
        if (result.Accepted) ShowWorld();
    }
    private void ShowTools()
    {
        if (Session == null) return;
        var page = Page("Run tools", $"Seed {Session.Trace.Header.Seed} · {Session.Sequence} recorded commands · {_persistence.LastSavedSequence} last saved");
        var policy = new OptionButton { CustomMinimumSize = new Vector2(320, 42) };
        policy.AddItem("Save on return to the overworld");
        policy.AddItem("Save after every accepted command");
        policy.Selected = (int)_persistence.Policy;
        policy.ItemSelected += index =>
        {
            _persistence.Policy = (SavePolicy)index;
            var preferences = new ConfigFile();
            preferences.SetValue("saving", "policy", (int)index);
            var error = preferences.Save(StoragePath + ".settings.cfg");
            if (error != Error.Ok) Report("Could not save the save-policy preference: " + error);
        };
        page.AddChild(policy);
        Button(page, "Save now", () => SaveNow());
        Button(page, "Export trace", ExportTrace);
        Button(page, "Inspect this trace", () => ShowReplay(Session.Trace, 0));
        Button(page, "Import trace", ImportTrace);
        page.AddChild(new Label { Text = "Inventory state" });
        page.AddChild(new TextEdit { Text = RunJson.Write(Session.State.Inventory), Editable = false, CustomMinimumSize = new Vector2(400, 100) });
        Button(page, "Back to run", ShowCurrent);
    }
    private void ExportTrace()
    {
        if (Session == null) return;
        var dialog = new FileDialog { Access = FileDialog.AccessEnum.Filesystem, FileMode = FileDialog.FileModeEnum.SaveFile,
            Title = "Export run trace", CurrentFile = $"run-{Session.Trace.Header.Seed}.json", Filters = new[] { "*.json ; Run trace" } };
        AddChild(dialog);
        dialog.FileSelected += path =>
        {
            try { System.IO.File.WriteAllText(path, RunJson.Write(Session.Trace)); Report("Trace exported."); }
            catch (Exception e) { Report(e.Message); }
            dialog.QueueFree();
        };
        dialog.Canceled += dialog.QueueFree;
        dialog.PopupCenteredRatio(0.8f);
    }
    private void ImportTrace()
    {
        var dialog = new FileDialog { Access = FileDialog.AccessEnum.Filesystem, FileMode = FileDialog.FileModeEnum.OpenFile,
            Title = "Import run trace", Filters = new[] { "*.json ; Run trace" } };
        AddChild(dialog);
        dialog.FileSelected += path =>
        {
            try { ShowReplay(RunJson.Read<RunTrace>(System.IO.File.ReadAllText(path)), 0); }
            catch (Exception e) { Report(e.Message); }
            dialog.QueueFree();
        };
        dialog.Canceled += dialog.QueueFree;
        dialog.PopupCenteredRatio(0.8f);
    }
    private async void ShowReplay(RunTrace trace, int sequence)
    {
        var page = Page("Trace inspector", $"Reconstructing command {sequence} of {trace.Commands.Count}...");
        Button(page, "Back", Session == null ? ShowLobby : ShowCurrent);
        _replayCancellation = new CancellationTokenSource();
        var token = _replayCancellation.Token;
        int generation = _viewGeneration;
        try
        {
            var replay = await Task.Run(() => RunSession.Replay(trace, sequence, cancellation: token), token);
            if (!IsInsideTree() || generation != _viewGeneration) return;
            var state = replay.State;
            page = Page("Trace inspector", $"Verified through command {sequence} / {trace.Commands.Count} · Seed {trace.Header.Seed}");
            var controls = new HBoxContainer();
            page.AddChild(controls);
            Button(controls, "Previous", () => ShowReplay(trace, sequence - 1)).Disabled = sequence == 0;
            Button(controls, "Next", () => ShowReplay(trace, sequence + 1)).Disabled = sequence == trace.Commands.Count;
            Button(controls, "Verify full trace", () => ShowReplay(trace, trace.Commands.Count));
            Button(controls, "Back to run", Session == null ? ShowLobby : ShowCurrent);
            page.AddChild(new Label { Text = sequence == 0 ? "Initial generated state" :
                $"{trace.Commands[sequence - 1].Command.Type} · {trace.Commands[sequence - 1].DecisionMilliseconds} ms decision time" });
            var data = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            page.AddChild(data);
            string? encounterId = state.ActiveEncounterId ?? (sequence > 0 ? trace.Commands[sequence - 1].Command.EncounterId : null);
            var encounter = state.Encounters.FirstOrDefault(e => e.Definition.Id == encounterId);
            if (encounter?.Data.Kind == "battle")
            {
                var catalog = ContentLibrary.CreateCatalog();
                BattleHandler.CreateBoardEffects(catalog, RunJson.Decode<BattleParameters>(encounter.Definition.Parameters));
                var board = new BoardView3D { State = GameState.Restore(encounter.Data.As<BattleState>("battle").Position, catalog),
                    SizeFlagsHorizontal = SizeFlags.ExpandFill };
                data.AddChild(board);
                board.CustomMinimumSize = new Vector2(600, 450);
                board.RefreshVisuals();
            }
            data.AddChild(new TextEdit { Editable = false, CustomMinimumSize = new Vector2(360, 300), SizeFlagsVertical = SizeFlags.ExpandFill,
                Text = $"Phase: {state.Phase}\nLocation: {state.X}, {state.Y}\nUndos used: {state.UndosUsed}\n\nInventory\n{RunJson.Write(state.Inventory)}\n\nState digest\n{RunJson.Hash(state)}" });
            Report("");
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { if (IsInsideTree() && generation == _viewGeneration) Report(e.Message); }
    }
}
