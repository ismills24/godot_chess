using System;
using System.Collections.Generic;
using System.Linq;
using RogueChess.Engine.Content;
using RogueChess.Engine.Content.Abilities;
using RogueChess.Engine.Content.BoardEffects;
using RogueChess.Engine.Content.Tiles;
using RogueChess.Engine.Core;
using RogueChess.Engine.Encounters;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;
using Xunit;

namespace RogueChess.Engine.Tests;

/// <summary>Handmade rosters and the Reinforcements board effect that spawns from them.</summary>
public class ReinforcementsTests
{
    // ------------------------------------------------------------------ Rosters

    [Fact]
    public void Soldier_roster_ships_with_the_content_library()
    {
        var r = ContentLibrary.CreateCatalog().FindRoster("SOLDIER");
        Assert.NotNull(r);
        Assert.Equal("Soldier", r!.Name);
        Assert.Equal(new[] { StandardPieces.Pawn, StandardPieces.Knight, StandardPieces.Bishop }, r.Pieces);
        Assert.Equal(new AbilityDef[] { MarksmanAbility.Instance, PiercingAbility.Instance }, r.Abilities);
    }

    [Fact]
    public void Roster_resolves_ids_case_insensitively_and_defaults_its_name()
    {
        var r = RosterJson.Parse("""{ "id": "scout", "pieces": ["knight"] }""", ContentLibrary.CreateCatalog());
        Assert.Equal("scout", r.Name);
        Assert.Equal(new[] { StandardPieces.Knight }, r.Pieces);
        Assert.Empty(r.Abilities);
    }

    [Theory]
    [InlineData("""{ "pieces": ["Knight"] }""")]
    [InlineData("""{ "id": "x" }""")]
    [InlineData("""{ "id": "x", "pieces": [] }""")]
    [InlineData("""{ "id": "x", "pieces": ["Dragon"] }""")]
    [InlineData("""{ "id": "x", "pieces": ["Knight"], "abilities": ["Laser"] }""")]
    [InlineData("""{ "id": "x", "pieces": ["Knight"], "colour": "red" }""")]
    [InlineData("""not json""")]
    public void Bad_rosters_throw_format_exceptions(string json)
        => Assert.Throws<FormatException>(() => RosterJson.Parse(json, ContentLibrary.CreateCatalog()));

    // ------------------------------------------------------------------ The effect

    private static readonly Catalog Catalog = ContentLibrary.CreateCatalog();
    private static readonly RosterDef Soldier = Catalog.FindRoster("soldier")!;

    /// <summary>Kings on a1 (White, to move) and h8 (Black).</summary>
    private const string Kings = "7k/8/8/8/8/8/8/K7 w - - 0 1";

    private static GameState Rogue(string fen) => Fen.Parse(fen, RuleSet.Rogue);
    private static int Sq(IBoardView s, string name) => MoveText.ParseSquare(s, name);
    private static (int X, int Y) At(string name) => (name[0] - 'a', int.Parse(name.AsSpan(1)) - 1);
    private static (int X, int Y)[] Squares(params string[] names) => names.Select(At).ToArray();
    private static (int X, int Y)[] Rows(params int[] ranks)
        => ranks.SelectMany(r => Enumerable.Range(0, 8).Select(x => (x, r - 1))).ToArray();

    private static RosterDef Roster(params PieceDef[] pieces) => new("test", "Test", pieces, Array.Empty<AbilityDef>());

    private static ReinforcementsEffect Effect(RosterDef roster, int pieceValue, int abilityValue, (int X, int Y)[] squares, Side side = Side.Black)
        => new(side, roster, pieceValue, abilityValue, squares, Catalog);

    private static GlobalInstance AddEffect(GameState s, BoardEffect effect, BoardCondition? condition = null)
    {
        var g = s.AddGlobal(new BoardEffectDef(condition ?? new TurnDelayCondition(1), effect));
        s.ClearHistory();
        return g;
    }

    private static void Play(GameState s, string move)
    {
        var m = MoveText.FindLegal(s, move) ?? throw new InvalidOperationException($"{move} is not legal");
        var result = Game.PlayTurn(s, m, new ResolveOptions(Trace: true));
        Assert.True(result.Accepted, result.RejectReason);
        Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
    }

    private static List<Piece> Newcomers(GameState s, Side side) =>
        s.PiecesOnBoard().Where(p => p.Side == side && !p.Def.IsRoyal).ToList();

    [Fact]
    public void Reinforcements_spawn_only_roster_pieces_and_abilities_inside_the_area()
    {
        var area = Rows(6, 7);
        var squares = area.Select(q => q.Y * 8 + q.X).ToHashSet();
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var s = Rogue(Kings);
            s.SeedRandom(seed);
            AddEffect(s, Effect(Soldier, 8, 4, area));
            Play(s, "a1b1");

            var spawned = Newcomers(s, Side.Black);
            Assert.NotEmpty(spawned);
            Assert.All(spawned, p =>
            {
                Assert.Contains(p.Def, Soldier.Pieces);
                Assert.Contains(p.Square, squares);
                Assert.All(p.Abilities, a => Assert.Contains(a.Def, Soldier.Abilities));
            });
            Assert.True(spawned.Sum(p => p.Def.Value) <= 8);
            Assert.True(spawned.Sum(p => p.Abilities.Sum(a => a.Def.Value)) <= 4);
        }
    }

    [Fact]
    public void Reinforcements_skip_occupied_and_walled_squares()
    {
        var s = Rogue("7k/8/R7/8/8/8/8/K7 w - - 0 1");
        s.SetTile(Sq(s, "b6"), WallTile.Instance);
        AddEffect(s, Effect(Roster(StandardPieces.Knight), 10, 0, Squares("a6", "b6", "c6", "d6")));

        Play(s, "a1b1");

        var spawned = Newcomers(s, Side.Black);
        Assert.Equal(new[] { Sq(s, "c6"), Sq(s, "d6") }, spawned.Select(p => p.Square).OrderBy(q => q));
        Assert.All(spawned, p => Assert.Equal(StandardPieces.Knight, p.Def));
        Assert.Equal(StandardPieces.Rook, s.PieceAt(Sq(s, "a6"))?.Def);
    }

    [Fact]
    public void Reinforcements_that_overflow_the_area_fill_it()
    {
        var s = Rogue(Kings);
        AddEffect(s, Effect(Roster(StandardPieces.Pawn, StandardPieces.Bishop), 20, 0, Squares("c6", "d6")));
        Play(s, "a1b1");
        Assert.Equal(2, Newcomers(s, Side.Black).Count);
    }

    [Fact]
    public void Arrange_puts_the_most_valuable_pieces_first_and_drops_the_rest()
    {
        var none = Array.Empty<AbilityDef>();
        var army = new List<ArmyPiece>
        {
            new(StandardPieces.Pawn, none),
            new(StandardPieces.Bishop, none),
            new(StandardPieces.Pawn, none),
            new(StandardPieces.Knight, new AbilityDef[] { PiercingAbility.Instance }),
        };

        var placed = ReinforcementsEffect.Arrange(army, new[] { 10, 20 });

        Assert.Equal(2, placed.Count);
        Assert.Equal((StandardPieces.Knight, 10), (placed[0].Piece.Def, placed[0].Square));
        Assert.Equal((StandardPieces.Bishop, 20), (placed[1].Piece.Def, placed[1].Square));
    }

    [Fact]
    public void Reinforcements_with_a_full_area_spawn_nothing_but_still_fire()
    {
        var s = Rogue("7k/8/NN6/8/8/8/8/K7 w - - 0 1");
        var g = AddEffect(s, Effect(Soldier, 6, 0, Squares("a6", "b6")));
        Play(s, "a1b1");
        Assert.Empty(Newcomers(s, Side.Black));
        Assert.Equal(1, g.Var(BoardEffectDef.TimesFiredVar));
    }

    [Fact]
    public void Reinforcements_from_a_royal_only_roster_spawn_nothing()
    {
        var s = Rogue(Kings);
        AddEffect(s, Effect(Roster(StandardPieces.King), 10, 0, Rows(6)));
        Play(s, "a1b1");
        Assert.Empty(Newcomers(s, Side.Black));
        Assert.Single(s.PiecesOnBoard(), p => p.Side == Side.Black);
    }

    [Fact]
    public void Reinforcements_are_reproducible_and_fire_again()
    {
        GameState Run()
        {
            var s = Rogue(Kings);
            s.SeedRandom(7);
            AddEffect(s, Effect(Soldier, 4, 3, Rows(5, 6, 7)), new TurnIntervalCondition(1));
            Play(s, "a1b1");
            return s;
        }

        var a = Run();
        var b = Run();
        Assert.Equal(a.Dump(), b.Dump());

        int first = Newcomers(a, Side.Black).Count;
        Play(a, "h8g8");
        var all = Newcomers(a, Side.Black);
        Assert.True(all.Count > first, $"{first} then {all.Count}");
        Assert.All(all, p => Assert.Contains(p.Def, Soldier.Pieces));
    }

    [Fact]
    public void Reinforcements_undo_exactly()
    {
        var s = Rogue(Kings);
        s.SeedRandom(3);
        AddEffect(s, Effect(Soldier, 6, 4, Rows(6, 7)));
        string start = s.Dump();
        ulong hash = s.Hash;

        Play(s, "a1b1");
        Assert.NotEmpty(Newcomers(s, Side.Black));

        s.UndoTo(0);
        Assert.Equal(start, s.Dump());
        Assert.Equal(hash, s.Hash);
    }

    [Fact]
    public void Reinforcements_reject_bad_arguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Effect(Soldier, 0, 0, Rows(6)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Effect(Soldier, 3, -1, Rows(6)));
        Assert.Throws<ArgumentException>(() => Effect(Soldier, 3, 0, Array.Empty<(int, int)>()));
    }

    [Fact]
    public void Reinforcements_describe_themselves()
    {
        var e = Effect(Soldier, 6, 3, Squares("b7", "a7"));
        Assert.Equal("Reinforcements(Black, soldier, pieces 6, abilities 3, a7 b7)", e.Key);
        Assert.Equal("Sends Black reinforcements from the Soldier roster (piece value 6, ability value 3) onto 2 square(s).", e.Describe());
    }

    // ------------------------------------------------------------------ Maps

    private static string ReinforcementsMap(string effectParams) => $$"""
        { "width": 8, "height": 8, "pieces": [
            { "type": "King", "color": "White", "x": 0, "y": 0 }, { "type": "King", "color": "Black", "x": 7, "y": 7 } ],
          "boardEffects": [ { "condition": { "type": "TurnInterval", "params": { "interval": 2 } },
                              "effect": { "type": "Reinforcements", "params": {{effectParams}} } } ] }
        """;

    private static ReinforcementsEffect Loaded(string effectParams)
    {
        var s = MapLoader.Load(ReinforcementsMap(effectParams), ContentLibrary.CreateCatalog());
        return (ReinforcementsEffect)((BoardEffectDef)Assert.Single(s.Globals).Def).Effect;
    }

    [Fact]
    public void Reinforcements_load_from_a_map()
    {
        var s = MapLoader.Load(ReinforcementsMap("""{ "side": "black", "roster": "Soldier", "pieceValue": 6, "abilityValue": 3, "squares": ["b7", "a7"] }"""),
            ContentLibrary.CreateCatalog());
        Assert.Equal("board-effect:Reinforcements(Black, soldier, pieces 6, abilities 3, a7 b7) when TurnInterval(every 2 from 2)",
            Assert.Single(s.Globals).Def.Id);
    }

    [Fact]
    public void Reinforcements_default_to_white_and_no_abilities()
    {
        var e = Loaded("""{ "roster": "soldier", "pieceValue": 2, "squares": ["a3"] }""");
        Assert.Equal(Side.White, e.Side);
        Assert.Equal(0, e.AbilityValue);
    }

    [Fact]
    public void Reinforcements_loaded_from_a_map_spawn()
    {
        var s = MapLoader.Load(ReinforcementsMap("""{ "side": "black", "roster": "soldier", "pieceValue": 4, "squares": ["a6", "b6", "c6", "d6"] }"""),
            ContentLibrary.CreateCatalog());
        Play(s, "a1b1");
        Play(s, "h8g8");
        Assert.NotEmpty(Newcomers(s, Side.Black));
    }

    [Theory]
    [InlineData("""{ "pieceValue": 3, "squares": ["a7"] }""")]
    [InlineData("""{ "roster": "wizard", "pieceValue": 3, "squares": ["a7"] }""")]
    [InlineData("""{ "roster": "soldier", "squares": ["a7"] }""")]
    [InlineData("""{ "roster": "soldier", "pieceValue": 0, "squares": ["a7"] }""")]
    [InlineData("""{ "roster": "soldier", "pieceValue": 3, "abilityValue": -1, "squares": ["a7"] }""")]
    [InlineData("""{ "roster": "soldier", "pieceValue": 3 }""")]
    [InlineData("""{ "roster": "soldier", "pieceValue": 3, "squares": [] }""")]
    [InlineData("""{ "roster": "soldier", "pieceValue": 3, "squares": ["9z"] }""")]
    public void Bad_reinforcements_entries_are_reported(string effectParams)
    {
        var e = Assert.Throws<MapLoadException>(() => MapLoader.Load(ReinforcementsMap(effectParams), ContentLibrary.CreateCatalog()));
        Assert.Contains(e.Problems, p => p.Contains("Reinforcements") && !p.Contains("unknown board effect"));
    }
}
