using System;
using System.Collections.Generic;
using RogueChess.Engine.Core;
using RogueChess.Engine.Rules;

namespace RogueChess.Engine.Notation;

/// <summary>Square names ("e4") and coordinate move strings ("e2e4", "e7e8q", and "a1a4@marksman" or
/// "c1e3@fire-mage" for an ability action) for any board up to 26 files.</summary>
public static class MoveText
{
    public static string SquareName(IBoardView s, int sq)
        => $"{(char)('a' + s.FileOf(sq))}{s.RankOf(sq) + 1}";

    public static int ParseSquare(IBoardView s, string name)
    {
        if (name.Length < 2 || name[0] < 'a' || name[0] > 'z' || !int.TryParse(name.AsSpan(1), out int rank))
            throw new FormatException($"Bad square name '{name}'.");
        int x = name[0] - 'a', y = rank - 1;
        if (!s.InBounds(x, y)) throw new FormatException($"Square '{name}' is off the board.");
        return s.Sq(x, y);
    }

    /// <summary>
    /// "e2e4", "e7e8q"; an ability action names its ability ("c1e3@fire-mage", "a1a4@marksman"); a consumable use
    /// is "use:fortify-potion@a3" (or "use:extra-turn-potion" untargeted).
    /// </summary>
    public static string Format(GameState s, Move m)
    {
        if (m.IsConsumable)
        {
            string id = m.Aux >= 0 && m.Aux < s.Globals.Count ? s.Globals[m.Aux].Def.Id : $"global{m.Aux}";
            return m.To >= 0 ? $"use:{id}@{SquareName(s, m.To)}" : $"use:{id}";
        }
        string text = SquareName(s, m.From) + SquareName(s, m.To);
        if (m.Is(MoveFlags.Promotion)) text += s.Rules.PromotionOptions[m.Promotion].Symbol;
        // Ability actions name their ability, so they differ from the plain move to the same square ("a1a4@marksman").
        if (m.Is(MoveFlags.Ability) && s.PieceAt(m.From) is { } p && m.Aux >= 0 && m.Aux < p.Abilities.Count)
            text += "@" + p.Abilities[m.Aux].Def.Id;
        return text;
    }

    /// <summary>Find the legal move matching a coordinate string, or null.</summary>
    public static Move? FindLegal(GameState s, string text)
    {
        var moves = new List<Move>();
        MoveGenerator.GenerateLegal(s, moves);
        foreach (var m in moves)
            if (Format(s, m) == text) return m;
        return null;
    }
}
