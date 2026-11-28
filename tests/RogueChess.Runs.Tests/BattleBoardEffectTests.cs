using System.Linq;
using System.Text.Json;
using RogueChess.Engine.AI;
using RogueChess.Engine.Content;
using RogueChess.Engine.Content.Tiles;
using RogueChess.Engine.Core;
using RogueChess.Runs;
using Xunit;

namespace RogueChess.Runs.Tests;

/// <summary>Battle encounters can carry board effects (the bartender's beer spill).</summary>
public class BattleBoardEffectTests
{
    private static readonly Catalog Catalog = ContentLibrary.CreateCatalog();

    private static JsonElement BeerSpill() => JsonDocument.Parse("""
        { "id": "beer-spill", "name": "Beer spill",
          "condition": { "type": "TurnInterval", "params": { "interval": 1, "startTurn": 1 } },
          "effect": { "type": "RandomTiles", "params": { "count": 2, "tile": "slippery", "duration": 4, "cause": "beer-spill" } } }
        """).RootElement.Clone();

    private static RunDefinition Definition(params JsonElement[] effects) => new()
    {
        Width = 3, Height = 3, StartX = 0,
        Encounters = new()
        {
            new() { Id = "inn", Name = "Inn", Kind = "bartender", X = 1, Y = 1,
                Parameters = RunJson.Data(new BattleParameters
                {
                    BoardEffects = effects.ToList(),
                    Ai = AgentProfile.Casual with { MaxDepth = 1, NodeBudget = 1000, QuiescenceDepth = 1 },
                }) },
        },
    };

    private static GameState Board(RunSession s)
    {
        var encounter = s.State.Encounters.Single(e => e.Definition.Id == "inn");
        BattleHandler.CreateBoardEffects(Catalog, RunJson.Decode<BattleParameters>(encounter.Definition.Parameters));
        return GameState.Restore(encounter.Data.As<BattleState>("battle").Position, Catalog);
    }

    private static int Slippery(GameState s) => Enumerable.Range(0, s.Area).Count(sq => s.TileAt(sq).Def == SlipperyTile.Instance);

    [Fact]
    public void A_battle_with_a_beer_spill_effect_spills_beer_and_replays_identically()
    {
        var session = RunSession.Start(7, Definition(BeerSpill()));
        Assert.True(session.Execute(RunCommand.Create("world.move", new WorldMove(1, 1))).Accepted);

        var board = Board(session);
        Assert.Single(board.Globals);
        var legal = new System.Collections.Generic.List<Move>();
        RogueChess.Engine.Rules.MoveGenerator.GenerateLegal(board, legal);
        var move = legal.First();
        Assert.True(session.Execute(RunCommand.Create("battle.move", new BattleMove(move.From, move.To), "inn")).Accepted);

        Assert.True(Slippery(Board(session)) > 0);
        var replay = RunSession.Replay(RunJson.Read<RunTrace>(RunJson.Write(session.Trace)));
        Assert.Equal(RunJson.Hash(session.State), RunJson.Hash(replay.State));
    }

    [Fact]
    public void A_battle_without_board_effects_has_no_globals()
    {
        var session = RunSession.Start(7, Definition());
        session.Execute(RunCommand.Create("world.move", new WorldMove(1, 1)));
        Assert.Empty(Board(session).Globals);
    }

    [Fact]
    public void An_invalid_board_effect_is_rejected_when_the_run_starts()
    {
        var bad = JsonDocument.Parse("""{ "condition": { "type": "Always" }, "effect": { "type": "Nope" } }""").RootElement.Clone();
        Assert.ThrowsAny<System.Exception>(() => RunSession.Start(7, Definition(bad)));
    }

    [Fact]
    public void The_prototype_runs_inn_bartender_spills_beer()
    {
        string dir = System.AppContext.BaseDirectory;
        while (!System.IO.File.Exists(System.IO.Path.Combine(dir, "run", "prototype.json"))) dir = System.IO.Path.GetDirectoryName(dir)!;
        var definition = RunJson.Read<RunDefinition>(System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "run", "prototype.json")));
        var session = RunSession.Start(3, definition);
        var inn = session.State.Encounters.Single(e => e.Definition.Id == "hanging-pawn");
        var effects = RunJson.Decode<BattleParameters>(inn.Definition.Parameters).BoardEffects;
        Assert.NotNull(effects);
        Assert.Contains("RandomTiles", effects![0].GetProperty("effect").GetProperty("type").GetString());
    }
}
