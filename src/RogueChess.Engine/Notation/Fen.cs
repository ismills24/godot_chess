using System;
using System.Linq;
using RogueChess.Engine.Content;
using RogueChess.Engine.Core;
using RogueChess.Engine.Rules;

namespace RogueChess.Engine.Notation;

/// <summary>
/// FEN reader for the standard pieces on any rectangular board (row count = height, multi-digit
/// empty runs allowed). Castling rights become "moves made" on kings and rooks: a piece with a right
/// is unmoved, every other king/rook is marked as having moved.
/// </summary>
public static class Fen
{
    public const string StartPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    public static GameState Parse(string fen, RuleSet? rules = null)
    {
        var parts = fen.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) throw new FormatException("Empty FEN.");

        var rows = parts[0].Split('/');
        int height = rows.Length;
        int width = RowWidth(rows[0]);
        if (rows.Any(r => RowWidth(r) != width))
            throw new FormatException("FEN rows have different widths.");

        var s = new GameState(width, height, rules ?? RuleSet.Standard);

        for (int r = 0; r < height; r++)
        {
            int y = height - 1 - r, x = 0, run = 0;
            foreach (char ch in rows[r])
            {
                if (char.IsDigit(ch))
                {
                    run = run * 10 + (ch - '0');
                    continue;
                }
                x += run;
                run = 0;
                var def = StandardPieces.FromSymbol(ch) ?? throw new FormatException($"Unknown piece '{ch}'.");
                s.Place(def, char.IsUpper(ch) ? Side.White : Side.Black, s.Sq(x, y));
                x++;
            }
        }

        if (parts.Length > 1 && parts[1] == "b") s.SetSideToMove(Side.Black);

        ApplyCastlingRights(s, parts.Length > 2 ? parts[2] : "-");

        if (parts.Length > 3 && parts[3] != "-")
            s.SetEnPassant(MoveText.ParseSquare(s, parts[3]));

        s.ClearHistory();
        return s;
    }

    private static int RowWidth(string row)
    {
        int width = 0, run = 0;
        foreach (char ch in row)
        {
            if (char.IsDigit(ch)) { run = run * 10 + (ch - '0'); continue; }
            width += run + 1;
            run = 0;
        }
        return width + run;
    }

    private static void ApplyCastlingRights(GameState s, string rights)
    {
        foreach (var p in s.PiecesOnBoard().ToList())
            if (p.Def.IsRoyal || p.Def.CastlesWithRoyal)
                s.SetMovesMade(p, 1);

        if (rights == "-") return;
        foreach (char ch in rights)
        {
            var side = char.IsUpper(ch) ? Side.White : Side.Black;
            int dir = char.ToLowerInvariant(ch) switch
            {
                'k' => 1,
                'q' => -1,
                _ => throw new FormatException($"Unsupported castling right '{ch}'."),
            };

            var king = s.PiecesOnBoard(side).FirstOrDefault(p => p.Def.IsRoyal)
                ?? throw new FormatException($"Castling right '{ch}' but no {side} king.");
            int ky = s.RankOf(king.Square), kx = s.FileOf(king.Square);

            // The outermost castling partner on the king's rank, on the requested side.
            for (int x = dir > 0 ? s.Width - 1 : 0; x != kx; x -= dir)
            {
                var p = s.PieceAt(x, ky);
                if (p == null || p.Side != side || !p.Def.CastlesWithRoyal) continue;
                s.SetMovesMade(p, 0);
                s.SetMovesMade(king, 0);
                break;
            }
        }
    }
}
