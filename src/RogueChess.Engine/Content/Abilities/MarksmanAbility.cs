using System.Collections.Generic;
using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content.Abilities;

/// <summary>
/// Marksman: while it has charges (variable 0, starting at 1), the piece may shoot an enemy it could capture
/// instead of capturing it. The target is destroyed and the marksman stays where it is; the shot spends a charge.
///
/// The shot is its own action (<see cref="MoveFlags.Ability"/>, written "a1a4@marksman"), offered next to the
/// normal capture of the same piece. A normal capture is still a normal capture and spends nothing. This fixes
/// the prototype bug (B4) where the ranged option duplicated the move and every capture became ranged while
/// charges remained.
///
/// Rules:
/// <list type="bullet">
/// <item>Targets are the enemies the piece's own movement could capture (its def's movement, not moves other
/// abilities add), so the result doesn't depend on the order of its abilities. Pieces in fog can't be targeted.</item>
/// <item>The shot is not a move capture (<see cref="Causes.Ranged"/>): a Guardian tile doesn't stop it, like an
/// explosion. A Scapegoat still dies in the target's place; the charge is spent either way.</item>
/// <item>The kill is credited to the marksman piece (Predator evolves; ChronoCapture can count it, fixing B3).</item>
/// <item>A frozen marksman can't shoot (it has no actions at all).</item>
/// </list>
/// </summary>
public sealed class MarksmanAbility : AbilityDef
{
    public const int ChargesVar = 0;

    /// <summary>The prototype's default number of ranged attacks.</summary>
    public const int DefaultCharges = 1;

    public const string ShotCause = "marksman-shot";

    public static readonly MarksmanAbility Instance = new();

    private MarksmanAbility() : base("marksman", "Marksman", value: 4, varCount: 1) { }

    public override int[] InitialVars() => new[] { DefaultCharges };

    public override void ModifyMoves(IBoardView board, Piece piece, AbilityInstance self, List<Move> moves, int firstIndex)
    {
        if (self.Var(ChargesVar) <= 0) return;
        int index = piece.AbilityIndex(this);
        for (int sq = 0; sq < board.Area; sq++)
        {
            var target = board.PieceAt(sq);
            if (target == null || target.Side == piece.Side) continue;
            foreach (var movement in piece.Def.Movements)
            {
                if (!movement.Attacks(board, piece, sq)) continue;
                moves.Add(new Move(piece.Square, sq, MoveFlags.Capture | MoveFlags.Ability, Aux: index));
                break;
            }
        }
    }

    public override void ProposeAction(IBoardView board, Piece piece, AbilityInstance self, Move action, List<Proposal> into)
    {
        if (self.Var(ChargesVar) <= 0) return;
        if (board.PieceAt(action.To) is { } target && target.Side != piece.Side)
            into.Add(Proposal.Destroy(target, Causes.Ranged));
        into.Add(Proposal.AddVar(VarTarget.OfAbility(piece, this), ChargesVar, -1, ShotCause));
    }
}
