using System;
using System.Collections.Generic;
using System.Linq;
using RogueChess.Engine.AI;
using RogueChess.Engine.Content;
using RogueChess.Engine.Core;
using RogueChess.Engine.Rules;

namespace RogueChess.Engine.Encounters;

/// <summary>One piece of a generated army: what it is and which abilities it carries.</summary>
public sealed record ArmyPiece(PieceDef Def, IReadOnlyList<AbilityDef> Abilities)
{
    public int Value => Def.Value + Abilities.Sum(a => a.Value);
    public override string ToString() => Abilities.Count == 0 ? Def.Name : $"{Def.Name} [{string.Join(", ", Abilities.Select(a => a.Name))}]";
}

/// <summary>
/// Value-budgeted armies and battle boards, ported from the prototype's roguelike mode
/// (rogue-manager/util/roster.ts and encounter/boardGeneration.ts), made deterministic: the same seed always
/// gives the same armies and layout.
/// </summary>
public static class EncounterGenerator
{
    /// <summary>
    /// A king (if requested, outside the budget), the minimum pawns, then random affordable pieces until the
    /// piece budget is spent; then random affordable abilities on random pieces until the ability budget is
    /// spent; then, if <see cref="ArmySpec.StartingAbilities"/> is set, one of those on the most valuable piece.
    /// </summary>
    public static List<ArmyPiece> GenerateArmy(ArmySpec spec, Catalog catalog, ulong seed)
    {
        var rng = new SplitMix(seed ^ 0xA5A5_5A5A_0F0F_F0F0UL);
        var defs = new List<PieceDef>();
        var abilities = new List<List<AbilityDef>>();

        void Add(PieceDef d) { defs.Add(d); abilities.Add(new List<AbilityDef>()); }

        var royal = catalog.Pieces.FirstOrDefault(p => p.IsRoyal);
        if (spec.HasKing && royal != null) Add(royal);

        var allowed = spec.Pieces?.Select(catalog.FindPiece).OfType<PieceDef>().ToHashSet();
        var pawn = allowed == null
            ? catalog.Pieces.FirstOrDefault(p => p.IsPawn) ?? StandardPieces.Pawn
            : allowed.Where(p => p.IsPawn).OrderBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
        int budget = spec.PieceValue;
        if (pawn != null)
        {
            int pawns = Math.Min(spec.MinimumPawns, pawn.Value > 0 ? budget / pawn.Value : spec.MinimumPawns);
            for (int i = 0; i < pawns; i++) { Add(pawn); budget -= pawn.Value; }
        }

        var buyable = catalog.Pieces.Where(p => !p.IsRoyal && p.Value > 0 && (allowed == null || allowed.Contains(p)))
            .OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
        while (budget > 0)
        {
            var affordable = buyable.Where(p => p.Value <= budget).ToList();
            if (affordable.Count == 0) break;
            var pick = affordable[rng.Next(affordable.Count)];
            Add(pick);
            budget -= pick.Value;
        }

        var pool = AbilityPool(spec.Abilities, catalog);
        int abilityBudget = spec.AbilityValue;
        while (abilityBudget > 0 && defs.Count > 0)
        {
            var affordable = pool.Where(a => a.Value > 0 && a.Value <= abilityBudget).ToList();
            if (affordable.Count == 0) break;
            var ability = affordable[rng.Next(affordable.Count)];
            var holders = Enumerable.Range(0, defs.Count).Where(i => !abilities[i].Contains(ability)).ToList();
            if (holders.Count == 0) break;
            abilities[holders[rng.Next(holders.Count)]].Add(ability);
            abilityBudget -= ability.Value;
        }

        if (spec.StartingAbilities is { Count: > 0 })
        {
            var starting = AbilityPool(spec.StartingAbilities, catalog);
            int strongest = -1;
            for (int i = 0; i < defs.Count; i++)
                if (!defs[i].IsRoyal && (strongest < 0 || defs[i].Value > defs[strongest].Value)) strongest = i;
            var options = strongest < 0 ? new List<AbilityDef>() : starting.Where(a => !abilities[strongest].Contains(a)).ToList();
            if (options.Count > 0) abilities[strongest].Add(options[rng.Next(options.Count)]);
        }

        return defs.Select((d, i) => new ArmyPiece(d, abilities[i])).ToList();
    }

    /// <summary>
    /// Place both armies on an empty board: White in the lower half, Black in the upper half. Per side:
    /// the king in the middle of the back row, pawns in random columns on random non-back rows, other pieces
    /// behind their column's pawn (or anywhere in a pawn-free column). White moves first.
    /// </summary>
    public static GameState BuildBattle(IReadOnlyList<ArmyPiece> white, IReadOnlyList<ArmyPiece> black,
        int width, int height, ulong seed, RuleSet? rules = null)
    {
        var s = new GameState(width, height, rules ?? RuleSet.Rogue);
        var rng = new SplitMix(seed ^ 0x5DEE_CE66_D1CE_4E5BUL);
        var whiteRows = Enumerable.Range(0, height).Where(r => r <= (height - 1) / 2).ToList();
        var blackRows = Enumerable.Range(0, height).Where(r => r >= (height + 1) / 2).ToList();
        Place(s, white, Side.White, whiteRows, ref rng);
        Place(s, black, Side.Black, blackRows, ref rng);
        s.SetSideToMove(Side.White);
        s.ClearHistory();
        return s;
    }

    /// <summary>The whole encounter: the player's army, the enemy's army and the board, all from one seed.</summary>
    public static GeneratedEncounter Generate(EncounterSet set, EncounterSpec encounter, Catalog catalog, ulong seed)
    {
        var player = GenerateArmy(set.Player, catalog, seed * 2 + 1);
        var enemy = GenerateArmy(encounter.Enemy, catalog, seed * 2 + 2);
        var state = BuildBattle(player, enemy, encounter.Width, encounter.Height, seed);
        return new GeneratedEncounter(encounter, seed, player, enemy, state);
    }

    private static List<AbilityDef> AbilityPool(IReadOnlyList<string>? ids, Catalog catalog) =>
        ids == null
            ? catalog.Abilities.OrderBy(a => a.Id, StringComparer.Ordinal).ToList()
            : ids.Select(catalog.FindAbility).OfType<AbilityDef>().Distinct().ToList();

    private static void Place(GameState s, IReadOnlyList<ArmyPiece> army, Side side, List<int> rows, ref SplitMix rng)
    {
        int width = s.Width;
        bool white = side == Side.White;
        int backRow = white ? rows[0] : rows[^1];
        var occupied = new HashSet<int>();
        var pawnRowByColumn = new Dictionary<int, int>();

        void Put(ArmyPiece piece, int x, int y)
        {
            var p = s.Place(piece.Def, side, s.Sq(x, y));
            foreach (var a in piece.Abilities) s.GrantAbility(p, a);
            occupied.Add(s.Sq(x, y));
        }

        // 1. King: back row, middle column (one of the two middles on even widths).
        var king = army.FirstOrDefault(p => p.Def.IsRoyal);
        if (king != null)
        {
            int lo = (width - 1) / 2, hi = width / 2;
            Put(king, lo == hi || rng.Next(2) == 0 ? lo : hi, backRow);
        }

        // 2. Pawns: distinct random columns, a random free non-back row each (back row as a fallback).
        var columns = Shuffled(Enumerable.Range(0, width), ref rng);
        var nonBack = rows.Where(r => r != backRow).ToList();
        var pawns = army.Where(p => p.Def.IsPawn).ToList();
        for (int i = 0; i < pawns.Count && i < columns.Count; i++)
        {
            int col = columns[i];
            int? row = Shuffled(nonBack, ref rng).Cast<int?>().FirstOrDefault(r => !occupied.Contains(s.Sq(col, r!.Value)));
            if (row == null && !occupied.Contains(s.Sq(col, backRow))) row = backRow;
            if (row == null) continue;
            Put(pawns[i], col, row.Value);
            pawnRowByColumn[col] = row.Value;
        }
        // Prototype bug fix: pawns beyond one per column were silently dropped. Put them on any free square,
        // front rows first.
        for (int i = columns.Count; i < pawns.Count; i++)
        {
            bool placed = false;
            foreach (int r in white ? Enumerable.Reverse(rows) : rows)
            {
                foreach (int col in Shuffled(Enumerable.Range(0, width), ref rng))
                {
                    if (occupied.Contains(s.Sq(col, r))) continue;
                    Put(pawns[i], col, r);
                    placed = true;
                    break;
                }
                if (placed) break;
            }
        }

        // 3. Everything else: behind its column's pawn, or anywhere in a column without one.
        var cols = Shuffled(Enumerable.Range(0, width), ref rng);
        var others = Shuffled(army.Where(p => !p.Def.IsRoyal && !p.Def.IsPawn), ref rng);
        for (int k = 0; k < others.Count; k++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < width && !placed; attempt++)
            {
                int col = cols[(k + attempt) % width];
                var candidates = pawnRowByColumn.TryGetValue(col, out int pawnRow)
                    ? rows.Where(r => white ? r >= backRow && r < pawnRow : r <= backRow && r > pawnRow).ToList()
                    : rows.ToList();
                foreach (int r in Shuffled(candidates, ref rng))
                {
                    if (occupied.Contains(s.Sq(col, r))) continue;
                    Put(others[k], col, r);
                    placed = true;
                    break;
                }
            }

            // Fix of a prototype bug: when no square behind a pawn was free, the TS version silently dropped the
            // piece. Fall back to any free square in our half, back rows first.
            if (!placed)
            {
                foreach (int r in white ? rows : Enumerable.Reverse(rows))
                {
                    foreach (int col in Shuffled(Enumerable.Range(0, width), ref rng))
                    {
                        if (occupied.Contains(s.Sq(col, r))) continue;
                        Put(others[k], col, r);
                        placed = true;
                        break;
                    }
                    if (placed) break;
                }
            }
        }
    }

    private static List<T> Shuffled<T>(IEnumerable<T> items, ref SplitMix rng)
    {
        var list = items.ToList();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}

/// <summary>A generated battle: the armies (for display) and the ready-to-play state.</summary>
public sealed record GeneratedEncounter(EncounterSpec Spec, ulong Seed, IReadOnlyList<ArmyPiece> Player,
    IReadOnlyList<ArmyPiece> Enemy, GameState State);
