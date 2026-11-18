using System;
using System.Collections.Generic;
using RogueChess.Engine.Content;
using RogueChess.Engine.Core;

namespace RogueChess.Engine.Resolution;

/// <summary>The action being resolved this turn, for rule triggers that need the player's choices (promotion).</summary>
internal readonly record struct ActionInfo(int PieceId, int PromotionIndex)
{
    public static readonly ActionInfo None = new(-1, -1);
}

/// <summary>What a trigger hook sees: the frozen snapshot, who it is, and a way to propose effects.</summary>
public sealed class TriggerContext
{
    private readonly List<Proposal> _sink;
    private GameState _state = null!;

    internal TriggerContext(List<Proposal> sink) => _sink = sink;

    public IBoardView Board => _state;
    public SourceRef Self { get; private set; }
    public int Wave { get; private set; }

    /// <summary>"turn-start", "action" or "turn-end".</summary>
    public string Phase { get; private set; } = "";

    public Piece? SelfPiece => Self.Piece;
    public AbilityInstance? SelfAbility => Self.Ability;

    /// <summary>Where this source stood in the snapshot (last-known square for a dead piece).</summary>
    public int SelfSquare => Self.Square;

    internal ActionInfo Action { get; private set; }

    internal void Setup(GameState state, string phase, int wave, ActionInfo action)
    {
        _state = state;
        Phase = phase;
        Wave = wave;
        Action = action;
    }

    internal void SetSelf(in SourceRef self) => Self = self;

    public void Propose(in Proposal proposal) => _sink.Add(proposal with { Source = Self });

    /// <summary>Deterministic random draw from the state's seeded generator, in [0, maxExclusive).</summary>
    public int Random(int maxExclusive) => _state.NextRandom(maxExclusive);
}

/// <summary>What a modifier hook sees: the wave's proposals as they were when its layer started.</summary>
public sealed class ModifierContext
{
    private readonly List<Claim> _claims;

    internal ModifierContext(List<Claim> claims) => _claims = claims;

    public IBoardView Board { get; private set; } = null!;
    public SourceRef Self { get; private set; }
    public ModifierLayer Layer { get; private set; }
    public IReadOnlyList<Proposal> Batch { get; private set; } = Array.Empty<Proposal>();
    public Piece? SelfPiece => Self.Piece;
    public int SelfSquare => Self.Square;

    internal void Setup(IBoardView board, ModifierLayer layer, IReadOnlyList<Proposal> batch)
    {
        Board = board;
        Layer = layer;
        Batch = batch;
    }

    internal void SetSelf(in SourceRef self) => Self = self;

    /// <summary>Claim one proposal and replace it with <paramref name="replacements"/> (none = cancel it).</summary>
    public void Claim(int index, params Proposal[] replacements) => Claim(new[] { index }, replacements);

    /// <summary>
    /// Claim several proposals together (all or nothing) and replace them with <paramref name="replacements"/>.
    /// A proposal can be rewritten by at most one modifier per layer; contested claims go to the source
    /// first in canonical order, measured from the first claimed proposal's square.
    /// </summary>
    public void Claim(int[] indices, params Proposal[] replacements)
    {
        if (indices.Length == 0) throw new ArgumentException("Claim at least one proposal.", nameof(indices));
        foreach (int i in indices)
            if (i < 0 || i >= Batch.Count) throw new ArgumentOutOfRangeException(nameof(indices), $"No proposal {i} in this batch.");

        var stamped = new Proposal[replacements.Length];
        for (int i = 0; i < replacements.Length; i++) stamped[i] = replacements[i] with { Source = Self };
        _claims.Add(new Claim(Layer, Self, (int[])indices.Clone(), stamped, _claims.Count));
    }
}

/// <summary>A modifier's claim on proposals in the current layer.</summary>
public sealed record Claim(ModifierLayer Layer, SourceRef Source, int[] Indices, Proposal[] Replacements, int Order)
{
    public bool Accepted { get; internal set; }
}
