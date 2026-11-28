using System.Collections.Generic;
using System.Linq;
using System.Text;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;

namespace RogueChess.Engine.Resolution;

/// <summary>Everything that happened in one turn, wave by wave. Drives debugging and animation.</summary>
public sealed class TurnTrace
{
    public List<ResolutionTrace> Resolutions { get; } = new();

    public IEnumerable<Fact> AllFacts => Resolutions.Where(r => !r.RolledBack).SelectMany(r => r.Waves).SelectMany(w => w.Facts);

    public string Format(IBoardView board)
    {
        var sb = new StringBuilder();
        foreach (var r in Resolutions) r.AppendTo(sb, board);
        return sb.ToString();
    }
}

public sealed class ResolutionTrace
{
    internal ResolutionTrace(string phase) => Phase = phase;

    public string Phase { get; }
    public List<WaveTrace> Waves { get; } = new();
    public bool RolledBack { get; internal set; }
    public string? Failure { get; internal set; }

    internal void AppendTo(StringBuilder sb, IBoardView board)
    {
        sb.Append($"== {Phase}{(RolledBack ? $" (rolled back: {Failure})" : "")}\n");
        foreach (var w in Waves) w.AppendTo(sb, board);
    }
}

public sealed class WaveTrace
{
    internal WaveTrace(int index) => Index = index;

    public int Index { get; }
    public List<Proposal> Gathered { get; } = new();
    public List<Claim> Claims { get; } = new();
    public List<Proposal> Final { get; } = new();
    public List<string> Notes { get; } = new();
    public List<Fact> Facts { get; } = new();
    public ulong HashAfter { get; internal set; }

    /// <summary>
    /// The wave as text, rendered while it resolved, so pieces are named as they were at that moment
    /// (a pawn that later evolves still reads as a pawn here).
    /// </summary>
    public List<string> Lines { get; } = new();

    internal void RecordGathered(IBoardView board)
    {
        foreach (var p in Gathered) Lines.Add("  propose " + TraceText.Describe(p, board));
    }

    internal void RecordClaims(IEnumerable<Claim> claims, IBoardView board)
    {
        foreach (var c in claims)
        {
            Claims.Add(c);
            Lines.Add($"  {(c.Accepted ? "claim" : "lost claim")} [{c.Layer}] {c.Source} on #{string.Join(",#", c.Indices)} -> "
                + (c.Replacements.Length == 0 ? "cancel" : string.Join("; ", c.Replacements.Select(r => TraceText.Describe(r, board)))));
        }
    }

    internal void RecordNote(string note)
    {
        Notes.Add(note);
        Lines.Add("  note " + note);
    }

    internal void RecordFacts(IEnumerable<Fact> facts, IBoardView board)
    {
        foreach (var f in facts)
        {
            Facts.Add(f);
            Lines.Add("  fact " + TraceText.Describe(f, board));
        }
    }

    internal void AppendTo(StringBuilder sb, IBoardView board)
    {
        sb.Append($"-- wave {Index}\n");
        foreach (var line in Lines) sb.Append(line).Append('\n');
    }
}

internal static class TraceText
{
    private static string Sq(IBoardView b, int sq) => sq < 0 ? "-" : MoveText.SquareName(b, sq);

    private static string PieceName(IBoardView b, int id) => b.GetPiece(id)?.ToString() ?? $"#{id}";

    public static string Describe(in Proposal p, IBoardView b) => p.Kind switch
    {
        ProposalKind.Destroy => $"destroy {PieceName(b, p.PieceId)} ({p.Cause}) by {p.Source}",
        ProposalKind.Move => $"move {PieceName(b, p.PieceId)} -> {Sq(b, p.Square)} ({p.Cause}) by {p.Source}",
        ProposalKind.Spawn => $"spawn {p.Side} {p.Def} @ {Sq(b, p.Square)} ({p.Cause}) by {p.Source}",
        ProposalKind.Revive => $"revive {PieceName(b, p.PieceId)} @ {Sq(b, p.Square)} ({p.Cause}) by {p.Source}",
        ProposalKind.ChangeDef => $"change {PieceName(b, p.PieceId)} to {p.Def} ({p.Cause}) by {p.Source}",
        ProposalKind.AddAbility => $"add {p.Ability} to {PieceName(b, p.PieceId)} ({p.Cause}) by {p.Source}",
        ProposalKind.RemoveAbility => $"remove {p.Ability} from {PieceName(b, p.PieceId)} ({p.Cause}) by {p.Source}",
        ProposalKind.ChangeTile => $"tile {Sq(b, p.Square)} -> {p.Tile}{(p.Value > 0 ? $" for {p.Value} turns" : "")} ({p.Cause}) by {p.Source}",
        ProposalKind.AddVar => $"var {p.Var}[{p.Slot}] += {p.Value} ({p.Cause}) by {p.Source}",
        ProposalKind.SetVar => $"var {p.Var}[{p.Slot}] = {p.Value} ({p.Cause}) by {p.Source}",
        ProposalKind.Clock => $"clock {p.Side} {p.Value:+#;-#;0}ms ({p.Cause}) by {p.Source}",
        _ => p.Kind.ToString(),
    };

    public static string Describe(in Fact f, IBoardView b) => f.Kind switch
    {
        Content.FactKinds.TurnStarted => $"turn started {f.Side}",
        Content.FactKinds.TurnEnded => $"turn ended {f.Side}",
        Content.FactKinds.Died => $"died {f.Piece} @ {Sq(b, f.From)} ({f.Cause})",
        Content.FactKinds.Killed => $"killed {f.Piece} by {f.Source}",
        Content.FactKinds.Moved => $"moved {f.Piece} {Sq(b, f.From)} -> {Sq(b, f.To)} ({f.Cause})",
        Content.FactKinds.Spawned => $"spawned {f.Piece} @ {Sq(b, f.To)} ({f.Cause})",
        Content.FactKinds.Collided => $"collided {f.Piece} {Sq(b, f.From)} -> {Sq(b, f.To)} blocked by {(f.Other?.ToString() ?? "tile")}",
        Content.FactKinds.PieceChanged => f.Ability != null
            ? $"{(f.Added ? "gained" : "lost")} {f.Ability}: {f.Piece}"
            : $"changed {f.Piece}: {f.OldDef} -> {f.NewDef}",
        Content.FactKinds.TileChanged => $"tile {Sq(b, f.From)}: {f.OldTile} -> {f.NewTile}",
        Content.FactKinds.VarChanged => $"var {f.Var}[{f.Slot}]: {f.OldValue} -> {f.NewValue}",
        Content.FactKinds.ClockChanged => $"clock {f.Side} {f.NewValue:+#;-#;0}ms",
        _ => f.Kind.ToString(),
    };
}
