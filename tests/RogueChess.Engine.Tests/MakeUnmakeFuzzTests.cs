using System;
using System.Collections.Generic;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;
using Xunit;

namespace RogueChess.Engine.Tests;

/// <summary>
/// Random self-play. After every move the incremental hash must equal a from-scratch hash and a clone must
/// be identical; unwinding the whole game must restore the starting state exactly.
/// </summary>
public class MakeUnmakeFuzzTests
{
    public static IEnumerable<object[]> Positions()
    {
        yield return new object[] { "start" };
        yield return new object[] { "kiwipete" };
        yield return new object[] { "position4" };
        yield return new object[] { "rogue6x6" };
    }

    private static GameState Load(string name) => name switch
    {
        "start" => Fen.Parse(Fen.StartPosition),
        "kiwipete" => Fen.Parse(PerftTests.Kiwipete),
        "position4" => Fen.Parse(PerftTests.Position4),
        "rogue6x6" => TestPositions.RogueSixBySix(),
        _ => throw new ArgumentException(name),
    };

    [Theory]
    [MemberData(nameof(Positions))]
    public void Random_games_unwind_exactly(string position)
    {
        var rng = new Random(12345);
        var moves = new List<Move>();

        for (int game = 0; game < 40; game++)
        {
            var s = Load(position);
            string startDump = s.Dump();
            ulong startHash = s.Hash;
            var marks = new Stack<int>();

            for (int ply = 0; ply < 120; ply++)
            {
                moves.Clear();
                MoveGenerator.GenerateLegal(s, moves);
                if (moves.Count == 0) break;

                marks.Push(s.UndoDepth);
                Assert.True(Game.PlayTurn(s, moves[rng.Next(moves.Count)]).Accepted);
                Assert.Equal(s.ComputeHashFromScratch(), s.Hash);

                if (ply % 15 == 0)
                {
                    var clone = s.Clone();
                    Assert.Equal(s.Dump(), clone.Dump());
                    Assert.Equal(s.Hash, clone.Hash);
                }
            }

            while (marks.Count > 0)
            {
                s.UndoTo(marks.Pop());
                Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
            }

            Assert.Equal(startDump, s.Dump());
            Assert.Equal(startHash, s.Hash);
        }
    }

    [Fact]
    public void Clone_is_independent()
    {
        var s = Fen.Parse(Fen.StartPosition);
        var clone = s.Clone();
        TestPositions.Play(clone, "e2e4");

        Assert.Equal(Fen.Parse(Fen.StartPosition).Dump(), s.Dump());
        Assert.NotEqual(s.Hash, clone.Hash);
    }
}
