using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;

namespace RogueChess.Engine.AI;

/// <summary>
/// Negamax alpha-beta with iterative deepening, a transposition table keyed by the state's Zobrist hash,
/// capture-only quiescence, and TT / MVV-LVA / killer / history move ordering.
///
/// Every node is a real turn played with <see cref="Game.PlayTurn"/> and taken back with
/// <see cref="GameState.UndoTo"/>, so the search sees exactly what the game would do: explosions, scapegoats,
/// guardians, turn-start effects, everything. Winning means the opponent has no royal piece left at the end
/// of a turn (RESOLUTION_RULES §7.1).
///
/// One instance belongs to one agent and is not thread-safe. Everything is reset per decision, so the same
/// position and profile always produce the same result unless the wall-clock cap cuts a search short.
/// </summary>
internal sealed class Search
{
    public const int Win = 1_000_000;
    public const int WinThreshold = Win - 10_000;
    private const int Inf = Win + 1;
    private const int MaxPly = 64;

    private readonly AgentProfile _profile;
    private readonly IEvaluator _eval;

    // Transposition table (structure of arrays, always-replace).
    private readonly ulong[] _ttKey;
    private readonly Move[] _ttMove;
    private readonly int[] _ttScore;
    private readonly sbyte[] _ttDepth;
    private readonly byte[] _ttFlag;
    private readonly ulong _ttMask;
    private const byte Exact = 1, Lower = 2, Upper = 3;

    // Per-ply scratch.
    private readonly List<Move>[] _lists = new List<Move>[MaxPly + 1];
    private readonly List<int>[] _orderScores = new List<int>[MaxPly + 1];
    private readonly Move[,] _killers = new Move[MaxPly + 1, 2];
    private readonly Move[,] _pv = new Move[MaxPly + 1, MaxPly + 1];
    private readonly int[] _pvLength = new int[MaxPly + 1];
    private readonly ulong[] _path = new ulong[MaxPly + 2];
    private int[] _history = Array.Empty<int>();

    // Per-decision state.
    private GameState _s = null!;
    private Side _rootSide;
    private bool _whiteHadRoyals, _blackHadRoyals;
    private long _nodes;
    private long _nodeBudget;
    private Stopwatch _clock = new();
    private long _deadlineTicks;
    private CancellationToken _cancel;
    private bool _aborted;

    public Search(AgentProfile profile, IEvaluator evaluator)
    {
        _profile = profile;
        _eval = evaluator;
        int bits = Math.Clamp(profile.TableBits, 10, 26);
        int size = 1 << bits;
        _ttKey = new ulong[size];
        _ttMove = new Move[size];
        _ttScore = new int[size];
        _ttDepth = new sbyte[size];
        _ttFlag = new byte[size];
        _ttMask = (ulong)(size - 1);
        for (int i = 0; i <= MaxPly; i++)
        {
            _lists[i] = new List<Move>(64);
            _orderScores[i] = new List<int>(64);
        }
    }

    // ================================================================ decision

    public AgentDecision Decide(GameState state, CancellationToken cancel)
    {
        _s = state;
        _rootSide = state.SideToMove;
        _cancel = cancel;
        _aborted = false;
        _nodes = 0;
        _nodeBudget = _profile.NodeBudget;
        _clock = Stopwatch.StartNew();
        _deadlineTicks = _profile.TimeBudgetMs > 0 ? _profile.TimeBudgetMs * Stopwatch.Frequency / 1000 : long.MaxValue;
        Array.Clear(_ttKey);
        Array.Clear(_ttFlag);
        Array.Clear(_killers);
        if (_history.Length != state.Area * state.Area) _history = new int[state.Area * state.Area];
        else Array.Clear(_history);
        _whiteHadRoyals = _blackHadRoyals = false;
        for (int sq = 0; sq < state.Area; sq++)
            if (state.PieceAt(sq) is { Def.IsRoyal: true } r)
            {
                if (r.Side == Side.White) _whiteHadRoyals = true;
                else _blackHadRoyals = true;
            }

        int mark = state.UndoDepth;
        try
        {
            return Run();
        }
        finally
        {
            if (state.UndoDepth != mark) state.UndoTo(mark);
        }
    }

    private AgentDecision Run()
    {
        var rootMoves = new List<Move>();
        MoveGenerator.GenerateLegal(_s, rootMoves);
        if (rootMoves.Count == 0)
            return new AgentDecision { Reason = "none", Elapsed = _clock.Elapsed };

        var rng = new SplitMix(_profile.Seed ^ _s.Hash ^ 0x9E3779B97F4A7C15UL);
        var noise = new int[rootMoves.Count];
        if (_profile.RootNoise > 0)
            for (int i = 0; i < noise.Length; i++)
            {
                var m = rootMoves[i];
                var moveRng = new SplitMix(_profile.Seed ^ _s.Hash ^ (ulong)(m.From * 7919 + m.To * 104729 + (int)m.Flags * 31 + m.Promotion + 1));
                noise[i] = moveRng.Next(2 * _profile.RootNoise + 1) - _profile.RootNoise;
            }

        if (rootMoves.Count == 1)
        {
            return new AgentDecision
            {
                Move = rootMoves[0], Reason = "only move", Elapsed = _clock.Elapsed,
                RootMoves = new[] { new RootMove(rootMoves[0], 0, false, 0) },
            };
        }

        // Moves within `slack` of the best get exact scores so the personality can choose among them.
        // Blunder-prone profiles score every move exactly, so a "blunder" never walks into a forced loss.
        bool fullWindow = _profile.BlunderChance > 0;
        int slack = _profile.ChoiceMargin + 2 * _profile.RootNoise;
        var scores = new int[rootMoves.Count];
        var exact = new bool[rootMoves.Count];
        var accepted = new bool[rootMoves.Count];
        var order = new int[rootMoves.Count];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        OrderRoot(order, rootMoves);

        int[]? doneScores = null;
        bool[]? doneExact = null, doneAccepted = null;
        int[]? doneOrder = null;
        Move[] donePv = Array.Empty<Move>();
        int completedDepth = 0;
        bool truncated = false;

        _path[0] = _s.Hash;
        for (int depth = 1; depth <= Math.Max(1, _profile.MaxDepth); depth++)
        {
            int best = -Inf;
            _pvLength[0] = 0;
            Array.Fill(accepted, false);
            foreach (int i in order)
            {
                var m = rootMoves[i];
                int mark = _s.UndoDepth;
                if (!Game.PlayTurn(_s, m).Accepted) continue;
                _path[1] = _s.Hash;
                int lower = best == -Inf || fullWindow ? -Inf : Math.Max(-Inf, best - slack);
                int score = -Negamax(depth - 1, 1, -Inf, -lower);
                _s.UndoTo(mark);
                if (_aborted) break;

                accepted[i] = true;
                scores[i] = score;
                exact[i] = score > lower;
                if (score > best)
                {
                    best = score;
                    _pv[0, 0] = m;
                    for (int k = 0; k < _pvLength[1]; k++) _pv[0, k + 1] = _pv[1, k];
                    _pvLength[0] = _pvLength[1] + 1;
                }
            }

            if (_aborted)
            {
                truncated = true;
                if (completedDepth > 0) break;
                // Not even depth 1 finished: use what we have.
            }

            doneScores = (int[])scores.Clone();
            doneExact = (bool[])exact.Clone();
            doneAccepted = (bool[])accepted.Clone();
            donePv = new Move[_pvLength[0]];
            for (int k = 0; k < donePv.Length; k++) donePv[k] = _pv[0, k];
            completedDepth = depth;

            // Next iteration: best moves first.
            Array.Sort(order, (a, b) =>
            {
                int sa = accepted[a] ? scores[a] : -Inf - 1, sb = accepted[b] ? scores[b] : -Inf - 1;
                return sa != sb ? sb.CompareTo(sa) : a.CompareTo(b);
            });
            doneOrder = (int[])order.Clone();

            if (_aborted) break;
            if (best >= WinThreshold || best <= -WinThreshold) break; // forced result found
        }

        // ------------------------------------------------------------ choose (personality)
        var table = new List<RootMove>();
        foreach (int i in doneOrder ?? order)
        {
            if (doneAccepted == null || !doneAccepted[i]) continue;
            int sc = doneScores![i];
            bool decisive = sc >= WinThreshold || sc <= -WinThreshold;
            table.Add(new RootMove(rootMoves[i], sc, doneExact![i], decisive ? 0 : noise[i]));
        }
        // Proven scores first (by score + noise), then moves only known to be worse.
        table.Sort((a, b) => a.Exact != b.Exact ? b.Exact.CompareTo(a.Exact)
            : a.Exact ? (b.Score + b.Noise).CompareTo(a.Score + a.Noise) : b.Score.CompareTo(a.Score));
        int bestIndex = table.Count > 0 && table[0].Exact ? 0 : -1;
        int bestAdjusted = bestIndex == 0 ? table[0].Score + table[0].Noise : -Inf;
        int bestRaw = table.Where(r => r.Exact).Select(r => r.Score).DefaultIfEmpty(-Inf).Max();

        if (table.Count == 0)
        {
            // Every move was rejected by the resolver's watchdog or nothing finished: play the first move.
            return new AgentDecision
            {
                Move = rootMoves[0], Reason = "fallback", Nodes = _nodes, Elapsed = _clock.Elapsed, Truncated = truncated,
            };
        }

        int chosen = bestIndex >= 0 ? bestIndex : 0;
        string reason = "best";

        // Personality never throws away a forced win.
        bool winning = table[chosen].Score >= WinThreshold;
        if (winning) { }
        else if (_profile.BlunderChance > 0 && rng.NextDouble() < _profile.BlunderChance)
        {
            var safe = new List<int>();
            for (int i = 0; i < table.Count; i++)
                if (table[i].Exact && table[i].Score > -WinThreshold) safe.Add(i);
            if (safe.Count > 0)
            {
                chosen = safe[rng.Next(safe.Count)];
                reason = "blunder";
            }
        }
        else if (_profile.ChoiceMargin > 0 && bestIndex >= 0)
        {
            var candidates = new List<int>();
            for (int i = 0; i < table.Count; i++)
                if (table[i].Exact && table[i].Score + table[i].Noise >= bestAdjusted - _profile.ChoiceMargin
                    && table[i].Score > -WinThreshold)
                    candidates.Add(i);
            if (candidates.Count > 1)
            {
                chosen = candidates[rng.Next(candidates.Count)];
                reason = "candidate";
            }
        }

        if (reason == "best" && table[chosen].Score < bestRaw) reason = "best (noise)";
        var choice = table[chosen];
        bool followsPv = donePv.Length > 0 && donePv[0] == choice.Move;

        return new AgentDecision
        {
            Move = choice.Move,
            Score = choice.Score,
            Depth = completedDepth,
            Nodes = _nodes,
            Elapsed = _clock.Elapsed,
            Reason = reason,
            RootMoves = table,
            PrincipalVariation = followsPv ? donePv : new[] { choice.Move },
            Truncated = truncated,
        };
    }

    // ================================================================ search

    private int Negamax(int depth, int ply, int alpha, int beta)
    {
        _pvLength[ply] = 0;
        if (Tick()) return 0;

        if (Terminal(ply, out int terminal)) return terminal;
        if (IsRepetition(ply)) return DrawScore();
        if (depth <= 0 || ply >= MaxPly - 1) return Quiesce(ply, alpha, beta, _profile.QuiescenceDepth);

        ulong hash = _s.Hash;
        ulong slot = hash & _ttMask;
        Move ttMove = default;
        bool hasTtMove = false;
        if (_ttFlag[slot] != 0 && _ttKey[slot] == hash)
        {
            ttMove = _ttMove[slot];
            hasTtMove = true;
            if (_ttDepth[slot] >= depth)
            {
                int ts = FromTt(_ttScore[slot], ply);
                byte flag = _ttFlag[slot];
                if (flag == Exact) return ts;
                if (flag == Lower && ts >= beta) return ts;
                if (flag == Upper && ts <= alpha) return ts;
            }
        }

        var moves = _lists[ply];
        moves.Clear();
        MoveGenerator.GenerateLegal(_s, moves);
        if (moves.Count == 0) return NoMoves(ply);
        ScoreMoves(ply, moves, hasTtMove, ttMove, capturesOnly: false);

        int alphaOrig = alpha;
        int best = -Inf;
        Move bestMove = default;
        bool any = false;

        for (int n = 0; n < moves.Count; n++)
        {
            var m = PickNext(ply, n);
            int mark = _s.UndoDepth;
            if (!Game.PlayTurn(_s, m).Accepted) continue;
            _path[ply + 1] = _s.Hash;
            int score = -Negamax(depth - 1, ply + 1, -beta, -alpha);
            _s.UndoTo(mark);
            if (_aborted) return 0;
            any = true;

            if (score > best)
            {
                best = score;
                bestMove = m;
                if (score > alpha)
                {
                    alpha = score;
                    _pv[ply, 0] = m;
                    for (int k = 0; k < _pvLength[ply + 1]; k++) _pv[ply, k + 1] = _pv[ply + 1, k];
                    _pvLength[ply] = _pvLength[ply + 1] + 1;
                    if (alpha >= beta)
                    {
                        if (!m.IsCapture)
                        {
                            if (_killers[ply, 0] != m) { _killers[ply, 1] = _killers[ply, 0]; _killers[ply, 0] = m; }
                            _history[m.From * _s.Area + m.To] += depth * depth;
                        }
                        break;
                    }
                }
            }
        }

        if (!any) return NoMoves(ply);

        _ttKey[slot] = hash;
        _ttMove[slot] = bestMove;
        _ttScore[slot] = ToTt(best, ply);
        _ttDepth[slot] = (sbyte)Math.Min(depth, sbyte.MaxValue);
        _ttFlag[slot] = best <= alphaOrig ? Upper : best >= beta ? Lower : Exact;
        return best;
    }

    private int Quiesce(int ply, int alpha, int beta, int left)
    {
        _pvLength[ply] = 0;
        if (Tick()) return 0;
        if (Terminal(ply, out int terminal)) return terminal;

        int standPat = _eval.Evaluate(_s);
        if (left <= 0 || ply >= MaxPly - 1) return standPat;
        if (standPat >= beta) return standPat;
        if (standPat > alpha) alpha = standPat;

        var moves = _lists[ply];
        moves.Clear();
        MoveGenerator.GenerateLegal(_s, moves);
        ScoreMoves(ply, moves, false, default, capturesOnly: true);

        for (int n = 0; n < moves.Count; n++)
        {
            var m = PickNext(ply, n);
            int mark = _s.UndoDepth;
            if (!Game.PlayTurn(_s, m).Accepted) continue;
            _path[ply + 1] = _s.Hash;
            int score = -Quiesce(ply + 1, -beta, -alpha, left - 1);
            _s.UndoTo(mark);
            if (_aborted) return 0;
            if (score >= beta) return score;
            if (score > alpha) alpha = score;
        }
        return alpha;
    }

    // ================================================================ helpers

    /// <summary>Counts a node and checks the budgets. Returns true when the search must stop.</summary>
    private bool Tick()
    {
        if (_aborted) return true;
        _nodes++;
        if (_nodeBudget > 0 && _nodes > _nodeBudget) return _aborted = true;
        if ((_nodes & 1023) == 0 && (_clock.ElapsedTicks > _deadlineTicks || _cancel.IsCancellationRequested))
            return _aborted = true;
        return false;
    }

    /// <summary>A side that started the search with royals and has none left has lost (RESOLUTION_RULES §7.1).</summary>
    private bool Terminal(int ply, out int score)
    {
        int white = 0, black = 0;
        for (int sq = 0; sq < _s.Area; sq++)
            if (_s.PieceAt(sq) is { Def.IsRoyal: true } r)
            {
                if (r.Side == Side.White) white++;
                else black++;
            }
        bool whiteLost = _whiteHadRoyals && white == 0, blackLost = _blackHadRoyals && black == 0;
        if (!whiteLost && !blackLost) { score = 0; return false; }
        if (whiteLost && blackLost) { score = DrawScore(); return true; }
        bool moverLost = _s.SideToMove == Side.White ? whiteLost : blackLost;
        score = moverLost ? -(Win - ply) : Win - ply;
        return true;
    }

    private int NoMoves(int ply)
    {
        if (_s.Rules.WinCondition == WinCondition.Checkmate && MoveGenerator.IsInCheck(_s, _s.SideToMove))
            return -(Win - ply);
        return DrawScore();
    }

    /// <summary>Draws are worth -contempt to the agent and +contempt to its opponent.</summary>
    private int DrawScore() => _s.SideToMove == _rootSide ? -_profile.Contempt : _profile.Contempt;

    private bool IsRepetition(int ply)
    {
        ulong h = _path[ply];
        for (int i = ply - 2; i >= 0; i -= 2)
            if (_path[i] == h) return true;
        return false;
    }

    private static int ToTt(int score, int ply) =>
        score >= WinThreshold ? score + ply : score <= -WinThreshold ? score - ply : score;

    private static int FromTt(int score, int ply) =>
        score >= WinThreshold ? score - ply : score <= -WinThreshold ? score + ply : score;

    private void ScoreMoves(int ply, List<Move> moves, bool hasTtMove, Move ttMove, bool capturesOnly)
    {
        var scores = _orderScores[ply];
        scores.Clear();
        int write = 0;
        for (int i = 0; i < moves.Count; i++)
        {
            var m = moves[i];
            bool tactical = m.IsCapture || m.Promotion >= 0;
            if (capturesOnly && !tactical) continue;

            int score;
            if (hasTtMove && m == ttMove) score = 2_000_000_000;
            else if (m.IsCapture)
            {
                var victim = _s.PieceAt(m.Is(MoveFlags.EnPassant) ? m.Aux : m.To);
                int victimValue = victim == null ? 1 : victim.Def.IsRoyal ? 1000 : victim.Value;
                int attackerValue = _s.PieceAt(m.From)?.Value ?? 0;
                score = 1_000_000_000 + victimValue * 64 - attackerValue;
            }
            else if (m.Promotion >= 0) score = 900_000_000;
            else if (m == _killers[ply, 0]) score = 800_000_000;
            else if (m == _killers[ply, 1]) score = 700_000_000;
            else if (m.Is(MoveFlags.Cast)) score = -1; // casts (up to 24 per caster) after every other move
            else score = Math.Min(_history[m.From * _s.Area + m.To], 600_000_000);

            moves[write++] = m;
            scores.Add(score);
        }
        moves.RemoveRange(write, moves.Count - write);
    }

    /// <summary>Selection sort step: swap the best remaining move into position <paramref name="n"/>.</summary>
    private Move PickNext(int ply, int n)
    {
        var moves = _lists[ply];
        var scores = _orderScores[ply];
        int best = n;
        for (int i = n + 1; i < moves.Count; i++)
            if (scores[i] > scores[best]) best = i;
        if (best != n)
        {
            (moves[n], moves[best]) = (moves[best], moves[n]);
            (scores[n], scores[best]) = (scores[best], scores[n]);
        }
        return moves[n];
    }

    private void OrderRoot(int[] order, List<Move> moves)
    {
        var keys = new int[order.Length];
        for (int i = 0; i < order.Length; i++)
        {
            var m = moves[i];
            if (m.IsCapture)
            {
                var victim = _s.PieceAt(m.Is(MoveFlags.EnPassant) ? m.Aux : m.To);
                keys[i] = 1_000_000 + (victim == null ? 1 : victim.Def.IsRoyal ? 1000 : victim.Value) * 64 - (_s.PieceAt(m.From)?.Value ?? 0);
            }
            else if (m.Promotion >= 0) keys[i] = 900_000;
            else if (m.Is(MoveFlags.Cast)) keys[i] = -1;
        }
        Array.Sort(order, (a, b) => keys[a] != keys[b] ? keys[b].CompareTo(keys[a]) : a.CompareTo(b));
    }
}

/// <summary>Small deterministic RNG (SplitMix64).</summary>
internal struct SplitMix
{
    private ulong _state;

    public SplitMix(ulong seed) => _state = seed;

    public ulong NextULong()
    {
        ulong z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public int Next(int maxExclusive) => maxExclusive <= 1 ? 0 : (int)(NextULong() % (ulong)maxExclusive);

    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));
}
