using System.Collections.Generic;
using RogueChess.Engine.Content.Tiles;
using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content.Consumables;

/// <summary>
/// Haste Draught (prototype <c>ExtraTurnPotion</c>): take one extra turn without passing play to the opponent.
/// Using it banks one extra turn (<see cref="GameState.ExtraTurns"/>); the owner then moves, and when that turn
/// ends the turn does not pass, so the owner moves again.
/// </summary>
public sealed class ExtraTurnPotion : ConsumableDef
{
    public const string Cause = "extra-turn";

    public static readonly ExtraTurnPotion Instance = new();

    private ExtraTurnPotion() : base("extra-turn-potion", "Haste Draught",
        "Take one extra turn without passing play to the opponent.", requiresTarget: false, value: 2) { }

    public override bool Use(IBoardView board, Side owner, int target, List<Proposal> into)
    {
        into.Add(Proposal.AddVar(VarTarget.OfSide(owner), VarTarget.ExtraTurnsSlot, 1, Cause));
        return true;
    }
}

/// <summary>
/// Fortify Potion (prototype <c>FortifyPotion</c>): pick a square; every empty cell in its row that isn't already
/// a wall becomes a wall. Occupied cells are skipped. The walls are ordinary walls: permanent until a piece lands on
/// one and breaks it. Only the target's row matters, so the generator offers one target per row (file a) for rows
/// where at least one wall would appear.
/// </summary>
public sealed class FortifyPotion : ConsumableDef
{
    public const string Cause = "fortify";

    public static readonly FortifyPotion Instance = new();

    private FortifyPotion() : base("fortify-potion", "Fortify Potion",
        "Select a square; every empty cell in that row becomes a wall.", requiresTarget: true, value: 1) { }

    public override void GenerateTargets(IBoardView board, Side owner, List<int> targets)
    {
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
                if (Fortifies(board, board.Sq(x, y)))
                {
                    targets.Add(board.Sq(0, y));
                    break;
                }
    }

    public override bool Use(IBoardView board, Side owner, int target, List<Proposal> into)
    {
        if (target < 0) return false;
        int y = board.RankOf(target);
        for (int x = 0; x < board.Width; x++)
        {
            int sq = board.Sq(x, y);
            if (Fortifies(board, sq)) into.Add(Proposal.ChangeTile(sq, WallTile.Instance, Cause));
        }
        return true;
    }

    private static bool Fortifies(IBoardView board, int sq)
        => board.PieceAt(sq) == null && board.TileAt(sq).Def != WallTile.Instance;
}
