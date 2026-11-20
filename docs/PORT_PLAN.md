# Godot Port Plan: Engine Core

Scope for this phase: the **rules engine** (state, move generation, turn resolution), the **content model** (pieces, abilities, tiles, board effects, relics, consumables), the **AI**, and a **headless test sandbox**. No roguelike run flow, shop, overworld, 3D board, or particles yet.

Source reviewed: `GigaChess/rogue-chess` @ `164be05` (branch `overworld-poc`). This is newer than `ismills24/chess` main (`8a88e44`). It adds FireMage, FreezeMage, ChronoCapture, board effects, relics, consumables, and the clock, so we port from it.

---

## 0. Design direction (2026-11-19)

- **Small boards, short battles.** Battles are not full chess games. The target feel is WarioWare: a few good moves decide a battle. Full 8x8 armies with content are too much to reason about, because the chain reactions of one move become unreadable. The prototype's roguelike mode is the reference: **6x6 boards** and small value-budgeted armies.
- **Test balance** (from the prototype, for testing, not final): the player gets a king plus 10 points (at least 2 pawns), with one starting ability on its most valuable piece. Enemies get 8 / 10+1 / 16+4 points (pieces + abilities). The sandbox generates these from `encounters/prototype.json` (Encounters row, Reroll for a new seed).
- **What this means for the engine:** reactions must stay readable on a crowded small board (the wave markers and trace matter), and the AI must decide fast (depth 5 takes about 0.15 s on 6x6).
- **Open: battle length.** AI-vs-AI encounter battles currently average about 55 plies, and a quarter don't finish within 120. The prototype had a 3-minute chess clock (+30 s per win) that forced decisions. A turn limit, a clock, a shrinking board or a killer-instinct AI are all options. This is a design decision, not an engine one.

## 1. What's wrong today

Most of the bugs come from the architecture rather than from individual pieces. The port fixes the causes listed below instead of patching each symptom.

### 1.1 Architectural problems

| # | Problem | Where | Consequence |
|---|---|---|---|
| A1 | **Listeners are collected once, from the initial state**, then reused for the whole resolution | `ChessEngine.resolveMove` → `EventQueue.process` | Pieces or tiles placed or transformed mid-resolution don't react. Removed pieces still react. Listeners read stale fields (e.g. `FireTile.turnsRemaining`, `FrozenAbility.turnsRemaining`). |
| A2 | **Listeners mutate their own fields** (`Marksman.rangedAttacksLeft`, `Exploding.pendingExplosions`) | abilities | The mutation lands on the *pre-move* `GameState` object held in `ChessManager` history, so undo/redo restores a Marksman with a spent charge. The new state is correct only by luck of clone timing. |
| A3 | **State lives outside `GameState`**: board-effect conditions (`TurnIntervalCondition.lastTriggeredTurn`, etc.), relics, and the clock are external listeners | `BoardEffectListener`, `ChessManager.extraListeners` | AI search and undo can't see or restore them. The AI never even passes them in (see C1). |
| A4 | **Identity depends on decorator order.** `piece.id` is the *outermost* decorator's id. Many abilities test `x.id === this.id` | Exploding, Scapegoat, Revenant, Bouncer, ChronoCapture | An ability only works while it's the outermost layer. **Example:** an Exploding pawn that walks over a FireTile gets wrapped in `OnFire` and no longer explodes. WindGust's `Frozen` wrap has the same effect. |
| A5 | **Capture and Move are separate events** joined by a side channel (`pendingCancellations` plus queue search for "the matching MoveEvent") | `EventQueue.ts` | Fragile. Every capture-replacing ability (Guardian, Scapegoat, Marksman, Piercing, Bouncer) depends on that hack firing correctly. |
| A6 | **Decorator chains need rebuilding** for every change (promotion, Predator, Frozen tick/thaw, RoyalPromotion), with `createAbilityClone` reached via `as any` | Pawn, Predator, Frozen, OnFire, RoyalPromotion | Lots of duplicated, error-prone code to swap a base piece or tick a counter. |
| A7 | **Same-priority tie order is board-scan order** (x-major) | `collectListeners` | Resolution order depends on where pieces stand. The resolution can't be predicted from the rules alone. |
| A8 | **Event overflow returns a half-resolved state** as final | `EventQueue` `MAX_EVENTS_PER_RESOLUTION` | Corrupt positions instead of a rejected action. |
| A9 | **Engine imports catalog** (`ChessEngine` → `Catalog.createTile`). Cast abilities are found by duck-typing `getCastTileType` | `ChessEngine.buildMoveEvents` | Layering cycle, and only one cast ability per piece is supported. |
| A10 | Non-determinism: `crypto.randomUUID()` on every event and piece. No seeded RNG in state | `Event`, `PieceBase`, `AbilityBase` | Replays and AI repro aren't stable. |

### 1.2 Rules and content bugs

| # | Bug | Where |
|---|---|---|
| B1 | `StandardChessRuleSet.getLegalMoves` reads `sq.x`/`sq.y` from `{square, type}` objects. The keys become `"undefined,undefined"`, so **the restriction filter is a no-op** | `rulesets/StandardChess.ts` |
| B2 | `CheckRules` uses `instanceof King` / `instanceof Pawn`. A king carrying *any* ability counts as "no king → in check". Check detection also ignores all abilities and tiles | `rulesets/CheckRules.ts` |
| B3 | ChronoCapture **never triggers on ranged kills**. Marksman tags `DestroyEvent.sourceId` with the base entity id, but ChronoCapture compares against its own ability id | `MarksmanAbility`, `ChronoCaptureAbility` |
| B4 | Marksman's "ranged" option is a duplicate `Move` (same from/to/isCapture), and *every* player capture is converted to ranged while charges remain. The player can't choose melee, and the AI sees duplicate moves | `MarksmanAbility` |
| B5 | Bouncer's doc says the target bounces to "the square behind it", but the code bounces it the attacker's full distance (a rook capturing from 5 away sends the target 5 further) | `BouncerAbility` |
| B6 | SlipperyTile after a knight move slides one *diagonal* step (sign of the L vector). It also ignores walls and other restrictions | `SlipperyTile` |
| B7 | `getJumpMoves` marks moves onto friendly pieces `isCapture = true` | `MovementPatterns` |
| B8 | `Board.getTile` out of bounds silently returns `tiles[0][0]` | `Board.ts` |
| B9 | `GameState.moveHistory` is never appended to | `EventApplier` |
| B10 | Revenant's tomb restores a piece that still has Revenant, so it revives forever. Intended? (see Open Questions) | `RevenantAbility`, `TombTile` |
| B11 | Cast moves (Fire/Freeze/Wall) can overwrite any tile, including Guardian, Tomb, and other casts | `ChessEngine.buildMoveEvents` |

### 1.3 AI bugs

| # | Bug |
|---|---|
| C1 | `simulateTurn` **skips TurnStart/TurnEnd** and passes **no external listeners**. In search, fire never burns, freezes never thaw, and relics and board effects never fire, so the AI plays a different game from the real one. |
| C2 | Terminal scores are `±Infinity`, so there's no preference for faster wins or slower losses. `NEGATIVE_INFINITY / 2` is still `-Infinity`. |
| C3 | The Zobrist hash ignores piece state (charges, timers) and tile state (fire duration), so the transposition table returns wrong hits. The TT stores `Move` objects that belong to other states. |
| C4 | If the first iterative-deepening pass times out, the AI plays a random move. |
| C5 | Evaluation is material only; nothing about hazards, statuses, or threats. Nothing can be tuned per encounter. |

### 1.4 Performance

- Every event clones the entire board, including every tile (`EventApplier.applyEvent`).
- The event log is copied for every event (O(n²)), `queue.shift()` is O(n), and a UUID is generated per event.
- The Zobrist hash is recomputed from scratch with `BigInt` at every node.
- `CheckRules.wouldMovePutKingInCheck` clones the full state for every candidate move. `MoveEvent.isStillValid` recomputes tile restrictions.
- AI runs in a web worker, so every search serialises and deserialises the whole state.

---

## 2. Target architecture

### 2.1 Language: C# for engine, content, and AI (**decided 2026-11-17**)

**Decision: C# (Godot .NET build) for the simulation layer; GDScript is fine for presentation later.**

- Search speed: AI depth is what makes the game feel smart, and C# is typically an order of magnitude faster than GDScript for this kind of tight integer loop. Content hooks run inside the search, so they must be in the same fast language.
- Threads share memory. The AI gets a cheap flat copy of the state on `WorkerThreadPool` or a `Task`, with no serialisation.
- Typed records and interfaces suit the content API. TS-to-C# is a short mental hop.
- Costs: you need the **Godot .NET editor**. Web export of C# projects has historically been unsupported, so check the current Godot release if browser builds matter. Desktop export is fine.

**Alternative: all GDScript.** Simpler toolchain and inline editing in the editor, but expect noticeably shallower search for the same time budget. The architecture below works either way.

### 2.2 State model: plain data, mutable, with undo

```
GameState
  Board        width, height; squares[] -> pieceId|-1; tiles[] -> TileInstance
  Pieces       Dictionary<int, PieceInstance> (dense int ids from a counter in state)
  Globals      relics[], boardEffects[] (with their own state vars), clock delta accumulator
  Turn         sideToMove, turnNumber, phase, extraTurns
  Rng          seeded PRNG state (deterministic)
  Hash         incrementally maintained 64-bit Zobrist
  UndoLog      stack of inverse operations (for make/unmake)

PieceInstance  id, owner, kind: PieceDef, square, movesMade, captures,
               abilities: List<AbilityInstance>, tags (royal, …)
AbilityInstance def: AbilityDef, vars: small int array (charges, turnsLeft, …)
TileInstance   def: TileDef, vars: small int array, ownerId?
```

- **Definitions are stateless; instances hold all state.** `PieceDef`, `AbilityDef`, `TileDef`, `BoardEffectDef`, and `RelicDef` are plain C# classes in the engine library (no Godot dependency), with parameters (range, charges, duration). A thin Godot `Resource` layer in the Godot project can expose those parameters in the inspector later. Mutable values live only in instance `vars` inside `GameState`. This removes A1, A2, A3, and A6.
- **Abilities are a list, not a decorator chain.** The piece's identity is its `id`. "Is this my piece?" is always `ctx.Self.PieceId == e.PieceId`. This removes A4.
- **All writes go through a small set of state ops** (`SetPiece`, `MovePiece`, `RemovePiece`, `SetTile`, `SetVar`, `AddAbility`, `RemoveAbility`, `ChangeKind`, `SetTurn`). Each one updates the hash and pushes its inverse to the undo log. Make/unmake replaces cloning, and history becomes `(action list + periodic snapshots)`.

### 2.3 Actions and move generation

- An `Action` is what a player or the AI chooses: `Move(from, to)`, `Ranged(pieceId, abilityIdx, target)`, `Cast(pieceId, abilityIdx, target)`, `UseConsumable(id, target)`, `Pass`. Every action names its source ability explicitly, which fixes B4, A9, and one-cast-per-piece.
- **Movement primitives are data.** A `PieceDef` lists `Slide(dirs, maxRange)`, `Leap(offsets)`, and `Pawnlike(forward, captureDiagonals, doubleStepIfUnmoved)` entries, with flags (`moveOnly`, `captureOnly`). Most new pieces need no code.
- **Abilities add, modify, or remove actions** through one hook, `ModifyActions(ctx, self, list)` (Phantom, Cannibal, Frozen, casters, Marksman).
- **Tile restrictions are flags on `TileDef`**, not code: `BlocksPassage`, `BlocksLanding`, `BlocksLandingIfOccupied` (Fog), `Capturable` (Wall). A per-square restriction array is updated by `SetTile`. Generation reads it once, so there's no per-event recompute and no special cases in Pawn.

### 2.4 Event resolution: the contract

**Superseded (2026-11-17).** The sequential event queue first sketched here (depth-first reactions, per-event before/after hooks, numeric priorities) was replaced by **simultaneous wave resolution**, modelled on a PLC scan cycle. Every rule reads one frozen snapshot, the engine resolves conflicts with fixed rules, and the batch commits atomically. Waves repeat until nothing changes. Ordering by iteration order or priority numbers is gone.

The full contract lives in [RESOLUTION_RULES.md](RESOLUTION_RULES.md). It still delivers the fixes this section promised: one death fact with correct kill credit (B3); captures as plain proposals with no side-channel cancellation (A5); a live, not stale, view of state each wave (A1); a board-visible canonical order instead of scan order (A7); rollback on overflow (A8); and a per-wave trace.

### 2.5 Turn structure: one code path for game and AI

`Rules.PlayTurn(state, action)` = `TurnStart(side)` → action → `TurnEnd(side)` → advance (or grant an extra turn). The UI, the tests, and the AI search all call this same function (fixes C1). Board effects, relics, and their state are part of `GameState`, so search sees them.

Rulesets are pluggable `RuleSetDef`s. The default is the roguelike "capture the royal piece" (royal is a *tag*, not `instanceof King`, which fixes B2). A "no moving into capture" rule is an optional filter that runs the real engine for the opponent's reply. It's expensive, so it's opt-in per ruleset.

### 2.6 Content authoring API

```csharp
[GlobalClass]
public partial class ExplodingAbility : AbilityDef
{
    [Export] public int Radius = 1;
    public override EventMask Subscribes => EventMask.PieceDied;

    public override void After(Ctx ctx, AbilityRef self, GameEvent e)
    {
        if (e is PieceDied d && d.PieceId == self.PieceId)
            foreach (var sq in ctx.Board.Within(d.Square, Radius))
                if (ctx.Board.PieceAt(sq) is { } victim)
                    ctx.Emit(new Destroy(victim, killer: self.PieceId, cause: "explosion"));
    }
}
```

Hooks available to every def (all optional): `ModifyActions`, `Before`, `After`, `OnTurnStart`, `OnTurnEnd`, `EvalBonus` (lets a piece tell the AI how valuable it is in context), and `Describe` (tooltip text from parameters). Hooks receive a `Ctx` that can read state and `Emit` events. They **cannot write state directly**, which keeps undo, hashing, and traces correct.

### 2.7 AI

- **Search:** iterative-deepening negamax with alpha-beta, make/unmake on the real engine, incremental Zobrist (covering vars, tile state, globals, and side to move), a fixed-size TT keyed on hash that stores compact move encodings, MVV-LVA, killer and history ordering, quiescence on captures and explosions, mate-distance scoring (fixes C2/C3), and an always-complete depth-1 fallback (fixes C4).
- **Branching control:** `AbilityDef.ActionPriority` / pruning hints. Cast moves (up to 24 per mage) are ordered last and can be capped per node by the AI profile.
- **Threading:** search runs on a worker over a copied state with a cancel token. It returns best move, principal variation, score, nodes, and depth.
- **Tunable eval via `AIProfile` resources:** material (def values + abilities + `EvalBonus`), mobility, royal safety, threatened pieces, hazard tiles under own pieces, status effects, and centre/advancement. Personality knobs: depth/time budget, aggression, softmax temperature for move choice, blunder rate. Different encounters get different profiles without code changes.
- **Debug:** "top N moves with scores and PV" for the sandbox, plus a self-play harness for comparing profiles.

### 2.8 Testing and sandbox

- **xUnit** against the pure engine library (same setup as Pig Project), run with `dotnet test` in GitHub Actions. No Godot is needed in CI. Godot-side checks (the sandbox scene) are done through Ziva playtesting or a headless `--quit-after` run.
- **Perft tests** for standard pieces on standard positions. Exact node counts prove move generation correct, and they double as benchmarks.
- **Scenario tests:** a JSON map (compatible with existing `assets/maps/*.json` so all 23 test maps carry over), a list of actions, and assertions on pieces, tiles, vars, and the event trace. Every bug in §1 becomes a scenario that fails on the old semantics.
- **Invariant fuzzing:** random self-play checking that make/unmake restores the hash and state byte-for-byte, that replay is deterministic for a given seed, and that there are no budget overflows.
- **Minimal debug sandbox scene** (2D grid, click to move, event-trace panel, "AI suggest" button). It's thin, but it's how you'll check that new pieces behave predictably.

---

## 3. Repo layout

```
godot_chess/
  project.godot, GodotChess.csproj, GodotChess.sln, Directory.Build.props
  src/RogueChess.Engine/            pure C# library (net8.0, no Godot reference)
    Core/      State, Ops+Undo, Zobrist, Actions, MoveGen, Resolver, Events, Rules
    Content/   Def base classes + Pieces/, Abilities/, Tiles/, BoardEffects/, Relics/, Consumables/
    AI/        Search, TT, Eval, AIProfile
  tests/RogueChess.Engine.Tests/    xUnit: perft, scenarios, fuzz
  maps/                             ported JSON scenario maps
  sandbox/                          Godot debug scene (references the engine library)
  docs/                             PORT_PLAN.md, RESOLUTION_RULES.md, CONTENT_GUIDE.md
```

The engine is a separate project that the Godot assembly references. A test (`Engine_does_not_depend_on_Godot`) enforces that it never references Godot, so it runs headless and in plain unit tests.

---

## 4. Milestones

| M | Deliverable | Done when |
|---|---|---|
| **M0** ✅ | Bootstrap: Godot 4.7 .NET project, engine library, xUnit tests, CI, README | `dotnet test` green; sandbox scene prints from the engine in Godot .NET headless |
| **M1** ✅ | State + ops + undo + Zobrist; standard pieces via movement primitives; standard rules | Perft matches known counts at depth 4 on the start position plus 2 tactical positions; make/unmake fuzz is clean |
| **M2** ✅ | Resolver (§2.4), `PlayTurn`, trace, JSON map importer, scenario runner, `RESOLUTION_RULES.md` | Scenario harness runs; ordering and loop and rollback rules each have a test |
| **M3** | Content port, each item with scenarios (see §5) | All 23 legacy maps load; every §1 bug has a regression scenario; `CONTENT_GUIDE.md` shows adding a piece end to end |
| **M4** ✅ | AI: search, eval, profiles, threading, debug output | Finds mate-in-1/2, avoids hanging the royal piece, respects Exploding/Guardian in tactics; nodes/sec benchmark recorded vs the TS `EfficientAI` on the same positions |
| **M5** | Debug sandbox scene | Load map, play both sides or vs AI, view trace and AI top-N |

M1–M2 are the critical path. M3 can run in parallel per content family once M2's API is frozen.

### M1 as built (2026-11-17)

- **State** (`Core/GameState.cs`): flat `Piece?[]` and `Tile[]` arrays plus a piece dictionary by id. Eight logged operations (spawn, remove, move, moves-made, def, tile, turn, en passant). Each one keeps the hash up to date and is undone with `UndoTo(mark)`. `Clone()` makes a thread-safe copy.
- **Hash** (`Core/Zobrist.cs`): keys are derived by mixing (category, def id hash, side, square), with no tables, so any board size or number of defs works and hashes are stable across runs. The "has moved" flag is hashed only for royal and castling pieces, so move-and-return sequences still transpose.
- **Content** (`Content/`): `PieceDef` (data + movement list + tags), `TileDef` (flags `BlocksPassage` and `BlocksLanding`), and movement primitives `Slide`, `Leap`, `PawnMovement`. The six standard pieces are pure data.
- **Rules** (`Rules/`): `RuleSet.Standard` (check, castling, en passant, under-promotion) and `RuleSet.Rogue` (capture the royal piece, no castling or en passant, auto-queen). Castling works on any board width and is driven by moves-made, not rights bits. `MoveApplier` applies moves directly; M2 wraps it in the event resolver.
- **Verified:** perft matches published counts for the start position (d4), Kiwipete (d4, 4,085,603), positions 3 (d5), 4 and 4-mirrored (d4), and 5 (d3). Random self-play fuzz (4 positions × 40 games × 120 plies) checks the incremental hash against a full recompute after every move and requires an exact state restore after unwinding. 48 tests take about 3 s in Release.
- **Speed baseline:** in the Godot editor (Debug build), the start position perft(4) runs at about 450k nodes/s. Legality is checked by make/unmake plus a scan of every attacker, and there is no piece list yet, so M4 has clear room to optimise.

### Encounters (2026-11-19)

- `Encounters/EncounterGenerator.cs` ports the prototype's `roster.ts` and `boardGeneration.ts`, made deterministic (seeded). It builds value-budgeted armies (king outside the budget, minimum pawns, random affordable pieces, then abilities within an ability budget, plus the player's starting ability on its most valuable piece) and places them: king back-middle, pawns in random columns, other pieces behind their pawn.
- **Prototype bugs fixed:** pieces that found no square behind their pawn, and pawns beyond one per column, were silently dropped from the army.
- `encounters/prototype.json` holds the prototype's balance and per-encounter AI (`casual`, `club`, an inline `boss` profile). Abilities that aren't ported are skipped and listed when the set loads. The prototype's own `ai` field was never read: every battle used EfficientAI at depth 10 / 2 s.
- The sandbox has an Encounters row with Reroll, and headless runs take `-- --encounter=boss-1 --seed=2 --white=club`. 8 encounter tests.

### M4 as built (2026-11-19): AI

- **Injection** (`AI/AgentFactory.cs`): a level names its opponent by profile id or describes it inline (the map's `"ai"` field, e.g. `{ "black": "aggressive" }`). Agent kinds, evaluators and profiles are each registered separately, so a new AI implementation or scoring function plugs in without touching the others. Details in [AI.md](AI.md).
- **Search** (`AI/Search.cs`): alpha-beta with iterative deepening, a Zobrist-keyed transposition table, quiescence on captures, and MVV-LVA / killer / history ordering. Every node is a real `Game.PlayTurn`, so explosions, scapegoats, guardians and future content are understood with no AI code. A decision is reproducible for a given profile, seed and position (the node budget is the limit; wall-clock time is only a safety cap).
- **Profiles** (`AI/AgentProfile.cs`, `ai/profiles.json`): strength (depth, nodes, quiescence) and personality (eval weights, choice margin, root noise, blunder chance, contempt). Built in: random, novice, casual, club, master, aggressive, defensive, greedy, chaotic. Examples of extending them in JSON: berserker, turtle, collector.
- **Sandbox**: a Players row (Human or any profile per side), Pause AI, an "AI move" button (also works as a hint), the AI's reasoning (top moves, score, depth, nodes/s) above each move's trace, undo back to your own turn, and `-- --scenario= --white= --black=` for headless AI-vs-AI runs. New scenario 07 "Versus the AI".
- **Verified:** 18 AI tests: king capture in 1 and 2, checkmate under standard rules, saving its own king, not capturing an Exploding piece next to its own king, using an enemy Exploding piece to kill the king, the Guardian tile without lookahead, determinism with the state left untouched, cancellation, seed variety, no blunder past a forced win, weights changing the evaluation, JSON extends/merge/errors, map players, custom kinds and evaluators, and beating random play 4/4. A headless Godot AI-vs-AI game ran cleanly. 129 tests.
- **Speed:** about 55–110k resolved turns per second in Release (start position, Kiwipete); a club move takes 20–150 ms.
- **Not done:** the comparison against the TS `EfficientAI`, a fast path for positions without hooked content, game-history repetition, and a "hunt the king" weight to convert won positions faster (deferred: a personality trait, not needed for the first release).

### M3 progress (2026-11-17): first content batch + interactive sandbox

- **Ported:** Exploding, Scapegoat, Phantom, Predator abilities; Wall, Guardian, Fog tiles (`Content/Abilities`, `Content/Tiles`, registered in `ContentLibrary`). 8 of 23 legacy maps now load and play. The remaining unknown ids are asserted in `MapLoaderTests` as the checklist.
- **Engine additions:** `TileDef.BlocksCapture` (Fog), `Causes` constants, setup ops `RemoveFromBoard` / `RevokeAbility`, an optional `"rules"` field in map files, and trace lines rendered at resolution time.
- **Sandbox** (`sandbox/`, main scene): scenario list (`maps/sandbox` + loadable legacy maps), hot-seat play, Place/Tile/Ability/Erase editing, Undo/Restart, wave-numbered arrows and markers on the board, and the full trace. Scenarios are generated by `tools/make_scenarios.py`, and `SandboxScenarioTests` checks every description's promised outcome.
- **Playtested through Ziva** (scripted clicks + screenshots): explosion chain with scapegoat, guardian/fog/walls, phantom move preview, the full predator evolution line, building a position in the editor and playing it, and capture + undo in mixed armies. All behaved as specified.
- **Found by playtesting and fixed:** a Scapegoat got kill credit for its own sacrifice (a Predator Scapegoat would have evolved by dying); trace lines named pieces by their current def instead of what they were at the time; and a sandbox description promised a capture a wall made impossible (caught by the scenario test).
- **Docs:** [CONTENT_GUIDE.md](CONTENT_GUIDE.md) shows how to add pieces, abilities and tiles end to end. 107 tests.

### M2 as built (2026-11-17)

- **Resolver** (`Resolution/Resolver.cs`): wave resolution exactly as specified in [RESOLUTION_RULES.md](RESOLUTION_RULES.md). Each wave is gather → redirect → prevent → resolve conflicts → commit → facts. Moves resolve as sliding blocks. Watchdog: 64 waves, 1024 proposals, and an oscillation check on (state hash + facts). Trace is optional.
- **Turn API** (`Resolution/Game.cs`): `Game.PlayTurn` (action → turn end → pass → turn start) and `Game.BeginGame`. `MoveApplier` is gone: plain chess, legality checks and perft all run through the resolver, so there is one code path.
- **Content hooks** (`Content/Behavior.cs`): `PieceDef`, `TileDef`, `AbilityDef` and `GlobalDef` share one `Behavior` base: `Triggers` + `OnFact` for triggers, `Layer` + `Modify` for modifiers. Abilities can also adjust moves with `ModifyMoves`. Hooks see only `IBoardView`, which is read-only.
- **State additions:** ability instances on pieces, variables on abilities, tiles and globals, global effects, a seeded RNG, and the operations put, add/remove ability and set variable. All are hashed and undoable.
- **Maps:** `Catalog` + `MapLoader` read the prototype's JSON format. The 23 legacy maps are in `maps/legacy/`. They all parse; the only problems reported are content ids M3 still has to port (the list is asserted in `MapLoaderTests`). Note: the prototype's "standard chess" map has kings on the d-file and queens on the e-file.
- **Verified:** 79 tests. Every conflict rule; the spec's worked examples (exploding chain rings; Exploding + Scapegoat identical across 21 shuffled hook orders; Guardian); watchdog rollback; last-known-information revive; timers; turn phases; promotion (including blocked promotion); determinism; exact undo; and random games with content keeping the hash correct. Perft is unchanged through the resolver.
- **Cost:** the resolver roughly doubles plain-chess make/unmake cost (Kiwipete perft(4) about 4 s against 2 s in M1, Release). Sources are collected by scanning the board each wave, and every legality check runs a full action resolution. M4 profiling can add a fast path for states with no hooked content.
- **Scenario harness:** scenario tests are fluent C# in the test project (`ResolverTests`), using test-only "probe" behaviors. Data-driven scenario files can come with M3 content if they turn out to be useful.

---

## 5. Content port checklist

| Content | Port notes |
|---|---|
| ✅ Pawn, Knight, Bishop, Rook, Queen, King | Movement primitives. Promotion is a rule trigger (`ChangeDef`, abilities kept). King = `Royal` tag |
| Marksman | Separate `Ranged` action with charges in vars. Melee still available (B4) |
| ✅ Exploding | Trigger on own `Died` → destroy everything within radius 1, including the capturer who moved in |
| ✅ Scapegoat | Prevent modifier: a destroy aimed at an adjacent ally is replaced by its own destroy. No kill credit for its own death |
| Revenant | `After(PieceDied self)` → `SetTile(Tomb{snapshot})`. Decide whether the revived piece keeps Revenant (B10) |
| Piercing / Bouncer | `Before(own MoveEvent with capture)` → rewrite. Fix the Bouncer distance semantics (B5) |
| Cannibal, ✅ Phantom | `ModifyMoves` only |
| ✅ Predator | `Killed` by self → `AddVar(kills)`; `VarChanged` → `ChangeDef` one step per kill |
| ChronoCapture | `After(PieceDied, killer = self)` → `ClockDelta` (fixes B3) |
| FireMage, FreezeMage, WallPlacement | `Cast` actions with range and tile params on the def. Decide which tiles casts may overwrite (B11) |
| OnFire, Frozen | Status abilities with `turnsLeft` var. `OnTurnEnd` → `SetVar` / `RemoveAbility` / `Destroy` |
| ✅ Wall, ✅ Fog, ✅ Guardian, Slippery, Tomb, Fire, Freeze tiles | Restriction flags + hooks, timers in tile vars. Fog = `BlocksCapture` flag; Guardian stops move captures only. Fix the Slippery direction rule (B6) |
| WindGust, CheckerboardWipe, WallsPlacement + Turn/BoardValue conditions | `BoardEffectDef` in `GameState.Globals`, condition state in vars |
| RoyalPromotion, TimeBank relics | `RelicDef` in Globals |
| ExtraTurn, Fortify consumables | `UseConsumable` action. Extra turn via the `Turn.extraTurns` var |
| Clock | Engine emits `ClockDelta`. The real-time clock stays outside the engine for now |

---

## 6. Open questions

1. ~~**Language:** C# or GDScript?~~ **Decided: C#.**
2. ~~**Reaction order:** depth-first or breadth-first?~~ **Decided: neither. Simultaneous wave resolution (PLC-style scan cycle). See [RESOLUTION_RULES.md](RESOLUTION_RULES.md).**
3. ~~**Default win rule:** capture the royal piece or checkmate?~~ **Decided: capture the king** (explosions make checkmate meaningless). No check rule; see [RESOLUTION_RULES.md §7.1](RESOLUTION_RULES.md).
4. **Revenant:** revive once or forever?
5. **Bouncer:** push the target one square or the attacker's full distance?
6. **Pawn diagonal wall capture** (pawns can "capture" walls diagonally today): keep it?
7. **Marksman range:** its normal capture squares (today) or its own range parameter?
8. **Source of truth:** confirm we port from `GigaChess/rogue-chess` `overworld-poc` and not `ismills24/chess` main.
