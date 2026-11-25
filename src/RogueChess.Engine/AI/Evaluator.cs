using System;
using System.Collections.Generic;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.AI;

/// <summary>Static evaluation of a position. Replaceable per profile (see <see cref="AgentFactory.RegisterEvaluator"/>).</summary>
public interface IEvaluator
{
    /// <summary>Centipawns from the side to move's point of view. Must not change <paramref name="state"/>.</summary>
    int Evaluate(GameState state);
}

/// <summary>
/// The default evaluator: material from the content's own values, plus positional terms weighted by
/// <see cref="EvalWeights"/>. Piece moves are generated with the same primitives and ability hooks as
/// real move generation, so new content is understood without extra code.
/// </summary>
public sealed class WeightedEvaluator : IEvaluator
{
    private readonly EvalWeights _w;
    private readonly bool _needMoves;
    private readonly List<Move> _moves = new(64);
    private readonly List<int> _royals = new(4);

    public WeightedEvaluator(EvalWeights weights)
    {
        _w = weights;
        _needMoves = weights.Mobility != 0 || weights.Threats != 0 || weights.KingPressure != 0;
    }

    public int Evaluate(GameState s)
    {
        // Royal squares of both sides, for king pressure and shelter.
        _royals.Clear();
        for (int sq = 0; sq < s.Area; sq++)
            if (s.PieceAt(sq) is { Def.IsRoyal: true }) _royals.Add(sq);

        int white = 0;
        double cx = (s.Width - 1) / 2.0, cy = (s.Height - 1) / 2.0;
        int maxCenter = (int)Math.Ceiling(Math.Max(cx, cy));

        for (int sq = 0; sq < s.Area; sq++)
        {
            var p = s.PieceAt(sq);
            if (p == null) continue;
            int x = s.FileOf(sq), y = s.RankOf(sq);
            int v = 0;

            if (!p.Def.IsRoyal)
            {
                v += p.Def.Value * _w.Material;
                foreach (var a in p.Abilities) v += a.Def.Value * _w.AbilityMaterial;

                if (p.Def.IsPawn)
                    v += _w.PawnAdvance * (p.Side == Side.White ? y - 1 : s.Height - 2 - y);
                else if (_w.Center != 0)
                {
                    int dist = (int)Math.Ceiling(Math.Max(Math.Abs(x - cx), Math.Abs(y - cy)));
                    v += _w.Center * (maxCenter - dist);
                }
            }

            if (_w.KingShelter != 0 || _w.KingPressure != 0)
            {
                foreach (int r in _royals)
                {
                    var royal = s.PieceAt(r)!;
                    if (royal == p) continue;
                    int d = Math.Max(Math.Abs(s.FileOf(r) - x), Math.Abs(s.RankOf(r) - y));
                    if (royal.Side == p.Side) { if (d == 1) v += _w.KingShelter; }
                    else if (d <= 2) v += _w.KingPressure;
                }
            }

            if (_needMoves && !p.IsImmobile)
            {
                _moves.Clear();
                foreach (var movement in p.Def.Movements) movement.Generate(s, p, _moves);
                foreach (var ability in p.Abilities) ability.Def.ModifyMoves(s, p, ability, _moves, 0);

                int mobility = 0;
                foreach (var m in _moves) if (!m.Is(MoveFlags.Cast)) mobility++; // casts are not mobility
                v += _w.Mobility * mobility;
                int biggestThreat = 0;
                foreach (var m in _moves)
                {
                    if (!m.IsCapture || s.PieceAt(m.To) is not { } victim || victim.Side == p.Side) continue;
                    int threat = victim.Def.IsRoyal ? 10 * _w.KingPressure : victim.Value * _w.Threats;
                    if (threat > biggestThreat) biggestThreat = threat;
                }
                v += biggestThreat;
            }

            white += p.Side == Side.White ? v : -v;
        }

        // Unused consumables are worth their value, so the search only spends one when it gains more.
        foreach (var g in s.Globals)
            if (g.Def is Content.ConsumableDef c)
                white += c.Value * _w.Material * (g.Var(Content.ConsumableDef.ChargesVar(Side.White)) - g.Var(Content.ConsumableDef.ChargesVar(Side.Black)));

        int score = s.SideToMove == Side.White ? white : -white;
        return score + _w.Tempo;
    }
}
