using System;
using System.Collections.Generic;
using RogueChess.Engine.Content;
using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Tests;

// Test-only behaviors that exercise the resolver. They mimic prototype mechanics closely enough to test the
// resolution contract, but they are not the M3 content ports.

internal static class Geo
{
    public static int Chebyshev(IBoardView b, int a, int c)
        => Math.Max(Math.Abs(b.FileOf(a) - b.FileOf(c)), Math.Abs(b.RankOf(a) - b.RankOf(c)));
}

/// <summary>When this piece dies, destroy every piece within one square of where it died (including the centre).</summary>
internal sealed class ProbeExploding : AbilityDef
{
    public static readonly ProbeExploding Def = new();
    private ProbeExploding() : base("probe-exploding", "Exploding", value: 2) { }

    public override FactKinds Triggers => FactKinds.Died;

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        if (fact.Piece != ctx.SelfPiece) return;
        var b = ctx.Board;
        int cx = b.FileOf(fact.From), cy = b.RankOf(fact.From);
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (b.PieceAt(cx + dx, cy + dy) is { } victim)
                    ctx.Propose(Proposal.Destroy(victim, "explosion"));
    }
}

/// <summary>Prevent layer: if an adjacent ally would be destroyed, destroy this piece instead.</summary>
internal sealed class ProbeScapegoat : AbilityDef
{
    public static readonly ProbeScapegoat Def = new();
    private ProbeScapegoat() : base("probe-scapegoat", "Scapegoat") { }

    public override ModifierLayer Layer => ModifierLayer.Prevent;

    public override void Modify(ModifierContext ctx)
    {
        var self = ctx.SelfPiece!;
        for (int i = 0; i < ctx.Batch.Count; i++)
        {
            var p = ctx.Batch[i];
            if (p.Kind != ProposalKind.Destroy) continue;
            var target = ctx.Board.GetPiece(p.PieceId);
            if (target == null || target == self || target.Side != self.Side) continue;
            if (Geo.Chebyshev(ctx.Board, target.Square, ctx.SelfSquare) == 1)
                ctx.Claim(i, Proposal.Destroy(self, "scapegoat"));
        }
    }
}

/// <summary>Marker: <see cref="ProbeSmite"/> destroys pieces carrying it at turn end.</summary>
internal sealed class ProbeDoomed : AbilityDef
{
    public static readonly ProbeDoomed Def = new();
    private ProbeDoomed() : base("probe-doomed", "Doomed") { }
}

/// <summary>Global: at every turn end, destroy all doomed pieces at once.</summary>
internal sealed class ProbeSmite : GlobalDef
{
    public static readonly ProbeSmite Def = new();
    private ProbeSmite() : base("probe-smite", "Smite") { }

    public override FactKinds Triggers => FactKinds.TurnEnded;

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        foreach (var p in ctx.Board.PiecesOnBoard())
            if (p.HasAbility(ProbeDoomed.Def)) ctx.Propose(Proposal.Destroy(p, "smite"));
    }
}

/// <summary>Tile, prevent layer: the first destroy aimed at the piece standing here is cancelled and the tile is used up.</summary>
internal sealed class ProbeGuardianTile : TileDef
{
    public static readonly ProbeGuardianTile Def = new();
    private ProbeGuardianTile() : base("probe-guardian", "Guardian") { }

    public override ModifierLayer Layer => ModifierLayer.Prevent;

    public override void Modify(ModifierContext ctx)
    {
        for (int i = 0; i < ctx.Batch.Count; i++)
        {
            var p = ctx.Batch[i];
            if (p.Kind == ProposalKind.Destroy && ctx.Board.GetPiece(p.PieceId)?.Square == ctx.SelfSquare)
            {
                ctx.Claim(i, Proposal.ChangeTile(ctx.SelfSquare, Standard, "guardian-consumed"));
                return;
            }
        }
    }
}

/// <summary>A tile with one variable, for var-merge tests.</summary>
internal sealed class ProbeCounterTile : TileDef
{
    public static readonly ProbeCounterTile Def = new();
    private ProbeCounterTile() : base("probe-counter-tile", "Counter tile", varCount: 1) { }
}

/// <summary>Global: at turn end, propose whatever the test's script returns.</summary>
internal sealed class ProbeScript : GlobalDef
{
    private readonly Func<IBoardView, SourceRef, IEnumerable<Proposal>> _script;

    public ProbeScript(string id, Func<IBoardView, IEnumerable<Proposal>> script)
        : base(id, id) => _script = (b, _) => script(b);

    public ProbeScript(string id, Func<IBoardView, SourceRef, IEnumerable<Proposal>> script)
        : base(id, id) => _script = script;

    public override FactKinds Triggers => FactKinds.TurnEnded;

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        foreach (var p in _script(ctx.Board, ctx.Self)) ctx.Propose(p);
    }
}

/// <summary>At turn end, spawn a knight of this piece's side on the square stored in var 0.</summary>
internal sealed class ProbeSpawner : AbilityDef
{
    public static readonly ProbeSpawner Def = new();
    private ProbeSpawner() : base("probe-spawner", "Spawner", varCount: 1) { }

    public override FactKinds Triggers => FactKinds.TurnEnded;

    public override void OnFact(TriggerContext ctx, in Fact fact)
        => ctx.Propose(Proposal.Spawn(StandardPieces.Knight, ctx.SelfPiece!.Side, ctx.SelfAbility!.Var(0), "spawner"));
}

/// <summary>At turn end, destroy the piece whose id is in var 0.</summary>
internal sealed class ProbeSniper : AbilityDef
{
    public static readonly ProbeSniper Def = new();
    private ProbeSniper() : base("probe-sniper", "Sniper", varCount: 1) { }

    public override FactKinds Triggers => FactKinds.TurnEnded;

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        if (ctx.Board.GetPiece(ctx.SelfAbility!.Var(0)) is { IsOnBoard: true } target)
            ctx.Propose(Proposal.Destroy(target, "snipe"));
    }
}

/// <summary>Counts kills credited to this piece in var 0.</summary>
internal sealed class ProbeKillCounter : AbilityDef
{
    public static readonly ProbeKillCounter Def = new();
    private ProbeKillCounter() : base("probe-kill-counter", "Kill counter", varCount: 1) { }

    public override FactKinds Triggers => FactKinds.Killed;

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        if (fact.Source.Piece == ctx.SelfPiece)
            ctx.Propose(Proposal.AddVar(VarTarget.OfAbility(ctx.SelfPiece!, Def), 0, 1, "kill"));
    }
}

/// <summary>Whenever this piece moves, it moves straight back: an infinite loop for the watchdog.</summary>
internal sealed class ProbePingPong : AbilityDef
{
    public static readonly ProbePingPong Def = new();
    private ProbePingPong() : base("probe-ping-pong", "Ping-pong") { }

    public override FactKinds Triggers => FactKinds.Moved;

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        if (fact.Piece == ctx.SelfPiece) ctx.Propose(Proposal.Move(ctx.SelfPiece!, fact.From, "bounce"));
    }
}

/// <summary>Global: increments its own counter on every turn end and every change to it, forever.</summary>
internal sealed class ProbeRunaway : GlobalDef
{
    public static readonly ProbeRunaway Def = new();
    private ProbeRunaway() : base("probe-runaway", "Runaway", varCount: 1) { }

    public override FactKinds Triggers => FactKinds.TurnEnded | FactKinds.VarChanged;

    public override void OnFact(TriggerContext ctx, in Fact fact)
        => ctx.Propose(Proposal.AddVar(VarTarget.OfGlobal(ctx.Self.GlobalIndex), 0, 1, "runaway"));
}

/// <summary>When this piece dies, it comes back (same id) on the square it died on.</summary>
internal sealed class ProbeRevenant : AbilityDef
{
    public static readonly ProbeRevenant Def = new();
    private ProbeRevenant() : base("probe-revenant", "Revenant") { }

    public override FactKinds Triggers => FactKinds.Died;

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        if (fact.Piece == ctx.SelfPiece) ctx.Propose(Proposal.Revive(ctx.SelfPiece!, fact.From, "revenant"));
    }
}

/// <summary>Status with a timer in var 0: counts down at its owner's turn end and removes itself at zero.</summary>
internal sealed class ProbeTimer : AbilityDef
{
    public static readonly ProbeTimer Def = new();
    private ProbeTimer() : base("probe-timer", "Timer", varCount: 1) { }

    public override int[] InitialVars() => new[] { 2 };

    public override FactKinds Triggers => FactKinds.TurnEnded | FactKinds.VarChanged;

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        var self = ctx.SelfPiece!;
        if (fact.Kind == FactKinds.TurnEnded && fact.Side == self.Side)
            ctx.Propose(Proposal.AddVar(VarTarget.OfAbility(self, Def), 0, -1, "tick"));
        else if (fact.Kind == FactKinds.VarChanged && fact.Var == VarTarget.OfAbility(self, Def) && fact.NewValue <= 0)
            ctx.Propose(Proposal.RemoveAbility(self, Def, "expired"));
    }
}

/// <summary>Global: counts turn starts in var 0.</summary>
internal sealed class ProbeTurnCounter : GlobalDef
{
    public static readonly ProbeTurnCounter Def = new();
    private ProbeTurnCounter() : base("probe-turn-counter", "Turn counter", varCount: 1) { }

    public override FactKinds Triggers => FactKinds.TurnStarted;

    public override void OnFact(TriggerContext ctx, in Fact fact)
        => ctx.Propose(Proposal.AddVar(VarTarget.OfGlobal(ctx.Self.GlobalIndex), 0, 1, "turn"));
}

/// <summary>Plain status with one variable, for ability merge tests.</summary>
internal sealed class ProbeStatus : AbilityDef
{
    public static readonly ProbeStatus Def = new();
    private ProbeStatus() : base("probe-status", "Status", varCount: 1) { }
}
