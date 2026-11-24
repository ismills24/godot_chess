using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using GodotChess.Inn;
using GodotChess.Run;
using RogueChess.Runs;

namespace GodotChess.Experiments;

/// <summary>Integration smoke: actual world buttons and inn adapter, with a separate save file.</summary>
public partial class VerifyRunFlow : Node
{
    public override async void _Ready()
    {
        try
        {
            await Frames(1);
            var host = new RunHost { SkipEncounterIntro = true, StoragePath = "user://runs/verification.json" };
            GetTree().Root.AddChild(host);
            host.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            host.NewRun(42);
            await Frames(3);
            Find<Button>(host, b => b.Name == "Square_2_1").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(6);
            var match = Find<InnMatch>(host);
            Require(match.IsPlayerTurn(), "Bartender did not attach to the run.");
            Require(host.Session!.Sequence == 1, "Entering the inn must record one world command.");
            Require(match.TryMove(12, 28), "Player move rejected.");
            await Until(() => match.IsPlayerTurn());
            Require(host.Session.Sequence == 2, "AI response must not create a second command.");
            Require(match.Undo(), "Undo was unavailable.");
            await Until(() => match.IsPlayerTurn());
            Require(host.Session.Sequence == 3 && host.Session.State.UndosUsed == 1, "Undo was not recorded.");
            Require(host.SaveNow(), "Saving inside the encounter failed.");
            var saved = RunJson.Read<RunSave>(File.ReadAllText(ProjectSettings.GlobalizePath(host.StoragePath)));
            var loaded = RunSession.Restore(saved);
            Require(RunJson.Hash(loaded.State) == RunJson.Hash(host.Session.State), "Saved encounter differs after load.");
            GetTree().Root.RemoveChild(host);
            host.QueueFree();
            host = new RunHost { SkipEncounterIntro = true, StoragePath = "user://runs/verification.json" };
            GetTree().Root.AddChild(host);
            host.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            await Frames(3);
            Find<Button>(host, b => b.Text == "Continue saved run").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(6);
            match = Find<InnMatch>(host);
            Require(match.IsPlayerTurn(), "Resumed inn is not accepting input.");
            Require(RunJson.Hash(host.Session!.State) == RunJson.Hash(loaded.State), "Godot resume changed the saved run.");
            var replay = await Task.Run(() => RunSession.Replay(host.Session.Trace));
            Require(RunJson.Hash(replay.State) == RunJson.Hash(host.Session.State), "Live trace diverges.");
            Require(match.Leave(), "Concede/return command was rejected.");
            await Until(() => host.Session.State.Phase == RunPhase.Overworld);
            await Frames(6);
            Require(host.Session.Sequence == 4, "Returning must preserve the complete command history.");
            // Exercise the ordinary battle presentation on the fixture map as well.
            Find<Button>(host, b => b.Name == "Square_3_2").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(3);
            Find<Button>(host, b => b.Name == "Square_4_3").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(6);
            Require(host.Session.State.ActiveEncounterId == "street-challenger", "Generic battle failed to launch.");
            Require(Find<BattleEncounterView>(host) != null, "Generic battle view missing.");
            GD.Print("[RunFlow] PASS: world -> bartender -> move/AI -> undo -> save/load -> resume inn -> replay -> return -> street battle");
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PrintErr("[RunFlow] FAIL: " + e);
            GetTree().Quit(1);
        }
    }
    private async Task Frames(int count) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Until(Func<bool> ready)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        while (!ready()) { if (started.Elapsed.TotalSeconds > 15) throw new TimeoutException("Run transition timed out."); await Frames(1); }
    }
    private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
    private static T Find<T>(Node node, Func<T, bool>? predicate = null) where T : Node
    {
        if (node is T match && (predicate == null || predicate(match))) return match;
        foreach (var child in node.GetChildren())
        {
            var matchInChild = FindOrNull<T>(child, predicate);
            if (matchInChild != null) return matchInChild;
        }
        throw new InvalidOperationException($"Missing {typeof(T).Name}.");
    }
    private static T? FindOrNull<T>(Node node, Func<T, bool>? predicate) where T : Node
    {
        if (node is T match && (predicate == null || predicate(match))) return match;
        return node.GetChildren().Select(child => FindOrNull(child, predicate)).FirstOrDefault(n => n != null);
    }
}
