namespace RogueChess.Engine;

/// <summary>
/// Build identity for the rules engine. Lets the Godot side and the test suite confirm
/// they are talking to the same engine assembly.
/// </summary>
public static class EngineInfo
{
    public const string Name = "RogueChess.Engine";

    /// <summary>Bumped when the resolution contract (docs/RESOLUTION_RULES.md) changes.</summary>
    public const int ResolutionContractVersion = 0;
}
