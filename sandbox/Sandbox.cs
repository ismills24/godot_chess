using System.Diagnostics;
using System.Text;
using Godot;
using RogueChess.Engine;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;
using Side = RogueChess.Engine.Core.Side;

namespace GodotChess.Sandbox;

/// <summary>
/// Debug sandbox entry point. For now it shows the start position as text and times a perft run, proving
/// the engine works inside Godot. The playable sandbox arrives in M5 (docs/PORT_PLAN.md §4).
/// </summary>
public partial class Sandbox : Control
{
    private const int PerftDepth = 4;

    public override void _Ready()
    {
        var state = Fen.Parse(Fen.StartPosition);

        var sw = Stopwatch.StartNew();
        long nodes = Perft.Count(state, PerftDepth);
        sw.Stop();
        double knps = nodes / sw.Elapsed.TotalSeconds / 1000.0;

        // Play a short line through the resolver and keep the trace of the capture.
        TurnTrace? lastTrace = null;
        foreach (var uci in new[] { "e2e4", "d7d5", "e4d5" })
        {
            var move = MoveText.FindLegal(state, uci)!.Value;
            lastTrace = Game.PlayTurn(state, move, new ResolveOptions(Trace: true)).Trace;
        }

        // Explicit '\n': AppendLine emits "\r\n" on Windows, which a Label renders as two line breaks.
        var text = new StringBuilder();
        text.Append($"{EngineInfo.Name} (resolution contract v{EngineInfo.ResolutionContractVersion})\n\n");
        text.Append(Board(state)).Append("\n\n");
        text.Append($"perft({PerftDepth}) = {nodes:N0} nodes in {sw.ElapsedMilliseconds} ms ({knps:N0} knps)\n\n");
        text.Append("Trace of 3. exd5:\n").Append(lastTrace!.Format(state).TrimEnd());

        GetNode<Label>("Status").Text = text.ToString();
        GD.Print($"[Sandbox] perft({PerftDepth}) = {nodes} in {sw.ElapsedMilliseconds} ms ({knps:F0} knps)");
    }

    private static string Board(GameState s)
    {
        var sb = new StringBuilder();
        for (int y = s.Height - 1; y >= 0; y--)
        {
            sb.Append(y + 1).Append("  ");
            for (int x = 0; x < s.Width; x++)
            {
                var p = s.PieceAt(x, y);
                char c = p == null ? '.' : p.Side == Side.White ? char.ToUpperInvariant(p.Def.Symbol) : p.Def.Symbol;
                sb.Append(c).Append(' ');
            }
            sb.Append('\n');
        }
        sb.Append("   ");
        for (int x = 0; x < s.Width; x++) sb.Append((char)('a' + x)).Append(' ');
        return sb.ToString();
    }
}
