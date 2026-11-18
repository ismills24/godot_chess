using System.Collections.Generic;
using RogueChess.Engine.Content;

namespace RogueChess.Engine.Core;

/// <summary>
/// A piece instance. All of its mutable data lives here and is only changed through
/// <see cref="GameState"/> operations, which keep the hash and undo log correct.
/// A removed piece keeps its def and abilities, which is the "last known information"
/// its death triggers read.
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

    internal List<AbilityInstance> AbilityList { get; } = new();
    public IReadOnlyList<AbilityInstance> Abilities => AbilityList;

    public int AbilityIndex(AbilityDef def)
    {
        for (int i = 0; i < AbilityList.Count; i++)
            if (ReferenceEquals(AbilityList[i].Def, def)) return i;
        return -1;
    }

    public bool HasAbility(AbilityDef def) => AbilityIndex(def) >= 0;

    public AbilityInstance? GetAbility(AbilityDef def)
    {
        int i = AbilityIndex(def);
        return i >= 0 ? AbilityList[i] : null;
    }

    /// <summary>Material value for the AI: the def's value plus every ability's value.</summary>
    public int Value
    {
        get
        {
            int v = Def.Value;
            foreach (var a in AbilityList) v += a.Def.Value;
            return v;
        }
    }

    internal Piece Copy()
    {
        var copy = new Piece(Id, Side, Def) { Square = Square, MovesMade = MovesMade };
        foreach (var a in AbilityList) copy.AbilityList.Add(a.Copy());
        return copy;
    }

    public override string ToString() => $"{Side} {Def.Name} #{Id}";
}
