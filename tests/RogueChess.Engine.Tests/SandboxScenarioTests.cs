using System;
using System.IO;
using RogueChess.Engine.Content;
using RogueChess.Engine.Content.Tiles;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Resolution;
using Xunit;

namespace RogueChess.Engine.Tests;

/// <summary>The sandbox scenarios' descriptions promise specific outcomes; these tests keep them honest.</summary>
public class SandboxScenarioTests
{
    private static string SandboxDir => Path.Combine(Path.GetDirectoryName(MapLoaderTests.LegacyMapsDir)!, "sandbox");

    private static GameState Load(string file)
    {
        var s = MapLoader.Load(File.ReadAllText(Path.Combine(SandboxDir, file)), ContentLibrary.CreateCatalog());
        Game.BeginGame(s);
        return s;
    }

    private static Piece? At(GameState s, string sq) => s.PieceAt(MoveText.ParseSquare(s, sq));

    private static void Play(GameState s, params string[] moves)
    {
        foreach (var move in moves)
        {
            var m = MoveText.FindLegal(s, move) ?? throw new InvalidOperationException($"{move} is not legal");
            Assert.True(Game.PlayTurn(s, m).Accepted);
        }
    }

    [Fact]
    public void All_sandbox_scenarios_load()
    {
        foreach (var file in Directory.GetFiles(SandboxDir, "*.json"))
            Load(Path.GetFileName(file));
    }

    [Fact]
    public void Beer_spill_spills_three_slippery_squares_on_turn_two_that_dry_up_after_four_turns()
    {
        var s = Load("08_beer_spill.json");
        int Slippery() { int n = 0; for (int sq = 0; sq < s.Area; sq++) if (s.TileAt(sq).Def == SlipperyTile.Instance) n++; return n; }

        Play(s, "e1d1");
        Assert.Equal(0, Slippery());
        var trace = Game.PlayTurn(s, MoveText.FindLegal(s, "e8d8")!.Value, new ResolveOptions(Trace: true)).Trace!;
        Assert.Equal(3, Slippery());
        Assert.Equal(3, Content.BoardEffects.RandomTilesEffect.Placements(trace, "beer-spill").Count);
        void PlayAnything()
        {
            var moves = new System.Collections.Generic.List<Move>();
            Rules.MoveGenerator.GenerateLegal(s, moves);
            Assert.True(Game.PlayTurn(s, moves[0]).Accepted);
        }
        for (int turn = 3; turn <= 5; turn++) PlayAnything();
        Assert.Equal(3, Slippery()); // turns 3-5: 4 → 1 turn left
        PlayAnything();
        Assert.Equal(0, Slippery()); // turn 6: dried up
    }

    [Fact]
    public void Explosion_chain_plays_as_described()
    {
        var s = Load("01_explosion_chain.json");
        var trace = Game.PlayTurn(s, MoveText.FindLegal(s, "d1d5")!.Value, new ResolveOptions(Trace: true)).Trace!;

        Assert.Equal(3, trace.Resolutions[0].Waves.Count);
        Assert.Null(At(s, "d5"));
        Assert.Null(At(s, "c6"));
        Assert.Null(At(s, "b7"));
        Assert.Null(At(s, "f7"));
        Assert.NotNull(At(s, "e6"));
    }

    [Fact]
    public void Guardian_scenario_plays_as_described()
    {
        var s = Load("02_guardian_fog_walls.json");
        Play(s, "a4d4");
        Assert.Same(StandardPieces.Queen, At(s, "d4")!.Def);
        Assert.NotNull(At(s, "a4"));
        Assert.True(s.TileAt(MoveText.ParseSquare(s, "d4")).Def.IsDefault);

        Play(s, "e1e2", "g7d4");
        Assert.Equal(Side.Black, At(s, "d4")!.Side);
        Assert.Same(FogTile.Instance, s.TileAt(MoveText.ParseSquare(s, "e8")).Def);
    }

    [Fact]
    public void Predator_hunt_plays_as_described()
    {
        var s = Load("04_predator_hunt.json");
        var predator = At(s, "d4")!;

        Play(s, "d4e5", "h8h7", "e5f7", "h7h6", "f7b3", "h6h5", "b3b8");

        Assert.Same(predator, At(s, "b8"));
        Assert.Same(StandardPieces.Queen, predator.Def);
    }
}
