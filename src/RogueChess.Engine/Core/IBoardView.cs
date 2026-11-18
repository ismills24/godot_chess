using System.Collections.Generic;
using RogueChess.Engine.Rules;

namespace RogueChess.Engine.Core;

/// <summary>
/// Read-only view of the game state. This is all content code (movement, triggers, modifiers) gets,
/// so content can propose changes but never make them directly.
/// </summary>
public interface IBoardView
{
    int Width { get; }
    int Height { get; }
    int Area { get; }
    RuleSet Rules { get; }
    Side SideToMove { get; }
    int Ply { get; }
    int EnPassantSquare { get; }
    IReadOnlyList<GlobalInstance> Globals { get; }

    int Sq(int x, int y);
    int FileOf(int sq);
    int RankOf(int sq);
    bool InBounds(int x, int y);

    Piece? PieceAt(int sq);
    Piece? PieceAt(int x, int y);
    Tile TileAt(int sq);
    Piece? GetPiece(int id);
    IEnumerable<Piece> PiecesOnBoard(Side? side = null);
}
