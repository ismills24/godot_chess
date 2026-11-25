using System;
using System.Collections.Generic;
using RogueChess.Engine.Content.Tiles;
using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content.Abilities;

/// <summary>
/// A caster: instead of moving, the piece places a tile on a square within <see cref="Range"/> (king-move
/// distance, diagonals included) and stays where it is. Each cast is its own action
/// (<see cref="MoveFlags.Cast"/>, naming this ability by index), so a piece can carry several casters and the
/// player always picks which one.
///
/// <para><b>Where a cast may go:</b></para>
/// <list type="number">
/// <item>within <see cref="Range"/> of the caster, not its own square;</item>
/// <item>the square is empty (as in the prototype);</item>
/// <item><b>the square has the plain floor tile</b> (<see cref="TileDef.Standard"/>). A cast never overwrites
/// another tile: not Wall, Guardian, Fog, Tomb, Slippery, nor another cast's Fire or Freeze. This is decision
/// B11 (the prototype let casts overwrite any tile). Items 2 and 3 are <see cref="CanTarget"/>, the one place
/// this rule lives;</item>
/// <item>the caster can't simply move there: movement takes priority, as in the prototype (checked against the
/// piece's moves generated before this ability).</item>
/// </list>
///
/// The cast proposes <c>ChangeTile(target, Tile, TileVars)</c> in wave 0, with this ability as the source.
/// Ranges, tiles and timers are the prototype's: range 2; Fire and Freeze tiles from a cast last 4 turn ends
/// (both sides' turns count) and give a 3-turn status.
/// </summary>
public sealed class CasterAbility : AbilityDef
{
    public static readonly CasterAbility FireMage = new("fire-mage", "Fire Mage", FireTile.Instance, range: 2, 4, 3);
    public static readonly CasterAbility FreezeMage = new("freeze-mage", "Freeze Mage", FreezeTile.Instance, range: 2, 4, 3);
    public static readonly CasterAbility WallPlacement = new("wall-placement", "Wall Placement", WallTile.Instance, range: 2);

    private readonly int[] _tileVars;

    /// <param name="tileVars">Variables the placed tile starts with (Fire/Freeze: turns left, status turns).</param>
    public CasterAbility(string id, string name, TileDef tile, int range, params int[] tileVars)
        : base(id, name, value: 2)
    {
        Tile = tile;
        Range = range;
        _tileVars = tileVars;
    }

    /// <summary>The tile this caster places.</summary>
    public TileDef Tile { get; }

    /// <summary>Maximum king-move (Chebyshev) distance from the caster to the target square.</summary>
    public int Range { get; }

    public IReadOnlyList<int> TileVars => _tileVars;

    /// <summary>
    /// Whether any cast may place a tile on <paramref name="square"/>: it must be empty and plain floor (B11).
    /// Range and movement priority are per caster and checked by move generation.
    /// </summary>
    public static bool CanTarget(IBoardView board, int square)
        => board.PieceAt(square) == null && board.TileAt(square).Def.IsDefault;

    public override void ModifyMoves(IBoardView board, Piece piece, AbilityInstance self, List<Move> moves, int firstIndex)
    {
        int index = IndexOf(piece, self);
        if (index < 0) return;
        int end = moves.Count;
        int px = board.FileOf(piece.Square), py = board.RankOf(piece.Square);

        for (int dy = -Range; dy <= Range; dy++)
        for (int dx = -Range; dx <= Range; dx++)
        {
            if (dx == 0 && dy == 0) continue;
            int x = px + dx, y = py + dy;
            if (!board.InBounds(x, y)) continue;
            int sq = board.Sq(x, y);
            if (!CanTarget(board, sq) || CanMoveTo(moves, firstIndex, end, sq)) continue;
            moves.Add(new Move(piece.Square, sq, MoveFlags.Cast, Aux: index));
        }
    }

    public override void ProposeAction(IBoardView board, Piece piece, AbilityInstance self, in Move action, List<Proposal> into)
    {
        int dist = Math.Max(Math.Abs(board.FileOf(action.To) - board.FileOf(piece.Square)),
                            Math.Abs(board.RankOf(action.To) - board.RankOf(piece.Square)));
        if (dist < 1 || dist > Range || !CanTarget(board, action.To)) return; // not a valid cast: rejected
        into.Add(Proposal.ChangeTile(action.To, Tile, Causes.Cast, _tileVars));
    }

    private static int IndexOf(Piece piece, AbilityInstance self)
    {
        for (int i = 0; i < piece.Abilities.Count; i++)
            if (ReferenceEquals(piece.Abilities[i], self)) return i;
        return -1;
    }

    private static bool CanMoveTo(List<Move> moves, int first, int end, int sq)
    {
        for (int i = first; i < end; i++)
            if (moves[i].To == sq && !moves[i].Is(MoveFlags.Cast)) return true;
        return false;
    }
}
