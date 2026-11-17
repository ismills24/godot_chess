using RogueChess.Engine.Content;

namespace RogueChess.Engine.Core;

/// <summary>
/// A tile instance on one square. Immutable for now, so clones can share instances;
/// per-tile state (timers etc.) arrives with the resolver in M2/M3.
/// </summary>
public sealed class Tile
{
    public static readonly Tile Standard = new(TileDef.Standard);

    public Tile(TileDef def) => Def = def;

    public TileDef Def { get; }

    public override string ToString() => Def.Id;
}
