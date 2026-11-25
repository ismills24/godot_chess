using System.Collections.Generic;
using RogueChess.Engine.Content;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.Resolution;

public enum ProposalKind : byte
{
    Destroy,
    Move,
    Spawn,
    Revive,
    ChangeDef,
    AddAbility,
    RemoveAbility,
    ChangeTile,
    AddVar,
    SetVar,
    Clock,
}

/// <summary>
/// Something content wants to happen this wave. Proposals are data; the engine merges them, resolves
/// conflicts and commits the survivors (RESOLUTION_RULES.md §6). Build them with the static factories.
/// <see cref="Source"/> is filled in by the engine when a hook proposes or claims.
/// </summary>
public readonly record struct Proposal(ProposalKind Kind)
{
    /// <summary>Piece the proposal acts on (Destroy, Move, ChangeDef, abilities, Revive).</summary>
    public int PieceId { get; init; } = -1;

    /// <summary>Destination (Move) or target square (Spawn, Revive, ChangeTile).</summary>
    public int Square { get; init; } = -1;

    public PieceDef? Def { get; init; }
    public Side Side { get; init; }
    public AbilityDef? Ability { get; init; }
    public IReadOnlyList<AbilityDef>? SpawnAbilities { get; init; }
    public TileDef? Tile { get; init; }

    /// <summary>Initial variables for AddAbility / ChangeTile.</summary>
    public int[]? Vars { get; init; }

    public VarTarget Var { get; init; }
    public int Slot { get; init; }

    /// <summary>Delta (AddVar, Clock) or value (SetVar).</summary>
    public int Value { get; init; }

    public string Cause { get; init; } = "";
    public SourceRef Source { get; init; }

    /// <summary>Move only: the piece jumps (a knight's L, Piercing's leap over its target) rather than slides.</summary>
    public bool Jump { get; init; }

    public static Proposal Destroy(Piece target, string cause)
        => new(ProposalKind.Destroy) { PieceId = target.Id, Cause = cause };

    public static Proposal Move(Piece piece, int to, string cause)
        => new(ProposalKind.Move) { PieceId = piece.Id, Square = to, Cause = cause };

    public static Proposal Spawn(PieceDef def, Side side, int square, string cause, IReadOnlyList<AbilityDef>? abilities = null)
        => new(ProposalKind.Spawn) { Def = def, Side = side, Square = square, Cause = cause, SpawnAbilities = abilities };

    /// <summary>Bring a removed piece back (same id, def and abilities) onto an empty square.</summary>
    public static Proposal Revive(Piece piece, int square, string cause)
        => new(ProposalKind.Revive) { PieceId = piece.Id, Square = square, Cause = cause };

    public static Proposal ChangeDef(Piece piece, PieceDef def, string cause)
        => new(ProposalKind.ChangeDef) { PieceId = piece.Id, Def = def, Cause = cause };

    public static Proposal AddAbility(Piece piece, AbilityDef ability, string cause, params int[] vars)
        => new(ProposalKind.AddAbility) { PieceId = piece.Id, Ability = ability, Cause = cause, Vars = vars.Length > 0 ? vars : ability.InitialVars() };

    public static Proposal RemoveAbility(Piece piece, AbilityDef ability, string cause)
        => new(ProposalKind.RemoveAbility) { PieceId = piece.Id, Ability = ability, Cause = cause };

    public static Proposal ChangeTile(int square, TileDef tile, string cause, params int[] vars)
        => new(ProposalKind.ChangeTile) { Square = square, Tile = tile, Cause = cause, Vars = vars };

    public static Proposal AddVar(VarTarget target, int slot, int delta, string cause)
        => new(ProposalKind.AddVar) { Var = target, Slot = slot, Value = delta, Cause = cause };

    public static Proposal SetVar(VarTarget target, int slot, int value, string cause)
        => new(ProposalKind.SetVar) { Var = target, Slot = slot, Value = value, Cause = cause };

    public static Proposal Clock(Side side, int deltaMs, string cause)
        => new(ProposalKind.Clock) { Side = side, Value = deltaMs, Cause = cause };

    /// <summary>The square a contest over this proposal is measured from (canonical order §8), or -1.</summary>
    internal int AnchorSquare(IBoardView board) => Kind switch
    {
        ProposalKind.Spawn or ProposalKind.Revive or ProposalKind.ChangeTile => Square,
        ProposalKind.AddVar or ProposalKind.SetVar => Var.Kind switch
        {
            VarTargetKind.Ability => board.GetPiece(Var.PieceId)?.Square ?? -1,
            VarTargetKind.Tile => Var.Square,
            _ => -1,
        },
        ProposalKind.Clock => -1,
        _ => board.GetPiece(PieceId)?.Square ?? -1,
    };
}
