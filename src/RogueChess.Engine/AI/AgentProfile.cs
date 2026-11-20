using System.Collections.Generic;

namespace RogueChess.Engine.AI;

/// <summary>
/// Evaluation weights: what an agent cares about. All weights are in centipawns (100 = one pawn) except
/// <see cref="Material"/> and <see cref="AbilityMaterial"/>, which are percentages of the content's own values.
/// Zero switches a term off (and skips its cost).
/// </summary>
public sealed record EvalWeights
{
    /// <summary>Percent of each piece's <c>PieceDef.Value</c> × 100 counted as material.</summary>
    public int Material { get; init; } = 100;

    /// <summary>Percent of each ability's <c>AbilityDef.Value</c> × 100 counted as material.</summary>
    public int AbilityMaterial { get; init; } = 100;

    /// <summary>Per available move of each piece.</summary>
    public int Mobility { get; init; } = 3;

    /// <summary>Per step closer to the centre, for non-royal, non-pawn pieces.</summary>
    public int Center { get; init; } = 4;

    /// <summary>Per rank a pawn has advanced.</summary>
    public int PawnAdvance { get; init; } = 6;

    /// <summary>Percent of an enemy piece's value earned for attacking it (the biggest threat per attacker counts).</summary>
    public int Threats { get; init; } = 10;

    /// <summary>Per own piece within two squares of an enemy royal (and a big bonus for attacking it).</summary>
    public int KingPressure { get; init; } = 8;

    /// <summary>Per own piece next to one of our royals.</summary>
    public int KingShelter { get; init; } = 8;

    /// <summary>Bonus for being the side to move.</summary>
    public int Tempo { get; init; } = 10;

    public static EvalWeights Balanced { get; } = new();
}

/// <summary>
/// Everything that makes one opponent different from another: strength (search limits) and personality
/// (weights, randomness, attitude to draws). Profiles are plain data, so levels can define them in JSON
/// and an <see cref="AgentFactory"/> turns them into agents.
/// </summary>
public sealed record AgentProfile
{
    public string Id { get; init; } = "custom";
    public string Name { get; init; } = "Custom";
    public string Description { get; init; } = "";

    /// <summary>Which agent implementation to build (see <see cref="AgentFactory.RegisterKind"/>).</summary>
    public string Kind { get; init; } = AgentFactory.SearchKind;

    /// <summary>Which evaluator to use (see <see cref="AgentFactory.RegisterEvaluator"/>).</summary>
    public string Evaluator { get; init; } = AgentFactory.WeightedEvaluator;

    // ---------------------------------------------------------------- strength

    /// <summary>Iterative deepening stops after this many plies.</summary>
    public int MaxDepth { get; init; } = 3;

    /// <summary>
    /// Node budget per decision. This is the reproducible strength limit: the same profile picks the same move
    /// on any machine. 0 = unlimited.
    /// </summary>
    public long NodeBudget { get; init; } = 300_000;

    /// <summary>Wall-clock safety cap in milliseconds (not reproducible across machines). 0 = none.</summary>
    public int TimeBudgetMs { get; init; } = 3_000;

    /// <summary>How many plies of captures to follow past the horizon. 0 = none (the agent misses recaptures).</summary>
    public int QuiescenceDepth { get; init; } = 6;

    /// <summary>Transposition table size as a power of two (entries).</summary>
    public int TableBits { get; init; } = 18;

    // ---------------------------------------------------------------- personality

    public EvalWeights Weights { get; init; } = EvalWeights.Balanced;

    /// <summary>Any root move within this many centipawns of the best is a candidate; one is picked at random.</summary>
    public int ChoiceMargin { get; init; }

    /// <summary>Each root move's score is shifted by a fixed random amount in [-RootNoise, +RootNoise] centipawns.</summary>
    public int RootNoise { get; init; }

    /// <summary>Chance (0–1) of playing a random move that doesn't lose the king outright.</summary>
    public double BlunderChance { get; init; }

    /// <summary>
    /// How much the agent dislikes draws, in centipawns. Positive = plays on and avoids repetitions;
    /// negative = happy to take a draw.
    /// </summary>
    public int Contempt { get; init; }

    /// <summary>Seeds all randomness. The same profile and position always give the same move.</summary>
    public ulong Seed { get; init; } = 1;

    public override string ToString() => Id;

    // ---------------------------------------------------------------- built-in presets

    /// <summary>Uniformly random legal moves.</summary>
    public static AgentProfile Random { get; } = new()
    {
        Id = "random", Name = "Random", Kind = AgentFactory.RandomKind,
        Description = "Plays any legal move.",
    };

    public static AgentProfile Novice { get; } = new()
    {
        Id = "novice", Name = "Novice", MaxDepth = 1, NodeBudget = 20_000, QuiescenceDepth = 2,
        ChoiceMargin = 150, RootNoise = 60, BlunderChance = 0.2,
        Description = "Looks one move ahead, often misses tactics, sometimes blunders.",
    };

    public static AgentProfile Casual { get; } = new()
    {
        Id = "casual", Name = "Casual", MaxDepth = 2, NodeBudget = 60_000, QuiescenceDepth = 4,
        ChoiceMargin = 60, RootNoise = 30, BlunderChance = 0.05,
        Description = "Sees simple tactics; makes the odd loose move.",
    };

    public static AgentProfile Club { get; } = new()
    {
        Id = "club", Name = "Club", MaxDepth = 3, NodeBudget = 300_000, QuiescenceDepth = 6,
        ChoiceMargin = 15, RootNoise = 10,
        Description = "Solid three-ply search with capture follow-ups.",
    };

    public static AgentProfile Master { get; } = new()
    {
        Id = "master", Name = "Master", MaxDepth = 8, NodeBudget = 2_000_000, TimeBudgetMs = 6_000,
        QuiescenceDepth = 8, TableBits = 20,
        Description = "Deep search, no randomness.",
    };

    public static AgentProfile Aggressive { get; } = Club with
    {
        Id = "aggressive", Name = "Aggressive", Contempt = 60,
        Weights = new EvalWeights { Material = 90, Threats = 25, KingPressure = 30, KingShelter = 2, Mobility = 5 },
        Description = "Club strength; swarms the enemy king, trades material for initiative, avoids draws.",
    };

    public static AgentProfile Defensive { get; } = Club with
    {
        Id = "defensive", Name = "Defensive", Contempt = -40,
        Weights = new EvalWeights { Threats = 4, KingPressure = 2, KingShelter = 30, Mobility = 2 },
        Description = "Club strength; keeps pieces around its king and happily takes a draw.",
    };

    public static AgentProfile Greedy { get; } = Club with
    {
        Id = "greedy", Name = "Greedy",
        Weights = new EvalWeights { Material = 120, AbilityMaterial = 160, Mobility = 1, Center = 1, KingPressure = 2 },
        Description = "Club strength; grabs material and prizes pieces with abilities.",
    };

    public static AgentProfile Chaotic { get; } = Casual with
    {
        Id = "chaotic", Name = "Chaotic", ChoiceMargin = 120, RootNoise = 80, BlunderChance = 0.1, Contempt = 30,
        Description = "Casual strength with a lot of randomness: unpredictable, rarely the best move.",
    };

    public static IReadOnlyList<AgentProfile> Presets { get; } = new[]
    {
        Random, Novice, Casual, Club, Master, Aggressive, Defensive, Greedy, Chaotic,
    };
}
