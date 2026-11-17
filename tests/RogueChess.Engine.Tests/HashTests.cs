using RogueChess.Engine.Notation;
using Xunit;

namespace RogueChess.Engine.Tests;

public class HashTests
{
    [Fact]
    public void Knight_shuffle_returns_to_the_start_hash()
    {
        var s = Fen.Parse(Fen.StartPosition);
        ulong start = s.Hash;

        foreach (var m in new[] { "g1f3", "g8f6", "f3g1", "f6g8" })
            TestPositions.Play(s, m);

        Assert.Equal(start, s.Hash);
    }

    [Fact]
    public void Move_order_transpositions_hash_equally()
    {
        var a = Fen.Parse(Fen.StartPosition);
        foreach (var m in new[] { "e2e3", "e7e6", "d2d3" }) TestPositions.Play(a, m);

        var b = Fen.Parse(Fen.StartPosition);
        foreach (var m in new[] { "d2d3", "e7e6", "e2e3" }) TestPositions.Play(b, m);

        Assert.Equal(a.Hash, b.Hash);
    }

    [Fact]
    public void Lost_castling_rights_change_the_hash()
    {
        var s = Fen.Parse(Fen.StartPosition);
        foreach (var m in new[] { "e2e4", "e7e5", "e1e2", "e8e7", "e2e1", "e7e8" })
            TestPositions.Play(s, m);

        // Same squares as a fresh game after 1.e4 e5, but the kings have moved so castling is gone.
        var fresh = Fen.Parse("rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 1");

        Assert.NotEqual(fresh.Hash, s.Hash);
        Assert.Equal(s.ComputeHashFromScratch(), s.Hash);
    }

    [Fact]
    public void Side_to_move_and_en_passant_are_hashed()
    {
        var white = Fen.Parse("4k3/8/8/8/8/8/8/4K3 w - - 0 1");
        var black = Fen.Parse("4k3/8/8/8/8/8/8/4K3 b - - 0 1");
        var withEp = Fen.Parse("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1");
        var withoutEp = Fen.Parse("4k3/8/8/3pP3/8/8/8/4K3 w - - 0 1");

        Assert.NotEqual(white.Hash, black.Hash);
        Assert.NotEqual(withEp.Hash, withoutEp.Hash);
    }

    [Fact]
    public void Tiles_are_hashed()
    {
        var plain = Fen.Parse(Fen.StartPosition);
        var walled = Fen.Parse(Fen.StartPosition);
        walled.SetTile(walled.Sq(3, 3), TestPositions.Wall);

        Assert.NotEqual(plain.Hash, walled.Hash);
        Assert.Equal(walled.ComputeHashFromScratch(), walled.Hash);
    }
}
