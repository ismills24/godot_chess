using System;
using System.Text;
using System.Linq;

namespace RogueChess.Runs;

/// <summary>Named SplitMix64 streams. Algorithm, UTF-8 domain hashing, and integer draws are versioned.</summary>
public static class RunRandom
{
    public const string Version = "splitmix64-utf8-v1";
    public static ulong Derive(ulong seed, string domain)
    {
        ulong hash = 14695981039346656037UL;
        foreach (byte value in Encoding.UTF8.GetBytes(domain)) hash = unchecked((hash ^ value) * 1099511628211UL);
        return Mix(seed ^ hash);
    }
    private static ulong Mix(ulong z)
    {
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        return z ^ (z >> 31);
    }
    public static int Next(RunState state, ulong seed, string domain, int maximum)
    {
        if (maximum < 1) throw new ArgumentOutOfRangeException(nameof(maximum));
        ulong current = state.RandomStreams.TryGetValue(domain, out var existing) ? existing : Derive(seed, domain);
        current = unchecked(current + 0x9E3779B97F4A7C15UL);
        state.RandomStreams[domain] = current;
        return (int)(Mix(current) % (ulong)maximum);
    }
}

public interface IWorldGenerator
{
    string Version { get; }
    RunState Generate(RunDefinition definition, ulong seed);
}
/// <summary>Fixture overworld: layout is configured; each encounter still generates from its own seed.</summary>
public sealed class ConfiguredWorldGenerator : IWorldGenerator
{
    public string Version => "configured-board-v1";
    public RunState Generate(RunDefinition definition, ulong seed) => new()
    {
        Width = definition.Width, Height = definition.Height, X = definition.StartX, Y = definition.StartY,
        Inventory = RunJson.Copy(definition.Inventory),
        Encounters = definition.Encounters.Select(e => new EncounterState { Definition = RunJson.Copy(e) }).ToList(),
    };
}
