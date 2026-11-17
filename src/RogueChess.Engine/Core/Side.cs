namespace RogueChess.Engine.Core;

public enum Side : byte
{
    White = 0,
    Black = 1,
}

public static class SideExtensions
{
    public static Side Opponent(this Side side) => side == Side.White ? Side.Black : Side.White;

    /// <summary>Rank direction a pawn of this side advances in. White starts at rank 0 and moves up.</summary>
    public static int Forward(this Side side) => side == Side.White ? 1 : -1;
}
