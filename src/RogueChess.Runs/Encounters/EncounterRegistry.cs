using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RogueChess.Runs;

public interface IEncounterHandler
{
    string Version { get; }
    EncounterDefinition Resolve(EncounterDefinition definition);
    StatePayload Generate(EncounterDefinition definition, ulong seed);
    void Enter(EncounterContext context);
    void Execute(EncounterContext context, RunCommand command);
}

public sealed class EncounterRegistry
{
    private readonly Dictionary<string, IEncounterHandler> _handlers = new(StringComparer.Ordinal);
    public EncounterRegistry Register(string kind, IEncounterHandler handler)
    {
        if (!_handlers.TryAdd(kind, handler)) throw new ArgumentException($"Encounter kind '{kind}' is already registered.");
        return this;
    }
    public IEncounterHandler Get(string kind) => _handlers.TryGetValue(kind, out var handler)
        ? handler : throw new FormatException($"Unknown encounter kind '{kind}'.");
    public SortedDictionary<string, string> Manifest(RunDefinition definition) => new(
        definition.Encounters.Select(e => e.Kind).Distinct().ToDictionary(k => k, k => Get(k).Version), StringComparer.Ordinal);
    public static EncounterRegistry CreateDefault() => new EncounterRegistry()
        .Register("battle", new BattleHandler()).Register("bartender", new BattleHandler(requireInnBoard: true));
}

/// <summary>Only ever receives the transaction's working copy, not committed session state.</summary>
public sealed class EncounterContext
{
    public RunState State { get; }
    public EncounterState Encounter { get; }
    public ulong Seed { get; }
    public CancellationToken Cancellation { get; }
    public List<RunEffect> Effects { get; } = new();
    public EncounterContext(RunState state, EncounterState encounter, ulong seed, CancellationToken cancellation)
        { State = state; Encounter = encounter; Seed = seed; Cancellation = cancellation; }
    public int Random(string purpose, int maximum) => RunRandom.Next(State, Seed,
        $"encounter/{Encounter.Definition.Id}/{purpose}", maximum);
    public void Complete(string outcome)
    {
        Encounter.Status = EncounterStatus.Completed;
        Encounter.Outcome = outcome;
        State.ActiveEncounterId = null;
        State.Phase = State.Encounters.All(e => e.Status == EncounterStatus.Completed) ? RunPhase.Ended : RunPhase.Overworld;
    }
}
