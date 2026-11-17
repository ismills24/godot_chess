using Godot;
using RogueChess.Engine;

namespace GodotChess.Sandbox;

/// <summary>
/// Debug sandbox entry point. For now it only proves the Godot assembly can reach the engine;
/// the playable sandbox arrives in M5 (docs/PORT_PLAN.md §4).
/// </summary>
public partial class Sandbox : Control
{
    public override void _Ready()
    {
        var text = $"{EngineInfo.Name} loaded (resolution contract v{EngineInfo.ResolutionContractVersion})";
        GetNode<Label>("Status").Text = text;
        GD.Print($"[Sandbox] {text}");
    }
}
