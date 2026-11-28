using System;
using RogueChess.Engine.Content.Abilities;
using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content.Tiles;

/// <summary>
/// A temporary hazard that gives a status to every piece that enters it (Fire → OnFire, Freeze → Frozen).
///
/// <list type="bullet">
/// <item><b>Entering</b>: a piece that ends a move here, slides across it, or is spawned or revived here gets
/// the status for <see cref="StatusTurnsVar"/> turns, unless it already has that status (it isn't refreshed,
/// as in the prototype). "Slides across" means a straight-line move (rank, file or diagonal) that passes over
/// the square; knights and other leaps jump over it.</item>
/// <item><b>Timer</b>: <see cref="TurnsLeftVar"/> drops by one at the end of every turn, both sides' turns
/// counting; when the last one runs out the tile turns back into plain floor.</item>
/// </list>
///
/// A piece already standing on the tile is not affected again. Pieces placed on it by map setup aren't
/// affected either (the prototype's maps give them the status explicitly). Map tiles start with
/// 3 turns and a 3-turn status (<see cref="InitialVars"/>); casts give 4 and 3 (<see cref="CasterAbility"/>).
/// </summary>
public abstract class StatusTile : TileDef
{
    public const int TurnsLeftVar = 0;
    public const int StatusTurnsVar = 1;

    /// <summary>The prototype's defaults for a tile placed by a map.</summary>
    public const int DefaultTurns = 3, DefaultStatusTurns = 3;

    private readonly string _applyCause, _expireCause;

    protected StatusTile(string id, string name, string applyCause, string expireCause)
        : base(id, name, varCount: 2)
    {
        _applyCause = applyCause;
        _expireCause = expireCause;
    }

    /// <summary>The status this tile gives (OnFire, Frozen).</summary>
    public abstract AbilityDef Status { get; }

    public override int[] InitialVars() => new[] { DefaultTurns, DefaultStatusTurns };

    private static readonly string[] Names = { "turns", "statusTurns" };

    /// <summary>"turns" (the tile's own timer) and "statusTurns" (how long the status it gives lasts).</summary>
    public override System.Collections.Generic.IReadOnlyList<string> VarNames => Names;

    public override FactKinds Triggers => FactKinds.Moved | FactKinds.Spawned | FactKinds.TurnEnded;

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        int here = ctx.SelfSquare;
        var tile = ctx.Board.TileAt(here);
        if (!ReferenceEquals(tile.Def, this)) return;

        if (fact.Kind == FactKinds.TurnEnded)
        {
            if (tile.Var(TurnsLeftVar) <= 1) ctx.Propose(Proposal.ChangeTile(here, Standard, _expireCause));
            else ctx.Propose(Proposal.AddVar(VarTarget.OfTile(here), TurnsLeftVar, -1, _expireCause + "-tick"));
            return;
        }

        bool entered = fact.To == here
            || (fact.Kind == FactKinds.Moved && fact.From >= 0 && Crosses(ctx.Board, fact.From, fact.To, here));
        if (!entered || fact.Piece is not { IsOnBoard: true } piece || piece.HasAbility(Status)) return;
        ctx.Propose(Proposal.AddAbility(piece, Status, _applyCause, tile.Var(StatusTurnsVar)));
    }

    /// <summary>Whether a straight-line move from <paramref name="from"/> to <paramref name="to"/> passes over <paramref name="sq"/>.</summary>
    public static bool Crosses(IBoardView board, int from, int to, int sq)
    {
        int fx = board.FileOf(from), fy = board.RankOf(from), tx = board.FileOf(to), ty = board.RankOf(to);
        int dx = tx - fx, dy = ty - fy;
        if (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy)) return false; // not a line: a leap
        int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
        int sx = Math.Sign(dx), sy = Math.Sign(dy);
        for (int i = 1; i < steps; i++)
            if (board.Sq(fx + sx * i, fy + sy * i) == sq) return true;
        return false;
    }
}

/// <summary>Fire: a piece that enters catches fire (<see cref="OnFireAbility"/>) and burns after its owner's next turns.</summary>
public sealed class FireTile : StatusTile
{
    public static readonly FireTile Instance = new();

    private FireTile() : base("fire", "Fire", "fire-apply", "fire-extinguish") { }

    public override AbilityDef Status => OnFireAbility.Instance;
}

/// <summary>Freeze: a piece that enters is frozen (<see cref="FrozenAbility"/>) for its owner's next turns.</summary>
public sealed class FreezeTile : StatusTile
{
    public static readonly FreezeTile Instance = new();

    private FreezeTile() : base("freeze", "Freeze", "freeze-apply", "freeze-melt") { }

    public override AbilityDef Status => FrozenAbility.Instance;
}
