using System.Collections.Generic;
using RogueChess.Engine.Content.Abilities;
using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content.Tiles;

/// <summary>
/// Tomb (prevent layer), left by a dying <see cref="RevenantAbility"/> piece. When an ally of the entombed piece
/// tries to move onto the empty tomb, the move is cancelled (the ally stays where it was), the entombed piece
/// comes back on the tomb's square and the tile becomes standard. As in the prototype:
/// <list type="bullet">
/// <item>Only allies of the entombed piece open it. Enemies can stand on it; it stays a tomb.</item>
/// <item>The square must be empty in the snapshot, so a capture onto the tomb never opens it.</item>
/// <item>Any move counts, not only the player's: a Slippery slide or a push onto the tomb opens it too.</item>
/// <item>That includes moves another modifier makes in the redirect layer (a Bouncer's knock, a Piercing jump), as in
/// the prototype where those were ordinary move events. That is why the tomb sits in the prevent layer: it cancels
/// the move as it is about to commit, after the redirects.</item>
/// </list>
/// The piece comes back as it died (same id, def, abilities and variables), except that a revive-once
/// Revenant is removed in the same wave (B10).
///
/// <para>State: variable 0 is the entombed piece's id. The piece itself stays in <see cref="GameState"/> as an
/// off-board piece, so undo restores it. Variable 1 is a digest of the piece as it died, so the position hash
/// (and the AI's transposition table) tells apart tombs holding different versions of the same piece.</para>
/// </summary>
public sealed class TombTile : TileDef
{
    public const int PieceVar = 0;
    public const int DigestVar = 1;
    public const string RestoreCause = "tomb-restore";
    public const string ClearedCause = "tomb-cleared";

    public static readonly TombTile Instance = new();

    private TombTile() : base("tomb", "Tomb", varCount: 2) { }

    /// <summary>The tile variables for a tomb holding <paramref name="piece"/>.</summary>
    public static int[] VarsFor(Piece piece) => new[] { piece.Id, Digest(piece) };

    /// <summary>The piece in the tomb on <paramref name="square"/>, if it is a tomb and the piece is off the board.</summary>
    public static Piece? Entombed(IBoardView board, int square)
    {
        var tile = board.TileAt(square);
        if (tile.Def != Instance) return null;
        var piece = board.GetPiece(tile.Var(PieceVar));
        return piece is { IsOnBoard: false } ? piece : null;
    }

    public override ModifierLayer Layer => ModifierLayer.Prevent;

    public override void Modify(ModifierContext ctx)
    {
        var board = ctx.Board;
        int sq = ctx.SelfSquare;
        if (board.PieceAt(sq) != null) return;
        var entombed = Entombed(board, sq);
        if (entombed == null) return;

        // Every allied move onto the tomb is cancelled together; otherwise a second one would take the square.
        List<int>? claimed = null;
        for (int i = 0; i < ctx.Batch.Count; i++)
        {
            var p = ctx.Batch[i];
            if (p.Kind != ProposalKind.Move || p.Square != sq) continue;
            if (board.GetPiece(p.PieceId)?.Side != entombed.Side) continue;
            (claimed ??= new List<int>()).Add(i);
        }
        if (claimed == null) return;

        var replacements = new List<Proposal>
        {
            Proposal.Revive(entombed, sq, RestoreCause),
            Proposal.ChangeTile(sq, Standard, ClearedCause),
        };
        foreach (var a in entombed.Abilities)
            if (a.Def is RevenantAbility { RevivesForever: false } revenant)
                replacements.Add(Proposal.RemoveAbility(entombed, revenant, RestoreCause));
        ctx.Claim(claimed.ToArray(), replacements.ToArray());
    }

    /// <summary>32-bit digest of everything a revive restores: def, side, moves made, abilities and their variables.</summary>
    internal static int Digest(Piece piece)
    {
        ulong h = Zobrist.Mix(piece.Def.HashId ^ ((ulong)piece.Side << 40) ^ ((ulong)(uint)piece.MovesMade << 8));
        foreach (var a in piece.Abilities)
        {
            h = Zobrist.Mix(h ^ a.Def.HashId);
            for (int i = 0; i < a.VarCount; i++) h = Zobrist.Mix(h ^ (uint)a.Var(i) ^ ((ulong)(uint)i << 32));
        }
        return (int)(h ^ (h >> 32));
    }
}
