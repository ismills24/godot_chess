# Content Guide

How to add pieces, abilities and tiles. Read [RESOLUTION_RULES.md](RESOLUTION_RULES.md) for *why* things resolve the way they do; this guide is the *how*.

## Where things live

| What | Where |
|---|---|
| Piece types | `src/RogueChess.Engine/Content/StandardPieces.cs` (or a new file next to it) |
| Abilities | `src/RogueChess.Engine/Content/Abilities/` |
| Tiles | `src/RogueChess.Engine/Content/Tiles/` |
| Registration (map ids, sandbox palette) | `src/RogueChess.Engine/Content/ContentLibrary.cs` |
| Relics, consumables | `src/RogueChess.Engine/Content/Relics/`, `Content/Consumables/` |
| Tests | `tests/RogueChess.Engine.Tests/ContentTests.cs` (`RunContentTests.cs` for relics, consumables, clock) |
| Sandbox scenarios | `tools/make_scenarios.py` → `maps/sandbox/*.json` |

Every new def needs one line in `ContentLibrary.CreateCatalog()`. That makes it loadable from map files and shows it in the sandbox palette.

## Gameplay visuals

The sandbox uses the wooden 3D board. Left-click a piece and a highlighted destination to play;
the Place, Tile, Ability, Erase and Undo tools operate on the same engine state as before.
Right- or middle-drag to rotate, scroll to zoom, or use the Rotate left/right buttons (45-degree steps), White view, Black view and Overhead. Selection faintly tints the piece itself; destination markers remain on the board.

`assets/pieces/visual_catalog.json` maps `PieceDef.Id` to a visual recipe. A wooden recipe selects
`base`, `body`, and `head` independently from `pawn`, `knight`, `bishop`, `rook`, `queen`, `king`.
For example, a custom piece can use `{ "base": "rook", "body": "pawn", "head": "knight", "yawDegrees": 90 }`.
The body's socket sets the head's height. Side determines maple/walnut and opposing orientation.
Optional `scale` (positive, default 1) and `yawDegrees` (default 0) apply to the whole model.

Alternatively, set `scene` to a `res://...tscn` PackedScene with a Node3D root. It replaces the
wooden module recipe; custom scenes provide their own materials. All models must have their
foot centered at local zero, with +Y up. Keep the footprint inside a 1.45-unit square.
Catalog changes are loaded when the game starts. No engine registration changes are needed
when replacing artwork for an existing piece type.

The presentation-only `PieceVisualResolver` reads the piece object and catalog;
`PieceVisualAssembler` builds the result. `BoardView3D` reconciles models by piece ID, updating
position and assembly from authoritative state after moves, edits, captures, evolution and undo.
Loading a different game clears those identities. Rendering never changes the rules or waits
for an animation. Head-swap animation is intentionally deferred.

Unmapped pieces, unavailable scenes/components and invalid transforms use a side-colored token
with the piece's symbol. An incomplete recipe falls back as a whole, avoiding a misleading
partial silhouette. Abilities use readable badges (including variable values); special tiles
use named outlines until they have artwork. These overlays remain visible above the 3D assets.

Run `res://experiments/gameplay/VerifyGameplayVisuals.tscn` in Godot .NET for integration checks
covering model identity, moves, captures, undo, evolution, mixed components, placeholders,
rectangular boards and picking. Build the C# project first. The original 2D `BoardView` remains
available as a reference renderer; gameplay uses `BoardView3D`.

## The rules every piece of content follows

1. **No mutable fields.** Defs are shared singletons. Anything that changes (charges, timers, kill counts) is a *variable* on the instance (`varCount` in the constructor), changed only by proposing `AddVar` / `SetVar`.
2. **Read, then propose.** Hooks get a read-only `IBoardView` and return proposals (`ctx.Propose`) or claims (`ctx.Claim`). They never change the board directly.
3. **Identify yourself with `ctx.SelfPiece`** (reference equality with `fact.Piece`), not ids or names.
4. **Use the cause constants** in `Causes` when it matters what kind of destruction something is (Guardian only stops `Causes.IsMoveCapture`).

## Recipe 1: a new piece type (pure data)

Movement is built from primitives: `Slide(directions, maxRange)`, `Leap(offsets)`, `PawnMovement()`.

```csharp
public static readonly PieceDef Archbishop = new("archbishop", "Archbishop", 'a', value: 8,
    new Movement[] { new Slide(Slide.Diagonal), new Leap(Leap.KnightOffsets) });
```

Register it with `.AddPiece(Archbishop, "Archbishop")`. Tags: `PieceTags.Royal` (losing all royals loses), `Pawn` (en passant victim, promotes), `CastlesWithRoyal`. The sandbox draws pieces it has no glyph for as their letter.

## Recipe 2: a trigger ability (reacts to what happened)

Override `Triggers` (which facts to receive) and `OnFact`. Facts arrive the wave after they happened. Your own `Died` fact still reaches you after death, with last-known information (`fact.From` is where you died).

```csharp
public sealed class ExplodingAbility : AbilityDef
{
    public static readonly ExplodingAbility Instance = new();
    private ExplodingAbility() : base("exploding", "Exploding", value: 2) { }

    public override FactKinds Triggers => FactKinds.Died;

    public override void OnFact(TriggerContext ctx, in Fact fact)
    {
        if (fact.Piece != ctx.SelfPiece) return;          // only my own death
        // ...propose Proposal.Destroy(victim, Causes.Explosion) for each neighbour
    }
}
```

Useful facts: `Died`, `Killed` (`fact.Source.Piece` is the killer), `Moved`, `Spawned`, `Collided`, `TurnStarted` / `TurnEnded` (`fact.Side`), `VarChanged`, `TileChanged`, `PieceChanged`.

## Recipe 3: a modifier ability (changes what is about to happen)

Override `Layer` (`Redirect` to change *what* happens, `Prevent` to stop it) and `Modify`. You see the whole wave's proposals in `ctx.Batch` and claim the ones you rewrite. A claim with no replacements cancels them.

```csharp
public override ModifierLayer Layer => ModifierLayer.Prevent;

public override void Modify(ModifierContext ctx)
{
    for (int i = 0; i < ctx.Batch.Count; i++)
        if (/* Batch[i] destroys my adjacent ally */)
            ctx.Claim(i, Proposal.Destroy(ctx.SelfPiece!, Causes.Scapegoat));
}
```

You don't handle conflicts yourself. If two modifiers claim the same proposal, the engine picks the one first in canonical order (closest, then clockwise from north).

## Recipe 4: an ability that changes movement

Override `ModifyMoves(board, piece, self, moves, firstIndex)`: add, remove or rewrite the piece's moves from `firstIndex` on. See `PhantomAbility` (adds slides through allies). The engine still resolves each resulting move through the normal pipeline.

## Recipe 4b: an ability with its own action (ranged attack)

An ability can offer an action that isn't a plain move: add a `Move` with `MoveFlags.Ability` (plus `Capture` if it takes the piece on `To`) and `Aux` = the ability's index on the piece (`piece.AbilityIndex(this)`), then override `ProposeAction(board, piece, self, action, into)` to turn it into wave-0 proposals. The engine stamps them with the ability as their source, so kills are credited to the piece. The action is offered beside the normal moves (written `a1a4@marksman`), so the player and the AI choose between them. See `MarksmanAbility` (destroy the target + spend a charge).

## Recipe 5: an ability with state

Declare variables and change them only through proposals. Use `VarChanged` to react to the new value (see `PredatorAbility`):

```csharp
private PredatorAbility() : base("predator", "Predator", value: 3, varCount: 1) { }
// on Killed by me:   ctx.Propose(Proposal.AddVar(VarTarget.OfAbility(self, this), 0, 1, "kill"));
// on VarChanged:     ctx.Propose(Proposal.ChangeDef(self, nextDef, "evolve"));
```

Deltas from several sources in one wave add up. Variables are hashed and undone automatically. Override `InitialVars()` for starting values (e.g. charges).

## Recipe 6: a tile

Plain movement rules are flags on `TileDef`; behaviour comes from the same hooks as abilities, with `ctx.SelfSquare` as the tile's square.

| Flag | Meaning | Example |
|---|---|---|
| `blocksPassage` | sliders stop here (may land) | Wall |
| `blocksLanding` | no piece may end a move here | pit |
| `blocksCapture` | the occupant can't be captured by a move | Fog |

`WallTile` (trigger: breaks when landed on) and `GuardianTile` (prevent modifier: cancels one capture) show the two hook styles.

Tiles with variables (timers) set their starting values with `InitialVars()`; they are used whenever a tile is placed without explicit values (map files, the sandbox, `ChangeTile` with no vars). See `StatusTile` (Fire, Freeze).

## Recipe 6b: a cast (an action that isn't a move)

A caster places a tile instead of moving. Most casters are data: `new CasterAbility(id, name, tile, range, tileVars...)` (see `CasterAbility.FireMage`). Casts may only target empty plain floor (`CasterAbility.CanTarget`, decision B11), never a square the piece could move to.

Under the hood, an ability's `ModifyMoves` adds `Move(from, target, MoveFlags.Cast, Aux: abilityIndex)` entries, and the engine asks that ability's `ProposeAction` for the wave-0 proposals (proposing nothing rejects the action). Because the move names its ability by index, a piece can carry several casts. Notation: `c1e3@fire-mage`. The AI searches casts after all other moves.

## Recipe 7: a board effect or condition

A board effect is a condition plus an effect (`Content/BoardEffects/`), wrapped in a `BoardEffectDef` global.
At the end of every turn (both sides' turns count; turn = `Ply + 1`, as in the prototype) the def asks its
`BoardCondition.IsMet(board, turn, timesFired)`. If the condition is met, it calls `BoardEffect.Apply(ctx, timesFired)`,
which proposes changes like any trigger, and adds 1 to the global's "times fired" variable. Conditions and effects keep
no state: anything they need to remember must come from `timesFired` (TurnDelay fires once, BoardValue's
`maxTriggers`, CheckerboardWipe's alternating colour) or from the board.

- New condition: subclass `BoardCondition` in `Conditions.cs` and add a case to `BoardEffectJson.ParseCondition`.
- New effect: subclass `BoardEffect` in `Effects.cs`, add a case to `BoardEffectJson.ParseEffect`, and register its type in `AddBoardEffects`.
- Random choices use `ctx.Random`, the game's seeded generator, so they are reproducible and undo cleanly.
- To freeze pieces, propose `Proposal.AddAbility(piece, FrozenAbility.Instance, cause, turns)`. Re-freezing keeps the longer duration.
- Watch out: Frozen is worth −2 board value, and a side whose pieces are all frozen has no legal moves, which ends the game as a draw.

Map format (same as the prototype):

```json
"boardEffects": [{ "name": "Periodic Wind Gust",
  "condition": { "type": "TurnInterval", "params": { "interval": 4, "startTurn": 4 } },
  "effect":    { "type": "WindGust", "params": { "targetPlayer": "White", "freezeDuration": 2 } } }]
```

| Condition | Params (defaults) |
|---|---|
| `TurnInterval` | `interval` (1), `startTurn` (= interval) |
| `TurnDelay` | `turnNumber` (1): fires once |
| `BoardValue` | `color` ("white"), `threshold` (10), `maxTriggers` (-1 = unlimited): fires while that side's non-royal value is below the threshold |

| Effect | Params (defaults) |
|---|---|
| `WallsPlacement` | `wallCount` (1): random empty, non-wall squares become walls |
| `WindGust` | `targetPlayer` ("white"), `freezeDuration` (2): in each file, that side's highest piece moves down a rank; if blocked, it and the blocker freeze |
| `CheckerboardWipe` | `targetColor` ("white" = x+y even), `freezeDuration` (2), `alternating` (false): freezes every unfrozen piece on that colour |

## Recipe 8: a relic, a consumable, the clock

**Relics** (`Content/Relics/`) subclass `RelicDef`, a `GlobalDef` with an `Owner`; build one instance per side
(`TimeBankRelic.For(side)`, id `"time-bank:white"`) and add it with `state.AddGlobal(def)`. They are ordinary global
triggers, so they run in AI search, and any state goes in global variables. Register with
`.AddRelic("time-bank", TimeBankRelic.For, "TimeBankRelic")`.

**Consumables** (`Content/Consumables/`) subclass `ConsumableDef`. One global per kind holds both sides' charges
(`ConsumableDef.ChargesVar(side)`); give them with `def.Give(state, white, black)`. Override `Use(board, owner, target, into)`
to propose the effect (return false for an invalid use; the engine spends the charge) and, for targeted ones,
`GenerateTargets` to offer only useful targets (each becomes a `Move.UseConsumable(globalIndex, target)` the AI also
searches). A use is a free action: the same side acts again. Grant extra turns with
`Proposal.AddVar(VarTarget.OfSide(side), VarTarget.ExtraTurnsSlot, 1, cause)`. `Value` tells the AI what an unused
charge is worth. Register with `.AddConsumable(def, "PrototypeId")`.

**Clock.** Content proposes `Proposal.Clock(side, deltaMs, cause)` (ChronoCapture, TimeBank). The engine sums them into
`GameState.ClockMs(side)` (undone with the state, not hashed), and `Game.PlayTurn` reports the turn's change in
`TurnResult.ClockDeltaMs(side)`. The host owns the real-time countdown: subtract elapsed time, add the reported deltas.

Maps may list them (an extension; the prototype kept them in the run state):

```json
"relics": [{ "id": "TimeBankRelic", "owner": "White" }],
"consumables": [{ "id": "FortifyPotion", "owner": "White", "charges": 1 }]
```

| Content | Behaviour |
|---|---|
| `ChronoCapture` (ability) | +5 s to its side for every enemy kill credited to the piece (captures, ranged, its own explosion) |
| `TimeBankRelic` | +3 s to the owner after each of its own piece moves |
| `RoyalPromotionRelic` | an owner's non-royal, non-pawn piece reaching its last rank becomes a Queen (abilities kept) |
| `ExtraTurnPotion` | free action: bank one extra turn |
| `FortifyPotion` | free action: every empty, non-wall cell in the target's row becomes a wall |

## Testing a new piece

1. **Unit test** in `ContentTests.cs`: set up with FEN (`Fen.Parse(fen, RuleSet.Rogue)`), `GrantAbility` / `SetTile`, then `Play(s, "e2e4")` and assert on the board. Include at least one interaction with existing content.
2. **Scenario** in `tools/make_scenarios.py`: a position whose description says what should happen, then run `python tools/make_scenarios.py`. Add an assertion to `SandboxScenarioTests.cs` so the description stays true.
3. **Playtest** in the sandbox (`sandbox/Sandbox.tscn`, the main scene): load the scenario, play it, read the wave markers and trace. The Place, Tile and Ability tools let you set up interactions without editing files.

The random-game tests (`Random_games_with_all_content_...`) also check that the hash and undo stay exact with your content in play. Add your content to that setup.
