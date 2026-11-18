using System;
using System.Collections.Generic;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.Content;

/// <summary>
/// Stateless description of an ability (Exploding, Marksman, Frozen...). Attach to pieces as an
/// <see cref="AbilityInstance"/>. Per-instance state goes in variables (slots 0..VarCount-1).
/// A piece holds at most one instance of each ability def.
/// </summary>
public abstract class AbilityDef : Behavior
{
    protected AbilityDef(string id, string name, int value = 0, int varCount = 0)
    {
        Id = id;
        Name = name;
        Value = value;
        VarCount = varCount;
        HashId = Zobrist.StableHash("ability:" + id);
    }

    public string Id { get; }
    public string Name { get; }

    /// <summary>Material value this ability adds for the AI (negative for debuffs).</summary>
    public int Value { get; }

    public int VarCount { get; }

    internal ulong HashId { get; }

    /// <summary>Variables a new instance starts with (e.g. Marksman's charges). Default: all zero.</summary>
    public virtual int[] InitialVars() => Array.Empty<int>();

    /// <summary>
    /// Adjust the piece's moves after its def generated them: add, remove or rewrite entries from
    /// <paramref name="firstIndex"/> on. Used by Phantom, Cannibal, Frozen and caster abilities.
    /// </summary>
    public virtual void ModifyMoves(IBoardView board, Piece piece, AbilityInstance self, List<Move> moves, int firstIndex) { }

    public override string ToString() => Id;
}

/// <summary>Stateless description of a board-wide effect or relic. Lives in <see cref="GameState.Globals"/>.</summary>
public abstract class GlobalDef : Behavior
{
    protected GlobalDef(string id, string name, int varCount = 0)
    {
        Id = id;
        Name = name;
        VarCount = varCount;
        HashId = Zobrist.StableHash("global:" + id);
    }

    public string Id { get; }
    public string Name { get; }
    public int VarCount { get; }

    internal ulong HashId { get; }

    public override string ToString() => Id;
}
