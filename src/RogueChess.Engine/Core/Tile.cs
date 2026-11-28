using System;
using RogueChess.Engine.Content;

namespace RogueChess.Engine.Core;

/// <summary>
/// A tile instance on one square: its <see cref="TileDef"/> plus variables (e.g. a fire timer).
/// Tiles without variables are immutable and shared; variables change only through resolver proposals.
///
/// <para>Any tile can also be <b>timed</b>: it then carries a lifetime in one extra variable slot after the def's
/// own (<see cref="LifetimeSlot"/>), counted down at the end of every turn (both sides' turns count) by the
/// built-in rules; when the last turn runs out the tile turns back into plain floor (<see cref="Resolution.Causes.TileExpired"/>).
/// Being a variable, the lifetime is hashed, undone and snapshotted with the rest of the tile.</para>
/// </summary>
public sealed class Tile
{
    public static readonly Tile Standard = new(TileDef.Standard);

    public Tile(TileDef def) : this(def, null) { }

    internal Tile(TileDef def, int[]? vars) : this(def, vars, 0) { }

    /// <param name="turnsLeft">Lifetime in turns; 0 (or less) means the tile is permanent.</param>
    internal Tile(TileDef def, int[]? vars, int turnsLeft)
    {
        Def = def;
        var own = VarStore.Create(def.VarCount, vars is { Length: > 0 } ? vars : def.VarCount > 0 ? def.InitialVars() : null);
        if (turnsLeft > 0 && !def.IsDefault)
        {
            Vars = new int[def.VarCount + 1];
            Array.Copy(own, Vars, own.Length);
            Vars[def.VarCount] = turnsLeft;
        }
        else
        {
            Vars = own;
        }
    }

    private Tile(TileDef def, int[] raw, bool _)
    {
        Def = def;
        Vars = (int[])raw.Clone();
    }

    /// <summary>A tile from its full variable list as <see cref="Vars"/> holds it (the def's own, plus the lifetime if timed).</summary>
    internal static Tile FromRaw(TileDef def, int[] raw)
    {
        if (def.IsDefault) return raw.Length == 0 ? Standard : throw new FormatException("Invalid tile state.");
        if (raw.Length != def.VarCount && raw.Length != def.VarCount + 1) throw new FormatException("Invalid tile state.");
        if (raw.Length == def.VarCount + 1 && raw[def.VarCount] <= 0) throw new FormatException("Invalid tile lifetime.");
        return raw.Length == 0 ? new Tile(def) : new Tile(def, raw, false);
    }

    public TileDef Def { get; }
    internal int[] Vars { get; }

    /// <summary>Every variable slot, the lifetime included when the tile is timed.</summary>
    public int VarCount => Vars.Length;
    public int Var(int slot) => Vars[slot];

    /// <summary>Whether this tile expires (see <see cref="TurnsLeft"/>).</summary>
    public bool IsTimed => Vars.Length > Def.VarCount;

    /// <summary>The variable slot holding the lifetime of a timed tile (right after the def's own variables).</summary>
    public int LifetimeSlot => Def.VarCount;

    /// <summary>Turns left before a timed tile turns back into floor; 0 for a permanent tile.</summary>
    public int TurnsLeft => IsTimed ? Vars[Def.VarCount] : 0;

    /// <summary>Tiles with variables must be copied per state; plain tiles can be shared.</summary>
    internal Tile CopyIfMutable() => Vars.Length == 0 ? this : new Tile(Def, Vars, false);

    public override string ToString()
    {
        if (!IsTimed) return VarStore.Describe(Def.Id, Vars);
        var own = VarStore.Describe(Def.Id, Vars[..Def.VarCount]);
        return $"{own}[{TurnsLeft} turns]";
    }
}
