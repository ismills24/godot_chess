using System;
using System.Collections.Generic;
using System.Linq;
using RogueChess.Engine.Content;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.Resolution;

/// <summary>Options for a resolution.</summary>
/// <param name="Trace">Record a wave-by-wave trace (off for AI search).</param>
/// <param name="ShuffleSeed">Testing only: shuffle hook call order to prove outcomes don't depend on it.</param>
public readonly record struct ResolveOptions(bool Trace = false, int ShuffleSeed = 0);

/// <summary>
/// Simultaneous wave resolution (docs/RESOLUTION_RULES.md). One instance per thread, reused to avoid
/// allocation; a re-entrant call (a hook asking for legal moves, say) gets a fresh instance.
/// </summary>
internal sealed class Resolver
{
    public const int MaxWaves = 64;
    public const int MaxProposals = 1024;

    [ThreadStatic] private static Resolver? _cached;

    public static Resolver Rent()
    {
        var r = _cached;
        if (r != null && !r._busy) return r;
        r = new Resolver();
        _cached ??= r;
        return r;
    }

    private bool _busy;
    private readonly List<SourceRef> _sources = new();
    private readonly List<Proposal> _batch = new();
    private readonly List<Proposal> _scratch = new();
    private List<Fact> _facts = new();
    private List<Fact> _newFacts = new();
    private readonly List<Claim> _claims = new();
    private readonly HashSet<ulong> _seen = new();
    private readonly TriggerContext _tctx;
    private readonly ModifierContext _mctx;

    // Conflict-resolution scratch, cleared every wave.
    private readonly Dictionary<int, int> _destroyWinner = new();
    private readonly List<(int VictimId, int Square, SourceRef Source, string Cause)> _kills = new();
    private readonly Dictionary<int, int> _moveWinner = new();
    private readonly List<MoveEntry> _moves = new();
    private readonly Dictionary<int, int> _moveFrom = new();
    private readonly Dictionary<int, int> _destCount = new();
    private readonly HashSet<int> _vacated = new();
    private readonly HashSet<int> _filled = new();
    private readonly Dictionary<int, int> _spawnWinner = new();
    private readonly Dictionary<int, int> _defWinner = new();
    private readonly Dictionary<(int PieceId, AbilityDef Ability), AbilityChange> _abilityChanges = new();
    private readonly Dictionary<int, int> _tileWinner = new();
    private readonly HashSet<int> _changedTiles = new();
    private readonly Dictionary<(VarTarget Target, int Slot), VarChange> _varChanges = new();
    private readonly List<int> _path = new();
    private readonly List<int> _keys = new();
    private readonly HashSet<int> _revived = new();
    private bool[] _claimed = new bool[64];

    private Resolver()
    {
        _tctx = new TriggerContext(_batch);
        _mctx = new ModifierContext(_claims);
    }

    /// <summary>
    /// Run waves until the board settles. On a watchdog failure the whole resolution is rolled back and
    /// false is returned with the reason.
    /// </summary>
    public bool Resolve(GameState s, string phase, Fact? startFact, IReadOnlyList<Proposal>? startProposals,
        ActionInfo action, in ResolveOptions options, ResolutionTrace? trace, out string? failure)
    {
        if (_busy) throw new InvalidOperationException("Resolver re-entered; use Rent().");
        _busy = true;
        int mark = s.UndoDepth;
        try
        {
            _facts.Clear();
            if (startFact is { } sf) _facts.Add(sf);
            _seen.Clear();
            _seen.Add(WaveKey(s.Hash, _facts));

            for (int wave = 0; ; wave++)
            {
                if (wave >= MaxWaves) return Fail(s, mark, trace, $"exceeded {MaxWaves} waves", out failure);

                _batch.Clear();
                if (wave == 0 && startProposals != null) _batch.AddRange(startProposals);
                CollectSources(s, options.ShuffleSeed, wave);
                _tctx.Setup(s, phase, wave, action);
                Gather();

                if (_batch.Count == 0) break;
                if (_batch.Count > MaxProposals) return Fail(s, mark, trace, $"exceeded {MaxProposals} proposals in a wave", out failure);

                WaveTrace? wt = null;
                if (trace != null)
                {
                    wt = new WaveTrace(wave);
                    if (wave == 0 && startFact is { } start) wt.RecordNote("start: " + TraceText.Describe(start, s));
                    wt.Gathered.AddRange(_batch);
                    wt.RecordGathered(s);
                    trace.Waves.Add(wt);
                }

                ApplyLayer(s, ModifierLayer.Redirect, wt);
                ApplyLayer(s, ModifierLayer.Prevent, wt);
                if (_batch.Count > MaxProposals) return Fail(s, mark, trace, $"exceeded {MaxProposals} proposals in a wave", out failure);
                wt?.Final.AddRange(_batch);

                _newFacts.Clear();
                Commit(s, wt);
                if (wt != null)
                {
                    wt.RecordFacts(_newFacts, s);
                    wt.HashAfter = s.Hash;
                }

                (_facts, _newFacts) = (_newFacts, _facts);
                if (_facts.Count == 0) break;
                if (!_seen.Add(WaveKey(s.Hash, _facts)))
                    return Fail(s, mark, trace, $"oscillation detected at wave {wave}", out failure);
            }

            failure = null;
            return true;
        }
        finally
        {
            _busy = false;
        }
    }

    private static bool Fail(GameState s, int mark, ResolutionTrace? trace, string reason, out string? failure)
    {
        s.UndoTo(mark);
        if (trace != null)
        {
            trace.RolledBack = true;
            trace.Failure = reason;
        }
        failure = reason;
        return false;
    }

    /// <summary>State plus this wave's facts: if both repeat, the resolution is looping forever.</summary>
    private static ulong WaveKey(ulong hash, List<Fact> facts)
    {
        ulong h = hash;
        foreach (var f in facts) h = Zobrist.Mix(h ^ f.Digest());
        return h;
    }

    // ---------------------------------------------------------------- gather

    private void CollectSources(GameState s, int shuffleSeed, int wave)
    {
        _sources.Clear();
        _sources.Add(SourceRef.Rules);
        for (int i = 0; i < s.Globals.Count; i++)
            if (s.Globals[i].Def.HasHooks) _sources.Add(SourceRef.ForGlobal(s.Globals[i].Def, i));

        for (int sq = 0; sq < s.Area; sq++)
        {
            var tile = s.TileAt(sq).Def;
            if (tile.HasHooks) _sources.Add(SourceRef.ForTile(tile, sq));
            var p = s.PieceAt(sq);
            if (p == null) continue;
            if (p.Def.HasHooks) _sources.Add(SourceRef.ForPiece(p, -1, sq));
            for (int a = 0; a < p.Abilities.Count; a++)
                if (p.Abilities[a].Def.HasHooks) _sources.Add(SourceRef.ForPiece(p, a, sq));
        }

        if (shuffleSeed != 0)
        {
            ulong rng = (ulong)shuffleSeed * 0x9E3779B97F4A7C15UL + (ulong)wave;
            for (int i = _sources.Count - 1; i > 0; i--)
            {
                rng = Zobrist.Mix(rng);
                int j = (int)(rng % (ulong)(i + 1));
                (_sources[i], _sources[j]) = (_sources[j], _sources[i]);
            }
        }
    }

    private void Gather()
    {
        foreach (var fact in _facts)
        {
            foreach (var src in _sources)
            {
                if ((src.Behavior!.Triggers & fact.Kind) == 0) continue;
                _tctx.SetSelf(src);
                src.Behavior.OnFact(_tctx, fact);
            }

            // A dead piece's own death triggers fire once, with last-known information.
            if (fact.Kind == FactKinds.Died && fact.Piece is { } dead)
            {
                if ((dead.Def.Triggers & FactKinds.Died) != 0)
                {
                    _tctx.SetSelf(SourceRef.ForPiece(dead, -1, fact.From));
                    dead.Def.OnFact(_tctx, fact);
                }
                for (int a = 0; a < dead.Abilities.Count; a++)
                {
                    var def = dead.Abilities[a].Def;
                    if ((def.Triggers & FactKinds.Died) == 0) continue;
                    _tctx.SetSelf(SourceRef.ForPiece(dead, a, fact.From));
                    def.OnFact(_tctx, fact);
                }
            }

            // Kill credit reaches the killer even if it is no longer on the board (it died in the same wave,
            // or the kill came from its own death trigger, e.g. an explosion). Last-known information, as above.
            if (fact.Kind == FactKinds.Killed && fact.Source.Category == SourceCategory.Piece
                && fact.Source.Piece is { IsOnBoard: false } killer)
            {
                if ((killer.Def.Triggers & FactKinds.Killed) != 0)
                {
                    _tctx.SetSelf(SourceRef.ForPiece(killer, -1, fact.Source.Square));
                    killer.Def.OnFact(_tctx, fact);
                }
                for (int a = 0; a < killer.Abilities.Count; a++)
                {
                    var def = killer.Abilities[a].Def;
                    if ((def.Triggers & FactKinds.Killed) == 0) continue;
                    _tctx.SetSelf(SourceRef.ForPiece(killer, a, fact.Source.Square));
                    def.OnFact(_tctx, fact);
                }
            }
        }
    }

    // ---------------------------------------------------------------- modify

    private void ApplyLayer(GameState s, ModifierLayer layer, WaveTrace? wt)
    {
        _claims.Clear();
        _mctx.Setup(s, layer, _batch);
        foreach (var src in _sources)
        {
            if (src.Behavior!.Layer != layer) continue;
            _mctx.SetSelf(src);
            src.Behavior.Modify(_mctx);
        }
        if (_claims.Count == 0) return;

        var ranked = _claims
            .Select(c => (Claim: c, Anchor: _batch[c.Indices[0]].AnchorSquare(s)))
            .ToList();
        ranked.Sort((x, y) =>
        {
            int c = CanonicalOrder.Compare(x.Claim.Source, x.Anchor, y.Claim.Source, y.Anchor, s);
            return c != 0 ? c : x.Claim.Order.CompareTo(y.Claim.Order);
        });

        if (_claimed.Length < _batch.Count) _claimed = new bool[Math.Max(_batch.Count, _claimed.Length * 2)];
        Array.Clear(_claimed, 0, _batch.Count);

        foreach (var (claim, _) in ranked)
        {
            bool free = claim.Indices.All(i => !_claimed[i]) && claim.Indices.Distinct().Count() == claim.Indices.Length;
            claim.Accepted = free;
            if (!free) continue;
            foreach (int i in claim.Indices) _claimed[i] = true;
        }

        _scratch.Clear();
        for (int i = 0; i < _batch.Count; i++)
            if (!_claimed[i]) _scratch.Add(_batch[i]);
        foreach (var (claim, _) in ranked)
            if (claim.Accepted) _scratch.AddRange(claim.Replacements);

        _batch.Clear();
        _batch.AddRange(_scratch);
        wt?.RecordClaims(ranked.Select(r => r.Claim), s);
    }

    // ---------------------------------------------------------------- resolve conflicts + commit

    private sealed class MoveEntry
    {
        public MoveEntry(Piece piece, int to, int proposal)
        {
            Piece = piece;
            From = piece.Square;
            To = to;
            Proposal = proposal;
        }

        public Piece Piece { get; }
        public int From { get; }
        public int To { get; }
        public int Proposal { get; }
        public MoveState State { get; set; }
        public Piece? Blocker { get; set; }
    }

    private enum MoveState : byte { Unknown, Visiting, Success, Fail }

    private sealed class AbilityChange
    {
        public bool Remove;
        public int[]? AddVars;
        public int Proposal = -1;
    }

    private sealed class VarChange
    {
        public int Sum;
        public int SetProposal = -1;
        public int Proposal = -1;
    }

    private bool Beats(GameState s, int candidate, int incumbent, int anchor)
        => CanonicalOrder.Compare(_batch[candidate].Source, _batch[incumbent].Source, anchor, s) < 0;

    private static void Note(WaveTrace? wt, string text) => wt?.RecordNote(text);

    private void Commit(GameState s, WaveTrace? wt)
    {
        _destroyWinner.Clear();
        _kills.Clear();
        _moveWinner.Clear();
        _moves.Clear();
        _moveFrom.Clear();
        _destCount.Clear();
        _vacated.Clear();
        _filled.Clear();
        _spawnWinner.Clear();
        _defWinner.Clear();
        _abilityChanges.Clear();
        _tileWinner.Clear();
        _changedTiles.Clear();
        _varChanges.Clear();
        int clockWhite = 0, clockBlack = 0;

        // ---- classify and merge
        for (int i = 0; i < _batch.Count; i++)
        {
            var p = _batch[i];
            switch (p.Kind)
            {
                case ProposalKind.Destroy:
                {
                    var piece = s.GetPiece(p.PieceId);
                    if (piece == null || !piece.IsOnBoard) { Note(wt, $"dropped destroy of absent #{p.PieceId}"); break; }
                    if (!_destroyWinner.TryGetValue(piece.Id, out int j)) _destroyWinner[piece.Id] = i;
                    else if (Beats(s, i, j, piece.Square)) _destroyWinner[piece.Id] = i;
                    // No kill credit for a piece's own death (e.g. a Scapegoat sacrificing itself).
                    bool credited = p.Source.Piece == piece;
                    foreach (var k in _kills)
                        if (k.VictimId == piece.Id && k.Source.SameAs(p.Source)) { credited = true; break; }
                    if (!credited) _kills.Add((piece.Id, piece.Square, p.Source, p.Cause));
                    break;
                }
                case ProposalKind.Move:
                {
                    var piece = s.GetPiece(p.PieceId);
                    if (piece == null || !piece.IsOnBoard || p.Square < 0 || p.Square >= s.Area || p.Square == piece.Square)
                    {
                        Note(wt, $"dropped invalid move of #{p.PieceId}");
                        break;
                    }
                    if (!_moveWinner.TryGetValue(piece.Id, out int j)) _moveWinner[piece.Id] = i;
                    else if (_batch[j].Square != p.Square)
                    {
                        if (Beats(s, i, j, piece.Square)) _moveWinner[piece.Id] = i;
                        Note(wt, $"conflicting moves for {piece}: canonical order picked one");
                    }
                    break;
                }
                case ProposalKind.Spawn:
                case ProposalKind.Revive:
                {
                    if (p.Square < 0 || p.Square >= s.Area) { Note(wt, "dropped off-board spawn"); break; }
                    if (!_spawnWinner.TryGetValue(p.Square, out int j)) _spawnWinner[p.Square] = i;
                    else if (Beats(s, i, j, p.Square)) _spawnWinner[p.Square] = i;
                    break;
                }
                case ProposalKind.ChangeDef:
                {
                    // Off-board pieces are kept here so a piece revived this wave can be changed in the same
                    // wave (Tomb strips Revenant). The commit below applies changes only to pieces on the board.
                    var piece = s.GetPiece(p.PieceId);
                    if (piece == null) break;
                    if (!_defWinner.TryGetValue(piece.Id, out int j)) _defWinner[piece.Id] = i;
                    else if (!ReferenceEquals(_batch[j].Def, p.Def) && Beats(s, i, j, piece.Square)) _defWinner[piece.Id] = i;
                    break;
                }
                case ProposalKind.AddAbility:
                case ProposalKind.RemoveAbility:
                {
                    var piece = s.GetPiece(p.PieceId);
                    if (piece == null || p.Ability == null) break;
                    var key = (piece.Id, p.Ability);
                    if (!_abilityChanges.TryGetValue(key, out var change)) _abilityChanges[key] = change = new AbilityChange { Proposal = i };
                    if (p.Kind == ProposalKind.RemoveAbility) change.Remove = true;
                    else change.AddVars = MaxMerge(change.AddVars, p.Vars, p.Ability.VarCount);
                    break;
                }
                case ProposalKind.ChangeTile:
                {
                    if (p.Square < 0 || p.Square >= s.Area || p.Tile == null) break;
                    if (!_tileWinner.TryGetValue(p.Square, out int j)) _tileWinner[p.Square] = i;
                    else if (!ReferenceEquals(_batch[j].Tile, p.Tile) && Beats(s, i, j, p.Square)) _tileWinner[p.Square] = i;
                    break;
                }
                case ProposalKind.AddVar:
                case ProposalKind.SetVar:
                {
                    var key = (p.Var, p.Slot);
                    if (!_varChanges.TryGetValue(key, out var change)) _varChanges[key] = change = new VarChange { Proposal = i };
                    if (p.Kind == ProposalKind.AddVar) change.Sum += p.Value;
                    else if (change.SetProposal < 0) change.SetProposal = i;
                    else if (_batch[change.SetProposal].Value != p.Value && Beats(s, i, change.SetProposal, p.AnchorSquare(s)))
                        change.SetProposal = i;
                    break;
                }
                case ProposalKind.Clock:
                    if (p.Side == Side.White) clockWhite += p.Value;
                    else clockBlack += p.Value;
                    break;
            }
        }

        // ---- moves: drop pieces that are being destroyed, then resolve like sliding blocks.
        // (_moves ends up in piece-id order, which is also the commit order below.)
        foreach (int pieceId in SortedKeys(_moveWinner))
        {
            int idx = _moveWinner[pieceId];
            if (_destroyWinner.ContainsKey(pieceId)) { Note(wt, $"move of #{pieceId} dropped: destroyed this wave"); continue; }
            var entry = new MoveEntry(s.GetPiece(pieceId)!, _batch[idx].Square, idx);
            _moveFrom[entry.From] = _moves.Count;
            _destCount[entry.To] = _destCount.TryGetValue(entry.To, out int n) ? n + 1 : 1;
            _moves.Add(entry);
        }
        ResolveMoves(s);

        // ---- 1. removals
        foreach (int pieceId in SortedKeys(_destroyWinner))
        {
            var piece = s.GetPiece(pieceId)!;
            var winner = _batch[_destroyWinner[pieceId]];
            int sq = piece.Square;
            _vacated.Add(sq);
            s.RemovePiece(piece);
            _newFacts.Add(new Fact(FactKinds.Died) { Piece = piece, From = sq, Source = winner.Source, Cause = winner.Cause });
        }
        foreach (var k in _kills)
            _newFacts.Add(new Fact(FactKinds.Killed) { Piece = s.GetPiece(k.VictimId), From = k.Square, Source = k.Source, Cause = k.Cause });

        // ---- 2. moves: lift every successful mover, then put them down (handles rotations)
        foreach (var m in _moves)
        {
            if (m.State != MoveState.Success) continue;
            _vacated.Add(m.From);
            _filled.Add(m.To);
            s.RemovePiece(m.Piece);
        }
        foreach (var m in _moves)
        {
            var prop = _batch[m.Proposal];
            if (m.State == MoveState.Success)
            {
                s.PutPiece(m.Piece, m.To);
                s.SetMovesMade(m.Piece, m.Piece.MovesMade + 1);
                _newFacts.Add(new Fact(FactKinds.Moved) { Piece = m.Piece, From = m.From, To = m.To, Source = prop.Source, Cause = prop.Cause, Jump = prop.Jump });
            }
            else
            {
                _newFacts.Add(new Fact(FactKinds.Collided) { Piece = m.Piece, Other = m.Blocker, From = m.From, To = m.To, Source = prop.Source, Cause = prop.Cause });
            }
        }

        // ---- 3. spawns and revives
        _revived.Clear();
        foreach (int sq in SortedKeys(_spawnWinner))
        {
            var p = _batch[_spawnWinner[sq]];
            if (OccupiedAfter(s, sq) || s.TileAt(sq).Def.BlocksLanding) { Note(wt, $"spawn at {sq} dropped: square not free"); continue; }
            Piece spawned;
            if (p.Kind == ProposalKind.Revive)
            {
                var dead = s.GetPiece(p.PieceId);
                if (dead == null || dead.IsOnBoard || !_revived.Add(dead.Id)) { Note(wt, $"revive of #{p.PieceId} dropped"); continue; }
                s.PutPiece(dead, sq);
                spawned = dead;
            }
            else
            {
                if (p.Def == null) continue;
                spawned = s.SpawnPiece(p.Def, p.Side, sq);
                if (p.SpawnAbilities != null)
                    foreach (var ability in p.SpawnAbilities)
                        if (!spawned.HasAbility(ability)) s.AddAbility(spawned, new AbilityInstance(ability, ability.InitialVars()));
            }
            _filled.Add(sq);
            _newFacts.Add(new Fact(FactKinds.Spawned) { Piece = spawned, To = sq, Source = p.Source, Cause = p.Cause });
        }

        // ---- 4. piece changes (survivors only)
        foreach (int pieceId in SortedKeys(_defWinner))
        {
            var piece = s.GetPiece(pieceId)!;
            var p = _batch[_defWinner[pieceId]];
            if (!piece.IsOnBoard || ReferenceEquals(piece.Def, p.Def)) continue;
            var old = piece.Def;
            s.SetDef(piece, p.Def!);
            _newFacts.Add(new Fact(FactKinds.PieceChanged) { Piece = piece, From = piece.Square, OldDef = old, NewDef = p.Def, Source = p.Source, Cause = p.Cause });
        }
        if (_abilityChanges.Count > 0)
        foreach (var ((pieceId, ability), change) in _abilityChanges.OrderBy(kv => kv.Key.PieceId).ThenBy(kv => kv.Key.Ability.Id, StringComparer.Ordinal))
        {
            var piece = s.GetPiece(pieceId)!;
            if (!piece.IsOnBoard) continue;
            var p = _batch[change.Proposal];
            int index = piece.AbilityIndex(ability);
            if (change.Remove)
            {
                if (index < 0) continue;
                s.RemoveAbility(piece, index);
                _newFacts.Add(new Fact(FactKinds.PieceChanged) { Piece = piece, From = piece.Square, Ability = ability, Added = false, Source = p.Source, Cause = p.Cause });
            }
            else if (index < 0)
            {
                s.AddAbility(piece, new AbilityInstance(ability, change.AddVars));
                _newFacts.Add(new Fact(FactKinds.PieceChanged) { Piece = piece, From = piece.Square, Ability = ability, Added = true, Source = p.Source, Cause = p.Cause });
            }
            else
            {
                // Re-adding an ability refreshes it: each variable keeps the larger value.
                var inst = piece.Abilities[index];
                for (int slot = 0; slot < inst.VarCount; slot++)
                {
                    int merged = Math.Max(inst.Var(slot), change.AddVars?[slot] ?? 0);
                    if (merged == inst.Var(slot)) continue;
                    int old = inst.Var(slot);
                    s.SetAbilityVar(piece, index, slot, merged);
                    _newFacts.Add(new Fact(FactKinds.VarChanged) { Var = VarTarget.OfAbility(piece, ability), Slot = slot, OldValue = old, NewValue = merged, Piece = piece, Source = p.Source, Cause = p.Cause });
                }
            }
        }

        // ---- 5. tiles
        foreach (int sq in SortedKeys(_tileWinner))
        {
            var p = _batch[_tileWinner[sq]];
            var old = s.TileAt(sq);
            var tile = p.Tile!.IsDefault ? Tile.Standard : new Tile(p.Tile, p.Vars, p.Value);
            s.ReplaceTile(sq, tile);
            _changedTiles.Add(sq);
            _newFacts.Add(new Fact(FactKinds.TileChanged) { From = sq, OldTile = old, NewTile = tile, Source = p.Source, Cause = p.Cause });
        }

        // ---- 6. variables: (winning set, else current) + sum of deltas
        if (_varChanges.Count > 0)
        foreach (var ((target, slot), change) in _varChanges.OrderBy(kv => kv.Key.Target.Kind).ThenBy(kv => kv.Key.Target.PieceId)
                     .ThenBy(kv => kv.Key.Target.Ability?.Id ?? "", StringComparer.Ordinal).ThenBy(kv => kv.Key.Target.Square)
                     .ThenBy(kv => kv.Key.Target.GlobalIndex).ThenBy(kv => kv.Key.Target.Side).ThenBy(kv => kv.Key.Slot))
        {
            if (!TryReadVar(s, target, slot, out int current, out var owner)) { Note(wt, $"var {target}[{slot}] dropped: target gone"); continue; }
            int value = (change.SetProposal >= 0 ? _batch[change.SetProposal].Value : current) + change.Sum;
            if (value == current) continue;
            WriteVar(s, target, slot, value);
            var p = _batch[change.Proposal];
            _newFacts.Add(new Fact(FactKinds.VarChanged) { Var = target, Slot = slot, OldValue = current, NewValue = value, Piece = owner, From = target.Square, Source = p.Source, Cause = p.Cause });
        }

        // ---- 7. clock
        // Deltas are accumulated in the state (GameState.ClockMs) for the host's real-time clock.
        if (clockWhite != 0)
        {
            s.AddClock(Side.White, clockWhite);
            _newFacts.Add(new Fact(FactKinds.ClockChanged) { Side = Side.White, NewValue = clockWhite });
        }
        if (clockBlack != 0)
        {
            s.AddClock(Side.Black, clockBlack);
            _newFacts.Add(new Fact(FactKinds.ClockChanged) { Side = Side.Black, NewValue = clockBlack });
        }
    }

    /// <summary>Keys in ascending order, in a reused buffer (iterate it fully before calling again).</summary>
    private List<int> SortedKeys<T>(Dictionary<int, T> map)
    {
        _keys.Clear();
        foreach (int k in map.Keys) _keys.Add(k);
        if (_keys.Count > 1) _keys.Sort();
        return _keys;
    }

    private bool OccupiedAfter(GameState s, int sq)
        => _filled.Contains(sq) || (s.PieceAt(sq) != null && !_vacated.Contains(sq));

    private static int[]? MaxMerge(int[]? current, int[]? incoming, int count)
    {
        if (count == 0) return current ?? Array.Empty<int>();
        var merged = new int[count];
        for (int i = 0; i < count; i++)
        {
            int a = current != null && i < current.Length ? current[i] : int.MinValue;
            int b = incoming != null && i < incoming.Length ? incoming[i] : 0;
            merged[i] = current == null ? b : Math.Max(a, b);
        }
        return merged;
    }

    private bool TryReadVar(GameState s, VarTarget target, int slot, out int value, out Piece? owner)
    {
        value = 0;
        owner = null;
        switch (target.Kind)
        {
            case VarTargetKind.Ability:
            {
                var piece = s.GetPiece(target.PieceId);
                if (piece == null || !piece.IsOnBoard || target.Ability == null) return false;
                var inst = piece.GetAbility(target.Ability);
                if (inst == null || slot < 0 || slot >= inst.VarCount) return false;
                owner = piece;
                value = inst.Var(slot);
                return true;
            }
            case VarTargetKind.Tile:
            {
                if (target.Square < 0 || target.Square >= s.Area || _changedTiles.Contains(target.Square)) return false;
                var tile = s.TileAt(target.Square);
                if (slot < 0 || slot >= tile.VarCount) return false;
                value = tile.Var(slot);
                return true;
            }
            case VarTargetKind.Global:
            {
                if (target.GlobalIndex < 0 || target.GlobalIndex >= s.Globals.Count) return false;
                var g = s.Globals[target.GlobalIndex];
                if (slot < 0 || slot >= g.VarCount) return false;
                value = g.Var(slot);
                return true;
            }
            case VarTargetKind.Side:
            {
                if (slot != VarTarget.ExtraTurnsSlot) return false;
                value = s.ExtraTurns(target.Side);
                return true;
            }
            default:
                return false;
        }
    }

    private static void WriteVar(GameState s, VarTarget target, int slot, int value)
    {
        switch (target.Kind)
        {
            case VarTargetKind.Ability:
                var piece = s.GetPiece(target.PieceId)!;
                s.SetAbilityVar(piece, piece.AbilityIndex(target.Ability!), slot, value);
                break;
            case VarTargetKind.Tile:
                s.SetTileVar(target.Square, slot, value);
                break;
            case VarTargetKind.Global:
                s.SetGlobalVar(target.GlobalIndex, slot, value);
                break;
            case VarTargetKind.Side:
                s.SetExtraTurns(target.Side, Math.Max(0, value));
                break;
        }
    }

    // ---------------------------------------------------------------- sliding-block move resolution

    /// <summary>
    /// A move succeeds if its destination is empty after this wave's removals, or its occupant is itself
    /// successfully moving away. Several moves to one square all fail; head-on swaps fail; rotations of
    /// three or more succeed. Destinations that can't be landed on fail.
    /// </summary>
    private void ResolveMoves(GameState s)
    {
        foreach (var m in _moves)
        {
            if (_destCount[m.To] > 1)
            {
                m.State = MoveState.Fail;
                m.Blocker = _moves.Where(o => o != m && o.To == m.To).OrderBy(o => o.Piece.Id).First().Piece;
            }
            else if (s.TileAt(m.To).Def.BlocksLanding)
            {
                m.State = MoveState.Fail;
            }
        }

        for (int i = 0; i < _moves.Count; i++)
        {
            _path.Clear();
            Visit(s, i);
        }
    }

    private void Visit(GameState s, int i)
    {
        var m = _moves[i];
        if (m.State is MoveState.Success or MoveState.Fail) return;

        if (m.State == MoveState.Visiting)
        {
            // Found a cycle: every move from here to the top of the path moves into the next one's square.
            int start = _path.IndexOf(i);
            int length = _path.Count - start;
            bool ok = length >= 3;
            for (int k = start; k < _path.Count; k++)
            {
                var member = _moves[_path[k]];
                member.State = ok ? MoveState.Success : MoveState.Fail;
                if (!ok) member.Blocker = _moves[_path[k + 1 < _path.Count ? k + 1 : start]].Piece;
            }
            return;
        }

        m.State = MoveState.Visiting;
        _path.Add(i);

        var occupant = s.PieceAt(m.To);
        if (occupant != null && _destroyWinner.ContainsKey(occupant.Id)) occupant = null;

        if (occupant == null)
        {
            m.State = MoveState.Success;
        }
        else if (_moveFrom.TryGetValue(m.To, out int j))
        {
            Visit(s, j);
            if (m.State == MoveState.Visiting)
            {
                bool ok = _moves[j].State == MoveState.Success;
                m.State = ok ? MoveState.Success : MoveState.Fail;
                if (!ok) m.Blocker = occupant;
            }
        }
        else
        {
            m.State = MoveState.Fail;
            m.Blocker = occupant;
        }

        _path.RemoveAt(_path.Count - 1);
    }
}
