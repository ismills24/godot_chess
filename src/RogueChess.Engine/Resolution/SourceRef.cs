using RogueChess.Engine.Content;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.Resolution;

/// <summary>Category of a proposal's source. Lower categories come first in canonical order (§8).</summary>
public enum SourceCategory : byte
{
    Rules = 0,
    Global = 1,
    Tile = 2,
    Piece = 3,
}

/// <summary>
/// Who proposed or modified something: the rules, a global effect, a tile, or a piece (its def, or one
/// of its abilities). <see cref="Square"/> is where the source stood in the wave's snapshot (or its
/// last-known square, for a dead piece). Canonical order uses it.
/// </summary>
public readonly record struct SourceRef(
    SourceCategory Category,
    Behavior? Behavior,
    Piece? Piece = null,
    int AbilityIndex = -1,
    int Square = -1,
    int GlobalIndex = -1)
{
    internal static SourceRef Rules => new(SourceCategory.Rules, RulesBehavior.Instance);

    internal static SourceRef ForPiece(Piece piece, int abilityIndex, int square)
        => new(SourceCategory.Piece,
            abilityIndex < 0 ? piece.Def : piece.Abilities[abilityIndex].Def,
            piece, abilityIndex, square);

    internal static SourceRef ForTile(TileDef def, int square) => new(SourceCategory.Tile, def, Square: square);

    internal static SourceRef ForGlobal(GlobalDef def, int index) => new(SourceCategory.Global, def, GlobalIndex: index);

    /// <summary>The ability instance this source is, if it's a piece ability.</summary>
    public AbilityInstance? Ability
        => Piece != null && AbilityIndex >= 0 && AbilityIndex < Piece.Abilities.Count ? Piece.Abilities[AbilityIndex] : null;

    /// <summary>Same logical source (used for one-credit-per-source on kills).</summary>
    public bool SameAs(in SourceRef other)
        => Category == other.Category
        && Piece?.Id == other.Piece?.Id
        && AbilityIndex == other.AbilityIndex
        && GlobalIndex == other.GlobalIndex
        && (Category != SourceCategory.Tile || Square == other.Square);

    public override string ToString() => Category switch
    {
        SourceCategory.Rules => "rules",
        SourceCategory.Global => $"global:{Behavior}",
        SourceCategory.Tile => $"tile:{Behavior}@{Square}",
        _ => AbilityIndex < 0 ? $"{Piece}" : $"{Piece}.{Behavior}",
    };
}

public enum VarTargetKind : byte
{
    None,
    Ability,
    Tile,
    Global,
}

/// <summary>Which variable store a var proposal targets: a piece's ability, a tile, or a global.</summary>
public readonly record struct VarTarget(VarTargetKind Kind, int PieceId = -1, AbilityDef? Ability = null, int Square = -1, int GlobalIndex = -1)
{
    public static VarTarget OfAbility(Piece piece, AbilityDef ability) => new(VarTargetKind.Ability, piece.Id, ability);
    public static VarTarget OfTile(int square) => new(VarTargetKind.Tile, Square: square);
    public static VarTarget OfGlobal(int index) => new(VarTargetKind.Global, GlobalIndex: index);

    public override string ToString() => Kind switch
    {
        VarTargetKind.Ability => $"#{PieceId}.{Ability}",
        VarTargetKind.Tile => $"tile@{Square}",
        VarTargetKind.Global => $"global[{GlobalIndex}]",
        _ => "none",
    };
}
