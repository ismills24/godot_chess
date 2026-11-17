using RogueChess.Engine.Notation;
using RogueChess.Engine.Rules;
using Xunit;

namespace RogueChess.Engine.Tests;

/// <summary>
/// Perft against published node counts (chessprogramming.org "Perft Results"). These exercise castling,
/// en passant (including discovered-check edge cases), promotion and pins.
/// </summary>
public class PerftTests
{
    public const string Kiwipete = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";
    public const string Position3 = "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1";
    public const string Position4 = "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1";
    public const string Position4Mirrored = "r2q1rk1/pP1p2pp/Q4n2/bbp1p3/Np6/1B3NBn/pPPP1PPP/R3K2R b KQ - 0 1";
    public const string Position5 = "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8";

    [Theory]
    [InlineData(Fen.StartPosition, 1, 20L)]
    [InlineData(Fen.StartPosition, 2, 400L)]
    [InlineData(Fen.StartPosition, 3, 8_902L)]
    [InlineData(Fen.StartPosition, 4, 197_281L)]
    [InlineData(Kiwipete, 1, 48L)]
    [InlineData(Kiwipete, 2, 2_039L)]
    [InlineData(Kiwipete, 3, 97_862L)]
    [InlineData(Kiwipete, 4, 4_085_603L)]
    [InlineData(Position3, 1, 14L)]
    [InlineData(Position3, 2, 191L)]
    [InlineData(Position3, 3, 2_812L)]
    [InlineData(Position3, 4, 43_238L)]
    [InlineData(Position3, 5, 674_624L)]
    [InlineData(Position4, 1, 6L)]
    [InlineData(Position4, 2, 264L)]
    [InlineData(Position4, 3, 9_467L)]
    [InlineData(Position4, 4, 422_333L)]
    [InlineData(Position4Mirrored, 3, 9_467L)]
    [InlineData(Position4Mirrored, 4, 422_333L)]
    [InlineData(Position5, 1, 44L)]
    [InlineData(Position5, 2, 1_486L)]
    [InlineData(Position5, 3, 62_379L)]
    public void Matches_known_node_counts(string fen, int depth, long expected)
    {
        var state = Fen.Parse(fen);
        Assert.Equal(expected, Perft.Count(state, depth));
    }

    [Fact]
    public void Perft_leaves_state_unchanged()
    {
        var state = Fen.Parse(Kiwipete);
        string before = state.Dump();
        ulong hash = state.Hash;

        Perft.Count(state, 3);

        Assert.Equal(before, state.Dump());
        Assert.Equal(hash, state.Hash);
        Assert.Equal(0, state.UndoDepth);
    }
}
