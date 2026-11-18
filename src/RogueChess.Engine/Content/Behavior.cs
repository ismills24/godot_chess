using System;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content;

/// <summary>Kinds of fact a trigger can subscribe to. See docs/RESOLUTION_RULES.md §5.5.</summary>
[Flags]
public enum FactKinds
{
    None = 0,
    TurnStarted = 1 << 0,
    TurnEnded = 1 << 1,
    Died = 1 << 2,
    Killed = 1 << 3,
    Moved = 1 << 4,
    Spawned = 1 << 5,
    Collided = 1 << 6,
    PieceChanged = 1 << 7,
    TileChanged = 1 << 8,
    VarChanged = 1 << 9,
    ClockChanged = 1 << 10,
    All = ~0,
}

/// <summary>Modifier layers, applied in this order every wave (RESOLUTION_RULES.md §5.2).</summary>
public enum ModifierLayer
{
    None = 0,

    /// <summary>Changes what happens (Piercing, Bouncer).</summary>
    Redirect = 1,

    /// <summary>Stops something happening, optionally at a cost (Guardian, Fog, Scapegoat).</summary>
    Prevent = 2,
}

/// <summary>
/// Hooks shared by everything that takes part in resolution: piece defs, abilities, tiles and global
/// effects. Hooks are pure. They read the frozen snapshot through their context and return proposals
/// (triggers) or claims (modifiers). They never change state and must keep no mutable fields.
/// </summary>
public abstract class Behavior
{
    private int _hookFlags = -1;

    /// <summary>Facts this behavior's <see cref="OnFact"/> wants to see.</summary>
    public virtual FactKinds Triggers => FactKinds.None;

    /// <summary>
    /// React to a committed fact by proposing effects for the next wave. Called once per matching fact per wave.
    /// A piece's own death triggers still run (with last-known information) in the wave after it dies.
    /// </summary>
    public virtual void OnFact(TriggerContext ctx, in Fact fact) { }

    /// <summary>Which modifier layer <see cref="Modify"/> runs in, or None.</summary>
    public virtual ModifierLayer Layer => ModifierLayer.None;

    /// <summary>Inspect the wave's proposals (as they were when the layer started) and claim the ones to rewrite.</summary>
    public virtual void Modify(ModifierContext ctx) { }

    internal bool HasHooks
    {
        get
        {
            if (_hookFlags < 0) _hookFlags = Triggers != FactKinds.None || Layer != ModifierLayer.None ? 1 : 0;
            return _hookFlags == 1;
        }
    }
}
