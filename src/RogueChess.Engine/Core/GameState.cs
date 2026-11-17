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
/// Every change goes through a small set of operations (spawn, remove, move, set moves-made, set def,
/// replace tile, set turn, set en passant). Each one updates <see cref="Hash"/> incrementally and pushes
/// its inverse onto an undo log, so callers make a move, inspect, then <see cref="UndoTo"/> a mark
/// instead of cloning. Use <see cref="Clone"/> only to hand a copy to another thread.
///
/// Squares are indexed <c>y * Width + x</c>; rank 0 is White's back rank.
/// </summary>
public sealed class GameState
{
    private readonly Piece?[] _board;
    private readonly Tile[] _tiles;
    private readonly Dictionary<int, Piece> _pieces = new();
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

    public void SetTile(int sq, TileDef def) => ReplaceTile(sq, def.IsDefault ? Tile.Standard : new Tile(def));

    public void SetSideToMove(Side side) => SetTurn(side, Ply);

    /// <summary>Forget the undo log (call once setup is finished so setup can't be undone).</summary>
    public void ClearHistory() => _undo.Clear();

    // ---------------------------------------------------------------- operations

    internal Piece SpawnPiece(PieceDef def, Side side, int sq)
    {
        var p = new Piece(_nextPieceId++, side, def);
        _pieces.Add(p.Id, p);
        RawPut(p, sq);
        _undo.Add(new UndoEntry(OpKind.Spawn, p));
        return p;
    }

    internal void RemovePiece(Piece p)
    {
        _undo.Add(new UndoEntry(OpKind.Remove, p, a: p.Square));
        RawLift(p);
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

    internal void SetDef(Piece p, PieceDef def)
    {
        _undo.Add(new UndoEntry(OpKind.Def, p, def: p.Def));
        RawSetDef(p, def);
    }

    internal void ReplaceTile(int sq, Tile tile)
    {
        _undo.Add(new UndoEntry(OpKind.Tile, tile: _tiles[sq], a: sq));
        RawSetTile(sq, tile);
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

    /// <summary>Roll back every operation recorded after <paramref name="mark"/> (a previous <see cref="UndoDepth"/>).</summary>
    public void UndoTo(int mark)
    {
        for (int i = _undo.Count - 1; i >= mark; i--)
        {
            var e = _undo[i];
            switch (e.Kind)
            {
                case OpKind.Spawn:
                    RawLift(e.Piece!);
                    _pieces.Remove(e.Piece!.Id);
                    _nextPieceId = e.Piece.Id;
                    break;
                case OpKind.Remove:
                    RawPut(e.Piece!, e.A);
                    break;
                case OpKind.Move:
                    RawLift(e.Piece!);
                    RawPut(e.Piece!, e.A);
                    break;
                case OpKind.MovesMade:
                    RawSetMovesMade(e.Piece!, e.A);
                    break;
                case OpKind.Def:
                    RawSetDef(e.Piece!, e.Def!);
                    break;
                case OpKind.Tile:
                    RawSetTile(e.A, e.Tile!);
                    break;
                case OpKind.Turn:
                    RawSetTurn((Side)e.A, e.B);
                    break;
                case OpKind.EnPassant:
                    RawSetEnPassant(e.A);
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

    private void RawSetMovesMade(Piece p, int movesMade)
    {
        if (p.IsOnBoard) Hash ^= PieceHash(p, p.Square);
        p.MovesMade = movesMade;
        if (p.IsOnBoard) Hash ^= PieceHash(p, p.Square);
    }

    private void RawSetDef(Piece p, PieceDef def)
    {
        if (p.IsOnBoard) Hash ^= PieceHash(p, p.Square);
        p.Def = def;
        if (p.IsOnBoard) Hash ^= PieceHash(p, p.Square);
    }

    private void RawSetTile(int sq, Tile tile)
    {
        Hash ^= TileHash(_tiles[sq], sq) ^ TileHash(tile, sq);
        _tiles[sq] = tile;
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

    // ---------------------------------------------------------------- hashing

    private static ulong PieceHash(Piece p, int sq)
    {
        ulong h = Zobrist.Key(Zobrist.PieceCategory, p.Def.HashId, (ulong)p.Side, (ulong)sq);
        // Only pieces whose rules depend on having moved (castling) hash that flag, so ordinary
        // move-and-return sequences still transpose.
        if (p.MovesMade > 0 && p.Def.HashesMovedFlag)
            h ^= Zobrist.Key(Zobrist.MovedCategory, (ulong)sq);
        return h;
    }

    private static ulong TileHash(Tile t, int sq)
        => t.Def.IsDefault ? 0 : Zobrist.Key(Zobrist.TileCategory, t.Def.HashId, (ulong)sq);

    private static ulong SideHash(Side side)
        => side == Side.Black ? Zobrist.Key(Zobrist.SideCategory, 1) : 0;

    private static ulong EnPassantHash(int sq)
        => sq < 0 ? 0 : Zobrist.Key(Zobrist.EnPassantCategory, (ulong)sq);

    /// <summary>Recompute the hash from nothing. Used to verify the incremental hash.</summary>
    public ulong ComputeHashFromScratch()
    {
        ulong h = SideHash(SideToMove) ^ EnPassantHash(EnPassantSquare);
        for (int sq = 0; sq < _board.Length; sq++)
        {
            if (_board[sq] is { } p) h ^= PieceHash(p, sq);
            h ^= TileHash(_tiles[sq], sq);
        }
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
            c._tiles[sq] = _tiles[sq];
        }
        c._nextPieceId = _nextPieceId;
        c.SideToMove = SideToMove;
        c.Ply = Ply;
        c.EnPassantSquare = EnPassantSquare;
        c.Hash = Hash;
        return c;
    }

    /// <summary>Complete, deterministic text form of the state (for tests and debugging).</summary>
    public string Dump()
    {
        var sb = new StringBuilder();
        sb.Append($"{Width}x{Height} {Rules.Name} side={SideToMove} ply={Ply} ep={EnPassantSquare} next={_nextPieceId}\n");
        for (int y = Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < Width; x++)
            {
                int sq = Sq(x, y);
                var p = _board[sq];
                sb.Append(p == null ? "." : $"{p.Def.Id}:{p.Side}:{p.Id}:{p.MovesMade}");
                if (!_tiles[sq].Def.IsDefault) sb.Append($"[{_tiles[sq].Def.Id}]");
                sb.Append(' ');
            }
            sb.Append('\n');
        }
        foreach (var p in _pieces.Values.Where(p => !p.IsOnBoard).OrderBy(p => p.Id))
            sb.Append($"off:{p.Def.Id}:{p.Side}:{p.Id}:{p.MovesMade}\n");
        return sb.ToString();
    }

    // ---------------------------------------------------------------- undo log entries

    private enum OpKind : byte { Spawn, Remove, Move, MovesMade, Def, Tile, Turn, EnPassant }

    private readonly struct UndoEntry
    {
        public UndoEntry(OpKind kind, Piece? piece = null, Tile? tile = null, PieceDef? def = null, int a = 0, int b = 0)
        {
            Kind = kind;
            Piece = piece;
            Tile = tile;
            Def = def;
            A = a;
            B = b;
        }

        public OpKind Kind { get; }
        public Piece? Piece { get; }
        public Tile? Tile { get; }
        public PieceDef? Def { get; }
        public int A { get; }
        public int B { get; }
    }
}
