using System;
using System.IO;
using System.Linq;
using RogueChess.Engine.AI;
using RogueChess.Engine.Content;
using RogueChess.Engine.Core;
using RogueChess.Engine.Encounters;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;
using Xunit;
using Xunit.Abstractions;

namespace RogueChess.Engine.Tests;

public class EncounterTests
{
    private readonly ITestOutputHelper _out;
    public EncounterTests(ITestOutputHelper output) => _out = output;

    private static readonly Catalog Catalog = ContentLibrary.CreateCatalog();

    private static EncounterSet Prototype()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "encounters", "prototype.json"))) dir = Path.GetDirectoryName(dir)!;
        return EncounterSet.Parse(File.ReadAllText(Path.Combine(dir, "encounters", "prototype.json")));
    }

    [Fact]
    public void Prototype_set_loads_and_names_only_unported_abilities_as_unknown()
    {
        var set = Prototype();
        Assert.Equal(new[] { "enemy-1", "enemy-2", "boss-1" }, set.Encounters.Select(e => e.Id));
        Assert.All(set.Encounters, e => Assert.Equal((6, 6), (e.Width, e.Height)));
        Assert.Equal(
            new[] { "Marksman", "Revenant", "ChronoCapture" },
            set.UnknownAbilities(Catalog));
        Assert.Equal("\"casual\"", set.Encounters[0].Ai);
    }

    [Theory]
    [InlineData(8, 0)]
    [InlineData(10, 1)]
    [InlineData(16, 4)]
    public void Armies_respect_their_budgets(int pieceValue, int abilityValue)
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var army = EncounterGenerator.GenerateArmy(new ArmySpec { PieceValue = pieceValue, AbilityValue = abilityValue }, Catalog, seed);
            Assert.Single(army, p => p.Def.IsRoyal);
            Assert.True(army.Count(p => p.Def.IsPawn) >= 2);
            Assert.Equal(pieceValue, army.Sum(p => p.Def.Value)); // pawns make every budget exactly spendable
            int abilities = army.Sum(p => p.Abilities.Sum(a => a.Value));
            Assert.InRange(abilities, 0, abilityValue);
            Assert.All(army, p => Assert.Equal(p.Abilities.Count, p.Abilities.Distinct().Count()));
        }
    }

    [Fact]
    public void The_player_gets_one_starting_ability_on_its_most_valuable_piece()
    {
        var spec = Prototype().Player;
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var army = EncounterGenerator.GenerateArmy(spec, Catalog, seed);
            var gifted = Assert.Single(army, p => p.Abilities.Count > 0);
            Assert.Equal(army.Where(p => !p.Def.IsRoyal).Max(p => p.Def.Value), gifted.Def.Value);
            Assert.Contains(gifted.Abilities[0].Id, new[] { "exploding", "phantom", "predator", "fire-mage", "freeze-mage", "wall-placement" });
        }
    }

    [Fact]
    public void Battles_put_each_army_in_its_own_half_with_kings_in_the_back_middle()
    {
        var set = Prototype();
        for (ulong seed = 1; seed <= 100; seed++)
        {
            foreach (var enc in set.Encounters)
            {
                var g = EncounterGenerator.Generate(set, enc, Catalog, seed);
                var s = g.State;
                Assert.Equal(Side.White, s.SideToMove);
                Assert.Equal(g.Player.Count, s.PiecesOnBoard(Side.White).Count());
                Assert.Equal(g.Enemy.Count, s.PiecesOnBoard(Side.Black).Count());
                Assert.All(s.PiecesOnBoard(Side.White), p => Assert.InRange(s.RankOf(p.Square), 0, 2));
                Assert.All(s.PiecesOnBoard(Side.Black), p => Assert.InRange(s.RankOf(p.Square), 3, 5));

                var wk = s.PiecesOnBoard(Side.White).Single(p => p.Def.IsRoyal);
                var bk = s.PiecesOnBoard(Side.Black).Single(p => p.Def.IsRoyal);
                Assert.Equal(0, s.RankOf(wk.Square));
                Assert.Equal(5, s.RankOf(bk.Square));
                Assert.InRange(s.FileOf(wk.Square), 2, 3);
                Assert.InRange(s.FileOf(bk.Square), 2, 3);

                Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
            }
        }
    }

    [Fact]
    public void The_same_seed_gives_the_same_battle()
    {
        var set = Prototype();
        var a = EncounterGenerator.Generate(set, set.Encounters[2], Catalog, 42);
        var b = EncounterGenerator.Generate(set, set.Encounters[2], Catalog, 42);
        var c = EncounterGenerator.Generate(set, set.Encounters[2], Catalog, 43);
        Assert.Equal(a.State.Dump(), b.State.Dump());
        Assert.NotEqual(a.State.Dump(), c.State.Dump());
    }

    /// <summary>
    /// Informational: how long AI-vs-AI encounter battles run. The battle loop is meant to be quick (a handful of
    /// good moves), so these numbers matter for design; the assertion only checks that games play out.
    /// </summary>
    [Fact]
    public void Encounter_battles_play_out_with_the_ai()
    {
        var set = Prototype();
        var factory = new AgentFactory();
        int decided = 0, games = 0;
        long totalPlies = 0;
        var started = System.Diagnostics.Stopwatch.StartNew();
        foreach (var enc in set.Encounters)
        {
            for (ulong seed = 1; seed <= 4; seed++)
            {
                var g = EncounterGenerator.Generate(set, enc, Catalog, seed);
                var s = g.State;
                var white = factory.Create(AgentProfile.Club with { Seed = seed });
                var black = factory.CreateFromJson(enc.Ai!);
                var outcome = GameOutcome.Ongoing;
                int ply = 0;
                for (; ply < 120 && (outcome = s.Rules.Evaluate(s)) == GameOutcome.Ongoing; ply++)
                {
                    var move = (s.SideToMove == Side.White ? white : black).Decide(s).Move;
                    if (move == null) break;
                    Game.PlayTurn(s, move.Value);
                }
                games++;
                totalPlies += ply;
                if (outcome is GameOutcome.WhiteWins or GameOutcome.BlackWins) decided++;
                _out.WriteLine($"{enc.Id} seed {seed}: {outcome} after {ply} plies. Player: {string.Join(", ", g.Player)} | Enemy: {string.Join(", ", g.Enemy)}");
            }
        }
        _out.WriteLine($"{decided}/{games} decided, average {totalPlies / (double)games:0.0} plies, {started.ElapsedMilliseconds} ms total");
        Assert.True(decided >= games / 2, $"only {decided}/{games} battles reached a result");
    }
}
