using System.Collections.Generic;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.Content.Abilities;

/// <summary>
/// Cannibal: the piece can only capture its own side's pieces. Its moves to empty squares are unchanged; every
/// capture of an enemy is removed; every square where an ally would normally block it becomes a capture.
///
/// Per movement primitive, as in the prototype: a slider captures the first ally along each line (it still stops
/// at walls and at enemies, which it can't take), a leaper captures allies on its offsets. Tiles still apply:
/// an ally in fog or on a square that blocks landing can't be taken.
///
/// <para>Deliberate difference: the prototype's pawn reported no "friendly block" squares, so a Cannibal pawn
/// couldn't capture anything. Here a Cannibal pawn captures allies diagonally forward (promoting on the far rank),
/// which is what the ability describes. No en passant on allies.</para>
///
/// Abilities modify moves in the order they were gained: an ability gained after Cannibal that adds enemy
/// captures (Phantom's slides past allies) can add them back.
/// </summary>
public sealed class CannibalAbility : AbilityDef
{
    public static readonly CannibalAbility Instance = new();

    private CannibalAbility() : base("cannibal", "Cannibal", value: 1) { }

    public override void ModifyMoves(IBoardView board, Piece piece, AbilityInstance self, List<Move> moves, int firstIndex)
    {
        int write = firstIndex;
        for (int i = firstIndex; i < moves.Count; i++)
        {
            var m = moves[i];
            bool enemyCapture = m.Is(MoveFlags.EnPassant)
                || (m.Is(MoveFlags.Capture) && board.PieceAt(m.To) is { } target && target.Side != piece.Side);
            if (!enemyCapture) moves[write++] = m;
        }
        moves.RemoveRange(write, moves.Count - write);

        foreach (var movement in piece.Def.Movements)
        {
            switch (movement)
            {
                case Slide slide:
                    foreach (var (dx, dy) in slide.Directions) AddSlideCapture(board, piece, slide.MaxRange, dx, dy, moves);
                    break;
                case Leap leap:
                    foreach (var (dx, dy) in leap.Offsets) AddCapture(board, piece, dx, dy, moves, promote: false);
                    break;
                case PawnMovement:
                    int dir = piece.Side.Forward();
                    AddCapture(board, piece, -1, dir, moves, promote: true);
                    AddCapture(board, piece, 1, dir, moves, promote: true);
                    break;
            }
        }
    }

    private static void AddSlideCapture(IBoardView board, Piece piece, int maxRange, int dx, int dy, List<Move> moves)
    {
        int x = board.FileOf(piece.Square), y = board.RankOf(piece.Square);
        for (int r = 1; maxRange == 0 || r <= maxRange; r++)
        {
            x += dx;
            y += dy;
            if (!board.InBounds(x, y)) return;
            int sq = board.Sq(x, y);
            if (board.PieceAt(sq) is { } occupant)
            {
                if (occupant.Side == piece.Side) AddIfCapturable(board, piece, sq, moves, promote: false);
                return;
            }
            if (board.TileAt(sq).Def.BlocksPassage) return;
        }
    }

    private static void AddCapture(IBoardView board, Piece piece, int dx, int dy, List<Move> moves, bool promote)
    {
        int x = board.FileOf(piece.Square) + dx, y = board.RankOf(piece.Square) + dy;
        if (!board.InBounds(x, y)) return;
        int sq = board.Sq(x, y);
        if (board.PieceAt(sq) is { } occupant && occupant.Side == piece.Side)
            AddIfCapturable(board, piece, sq, moves, promote);
    }

    private static void AddIfCapturable(IBoardView board, Piece piece, int sq, List<Move> moves, bool promote)
    {
        var tile = board.TileAt(sq).Def;
        if (tile.BlocksLanding || tile.BlocksCapture) return;
        foreach (var m in moves)
            if (m.From == piece.Square && m.To == sq) return;

        int lastRank = piece.Side == Side.White ? board.Height - 1 : 0;
        var options = board.Rules.PromotionOptions;
        if (promote && board.RankOf(sq) == lastRank && options.Count > 0)
        {
            for (int i = 0; i < options.Count; i++)
                moves.Add(new Move(piece.Square, sq, MoveFlags.Capture | MoveFlags.Promotion, Promotion: (sbyte)i));
        }
        else
        {
            moves.Add(new Move(piece.Square, sq, MoveFlags.Capture));
        }
    }
}
