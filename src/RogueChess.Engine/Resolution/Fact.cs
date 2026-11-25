using RogueChess.Engine.Content;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.Resolution;

/// <summary>
/// Something that happened in a committed wave. Triggers react to facts.
///
/// Field use by kind:
/// <list type="bullet">
/// <item>TurnStarted / TurnEnded: <see cref="Side"/>.</item>
/// <item>Died: <see cref="Piece"/> (last-known info), <see cref="From"/> = square it died on, <see cref="Source"/> and <see cref="Cause"/> of the winning destroy.</item>
/// <item>Killed: <see cref="Piece"/> = victim, <see cref="From"/>, <see cref="Source"/> = killer (one fact per distinct killer).</item>
/// <item>Moved: <see cref="Piece"/>, <see cref="From"/>, <see cref="To"/>.</item>
/// <item>Spawned: <see cref="Piece"/>, <see cref="To"/> (also used when a piece is revived).</item>
/// <item>Collided: <see cref="Piece"/> = blocked mover, <see cref="Other"/> = blocker (null for a tile), <see cref="From"/>, <see cref="To"/> = intended destination.</item>
/// <item>PieceChanged: <see cref="Piece"/> plus <see cref="OldDef"/>/<see cref="NewDef"/>, or <see cref="Ability"/> with <see cref="Added"/>.</item>
/// <item>TileChanged: <see cref="From"/> = square, <see cref="OldTile"/>, <see cref="NewTile"/>.</item>
/// <item>VarChanged: <see cref="Var"/>, <see cref="Slot"/>, <see cref="OldValue"/>, <see cref="NewValue"/>.</item>
/// <item>ClockChanged: <see cref="Side"/>, <see cref="NewValue"/> = delta in ms.</item>
/// </list>
/// </summary>
public readonly record struct Fact(FactKinds Kind)
{
    public Piece? Piece { get; init; }
    public Piece? Other { get; init; }
    public int From { get; init; } = -1;
    public int To { get; init; } = -1;
    public SourceRef Source { get; init; }
    public string Cause { get; init; } = "";

    /// <summary>Moved only: the move was a jump, not a slide or step (see <see cref="Proposal.Jump"/>).</summary>
    public bool Jump { get; init; }
    public Side Side { get; init; }
    public PieceDef? OldDef { get; init; }
    public PieceDef? NewDef { get; init; }
    public AbilityDef? Ability { get; init; }
    public bool Added { get; init; }
    public Tile? OldTile { get; init; }
    public Tile? NewTile { get; init; }
    public VarTarget Var { get; init; }
    public int Slot { get; init; }
    public int OldValue { get; init; }
    public int NewValue { get; init; }

    /// <summary>The square this fact is "at": destination for Moved/Spawned, otherwise <see cref="From"/>.</summary>
    public int Square => Kind is FactKinds.Moved or FactKinds.Spawned ? To : From;

    /// <summary>Order-sensitive digest of the fact, used by the oscillation watchdog.</summary>
    internal ulong Digest()
    {
        ulong h = Zobrist.Key(Zobrist.FactCategory, (ulong)Kind, (ulong)(Piece?.Id ?? 0), (ulong)(Other?.Id ?? 0));
        h = Zobrist.Mix(h ^ (((ulong)(uint)From << 32) | (uint)To));
        h = Zobrist.Mix(h ^ (((ulong)(uint)Slot << 32) | (uint)NewValue));
        h = Zobrist.Mix(h ^ (ulong)Side ^ ((ulong)(Added ? 1 : 0) << 8));
        h = Zobrist.Mix(h ^ (NewDef?.HashId ?? 0) ^ (Ability?.HashId ?? 0) ^ (NewTile?.Def.HashId ?? 0));
        return h;
    }
}
