using System.Collections.Generic;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.Content.Abilities;

/// <summary>
/// Phantom: the piece's sliding moves pass through friendly pieces. It still stops at enemies (which it can
/// capture), walls (which block passage) and the board edge, and it never lands on a friendly piece.
/// Leaping and pawn moves are unchanged.
/// </summary>
public sealed class PhantomAbility : AbilityDef
{
    public static readonly PhantomAbility Instance = new();

    private PhantomAbility() : base("phantom", "Phantom", value: 2) { }

    public override void ModifyMoves(IBoardView board, Piece piece, AbilityInstance self, List<Move> moves, int firstIndex)
    {
        foreach (var movement in piece.Def.Movements)
        {
            if (movement is not Slide slide) continue;
            foreach (var (dx, dy) in slide.Directions)
                ExtendPastAllies(board, piece, slide.MaxRange, dx, dy, moves);
        }
    }

    /// <summary>Adds only the squares beyond the first ally; the normal slide already covers the ones before it.</summary>
    private static void ExtendPastAllies(IBoardView board, Piece piece, int maxRange, int dx, int dy, List<Move> moves)
    {
        int x = board.FileOf(piece.Square), y = board.RankOf(piece.Square);
        bool pastAlly = false;
        for (int r = 1; maxRange == 0 || r <= maxRange; r++)
        {
            x += dx;
            y += dy;
            if (!board.InBounds(x, y)) return;
            int sq = board.Sq(x, y);
            var tile = board.TileAt(sq).Def;
            var occupant = board.PieceAt(sq);

            if (occupant == null)
            {
                if (pastAlly && !tile.BlocksLanding) moves.Add(new Move(piece.Square, sq));
            }
            else if (occupant.Side == piece.Side)
            {
                pastAlly = true;
            }
            else
            {
                if (pastAlly && !tile.BlocksLanding && !tile.BlocksCapture)
                    moves.Add(new Move(piece.Square, sq, MoveFlags.Capture));
                return;
            }

            if (tile.BlocksPassage) return;
        }
    }
}
