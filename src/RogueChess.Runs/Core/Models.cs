using System;
using System.Collections.Generic;
using System.Text.Json;

namespace RogueChess.Runs;

public enum RunPhase { Overworld, Encounter, Ended }
public enum EncounterStatus { Available, Active, Completed }

public sealed record RunDefinition
{
    public string Id { get; init; } = "prototype";
    public int Width { get; init; } = 5;
    public int Height { get; init; } = 5;
    public int StartX { get; init; } = 2;
    public int StartY { get; init; }
    public StatePayload Inventory { get; init; } = new();
    public List<EncounterDefinition> Encounters { get; init; } = new();
}
public sealed record EncounterDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "battle";
    public int X { get; init; }
    public int Y { get; init; }
    public JsonElement Parameters { get; init; } = RunJson.Data(new { });
}
public sealed class EncounterState
{
    public EncounterDefinition Definition { get; set; } = new();
    public EncounterStatus Status { get; set; }
    public StatePayload Data { get; set; } = new();
    public string? Outcome { get; set; }
}
public sealed class RunState
{
    public RunPhase Phase { get; set; } = RunPhase.Overworld;
    public int Width { get; set; }
    public int Height { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public StatePayload Inventory { get; set; } = new();
    public string? ActiveEncounterId { get; set; }
    public List<EncounterState> Encounters { get; set; } = new();
    public SortedDictionary<string, ulong> RandomStreams { get; set; } = new(StringComparer.Ordinal);
    public long NextEntityId { get; set; } = 1;
    public int UndosUsed { get; set; }
}
public sealed record RunCommand
{
    public string Type { get; init; } = "";
    public int Version { get; init; } = 1;
    public string? EncounterId { get; init; }
    public JsonElement Parameters { get; init; } = RunJson.Data(new { });
    public static RunCommand Create<T>(string type, T parameters, string? encounterId = null)
        => new() { Type = type, Parameters = RunJson.Data(parameters), EncounterId = encounterId };
}
public sealed record WorldMove(int X, int Y);
public sealed record BattleMove(int From, int To, int Promotion = -1);
public sealed record CommandResult(bool Accepted, string? Reason, long Sequence, IReadOnlyList<RunEffect> Effects);
public abstract record RunEffect;
public sealed record BattleStep(RogueChess.Engine.Core.GameStateSnapshot State, int From, int To, bool ByEnemy) : RunEffect;
public sealed class CommandRejectedException : Exception
{
    public CommandRejectedException(string message) : base(message) { }
}
