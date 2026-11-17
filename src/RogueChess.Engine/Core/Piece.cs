using RogueChess.Engine.Content;

namespace RogueChess.Engine.Core;

/// <summary>
/// A piece instance. All of its mutable data lives here and is only changed through
/// <see cref="GameState"/> operations, which keep the hash and undo log correct.
/// </summary>
public sealed class Piece
{
    internal Piece(int id, Side side, PieceDef def)
    {
        Id = id;
        Side = side;
        Def = def;
    }

    /// <summary>Stable identity for the whole game. Never reused while the piece exists.</summary>
    public int Id { get; }
    public Side Side { get; }
    public PieceDef Def { get; internal set; }

    /// <summary>Board square, or -1 when the piece is off the board (removed).</summary>
    public int Square { get; internal set; } = -1;

    public int MovesMade { get; internal set; }
    public bool IsOnBoard => Square >= 0;

    internal Piece Copy() => new(Id, Side, Def) { Square = Square, MovesMade = MovesMade };

    public override string ToString() => $"{Side} {Def.Name} #{Id} @{Square}";
}
