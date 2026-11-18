using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RogueChess.Engine.Content;
using RogueChess.Engine.Rules;

namespace RogueChess.Engine.Core;

/// <summary>
/// The whole game position as plain mutable data.
///
/// Every change goes through a small set of logged operations. Each one updates <see cref="Hash"/>
/// incrementally and pushes its inverse onto an undo log, so callers make a move, inspect, then
/// <see cref="UndoTo"/> a mark instead of cloning. Use <see cref="Clone"/> only to hand a copy to another thread.
///
/// Only the resolver and setup code change state. Content receives the read-only <see cref="IBoardView"/>.
/// Squares are indexed <c>y * Width + x</c>; rank 0 is White's back rank.
/// </summary>
public sealed class GameState : IBoardView
{
    private readonly Piece?[] _board;
    private readonly Tile[] _tiles;
    private readonly Dictionary<int, Piece> _pieces = new();
    private readonly List<GlobalInstance> _globals = new();
    private readonly List<UndoEntry> _undo = new();
    private int _nextPieceId = 1;

    public GameState(int width, int height, RuleSet rules)
    {
        if (width < 1 || height < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "Board must be at least 1x1.");
        if (width > 26)
            throw new ArgumentOutOfRangeException(nameof(width), "Square names support at most 26 files.");

        Width = width;
        Height = height;
        Rules = rules;
        _board = new Piece?[width * height];
        _tiles = new Tile[width * height];
        Array.Fill(_tiles, Tile.Standard);
        Hash = ComputeHashFromScratch();
    }

    public int Width { get; }
    public int Height { get; }
    public int Area => _board.Length;
    public RuleSet Rules { get; }
    public Side SideToMove { get; private set; }

    /// <summary>Half-moves played since setup.</summary>
    public int Ply { get; private set; }

    /// <summary>Square a pawn may capture into en passant this turn, or -1.</summary>
    public int EnPassantSquare { get; private set; } = -1;

    /// <summary>State of the seeded random generator content draws from.</summary>
    public ulong RandomState { get; private set; }

    public IReadOnlyList<GlobalInstance> Globals => _globals;

    /// <summary>64-bit Zobrist hash of the position, maintained incrementally.</summary>
    public ulong Hash { get; private set; }

    /// <summary>Current undo-log length. Pass it to <see cref="UndoTo"/> to roll back everything after it.</summary>
    public int UndoDepth => _undo.Count;

    // ---------------------------------------------------------------- geometry

    public int Sq(int x, int y) => y * Width + x;
    public int FileOf(int sq) => sq % Width;
    public int RankOf(int sq) => sq / Width;
    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    // ---------------------------------------------------------------- queries

    public Piece? PieceAt(int sq) => _board[sq];
    public Piece? PieceAt(int x, int y) => InBounds(x, y) ? _board[Sq(x, y)] : null;
    public Tile TileAt(int sq) => _tiles[sq];
    public Piece? GetPiece(int id) => _pieces.TryGetValue(id, out var p) ? p : null;

    /// <summary>Pieces on the board in square order (deterministic).</summary>
    public IEnumerable<Piece> PiecesOnBoard(Side? side = null)
    {
        foreach (var p in _board)
            if (p != null && (side == null || p.Side == side))
                yield return p;
    }

    // ---------------------------------------------------------------- setup API

    /// <summary>Place a new piece on an empty square. Undoable like any other operation.</summary>
    public Piece Place(PieceDef def, Side side, int sq)
    {
        if (_board[sq] != null)
            throw new InvalidOperationException($"Square {sq} is already occupied.");
        return SpawnPiece(def, side, sq);
    }

    /// <summary>Give a piece an ability during setup. Uses the def's initial variables unless <paramref name="vars"/> are given.</summary>
    public AbilityInstance GrantAbility(Piece piece, AbilityDef def, params int[] vars)
    {
        if (piece.HasAbility(def))
            throw new InvalidOperationException($"{piece} already has {def.Id}.");
        var inst = new AbilityInstance(def, vars.Length > 0 ? vars : def.InitialVars());
        AddAbility(piece, inst);
        return inst;
    }

    public void SetTile(int sq, TileDef def, params int[] vars)
        => ReplaceTile(sq, def.IsDefault ? Tile.Standard : new Tile(def, vars));

    public GlobalInstance AddGlobal(GlobalDef def, params int[] vars)
    {
        var inst = new GlobalInstance(def, vars);
        _undo.Add(new UndoEntry(OpKind.AddGlobal, obj: inst));
        _globals.Add(inst);
        Hash ^= GlobalHash(inst, _globals.Count - 1);
        return inst;
    }

    public void SetSideToMove(Side side) => SetTurn(side, Ply);

    public void SeedRandom(ulong seed) => SetRandomState(seed);

    /// <summary>Forget the undo log (call once setup is finished so setup can't be undone).</summary>
    public void ClearHistory() => _undo.Clear();

    // ---------------------------------------------------------------- operations (resolver only)

    internal Piece SpawnPiece(PieceDef def, Side side, int sq)
    {
        var p = new Piece(_nextPieceId++, side, def);
        _pieces.Add(p.Id, p);
        RawPut(p, sq);
        _undo.Add(new UndoEntry(OpKind.Spawn, p));
        return p;
    }

    /// <summary>Take a piece off the board. It keeps its def and abilities (last-known information).</summary>
    internal void RemovePiece(Piece p)
    {
        _undo.Add(new UndoEntry(OpKind.Remove, p, a: p.Square));
        RawLift(p);
    }

    /// <summary>Put an off-board piece (removed earlier, or lifted mid-commit) onto an empty square.</summary>
    internal void PutPiece(Piece p, int sq)
    {
        if (p.IsOnBoard) throw new InvalidOperationException($"{p} is already on the board.");
        if (_board[sq] != null) throw new InvalidOperationException($"Cannot put {p} onto occupied square {sq}.");
        _undo.Add(new UndoEntry(OpKind.Put, p));
        RawPut(p, sq);
    }

    internal void MovePiece(Piece p, int to)
    {
        if (_board[to] != null)
            throw new InvalidOperationException($"Cannot move {p} onto occupied square {to}.");
        _undo.Add(new UndoEntry(OpKind.Move, p, a: p.Square));
        RawLift(p);
        RawPut(p, to);
    }

    internal void SetMovesMade(Piece p, int movesMade)
    {
        _undo.Add(new UndoEntry(OpKind.MovesMade, p, a: p.MovesMade));
        RawSetMovesMade(p, movesMade);
    }

    // Hot path (every move), so no closure.
    private void RawSetMovesMade(Piece p, int movesMade)
    {
        if (p.IsOnBoard) Hash ^= PieceHash(p, p.Square);
        p.MovesMade = movesMade;
        if (p.IsOnBoard) Hash ^= PieceHash(p, p.Square);
    }

    internal void SetDef(Piece p, PieceDef def)
    {
        _undo.Add(new UndoEntry(OpKind.Def, p, def: p.Def));
        Rehash(p, () => p.Def = def);
    }

    internal void AddAbility(Piece p, AbilityInstance inst)
    {
        _undo.Add(new UndoEntry(OpKind.AddAbility, p, obj: inst));
        Rehash(p, () => p.AbilityList.Add(inst));
    }

    internal void RemoveAbility(Piece p, int index)
    {
        var inst = p.AbilityList[index];
        _undo.Add(new UndoEntry(OpKind.RemoveAbility, p, obj: inst, a: index));
        Rehash(p, () => p.AbilityList.RemoveAt(index));
    }

    internal void SetAbilityVar(Piece p, int abilityIndex, int slot, int value)
    {
        var inst = p.AbilityList[abilityIndex];
        _undo.Add(new UndoEntry(OpKind.AbilityVar, p, a: abilityIndex, b: slot, c: inst.Vars[slot]));
        Rehash(p, () => inst.Vars[slot] = value);
    }

    internal void ReplaceTile(int sq, Tile tile)
    {
        _undo.Add(new UndoEntry(OpKind.Tile, tile: _tiles[sq], a: sq));
        RawSetTile(sq, tile);
    }

    internal void SetTileVar(int sq, int slot, int value)
    {
        var tile = _tiles[sq];
        _undo.Add(new UndoEntry(OpKind.TileVar, a: sq, b: slot, c: tile.Vars[slot]));
        RawSetTileVar(sq, slot, value);
    }

    internal void SetGlobalVar(int index, int slot, int value)
    {
        _undo.Add(new UndoEntry(OpKind.GlobalVar, a: index, b: slot, c: _globals[index].Vars[slot]));
        RawSetGlobalVar(index, slot, value);
    }

    internal void SetTurn(Side side, int ply)
    {
        _undo.Add(new UndoEntry(OpKind.Turn, a: (int)SideToMove, b: Ply));
        RawSetTurn(side, ply);
    }

    internal void SetEnPassant(int sq)
    {
        _undo.Add(new UndoEntry(OpKind.EnPassant, a: EnPassantSquare));
        RawSetEnPassant(sq);
    }

    internal void SetRandomState(ulong value)
    {
        _undo.Add(new UndoEntry(OpKind.Random, u: RandomState));
        RawSetRandom(value);
    }

    /// <summary>Draw from the seeded generator (SplitMix64). Logged, so undo replays the same draws.</summary>
    internal int NextRandom(int maxExclusive)
    {
        if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        ulong next = RandomState + 0x9E3779B97F4A7C15UL;
        SetRandomState(next);
        return (int)(Zobrist.Mix(next) % (ulong)maxExclusive);
    }

    /// <summary>Roll back every operation recorded after <paramref name="mark"/> (a previous <see cref="UndoDepth"/>).</summary>
    public void UndoTo(int mark)
    {
        for (int i = _undo.Count - 1; i >= mark; i--)
        {
            var e = _undo[i];
            var p = e.Piece;
            switch (e.Kind)
            {
                case OpKind.Spawn:
                    RawLift(p!);
                    _pieces.Remove(p!.Id);
                    _nextPieceId = p.Id;
                    break;
                case OpKind.Remove:
                    RawPut(p!, e.A);
                    break;
                case OpKind.Put:
                    RawLift(p!);
                    break;
                case OpKind.Move:
                    RawLift(p!);
                    RawPut(p!, e.A);
                    break;
                case OpKind.MovesMade:
                    RawSetMovesMade(p!, e.A);
                    break;
                case OpKind.Def:
                    Rehash(p!, () => p!.Def = e.Def!);
                    break;
                case OpKind.AddAbility:
                    Rehash(p!, () => p!.AbilityList.RemoveAt(p.AbilityList.Count - 1));
                    break;
                case OpKind.RemoveAbility:
                    Rehash(p!, () => p!.AbilityList.Insert(e.A, (AbilityInstance)e.Obj!));
                    break;
                case OpKind.AbilityVar:
                    Rehash(p!, () => p!.AbilityList[e.A].Vars[e.B] = e.C);
                    break;
                case OpKind.Tile:
                    RawSetTile(e.A, e.Tile!);
                    break;
                case OpKind.TileVar:
                    RawSetTileVar(e.A, e.B, e.C);
                    break;
                case OpKind.AddGlobal:
                    Hash ^= GlobalHash(_globals[^1], _globals.Count - 1);
                    _globals.RemoveAt(_globals.Count - 1);
                    break;
                case OpKind.GlobalVar:
                    RawSetGlobalVar(e.A, e.B, e.C);
                    break;
                case OpKind.Turn:
                    RawSetTurn((Side)e.A, e.B);
                    break;
                case OpKind.EnPassant:
                    RawSetEnPassant(e.A);
                    break;
                case OpKind.Random:
                    RawSetRandom(e.U);
                    break;
            }
        }
        _undo.RemoveRange(mark, _undo.Count - mark);
    }

    // ---------------------------------------------------------------- raw mutation (hash-aware, unlogged)

    private void RawPut(Piece p, int sq)
    {
        _board[sq] = p;
        p.Square = sq;
        Hash ^= PieceHash(p, sq);
    }

    private void RawLift(Piece p)
    {
        int sq = p.Square;
        Hash ^= PieceHash(p, sq);
        _board[sq] = null;
        p.Square = -1;
    }

    /// <summary>Apply a change to a piece's hashed data, keeping the hash in step while it's on the board.</summary>
    private void Rehash(Piece p, Action change)
    {
        if (p.IsOnBoard) Hash ^= PieceHash(p, p.Square);
        change();
        if (p.IsOnBoard) Hash ^= PieceHash(p, p.Square);
    }

    private void RawSetTile(int sq, Tile tile)
    {
        Hash ^= TileHash(_tiles[sq], sq) ^ TileHash(tile, sq);
        _tiles[sq] = tile;
    }

    private void RawSetTileVar(int sq, int slot, int value)
    {
        var tile = _tiles[sq];
        Hash ^= TileHash(tile, sq);
        tile.Vars[slot] = value;
        Hash ^= TileHash(tile, sq);
    }

    private void RawSetGlobalVar(int index, int slot, int value)
    {
        var g = _globals[index];
        Hash ^= GlobalHash(g, index);
        g.Vars[slot] = value;
        Hash ^= GlobalHash(g, index);
    }

    private void RawSetTurn(Side side, int ply)
    {
        Hash ^= SideHash(SideToMove) ^ SideHash(side);
        SideToMove = side;
        Ply = ply;
    }

    private void RawSetEnPassant(int sq)
    {
        Hash ^= EnPassantHash(EnPassantSquare) ^ EnPassantHash(sq);
        EnPassantSquare = sq;
    }

    private void RawSetRandom(ulong value)
    {
        Hash ^= RandomHash(RandomState) ^ RandomHash(value);
        RandomState = value;
    }

    // ---------------------------------------------------------------- hashing

    private static ulong Pack(int slot, int value) => ((ulong)(uint)slot << 32) | (uint)value;

    private static ulong VarsHash(ulong category, ulong defHash, int[] vars, ulong position)
    {
        ulong h = 0;
        for (int i = 0; i < vars.Length; i++)
            if (vars[i] != 0) h ^= Zobrist.Key(category, defHash, Pack(i, vars[i]), position);
        return h;
    }

    private static ulong PieceHash(Piece p, int sq)
    {
        ulong h = Zobrist.Key(Zobrist.PieceCategory, p.Def.HashId, (ulong)p.Side, (ulong)sq);
        // Only pieces whose rules depend on having moved (castling) hash that flag, so ordinary
        // move-and-return sequences still transpose.
        if (p.MovesMade > 0 && p.Def.HashesMovedFlag)
            h ^= Zobrist.Key(Zobrist.MovedCategory, (ulong)sq);
        foreach (var a in p.AbilityList)
        {
            h ^= Zobrist.Key(Zobrist.AbilityCategory, a.Def.HashId, (ulong)sq);
            h ^= VarsHash(Zobrist.AbilityVarCategory, a.Def.HashId, a.Vars, (ulong)sq);
        }
        return h;
    }

    private static ulong TileHash(Tile t, int sq)
    {
        if (t.Def.IsDefault) return 0;
        return Zobrist.Key(Zobrist.TileCategory, t.Def.HashId, (ulong)sq)
             ^ VarsHash(Zobrist.TileVarCategory, t.Def.HashId, t.Vars, (ulong)sq);
    }

    private static ulong GlobalHash(GlobalInstance g, int index)
        => Zobrist.Key(Zobrist.GlobalCategory, g.Def.HashId, (ulong)index)
         ^ VarsHash(Zobrist.GlobalVarCategory, g.Def.HashId, g.Vars, (ulong)index);

    private static ulong SideHash(Side side)
        => side == Side.Black ? Zobrist.Key(Zobrist.SideCategory, 1) : 0;

    private static ulong EnPassantHash(int sq)
        => sq < 0 ? 0 : Zobrist.Key(Zobrist.EnPassantCategory, (ulong)sq);

    private static ulong RandomHash(ulong state)
        => state == 0 ? 0 : Zobrist.Key(Zobrist.RandomCategory, state);

    /// <summary>Recompute the hash from nothing. Used to verify the incremental hash.</summary>
    public ulong ComputeHashFromScratch()
    {
        ulong h = SideHash(SideToMove) ^ EnPassantHash(EnPassantSquare) ^ RandomHash(RandomState);
        for (int sq = 0; sq < _board.Length; sq++)
        {
            if (_board[sq] is { } p) h ^= PieceHash(p, sq);
            h ^= TileHash(_tiles[sq], sq);
        }
        for (int i = 0; i < _globals.Count; i++) h ^= GlobalHash(_globals[i], i);
        return h;
    }

    // ---------------------------------------------------------------- copying / debugging

    /// <summary>Deep copy with an empty undo log, safe to hand to another thread.</summary>
    public GameState Clone()
    {
        var c = new GameState(Width, Height, Rules);
        foreach (var p in _pieces.Values)
            c._pieces.Add(p.Id, p.Copy());
        for (int sq = 0; sq < _board.Length; sq++)
        {
            c._board[sq] = _board[sq] is { } p ? c._pieces[p.Id] : null;
            c._tiles[sq] = _tiles[sq].CopyIfMutable();
        }
        foreach (var g in _globals) c._globals.Add(g.Copy());
        c._nextPieceId = _nextPieceId;
        c.SideToMove = SideToMove;
        c.Ply = Ply;
        c.EnPassantSquare = EnPassantSquare;
        c.RandomState = RandomState;
        c.Hash = Hash;
        return c;
    }

    /// <summary>Complete, deterministic text form of the state (for tests and debugging).</summary>
    public string Dump()
    {
        var sb = new StringBuilder();
        sb.Append($"{Width}x{Height} {Rules.Name} side={SideToMove} ply={Ply} ep={EnPassantSquare} next={_nextPieceId} rng={RandomState}\n");
        for (int y = Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < Width; x++)
            {
                int sq = Sq(x, y);
                var p = _board[sq];
                sb.Append(p == null ? "." : DescribePiece(p));
                if (!_tiles[sq].Def.IsDefault) sb.Append($"[{_tiles[sq]}]");
                sb.Append(' ');
            }
            sb.Append('\n');
        }
        foreach (var p in _pieces.Values.Where(p => !p.IsOnBoard).OrderBy(p => p.Id))
            sb.Append($"off:{DescribePiece(p)}\n");
        foreach (var g in _globals)
            sb.Append($"global:{g}\n");
        return sb.ToString();
    }

    private static string DescribePiece(Piece p)
    {
        string s = $"{p.Def.Id}:{p.Side}:{p.Id}:{p.MovesMade}";
        return p.AbilityList.Count == 0 ? s : s + "{" + string.Join(",", p.AbilityList) + "}";
    }

    // ---------------------------------------------------------------- undo log entries

    private enum OpKind : byte
    {
        Spawn, Remove, Put, Move, MovesMade, Def, AddAbility, RemoveAbility, AbilityVar,
        Tile, TileVar, AddGlobal, GlobalVar, Turn, EnPassant, Random,
    }

    private readonly struct UndoEntry
    {
        public UndoEntry(OpKind kind, Piece? piece = null, Tile? tile = null, PieceDef? def = null,
            object? obj = null, int a = 0, int b = 0, int c = 0, ulong u = 0)
        {
            Kind = kind;
            Piece = piece;
            Tile = tile;
            Def = def;
            Obj = obj;
            A = a;
            B = b;
            C = c;
            U = u;
        }

        public OpKind Kind { get; }
        public Piece? Piece { get; }
        public Tile? Tile { get; }
        public PieceDef? Def { get; }
        public object? Obj { get; }
        public int A { get; }
        public int B { get; }
        public int C { get; }
        public ulong U { get; }
    }
}
