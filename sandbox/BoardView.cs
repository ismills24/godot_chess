using System;
using System.Collections.Generic;
using Godot;
using RogueChess.Engine.Core;
using Side = RogueChess.Engine.Core.Side;

namespace GodotChess.Sandbox;

/// <summary>
/// Draws a <see cref="GameState"/> (squares, tiles, pieces, ability badges, legal moves and what happened last
/// turn) and reports clicked squares. It doesn't change the state; <see cref="Sandbox"/> does.
/// </summary>
public partial class BoardView : Control
{
    public const int BoardPixels = 840;
    private static readonly Vector2 Origin = new(30, 10);

    public enum MarkerKind { Move, Collide, Death, Spawn, Tile, Change }

    public readonly record struct Marker(MarkerKind Kind, int From, int To, int Wave);

    public event Action<int>? SquareClicked;

    public GameState? State { get; set; }
    public int Selected { get; set; } = -1;
    public HashSet<int> QuietTargets { get; } = new();
    public HashSet<int> CaptureTargets { get; } = new();
    public List<Marker> Markers { get; } = new();

    private Font _glyphs = null!;
    private Font _label = null!;

    private static readonly Color Light = new(0.93f, 0.86f, 0.73f);
    private static readonly Color Dark = new(0.70f, 0.53f, 0.40f);

    /// <summary>One colour per wave, so the order things happened in is visible at a glance.</summary>
    private static readonly Color[] WaveColors =
    {
        new(1.00f, 0.84f, 0.10f), new(1.00f, 0.55f, 0.10f), new(0.95f, 0.20f, 0.20f),
        new(0.85f, 0.20f, 0.75f), new(0.45f, 0.30f, 0.95f), new(0.20f, 0.60f, 1.00f),
    };

    public int Cell => State == null ? 0 : BoardPixels / Math.Max(State.Width, State.Height);

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(BoardPixels + 40, BoardPixels + 40);
        MouseFilter = MouseFilterEnum.Stop;
        _glyphs = new SystemFont { FontNames = new[] { "Segoe UI Symbol", "Noto Sans Symbols 2", "DejaVu Sans" } };
        _label = ThemeDB.FallbackFont;
    }

    public Rect2 CellRect(int sq)
    {
        int x = State!.FileOf(sq), y = State.RankOf(sq);
        return new Rect2(Origin + new Vector2(x * Cell, (State.Height - 1 - y) * Cell), new Vector2(Cell, Cell));
    }

    public override void _GuiInput(InputEvent e)
    {
        if (State == null || e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb) return;
        var local = mb.Position - Origin;
        int x = (int)Math.Floor(local.X / Cell), row = (int)Math.Floor(local.Y / Cell);
        int y = State.Height - 1 - row;
        if (!State.InBounds(x, y)) return;
        AcceptEvent();
        SquareClicked?.Invoke(State.Sq(x, y));
    }

    public override void _Draw()
    {
        if (State == null) return;
        var s = State;

        for (int sq = 0; sq < s.Area; sq++)
        {
            var r = CellRect(sq);
            DrawRect(r, (s.FileOf(sq) + s.RankOf(sq)) % 2 == 0 ? Dark : Light);
            DrawTile(s.TileAt(sq), r);
        }

        foreach (var m in Markers)
        {
            var color = WaveColors[m.Wave % WaveColors.Length];
            if (m.Kind is MarkerKind.Death) DrawRect(CellRect(m.From), color with { A = 0.45f });
            if (m.Kind is MarkerKind.Spawn or MarkerKind.Change) DrawRect(CellRect(m.From).Grow(-3), color, filled: false, width: 4);
            if (m.Kind is MarkerKind.Tile) DrawRect(CellRect(m.From).Grow(-8), color, filled: false, width: 3);
        }

        if (Selected >= 0) DrawRect(CellRect(Selected), new Color(0.2f, 0.8f, 1f, 0.45f));

        for (int sq = 0; sq < s.Area; sq++)
            if (s.PieceAt(sq) is { } p) DrawPiece(p, CellRect(sq));

        foreach (var m in Markers)
        {
            var color = WaveColors[m.Wave % WaveColors.Length];
            if (m.Kind == MarkerKind.Move) DrawArrow(CellRect(m.From).GetCenter(), CellRect(m.To).GetCenter(), color);
            if (m.Kind == MarkerKind.Collide)
            {
                var to = CellRect(m.To).GetCenter();
                DrawArrow(CellRect(m.From).GetCenter(), to, color with { A = 0.6f });
                DrawCross(to, Cell * 0.18f, new Color(0.9f, 0.1f, 0.1f));
            }
            // On a capture the capturer now stands on the death square; a big cross there would read as "it died".
            if (m.Kind == MarkerKind.Death && s.PieceAt(m.From) == null) DrawCross(CellRect(m.From).GetCenter(), Cell * 0.3f, color.Darkened(0.3f));
            DrawWaveNumber(m);
        }

        foreach (int sq in QuietTargets) DrawCircle(CellRect(sq).GetCenter(), Cell * 0.13f, new Color(0.1f, 0.4f, 0.1f, 0.6f));
        foreach (int sq in CaptureTargets) DrawArc(CellRect(sq).GetCenter(), Cell * 0.44f, 0, Mathf.Tau, 48, new Color(0.8f, 0.1f, 0.1f, 0.8f), 5);

        int fontSize = Math.Max(12, Cell / 6);
        for (int x = 0; x < s.Width; x++)
            DrawString(_label, Origin + new Vector2(x * Cell, s.Height * Cell + fontSize + 4), ((char)('a' + x)).ToString(),
                HorizontalAlignment.Center, Cell, fontSize, Colors.White);
        for (int y = 0; y < s.Height; y++)
            DrawString(_label, new Vector2(4, Origin.Y + (s.Height - 1 - y) * Cell + Cell / 2 + fontSize / 3), (y + 1).ToString(),
                HorizontalAlignment.Left, -1, fontSize, Colors.White);
    }

    private void DrawTile(Tile tile, Rect2 r)
    {
        if (tile.Def.IsDefault) return;
        int fontSize = Math.Max(10, Cell / 7);
        switch (tile.Def.Id)
        {
            case "wall":
                DrawRect(r.Grow(-2), new Color(0.33f, 0.29f, 0.27f));
                for (int i = 1; i < 4; i++)
                {
                    float y = r.Position.Y + r.Size.Y * i / 4;
                    DrawLine(new Vector2(r.Position.X + 2, y), new Vector2(r.End.X - 2, y), new Color(0.55f, 0.5f, 0.45f), 2);
                }
                break;
            case "guardian":
                DrawRect(r.Grow(-3), new Color(1f, 0.8f, 0.1f), filled: false, width: 6);
                DrawString(_label, r.Position + new Vector2(6, fontSize + 4), "GUARD", HorizontalAlignment.Left, -1, fontSize, new Color(0.6f, 0.4f, 0f));
                break;
            case "fog":
                DrawRect(r, new Color(0.55f, 0.6f, 0.65f, 0.75f));
                DrawString(_label, r.Position + new Vector2(6, fontSize + 4), "FOG", HorizontalAlignment.Left, -1, fontSize, new Color(0.2f, 0.25f, 0.3f));
                break;
            default:
                DrawRect(r.Grow(-3), new Color(0.6f, 0.2f, 0.8f), filled: false, width: 4);
                DrawString(_label, r.Position + new Vector2(6, fontSize + 4), tile.ToString(), HorizontalAlignment.Left, -1, fontSize, new Color(0.4f, 0.1f, 0.6f));
                break;
        }
    }

    private static string Glyph(Piece p) => p.Def.Id switch
    {
        "king" => "♚",
        "queen" => "♛",
        "rook" => "♜",
        "bishop" => "♝",
        "knight" => "♞",
        "pawn" => "♟︎",
        _ => char.ToUpperInvariant(p.Def.Symbol).ToString(),
    };

    private void DrawPiece(Piece p, Rect2 r)
    {
        int size = (int)(Cell * 0.68f);
        var pos = new Vector2(r.Position.X, r.Position.Y + Cell * 0.5f + size * 0.36f);
        bool white = p.Side == Side.White;
        DrawStringOutline(_glyphs, pos, Glyph(p), HorizontalAlignment.Center, Cell, size, 6, white ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.95f, 0.95f, 0.95f));
        DrawString(_glyphs, pos, Glyph(p), HorizontalAlignment.Center, Cell, size, white ? Colors.White : new Color(0.12f, 0.12f, 0.12f));

        // Ability badges along the bottom edge, with any non-zero variable shown after the code.
        float badge = Cell * 0.17f;
        for (int i = 0; i < p.Abilities.Count; i++)
        {
            var a = p.Abilities[i];
            var center = new Vector2(r.Position.X + badge + 2 + i * (badge * 2 + 2), r.End.Y - badge - 2);
            DrawCircle(center, badge, BadgeColor(a.Def.Id));
            DrawArc(center, badge, 0, Mathf.Tau, 24, Colors.Black, 1.5f);
            int fs = Math.Max(9, (int)(badge * 0.95f));
            string text = BadgeCode(a.Def.Id, a.Def.Name);
            DrawString(_label, center + new Vector2(-badge, fs * 0.35f), text, HorizontalAlignment.Center, badge * 2, fs, Colors.White);
            for (int v = 0; v < a.VarCount; v++)
                if (a.Var(v) != 0)
                    DrawString(_label, center + new Vector2(badge * 0.6f, -badge * 0.6f), a.Var(v).ToString(), HorizontalAlignment.Left, -1, fs, Colors.Black);
        }
    }

    public static string BadgeCode(string id, string name) => id switch
    {
        "exploding" => "Ex",
        "scapegoat" => "Sg",
        "phantom" => "Ph",
        "predator" => "Pr",
        _ => name.Length >= 2 ? name[..2] : name,
    };

    public static Color BadgeColor(string id) => id switch
    {
        "exploding" => new Color(0.85f, 0.15f, 0.1f),
        "scapegoat" => new Color(0.15f, 0.6f, 0.2f),
        "phantom" => new Color(0.55f, 0.3f, 0.85f),
        "predator" => new Color(0.95f, 0.5f, 0.05f),
        _ => Color.FromHsv((id.GetHashCode() & 0xFFFF) / 65535f, 0.6f, 0.75f),
    };

    private void DrawArrow(Vector2 from, Vector2 to, Color color)
    {
        var dir = (to - from).Normalized();
        var tip = to - dir * Cell * 0.15f;
        var bodyEnd = tip - dir * Cell * 0.18f;
        DrawLine(from, bodyEnd, color, Cell * 0.09f, true);
        var normal = new Vector2(-dir.Y, dir.X) * Cell * 0.14f;
        DrawColoredPolygon(new[] { tip, bodyEnd + normal, bodyEnd - normal }, color);
    }

    private void DrawCross(Vector2 c, float half, Color color)
    {
        DrawLine(c + new Vector2(-half, -half), c + new Vector2(half, half), color, 5, true);
        DrawLine(c + new Vector2(-half, half), c + new Vector2(half, -half), color, 5, true);
    }

    private void DrawWaveNumber(Marker m)
    {
        int sq = m.Kind is MarkerKind.Move or MarkerKind.Collide ? m.To : m.From;
        var r = CellRect(sq);
        int fs = Math.Max(11, Cell / 7);
        var corner = new Vector2(r.End.X - fs - 2, r.Position.Y + 2);
        DrawRect(new Rect2(corner, new Vector2(fs, fs + 2)), WaveColors[m.Wave % WaveColors.Length]);
        DrawString(_label, corner + new Vector2(0, fs - 1), m.Wave.ToString(), HorizontalAlignment.Center, fs, fs - 1, Colors.Black);
    }
}
