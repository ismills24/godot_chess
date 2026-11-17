using System.Collections.Generic;
using RogueChess.Engine.Content;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.Rules;

public enum WinCondition
{
    /// <summary>No legal moves: checkmate if a royal piece is attacked, otherwise stalemate (draw).</summary>
    Checkmate,

    /// <summary>A side with no royal pieces left loses. No legal moves is a draw.</summary>
    CaptureRoyal,
}

public enum GameOutcome
{
    Ongoing,
    WhiteWins,
    BlackWins,
    Draw,
}

/// <summary>Game-wide rules. Pieces and tiles describe themselves; this decides legality and how games end.</summary>
public sealed class RuleSet
{
    /// <summary>Orthodox chess: check, castling, en passant, promotion to Q/R/B/N.</summary>
    public static readonly RuleSet Standard = new(
        "standard", forbidSelfCheck: true, castling: true, enPassant: true,
        new[] { StandardPieces.Queen, StandardPieces.Rook, StandardPieces.Bishop, StandardPieces.Knight },
        WinCondition.Checkmate);

    /// <summary>The prototype's roguelike rules: capture the royal piece, no castling or en passant, auto-queen.</summary>
    public static readonly RuleSet Rogue = new(
        "rogue", forbidSelfCheck: false, castling: false, enPassant: false,
        new[] { StandardPieces.Queen },
        WinCondition.CaptureRoyal);

    public RuleSet(string name, bool forbidSelfCheck, bool castling, bool enPassant,
        IReadOnlyList<PieceDef> promotionOptions, WinCondition winCondition)
    {
        Name = name;
        ForbidSelfCheck = forbidSelfCheck;
        Castling = castling;
        EnPassant = enPassant;
        PromotionOptions = promotionOptions;
        WinCondition = winCondition;
    }

    public string Name { get; }

    /// <summary>A move may not leave one of the mover's royal pieces attacked.</summary>
    public bool ForbidSelfCheck { get; }

    public bool Castling { get; }
    public bool EnPassant { get; }

    /// <summary>What a pawn may become on the far rank (one move per option). Empty disables promotion.</summary>
    public IReadOnlyList<PieceDef> PromotionOptions { get; }

    public WinCondition WinCondition { get; }

    public GameOutcome Evaluate(GameState state)
    {
        int whiteRoyals = 0, blackRoyals = 0;
        foreach (var p in state.PiecesOnBoard())
        {
            if (!p.Def.IsRoyal) continue;
            if (p.Side == Side.White) whiteRoyals++;
            else blackRoyals++;
        }
        if (whiteRoyals == 0 && blackRoyals == 0) return GameOutcome.Draw;
        if (whiteRoyals == 0) return GameOutcome.BlackWins;
        if (blackRoyals == 0) return GameOutcome.WhiteWins;

        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(state, moves);
        if (moves.Count > 0) return GameOutcome.Ongoing;

        if (WinCondition == WinCondition.Checkmate && MoveGenerator.IsInCheck(state, state.SideToMove))
            return state.SideToMove == Side.White ? GameOutcome.BlackWins : GameOutcome.WhiteWins;
        return GameOutcome.Draw;
    }

    public override string ToString() => Name;
}
