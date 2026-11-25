using System;
using System.Collections.Generic;

namespace RogueChess.Runs;

public sealed record RunTraceHeader
{
    public int FormatVersion { get; init; } = 1;
    public string SimulationVersion { get; init; } = RunSession.SimulationVersion;
    public string ContentVersion { get; init; } = RunSession.ContentVersion;
    public string RngVersion { get; init; } = RunRandom.Version;
    public string GeneratorVersion { get; init; } = "";
    public SortedDictionary<string, string> Handlers { get; init; } = new(StringComparer.Ordinal);
    public string RunId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public ulong Seed { get; init; }
    public RunDefinition Definition { get; init; } = new();
    public string ConfigurationHash { get; init; } = "";
    public string InitialStateHash { get; init; } = "";
}
public sealed record RunCommandEntry(long Sequence, RunCommand Command, long DecisionMilliseconds, string StateHash);
public sealed record RunTrace
{
    public RunTraceHeader Header { get; init; } = new();
    public List<RunCommandEntry> Commands { get; init; } = new();
}
public sealed record RunCheckpoint(long Sequence, RunState State, string StateHash);
public sealed record RunSave
{
    public int Version { get; init; } = 1;
    public RunTrace Trace { get; init; } = new();
    public RunCheckpoint Checkpoint { get; init; } = new(0, new(), "");
}
public sealed class ReplayDivergenceException : Exception
{
    public long Sequence { get; }
    public ReplayDivergenceException(long sequence, string message) : base($"Replay diverged at command {sequence}: {message}") => Sequence = sequence;
}
