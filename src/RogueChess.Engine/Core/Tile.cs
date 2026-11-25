using RogueChess.Engine.Content;

namespace RogueChess.Engine.Core;

/// <summary>
/// A tile instance on one square: its <see cref="TileDef"/> plus variables (e.g. a fire timer).
/// Tiles without variables are immutable and shared; variables change only through resolver proposals.
/// </summary>
public sealed class Tile
{
    public static readonly Tile Standard = new(TileDef.Standard);

    public Tile(TileDef def) : this(def, null) { }

    internal Tile(TileDef def, int[]? vars)
    {
        Def = def;
        Vars = VarStore.Create(def.VarCount, vars is { Length: > 0 } ? vars : def.VarCount > 0 ? def.InitialVars() : null);
    }

    public TileDef Def { get; }
    internal int[] Vars { get; }
    public int VarCount => Vars.Length;
    public int Var(int slot) => Vars[slot];

    /// <summary>Tiles with variables must be copied per state; plain tiles can be shared.</summary>
    internal Tile CopyIfMutable() => Vars.Length == 0 ? this : new Tile(Def, Vars);

    public override string ToString() => VarStore.Describe(Def.Id, Vars);
}
