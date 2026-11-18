using RogueChess.Engine.Content;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;

namespace RogueChess.Engine.Tests;

internal static class TestPositions
{
    public static readonly TileDef Wall = new("test-wall", "Wall", blocksPassage: true);
    public static readonly TileDef Pit = new("test-pit", "Pit", blocksLanding: true);

    /// <summary>A 6x6 roguelike-rules board with a couple of obstacle tiles, like the prototype's test maps.</summary>
    public static GameState RogueSixBySix()
    {
        var s = Fen.Parse("rnqkbr/pppppp/6/6/PPPPPP/RNQKBR w - - 0 1", RuleSet.Rogue);
        s.SetTile(s.Sq(1, 2), Wall);
        s.SetTile(s.Sq(4, 3), Wall);
        s.SetTile(s.Sq(2, 3), Pit);
        s.ClearHistory();
        return s;
    }

    public static Move Play(GameState s, string move)
    {
        var m = MoveText.FindLegal(s, move) ?? throw new System.InvalidOperationException($"{move} is not legal here.");
        var result = Game.PlayTurn(s, m);
        if (!result.Accepted) throw new System.InvalidOperationException($"{move} was rejected: {result.RejectReason}");
        return m;
    }
}
