namespace GodotChess.Sandbox.Visuals;

/// <summary>
/// The look of one stage of a run: the board the battle is played on and the place it
/// is set down. Both are presentation only. Board scenes follow the board contract
/// (`board_width`/`board_height` set before entering the tree, a `Squares` child, Y=0
/// ground, 1.45-unit squares, centred on the origin). Setting scenes supply environment,
/// lights and surroundings and expose `fit(width, height)`; an empty setting uses neutral lights.
/// </summary>
public sealed record StageVisuals(string Name, string BoardScene, string SettingScene)
{
    /// <summary>The first stage: hand-drawn chalk on a sheltered, lamplit stone landing.</summary>
    public static readonly StageVisuals Street = new("Street",
        "res://stages/street/StreetBoard.tscn", "res://stages/street/StreetSetting.tscn");

    /// <summary>The clean maple/walnut board under neutral lights (tools and previews).</summary>
    public static readonly StageVisuals Workshop = new("Workshop", "res://assets/boards/wood/WoodBoard.tscn", "");
}
