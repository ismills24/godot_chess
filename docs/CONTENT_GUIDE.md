# Content Guide

How to add pieces, abilities and tiles. Read [RESOLUTION_RULES.md](RESOLUTION_RULES.md) for *why* things resolve the way they do; this guide is the *how*.

## Where things live

| What | Where |
|---|---|
| Piece types | `src/RogueChess.Engine/Content/StandardPieces.cs` (or a new file next to it) |
| Abilities | `src/RogueChess.Engine/Content/Abilities/` |
| Tiles | `src/RogueChess.Engine/Content/Tiles/` |
| Registration (map ids, sandbox palette) | `src/RogueChess.Engine/Content/ContentLibrary.cs` |
| Tests | `tests/RogueChess.Engine.Tests/ContentTests.cs` |
| Sandbox scenarios | `tools/make_scenarios.py` → `maps/sandbox/*.json` |

Every new def needs one line in `ContentLibrary.CreateCatalog()`. That makes it loadable from map files and shows it in the sandbox palette.

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

## Testing a new piece

1. **Unit test** in `ContentTests.cs`: set up with FEN (`Fen.Parse(fen, RuleSet.Rogue)`), `GrantAbility` / `SetTile`, then `Play(s, "e2e4")` and assert on the board. Include at least one interaction with existing content.
2. **Scenario** in `tools/make_scenarios.py`: a position whose description says what should happen, then run `python tools/make_scenarios.py`. Add an assertion to `SandboxScenarioTests.cs` so the description stays true.
3. **Playtest** in the sandbox (`sandbox/Sandbox.tscn`, the main scene): load the scenario, play it, read the wave markers and trace. The Place, Tile and Ability tools let you set up interactions without editing files.

The random-game tests (`Random_games_with_all_content_...`) also check that the hash and undo stay exact with your content in play. Add your content to that setup.
