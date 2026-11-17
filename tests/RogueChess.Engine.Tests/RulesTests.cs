using System.Collections.Generic;
using System.Linq;
using RogueChess.Engine.Content;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Rules;
using Xunit;

namespace RogueChess.Engine.Tests;

public class RulesTests
{
    private static List<string> LegalMoves(GameState s, string? fromSquare = null)
    {
        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(s, moves);
        return moves
            .Where(m => fromSquare == null || m.From == MoveText.ParseSquare(s, fromSquare))
            .Select(m => MoveText.Format(s, m))
            .OrderBy(x => x)
            .ToList();
    }

    [Fact]
    public void Fools_mate_is_checkmate()
    {
        var s = Fen.Parse(Fen.StartPosition);
        foreach (var m in new[] { "f2f3", "e7e5", "g2g4", "d8h4" })
            TestPositions.Play(s, m);

        Assert.Equal(GameOutcome.BlackWins, s.Rules.Evaluate(s));
    }

    [Fact]
    public void Stalemate_is_a_draw()
    {
        var s = Fen.Parse("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1");
        Assert.Empty(LegalMoves(s));
        Assert.Equal(GameOutcome.Draw, s.Rules.Evaluate(s));
    }

    [Fact]
    public void Standard_rules_forbid_moving_into_check_but_rogue_rules_allow_it()
    {
        const string fen = "k7/8/8/8/8/8/8/1R5K b - - 0 1";
        Assert.DoesNotContain("a8b8", LegalMoves(Fen.Parse(fen, RuleSet.Standard)));
        Assert.Contains("a8b8", LegalMoves(Fen.Parse(fen, RuleSet.Rogue)));
    }

    [Fact]
    public void Rogue_rules_end_the_game_when_the_king_is_captured()
    {
        var s = Fen.Parse("k7/8/8/8/8/8/8/R6K w - - 0 1", RuleSet.Rogue);
        Assert.Equal(GameOutcome.Ongoing, s.Rules.Evaluate(s));

        TestPositions.Play(s, "a1a8");

        Assert.Equal(GameOutcome.WhiteWins, s.Rules.Evaluate(s));
    }

    [Fact]
    public void Rogue_rules_have_no_castling_and_promote_only_to_queen()
    {
        var s = Fen.Parse("4k3/P7/8/8/8/8/8/R3K2R w KQ - 0 1", RuleSet.Rogue);
        var moves = LegalMoves(s);

        Assert.DoesNotContain("e1g1", moves);
        Assert.DoesNotContain("e1c1", moves);
        Assert.Equal(new[] { "a7a8q" }, LegalMoves(s, "a7"));

        TestPositions.Play(s, "a7a8q");
        Assert.Same(StandardPieces.Queen, s.PieceAt(MoveText.ParseSquare(s, "a8"))!.Def);
    }

    [Fact]
    public void Promotion_keeps_the_piece_identity()
    {
        var s = Fen.Parse("4k3/P7/8/8/8/8/8/4K3 w - - 0 1");
        int id = s.PieceAt(MoveText.ParseSquare(s, "a7"))!.Id;

        TestPositions.Play(s, "a7a8n");

        var promoted = s.PieceAt(MoveText.ParseSquare(s, "a8"))!;
        Assert.Equal(id, promoted.Id);
        Assert.Same(StandardPieces.Knight, promoted.Def);
    }

    [Fact]
    public void Castling_moves_both_pieces_and_is_undone()
    {
        var s = Fen.Parse("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
        string before = s.Dump();
        int mark = s.UndoDepth;

        TestPositions.Play(s, "e1c1");

        Assert.Same(StandardPieces.King, s.PieceAt(MoveText.ParseSquare(s, "c1"))!.Def);
        Assert.Same(StandardPieces.Rook, s.PieceAt(MoveText.ParseSquare(s, "d1"))!.Def);
        Assert.Null(s.PieceAt(MoveText.ParseSquare(s, "a1")));

        s.UndoTo(mark);
        Assert.Equal(before, s.Dump());
    }

    [Fact]
    public void Castling_works_on_a_wider_board()
    {
        // 10 files: king on f1, rooks in the corners.
        var s = Fen.Parse("r4k3r/10/10/10/10/10/10/R4K3R w KQkq - 0 1");
        var kingMoves = LegalMoves(s, "f1");
        Assert.Contains("f1h1", kingMoves);
        Assert.Contains("f1d1", kingMoves);

        TestPositions.Play(s, "f1h1");
        Assert.Same(StandardPieces.Rook, s.PieceAt(MoveText.ParseSquare(s, "g1"))!.Def);
    }

    [Fact]
    public void Wall_stops_slides_but_can_be_landed_on()
    {
        var s = Fen.Parse("4k3/8/8/8/8/8/8/R3K3 w - - 0 1");
        s.SetTile(MoveText.ParseSquare(s, "a4"), TestPositions.Wall);

        var rook = LegalMoves(s, "a1");

        Assert.Contains("a1a4", rook);
        Assert.DoesNotContain("a1a5", rook);
    }

    [Fact]
    public void Pit_can_be_passed_over_but_not_landed_on()
    {
        var s = Fen.Parse("4k3/8/8/8/8/8/8/R3K3 w - - 0 1");
        s.SetTile(MoveText.ParseSquare(s, "a4"), TestPositions.Pit);

        var rook = LegalMoves(s, "a1");

        Assert.DoesNotContain("a1a4", rook);
        Assert.Contains("a1a8", rook);
    }

    [Fact]
    public void Piece_on_a_pit_cannot_be_captured_or_give_check_through_it()
    {
        var s = Fen.Parse("4k3/8/8/8/n7/8/8/R3K3 w - - 0 1");
        s.SetTile(MoveText.ParseSquare(s, "a4"), TestPositions.Pit);

        Assert.DoesNotContain("a1a4", LegalMoves(s, "a1"));
        Assert.False(MoveGenerator.IsAttacked(s, MoveText.ParseSquare(s, "a4"), Side.White));
    }

    [Fact]
    public void Knight_jumps_over_walls_and_pawn_cannot_push_into_one()
    {
        var s = Fen.Parse("4k3/8/8/8/8/8/4P3/1N2K3 w - - 0 1");
        s.SetTile(MoveText.ParseSquare(s, "b2"), TestPositions.Wall);
        s.SetTile(MoveText.ParseSquare(s, "c2"), TestPositions.Wall);
        s.SetTile(MoveText.ParseSquare(s, "e3"), TestPositions.Wall);

        Assert.Contains("b1c3", LegalMoves(s, "b1"));
        Assert.Empty(LegalMoves(s, "e2"));
    }

    [Fact]
    public void Wall_between_king_and_rook_blocks_castling()
    {
        var s = Fen.Parse("4k3/8/8/8/8/8/8/R3K2R w KQ - 0 1");
        s.SetTile(MoveText.ParseSquare(s, "b1"), TestPositions.Wall);

        var king = LegalMoves(s, "e1");

        Assert.DoesNotContain("e1c1", king);
        Assert.Contains("e1g1", king);
    }
}
