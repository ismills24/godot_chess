using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace RogueChess.Runs;

/// <summary>Single writer. Commands run on copies; only settled, changed transitions enter the trace.</summary>
public sealed class RunSession
{
    public const string SimulationVersion = "runs-v1";
    private readonly object _gate = new();
    private readonly EncounterRegistry _registry;
    private RunState _state;
    private readonly RunTrace _trace;
    public RunState State { get { lock (_gate) return RunJson.Copy(_state); } }
    public RunTrace Trace { get { lock (_gate) return RunJson.Copy(_trace); } }
    public long Sequence { get { lock (_gate) return _trace.Commands.Count; } }
    private RunSession(RunState state, RunTrace trace, EncounterRegistry registry)
        { _state = state; _trace = trace; _registry = registry; }

    public static RunSession Start(ulong seed, RunDefinition definition, EncounterRegistry? registry = null,
        IWorldGenerator? generator = null)
    {
        registry ??= EncounterRegistry.CreateDefault();
        generator ??= new ConfiguredWorldGenerator();
        definition = RunJson.Copy(definition);
        Validate(definition, registry);
        definition = definition with { Encounters = definition.Encounters.Select(d => registry.Get(d.Kind).Resolve(d)).ToList() };
        var state = generator.Generate(definition, seed);
        var generatedDefinition = definition with { Width = state.Width, Height = state.Height, StartX = state.X, StartY = state.Y,
            Encounters = state.Encounters.Select(e => e.Definition).ToList() };
        Validate(generatedDefinition, registry);
        foreach (var encounter in state.Encounters)
        {
            var handler = registry.Get(encounter.Definition.Kind);
            encounter.Definition = handler.Resolve(encounter.Definition);
            encounter.Data = handler.Generate(encounter.Definition, RunRandom.Derive(seed, $"generation/{encounter.Definition.Id}"));
        }
        var header = new RunTraceHeader
        {
            Seed = seed, Definition = definition, ConfigurationHash = RunJson.Hash(definition),
            InitialStateHash = RunJson.Hash(state), GeneratorVersion = generator.Version, Handlers = registry.Manifest(generatedDefinition),
        };
        return new RunSession(state, new RunTrace { Header = header }, registry);
    }

    public CommandResult Execute(RunCommand command, long decisionMilliseconds = 0, long? expectedRevision = null,
        CancellationToken cancellation = default)
    {
        lock (_gate)
        {
            long sequence = _trace.Commands.Count;
            CommandResult Reject(string reason) => new(false, reason, sequence, Array.Empty<RunEffect>());
            if (expectedRevision.HasValue && expectedRevision.Value != sequence) return Reject("The run changed before this command arrived.");
            if (command.Version != 1 || decisionMilliseconds < 0) return Reject("Unsupported command version or duration.");
            var working = RunJson.Copy(_state);
            command = RunJson.Copy(command);
            var effects = new List<RunEffect>();
            try
            {
                cancellation.ThrowIfCancellationRequested();
                if (working.Phase == RunPhase.Overworld && command.Type == "world.move" && command.EncounterId == null)
                {
                    var move = RunJson.Decode<WorldMove>(command.Parameters);
                    long dx = Math.Abs((long)move.X - working.X);
                    long dy = Math.Abs((long)move.Y - working.Y);
                    if (move.X < 0 || move.Y < 0 || move.X >= working.Width || move.Y >= working.Height
                        || Math.Max(dx, dy) != 1) return Reject("Move one square in any direction.");
                    working.X = move.X; working.Y = move.Y;
                    var encounter = working.Encounters.SingleOrDefault(e => e.Definition.X == move.X && e.Definition.Y == move.Y
                        && e.Status == EncounterStatus.Available);
                    if (encounter != null)
                    {
                        encounter.Status = EncounterStatus.Active;
                        working.ActiveEncounterId = encounter.Definition.Id;
                        working.Phase = RunPhase.Encounter;
                        var context = new EncounterContext(working, encounter, _trace.Header.Seed, cancellation);
                        _registry.Get(encounter.Definition.Kind).Enter(context);
                        effects.AddRange(context.Effects);
                    }
                }
                else if (working.Phase == RunPhase.Encounter && command.EncounterId == working.ActiveEncounterId)
                {
                    var encounter = working.Encounters.Single(e => e.Definition.Id == working.ActiveEncounterId);
                    var context = new EncounterContext(working, encounter, _trace.Header.Seed, cancellation);
                    _registry.Get(encounter.Definition.Kind).Execute(context, command);
                    effects.AddRange(context.Effects);
                }
                else return Reject("That command is not available here.");
                cancellation.ThrowIfCancellationRequested();
            }
            catch (CommandRejectedException e) { return Reject(e.Message); }
            catch (JsonException) { return Reject("Malformed command parameters."); }
            string digest = RunJson.Hash(working);
            if (digest == RunJson.Hash(_state)) return Reject("The command did not change game state.");
            _trace.Commands.Add(new RunCommandEntry(sequence + 1, command, decisionMilliseconds, digest));
            _state = working;
            return new CommandResult(true, null, sequence + 1, effects);
        }
    }

    public RunSave CaptureSave()
    {
        lock (_gate) return new RunSave { Trace = RunJson.Copy(_trace),
            Checkpoint = new RunCheckpoint(_trace.Commands.Count, RunJson.Copy(_state), RunJson.Hash(_state)) };
    }

    public static RunSession Replay(RunTrace trace, long? through = null, EncounterRegistry? registry = null,
        IWorldGenerator? generator = null, CancellationToken cancellation = default)
    {
        var fresh = ValidateTrace(trace, registry, generator);
        long end = through ?? trace.Commands.Count;
        if (end < 0 || end > trace.Commands.Count) throw new ArgumentOutOfRangeException(nameof(through));
        for (int i = 0; i < end; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var entry = trace.Commands[i];
            var result = fresh.Execute(entry.Command, entry.DecisionMilliseconds, i, cancellation);
            if (!result.Accepted) throw new ReplayDivergenceException(entry.Sequence, result.Reason ?? "Command rejected.");
            string actual = fresh._trace.Commands[^1].StateHash;
            if (actual != entry.StateHash) throw new ReplayDivergenceException(entry.Sequence, $"expected {entry.StateHash}, got {actual}.");
        }
        return fresh;
    }

    public static RunSession Restore(RunSave save, EncounterRegistry? registry = null, IWorldGenerator? generator = null)
    {
        if (save.Version != 1) throw new FormatException($"Unsupported save version {save.Version}.");
        var fresh = ValidateTrace(save.Trace, registry, generator);
        var checkpoint = save.Checkpoint;
        if (checkpoint.Sequence < 0 || checkpoint.Sequence > save.Trace.Commands.Count) throw new FormatException("Invalid checkpoint sequence.");
        string expected = checkpoint.Sequence == 0 ? save.Trace.Header.InitialStateHash
            : save.Trace.Commands[(int)checkpoint.Sequence - 1].StateHash;
        if (expected != checkpoint.StateHash || RunJson.Hash(checkpoint.State) != expected)
            throw new FormatException("Checkpoint does not match its trace.");
        fresh._state = RunJson.Copy(checkpoint.State);
        fresh._trace.Commands.AddRange(RunJson.Copy(save.Trace.Commands.Take((int)checkpoint.Sequence).ToList()));
        for (int i = (int)checkpoint.Sequence; i < save.Trace.Commands.Count; i++)
        {
            var entry = save.Trace.Commands[i];
            var result = fresh.Execute(entry.Command, entry.DecisionMilliseconds, i);
            if (!result.Accepted || fresh._trace.Commands[^1].StateHash != entry.StateHash)
                throw new ReplayDivergenceException(entry.Sequence, "Checkpoint tail does not reproduce.");
        }
        return fresh;
    }

    private static RunSession ValidateTrace(RunTrace trace, EncounterRegistry? registry, IWorldGenerator? generator)
    {
        var header = trace.Header;
        if (header.FormatVersion != 1 || header.SimulationVersion != SimulationVersion
            || header.RngVersion != RunRandom.Version || header.ContentVersion != "engine-content-v1")
            throw new FormatException("This trace requires a different game/content version.");
        if (RunJson.Hash(header.Definition) != header.ConfigurationHash) throw new FormatException("Trace configuration hash does not match.");
        for (int i = 0; i < trace.Commands.Count; i++)
            if (trace.Commands[i].Sequence != i + 1 || trace.Commands[i].DecisionMilliseconds < 0 || trace.Commands[i].Command.Version != 1)
                throw new FormatException("Invalid command sequence or version.");
        var generated = Start(header.Seed, header.Definition, registry, generator);
        var actual = generated._trace.Header;
        if (actual.GeneratorVersion != header.GeneratorVersion || RunJson.Hash(actual.Handlers) != RunJson.Hash(header.Handlers)
            || actual.ConfigurationHash != header.ConfigurationHash)
            throw new FormatException("This trace requires different encounter or generation rules.");
        if (actual.InitialStateHash != header.InitialStateHash) throw new ReplayDivergenceException(0, "Initial state differs.");
        return new RunSession(generated._state, new RunTrace { Header = RunJson.Copy(header) }, generated._registry);
    }
    private static void Validate(RunDefinition d, EncounterRegistry registry)
    {
        if (d.Width < 1 || d.Width > 26 || d.Height < 1 || d.Height > 26
            || d.StartX < 0 || d.StartX >= d.Width || d.StartY < 0 || d.StartY >= d.Height)
            throw new FormatException("Invalid overworld dimensions or starting square.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var squares = new HashSet<(int, int)>();
        foreach (var e in d.Encounters)
        {
            if (string.IsNullOrWhiteSpace(e.Id) || !ids.Add(e.Id) || !squares.Add((e.X, e.Y))
                || e.X < 0 || e.X >= d.Width || e.Y < 0 || e.Y >= d.Height || (e.X == d.StartX && e.Y == d.StartY))
                throw new FormatException("Encounter IDs and squares must be unique and inside the overworld.");
            _ = registry.Get(e.Kind);
        }
    }
}
