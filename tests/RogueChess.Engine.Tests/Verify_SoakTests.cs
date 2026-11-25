using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RogueChess.Engine.AI;
using RogueChess.Engine.Content;
using RogueChess.Engine.Content.Abilities;
using RogueChess.Engine.Content.BoardEffects;
using RogueChess.Engine.Content.Consumables;
using RogueChess.Engine.Content.Relics;
using RogueChess.Engine.Content.Tiles;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;
using Xunit;
using Xunit.Abstractions;

namespace RogueChess.Engine.Tests;

/// <summary>
/// Cross-family soak: random play and AI-vs-AI on boards that mix every ability, tile, board effect, relic and
/// consumable, plus every legacy and sandbox map. After every ply: incremental hash = from-scratch hash, clone is
/// identical (dump, hash, history, legal moves), every legal move has a unique text that parses back to it, a sample
/// of legal moves (at least one of every action kind present) plays and undoes exactly, and at the end unwinding
/// the whole game restores the start exactly.
///
/// The heavy variant runs many more seeds; it only runs when the environment variable ROGUECHESS_SOAK=heavy is set
/// (e.g. <c>ROGUECHESS_SOAK=heavy dotnet test --filter Category=Heavy</c>), otherwise it returns immediately.
/// </summary>
public class Verify_SoakTests
{
    private readonly ITestOutputHelper _out;
    public Verify_SoakTests(ITestOutputHelper output) => _out = output;

    private static readonly Catalog Content = ContentLibrary.CreateCatalog();

    private static readonly AbilityDef[] AllAbilities =
        Content.Abilities.Append(RevenantAbility.Forever).ToArray();

    private static readonly TileDef[] HazardTiles =
    {
        WallTile.Instance, FogTile.Instance, GuardianTile.Instance, FireTile.Instance, FreezeTile.Instance,
        SlipperyTile.Instance,
    };

    internal sealed class Stats
    {
        public int Games, Plies, Rejected, PlayUndoChecks;
        public readonly Dictionary<string, int> Kinds = new();
        public void Count(string kind) => Kinds[kind] = Kinds.GetValueOrDefault(kind) + 1;
        public override string ToString()
            => $"games={Games} plies={Plies} rejected={Rejected} play/undo checks={PlayUndoChecks} kinds: "
             + string.Join(", ", Kinds.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"));
    }

    internal static string Kind(Move m)
    {
        if (m.IsConsumable) return m.To >= 0 ? "consumable-targeted" : "consumable";
        if (m.Is(MoveFlags.Cast)) return "cast";
        if (m.Is(MoveFlags.Ability)) return "ability";
        if (m.Is(MoveFlags.Castle)) return "castle";
        if (m.Is(MoveFlags.EnPassant)) return "en-passant";
        if (m.Is(MoveFlags.Promotion)) return "promotion";
        if (m.Is(MoveFlags.Capture)) return "capture";
        return "quiet";
    }

    // ------------------------------------------------------------------ positions

    /// <summary>An 8x8 game where every piece may carry abilities and the middle has every tile kind.</summary>
    internal static GameState MixedBoard(int seed)
    {
        var rng = new Random(seed);
        var s = Fen.Parse(seed % 2 == 0
            ? "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"
            : "r3k2r/ppp2ppp/2n1b3/8/8/2N1B3/PPP2PPP/R3K2R w KQkq - 0 1",
            // Every third seed uses standard rules (castling, en passant, no self-check), so ability actions, casts and
            // consumables also go through the legality check that resolves every move.
            seed % 3 == 0 ? RuleSet.Standard : RuleSet.Rogue);

        foreach (var p in s.PiecesOnBoard().ToList())
        {
            int count = rng.Next(4) == 0 ? 0 : rng.Next(1, 4);
            for (int i = 0; i < count; i++)
            {
                var a = AllAbilities[rng.Next(AllAbilities.Length)];
                if (p.HasAbility(a)) continue;
                // Revenant and its forever variant on one piece would leave two tombs fighting over a square: fine, but
                // keep it rarer than other combos by skipping half the time.
                if (a is RevenantAbility && p.Abilities.Any(x => x.Def is RevenantAbility) && rng.Next(2) == 0) continue;
                if (a == FrozenAbility.Instance) s.GrantAbility(p, a, rng.Next(1, 3));
                else if (a == OnFireAbility.Instance) s.GrantAbility(p, a, rng.Next(2, 6));
                else if (a == MarksmanAbility.Instance) s.GrantAbility(p, a, rng.Next(1, 4));
                else s.GrantAbility(p, a);
            }
        }

        for (int y = 2; y <= 5; y++)
            for (int x = 0; x < 8; x++)
            {
                int sq = s.Sq(x, y);
                if (s.PieceAt(sq) != null || rng.Next(3) != 0) continue;
                s.SetTile(sq, HazardTiles[rng.Next(HazardTiles.Length)]);
            }

        // A tomb on each side holding a piece that died before the game: an ally walking onto it revives it.
        foreach (var side in new[] { Side.White, Side.Black })
        {
            int y = side == Side.White ? 3 : 4;
            int x = rng.Next(8);
            int sq = s.Sq(x, y);
            if (s.PieceAt(sq) != null) continue;
            var dead = s.Place(rng.Next(2) == 0 ? StandardPieces.Knight : StandardPieces.Bishop, side, sq);
            s.GrantAbility(dead, RevenantAbility.Instance);
            s.GrantAbility(dead, MarksmanAbility.Instance);
            s.RemoveFromBoard(dead);
            s.SetTile(sq, TombTile.Instance, TombTile.VarsFor(dead));
        }

        // Each board effect is in play in about two games out of three, so seeds differ in what they mix.
        if (rng.Next(3) != 0) s.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(3 + rng.Next(4)), new WindGustEffect(rng.Next(2) == 0 ? Side.White : Side.Black, 1)));
        if (rng.Next(3) != 0) s.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(5 + rng.Next(4), 2), new CheckerboardWipeEffect(SquareColor.White, 1, alternating: true)));
        if (rng.Next(3) != 0) s.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(4 + rng.Next(4)), new WallsPlacementEffect(1 + rng.Next(2))));
        if (rng.Next(3) != 0) s.AddGlobal(new BoardEffectDef(new TurnDelayCondition(5 + rng.Next(5)), new WallsPlacementEffect(3)));
        if (rng.Next(3) != 0) s.AddGlobal(new BoardEffectDef(new BoardValueCondition(Side.White, 30, 2), new CheckerboardWipeEffect(SquareColor.Black, 1)));
        s.AddGlobal(TimeBankRelic.White);
        s.AddGlobal(TimeBankRelic.Black);
        s.AddGlobal(RoyalPromotionRelic.White);
        if (rng.Next(2) == 0) s.AddGlobal(RoyalPromotionRelic.Black);
        ExtraTurnPotion.Instance.Give(s, 1 + rng.Next(2), 1 + rng.Next(2));
        FortifyPotion.Instance.Give(s, rng.Next(2), 1);
        s.SeedRandom((ulong)seed * 7919 + 1);
        s.ClearHistory();
        Game.BeginGame(s);
        return s;
    }

    internal static GameState LoadMap(string path)
    {
        var s = MapLoader.Load(File.ReadAllText(path), ContentLibrary.CreateCatalog());
        s.SeedRandom(0xC0FFEE);
        s.ClearHistory();
        Game.BeginGame(s);
        return s;
    }

    private static string MapsDir => Path.GetDirectoryName(MapLoaderTests.LegacyMapsDir)!;

    public static IEnumerable<object[]> AllMaps()
    {
        foreach (var dir in new[] { "legacy", "sandbox" })
            foreach (var file in Directory.GetFiles(Path.Combine(MapsDir, dir), "*.json").OrderBy(f => f, StringComparer.Ordinal))
                yield return new object[] { dir + "/" + Path.GetFileName(file) };
    }

    // ------------------------------------------------------------------ exactness

    internal static string FullDump(GameState s)
        => s.Dump() + "hash=" + s.Hash + " history=" + string.Join(";", s.History.Select(h => $"{h.Side}:{h.Ply}:{h.Move}"))
         + $" clock={s.ClockMs(Side.White)}/{s.ClockMs(Side.Black)}";

    private static List<string> Formats(GameState s, List<Move> moves) => moves.Select(m => MoveText.Format(s, m)).ToList();

    /// <summary>All per-ply checks except play/undo. Returns the legal moves.</summary>
    internal static List<Move> CheckPosition(GameState s)
    {
        Assert.Equal(s.ComputeHashFromScratch(), s.Hash);

        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(s, moves);
        var texts = Formats(s, moves);

        // Every move has its own text, so MoveText.FindLegal(Format(m)) finds exactly m.
        var seen = new Dictionary<string, Move>();
        for (int i = 0; i < moves.Count; i++)
        {
            if (seen.TryGetValue(texts[i], out var other))
                Assert.Fail($"two legal moves format as '{texts[i]}': {other} and {moves[i]}\n{s.Dump()}");
            seen[texts[i]] = moves[i];
        }

        var clone = s.Clone();
        Assert.Equal(FullDump(s), FullDump(clone));
        Assert.Equal(clone.ComputeHashFromScratch(), clone.Hash);
        var cloneMoves = new List<Move>();
        MoveGenerator.GenerateLegal(clone, cloneMoves);
        Assert.Equal(texts, Formats(clone, cloneMoves));
        return moves;
    }

    /// <summary>Play <paramref name="m"/> and undo it: the state must come back exactly.</summary>
    internal static void CheckPlayUndo(GameState s, Move m)
    {
        string before = FullDump(s);
        int mark = s.UndoDepth;
        var result = Game.PlayTurn(s, m);
        Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
        if (!result.Accepted) Assert.Equal(before, FullDump(s));
        s.UndoTo(mark);
        Assert.Equal(before, FullDump(s));
    }

    /// <summary>One random game; the sample size controls how many legal moves are play/undo-checked per ply.</summary>
    /// <remarks>Play continues past the end of the game (a side without royals): the engine must stay exact on any
    /// position, and longer games reach more combinations.</remarks>
    internal static void RandomGame(GameState s, int seed, int maxPlies, int sample, Stats stats)
    {
        var rng = new Random(seed);
        string start = FullDump(s);
        int startMark = s.UndoDepth;
        var marks = new Stack<(int Mark, string Dump)>();

        for (int ply = 0; ply < maxPlies; ply++)
        {
            var moves = CheckPosition(s);
            if (moves.Count == 0) { stats.Count("(ended: no moves)"); break; }

            // Round-trip one random move through the parser.
            var probe = moves[rng.Next(moves.Count)];
            Assert.Equal(probe, MoveText.FindLegal(s, MoveText.Format(s, probe)));

            // Play/undo: one move of every action kind present, plus a random sample.
            var checkedSet = new HashSet<int>();
            foreach (var group in Enumerable.Range(0, moves.Count).GroupBy(i => Kind(moves[i])))
                checkedSet.Add(group.ElementAt(rng.Next(group.Count())));
            for (int i = 0; i < sample && checkedSet.Count < moves.Count; i++) checkedSet.Add(rng.Next(moves.Count));
            if (sample < 0) for (int i = 0; i < moves.Count; i++) checkedSet.Add(i);
            foreach (int i in checkedSet)
            {
                CheckPlayUndo(s, moves[i]);
                stats.PlayUndoChecks++;
            }

            // Prefer the special action kinds so they get played, not just generated.
            // Ranged shots are rare among the legal moves, so they are always picked when offered half the time.
            var shots = moves.Where(m => m.Is(MoveFlags.Ability) && !m.Is(MoveFlags.Cast)).ToList();
            var special = shots.Count > 0 && rng.Next(2) == 0 ? shots : moves.Where(m => m.Is(MoveFlags.Ability) || m.IsConsumable).ToList();
            var move = special.Count > 0 && rng.Next(4) == 0 ? special[rng.Next(special.Count)] : moves[rng.Next(moves.Count)];

            marks.Push((s.UndoDepth, FullDump(s)));
            int historyBefore = s.History.Count;
            var result = Game.PlayTurn(s, move);
            Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
            if (!result.Accepted)
            {
                stats.Rejected++;
                Assert.Equal(marks.Peek().Dump, FullDump(s));
                marks.Pop();
                continue;
            }
            Assert.Equal(historyBefore + 1, s.History.Count);
            stats.Plies++;
            stats.Count(Kind(move));
        }

        while (marks.Count > 0)
        {
            var (mark, dump) = marks.Pop();
            s.UndoTo(mark);
            Assert.Equal(dump, FullDump(s));
        }
        s.UndoTo(startMark);
        Assert.Equal(start, FullDump(s));
        stats.Games++;
    }

    internal static void AiGame(GameState s, IChessAgent white, IChessAgent black, int maxPlies, Stats stats)
    {
        string start = FullDump(s);
        int startMark = s.UndoDepth;
        for (int ply = 0; ply < maxPlies; ply++)
        {
            if (s.Rules.Evaluate(s) != GameOutcome.Ongoing) break;
            var moves = CheckPosition(s);
            if (moves.Count == 0) break;

            string before = FullDump(s);
            int depth = s.UndoDepth;
            var agent = s.SideToMove == Side.White ? white : black;
            var decision = agent.Decide(s);
            Assert.Equal(before, FullDump(s));
            Assert.Equal(depth, s.UndoDepth);
            Assert.NotNull(decision.Move);
            var move = decision.Move!.Value;
            Assert.Contains(move, moves);

            var result = Game.PlayTurn(s, move);
            Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
            if (!result.Accepted) { stats.Rejected++; Assert.Equal(before, FullDump(s)); break; }
            stats.Plies++;
            stats.Count(Kind(move));
        }
        s.UndoTo(startMark);
        Assert.Equal(start, FullDump(s));
        stats.Games++;
    }

    private static IChessAgent QuickAgent(ulong seed, int nodes = 1500)
        => new AgentFactory().Create(AgentProfile.Casual with
        {
            Id = "soak", MaxDepth = 2, NodeBudget = nodes, TimeBudgetMs = 0, QuiescenceDepth = 2, TableBits = 12, Seed = seed,
        });

    // ------------------------------------------------------------------ tests

    [Fact]
    public void Random_play_with_all_content_stays_exact()
    {
        var stats = new Stats();
        for (int seed = 1; seed <= 8; seed++)
            RandomGame(MixedBoard(seed), seed, maxPlies: 50, sample: 3, stats);
        _out.WriteLine(stats.ToString());
        Assert.True(stats.Plies > 250, stats.ToString());
        foreach (var kind in new[] { "ability", "cast", "consumable", "capture", "quiet" })
            Assert.True(stats.Kinds.GetValueOrDefault(kind) > 0, $"no {kind} played: {stats}");
    }

    [Fact]
    public void AI_vs_AI_with_all_content_stays_exact()
    {
        var stats = new Stats();
        for (int seed = 11; seed <= 12; seed++)
            AiGame(MixedBoard(seed), QuickAgent((ulong)seed), QuickAgent((ulong)seed + 100), maxPlies: 14, stats);
        _out.WriteLine(stats.ToString());
        Assert.True(stats.Plies >= 10, stats.ToString());
    }

    [Theory]
    [MemberData(nameof(AllMaps))]
    public void Map_plays_random_and_AI_games_exactly(string map)
    {
        var path = Path.Combine(MapsDir, map);
        var stats = new Stats();
        RandomGame(LoadMap(path), 3, maxPlies: 30, sample: 2, stats);
        AiGame(LoadMap(path), QuickAgent(1, 800), QuickAgent(2, 800), maxPlies: 8, stats);
        _out.WriteLine($"{map}: {stats}");
    }

    [Fact]
    [Trait("Category", "Heavy")]
    public void Heavy_soak_many_seeds()
    {
        if (Environment.GetEnvironmentVariable("ROGUECHESS_SOAK") != "heavy") return;
        int seeds = int.TryParse(Environment.GetEnvironmentVariable("ROGUECHESS_SOAK_SEEDS"), out int n) ? n : 200;

        var random = new Stats();
        for (int seed = 1000; seed < 1000 + seeds; seed++)
            RandomGame(MixedBoard(seed), seed, maxPlies: 120, sample: -1, random);
        _out.WriteLine("random: " + random);

        var ai = new Stats();
        for (int seed = 5000; seed < 5000 + Math.Max(4, seeds / 10); seed++)
            AiGame(MixedBoard(seed), QuickAgent((ulong)seed, 4000), QuickAgent((ulong)seed * 3, 4000), maxPlies: 40, ai);
        _out.WriteLine("ai: " + ai);

        var maps = new Stats();
        foreach (var row in AllMaps())
        {
            var path = Path.Combine(MapsDir, (string)row[0]);
            for (int seed = 1; seed <= 10; seed++)
                RandomGame(LoadMap(path), seed, maxPlies: 80, sample: -1, maps);
            AiGame(LoadMap(path), QuickAgent(7, 4000), QuickAgent(8, 4000), maxPlies: 30, maps);
        }
        _out.WriteLine("maps: " + maps);
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "roguechess_soak.txt"), $"random: {random}\nai: {ai}\nmaps: {maps}\n");
    }
}
