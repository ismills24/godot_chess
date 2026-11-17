# Resolution Rules

How a turn is resolved. This document is the contract that content (pieces, abilities, tiles, board effects, relics) is written against. The engine and its tests implement exactly what's written here. If the code and this document disagree, the code has a bug.

Status: **draft for M2**. Contract version 1 (it becomes `EngineInfo.ResolutionContractVersion = 1` when implemented).

---

## 1. Goals

1. **Reproducible.** The same state, actions, and seed always give the same result, on any machine.
2. **Predictable.** A player or designer can work out the outcome from the rules on the page, without knowing iteration order, priorities, or internals.
3. **Composable.** Adding a new piece doesn't change how existing pieces interact with each other.
4. **Cheap and undoable.** The AI resolves hundreds of thousands of turns per decision. Everything goes through M1's logged operations, so any resolution can be rolled back exactly.

The TypeScript engine applied one event at a time and let the next listener see a half-updated board. That's reproducible, but not predictable: the outcome depended on which trigger happened to run first. This engine works like a **PLC scan cycle / synchronous logic**: every rule reads the same frozen snapshot, all outputs commit together, and repeat until nothing changes.

---

## 2. Vocabulary

| Term | Meaning |
|---|---|
| **Snapshot** | The game state at the start of a wave. Read-only for everything during the wave. |
| **Proposal** | Something content wants to happen: destroy a piece, move a piece, change a tile, and so on (§6). Proposals are data, not mutations. |
| **Wave** | One scan: gather proposals → modify → resolve conflicts → commit → detect facts. |
| **Fact** | Something that *did* happen in a committed wave, e.g. a piece died or a piece entered a square. Facts are what triggers react to. |
| **Trigger** | A content hook that reacts to facts by returning proposals for the next wave. |
| **Modifier** | A content hook that rewrites proposals in the current wave before they commit (§5.2). |
| **Resolution** | Waves repeated from one starting input until a wave proposes nothing. |
| **Canonical order** | The engine's single fixed ordering rule for "who goes first" and "who wins a tie" (§8). |

---

## 3. Two clocks

**Fast clock: waves.** Logical steps inside one resolution. A resolution runs until it settles, never for a fixed count, so a chain reaction always finishes within the turn. Waves have no wall-clock duration: the Godot layer decides how long each wave takes on screen (one wave = one animation beat), and the AI doesn't animate at all.

**Slow clock: turns.** Each turn runs three resolutions in order:

1. **Turn start** — the starting input is the fact `TurnStarted(side)`.
2. **Action** — the starting input is the player's chosen action converted to proposals (§7).
3. **Turn end** — the starting input is the fact `TurnEnded(side)`.

Then the engine passes the turn to the other side. Timers (`turnsLeft` on Frozen, OnFire, Fire tiles, interval board effects) tick on the slow clock by reacting to `TurnStarted` / `TurnEnded`, the same way a PLC's timer instructions work.

Each resolution settles completely before the next one starts.

---

## 4. Inputs and purity

- **The only inputs are** the starting state, the player's actions, and the RNG seed held in the state. Replaying those three reproduces a game exactly, so a bug report is "state + seed + actions".
- **Content hooks are pure functions.** They receive a read-only view of the snapshot plus the facts or proposals they're asked about, and return proposals. They can't mutate state; M1's mutation operations are internal to the engine.
- **No hidden state.** Content classes hold no mutable fields or statics. Per-piece and per-tile values (charges, timers) live in the game state as variables and change only through proposals.
- **Integers only.** No floating point in the engine.
- **Randomness** comes from the state's seeded RNG via the hook context. Draws happen in canonical order, so the sequence is fixed.

Enforced by tests: resolving the same turn from two cloned states must give identical traces and identical hashes after every wave.

---

## 5. The wave

Every wave runs the same five phases. Nothing reads anything written within the same wave.

### 5.1 Gather

Each trigger subscribed to a fact from the previous wave is called with the **snapshot** and those facts, and returns proposals. Wave 0 of a resolution uses the starting input from §3. Hooks are called in canonical order. That only affects RNG draws and the order shown in the trace, never the outcome.

### 5.2 Modify

Modifiers run in two fixed **layers**. Content declares which layer a modifier belongs to; there are no numeric priorities.

1. **Redirect** — changes *what* happens. Examples: Piercing turns a capture into a jump past the target; Bouncer pushes the target instead of removing it.
2. **Prevent** — stops something happening, optionally replacing it with a cost. Examples: Guardian tile cancels the destroy and consumes itself; Fog cancels a capture; Scapegoat cancels a destroy and destroys itself instead.

Within a layer:
- Every modifier sees **the batch as it was when the layer started**. Modifiers in the same layer don't see each other's rewrites.
- A proposal can be rewritten by **at most one** modifier per layer. If several claim the same proposal, the winner is chosen by canonical order (§8). The losers' rewrites of that proposal are dropped.
- Proposals that a modifier *adds* (e.g. Scapegoat's self-destroy) go into the batch for the next layer and are never re-modified by the layer that created them.

### 5.3 Resolve conflicts

After modification, the engine merges the batch with fixed rules (§6). Content never decides these. Every conflict outcome is recorded as a fact (e.g. `Collided`), so content can react to it in the next wave.

### 5.4 Commit

The surviving proposals are applied through M1's logged operations, in this fixed category order:

1. removals (destroy)
2. moves
3. spawns
4. piece changes (def, add or remove ability)
5. tile changes
6. variable changes
7. clock changes

After conflict resolution, no two proposals in the same category touch the same thing, so order within a category can't matter. A wave either commits completely or not at all.

### 5.5 Detect

The engine compares the snapshot with the committed state and emits facts: `PieceDied`, `PieceMoved`, `PieceEntered`, `PieceLeft`, `Collided`, `TileChanged`, `PieceChanged`, `VarChanged`, `Killed`.

**Last-known information:** facts about a piece that left the board carry its def, abilities, owner, and square as they were in the snapshot. A dead piece's own death triggers ("when I die…") fire in the next wave using that information. A dead piece can't do anything else.

If any facts were produced, the next wave starts. Otherwise the resolution is over.

---

## 6. Proposals and conflict rules

| Proposal | Merge / conflict rule |
|---|---|
| `Destroy(piece, source, cause)` | **Set semantics.** Several destroys of the same piece merge into one death. Every distinct source gets a `Killed(source, victim)` fact, so each one gets credit (ChronoCapture, Predator). |
| `Move(piece, to, cause)` | Resolved **like sliding blocks**, against the squares as they will be after this wave's removals: a move succeeds if its destination is empty, or its occupant is being destroyed or successfully moving away this wave. Two moves to the same square: both blocked. A move into a piece that stays put: blocked. Head-on swaps (A→B's square while B→A's square): blocked. Rotations of 3 or more pieces: allowed. Each blocked move produces `Collided(mover, blocker or other mover)`. A piece that is both destroyed and moved: the destroy wins and the move is dropped. |
| `Spawn(def, side, square)` | Allowed only if the square is empty after moves. Two spawns on one square: canonical order picks one; the rest are dropped. |
| `ChangePiece(piece, …)` (def swap, add or remove ability) | Applied only if the piece survives the wave. Adding the same status twice merges, keeping the larger duration. Add and remove of the same status in one wave: remove wins. Two def swaps: canonical order picks one. |
| `ChangeTile(square, def)` | Two different changes to one square: canonical order picks one. |
| `AddVar(target, name, delta)` | **Deltas sum**, so the result doesn't depend on order. Counters such as charges and timers always use deltas. |
| `SetVar(target, name, value)` | Two different values for one variable: canonical order picks one. Prefer `AddVar` wherever possible. |
| `Clock(side, deltaMs)` | Deltas sum. |

---

## 7. The player's action (wave 0)

An action becomes ordinary proposals. There are no special compound events.

| Action | Wave-0 proposals |
|---|---|
| Move to an empty square | `Move(mover, to)` |
| Capture | `Destroy(target, source = mover, cause = capture)` + `Move(mover, to)`. The move succeeds because the occupant is destroyed in the same wave (§6). |
| Ranged attack (e.g. Marksman) | `Destroy(target, source = attacker, cause = ranged)` + `AddVar(attacker, charges, -1)` |
| Cast (e.g. FireMage) | `ChangeTile(target, def)` (+ cost vars) |
| Use consumable | Whatever the consumable's definition returns |

Ranged attacks and melee captures are **separate actions**, so the player always chooses between them. This fixes the TS Marksman bug.

Worked consequence: if a Guardian tile prevents the capture's `Destroy`, the target stays, the attacker's `Move` hits a piece that stays put, and it's blocked (`Collided`). The attacker stays where it was and the turn proceeds. No special-case code is needed.

---

## 8. Canonical order

There is **one** ordering rule, used everywhere a single winner is needed (claims within a modifier layer, conflicting spawns, tiles, def swaps, `SetVar`) and to order hook calls:

1. **Source category:** rules < global effects and relics < tiles < pieces.
2. **Distance** from the source to the contested square (Chebyshev, i.e. king-move distance). Closer wins.
3. **Clockwise from north**, where north = toward higher ranks (Black's side): N, NE, E, SE, S, SW, W, NW, and at equal angle the nearer one.
4. **Piece id** (only for identical position, which shouldn't happen).

Rules 1–3 are visible on the board, so players can learn them. Hook calls with no contested square use square order, then id.

---

## 9. Termination and watchdog

- **Wave cap:** 64 waves per resolution. **Proposal cap:** 1024 per wave.
- **Oscillation check:** the state hash after each wave is remembered for that resolution. A repeat means the resolution is looping.
- **Fire-once rule:** a given trigger on a given source reacts at most once per fact per wave.
- **Cap or loop hit:** the whole resolution is rolled back to its start. An *action* resolution then rejects the action as illegal. A turn-start or turn-end resolution is skipped and the event is logged.

---

## 10. Trace

Every resolution can produce a trace (it's off in AI search for speed). For each wave it records:
- the proposals as gathered, with their source;
- each modification, with the modifier and layer;
- every conflict and its outcome;
- the facts detected.

The sandbox shows it, and the Godot layer animates from it: one wave per beat, everything in a wave at once.

---

## 11. Worked examples

**Exploding chain.** E1 is captured. Wave 0: `Destroy(E1)` + `Move(attacker → E1's square)` commit. Wave 1: E1's death trigger proposes `Destroy` for everything within radius 1, including the attacker now on its square. All commit together. Wave 2: any Exploding piece killed in wave 1 detonates. The blast spreads in rings, one ring per wave, and doesn't depend on which explosion was processed first.

**Exploding + Scapegoat (the case that broke sequential resolution).** In one wave, E1's blast proposes `Destroy(F)` and E2's blast proposes `Destroy(S)`. S is a Scapegoat adjacent to F. In the prevent layer, S's modifier reads the snapshot, where S and F are both alive, and claims `Destroy(F)`: it cancels it and adds `Destroy(S)`. Merging gives one death for S, and F lives. **Same result whatever the processing order.** (This uses the default in Q1 below.)

**Wind gust.** The effect proposes one `Move` per column, all in one wave. Moves into stationary pieces are blocked and produce `Collided`. The board effect's own trigger reacts to `Collided` next wave by freezing both pieces. Collision handling lives in the engine, not in the effect.

**Piercing.** A capture proposes `Destroy(T)` + `Move(P → T's square)`. In the redirect layer Piercing claims both and rewrites them to `Move(P → square beyond T)` plus `Destroy(whatever stands there)`, if anything. T is untouched.

---

## 12. Mapping the TS content onto this model

| Content | Hook kind | Layer / trigger |
|---|---|---|
| Exploding, Revenant | Trigger | `PieceDied(self)` |
| ChronoCapture, Predator | Trigger | `Killed(source = self)` |
| Scapegoat, Guardian, Fog | Modifier | Prevent |
| Piercing, Bouncer | Modifier | Redirect (own capture proposals only) |
| Fire, Freeze tiles | Trigger | `PieceEntered(this square)`, `TurnEnded` |
| Slippery tile | Trigger | `PieceEntered` → `Move` one more step in the direction travelled (direction rule to settle in M3) |
| Wall tile | Trigger | `PieceEntered` → `ChangeTile(standard)` |
| Tomb tile | Trigger | `PieceEntered` by an ally → `Spawn` the entombed piece |
| OnFire, Frozen | Trigger | `TurnEnded` → `AddVar(turnsLeft, -1)`, then remove or destroy at 0 |
| WindGust, CheckerboardWipe, WallsPlacement | Trigger (global) | `TurnEnded` with condition variables in state |
| Pawn promotion, RoyalPromotion relic | Trigger | `PieceEntered(last rank)` → `ChangePiece(def)` |

---

## 13. Open questions (defaults in bold, used until you decide)

1. **Can a piece being destroyed this wave still protect a neighbour?** **Yes**: modifiers read the snapshot, so a Scapegoat that dies anyway still sacrifices for its ally.
2. **Head-on swaps:** **blocked**, while rotations of 3 or more are allowed. Alternative: block all cycles, or allow all of them.
3. **Kill credit when several sources destroy one piece:** **every source gets credit.** Alternative: only the canonical-order winner.
4. **Tie-break visibility:** is "closest, then clockwise from north" learnable enough, or should ties favour the side to move?
5. **Guardian outcome:** the attacker is **blocked and stays put** (falls out of §7 naturally). The TS version also "ended the turn", which is the same thing here.

---

## 14. Test plan for M2

- Determinism: same input on two clones gives the same trace and the same per-wave hashes.
- Order independence: for each multi-source example in §11, shuffling hook call order gives an identical result (a test-only switch).
- Each conflict rule in §6 has a direct test (merge, collision, head-on, rotation, destroy-beats-move, delta sums).
- Watchdog: a deliberately oscillating test trigger is rolled back and its action rejected. Hitting the wave cap rolls back.
- Undo: a resolution followed by `UndoTo(mark)` restores the exact state and hash.
- Perft is unchanged when a standard game runs through the resolver (the resolver must add no behaviour for plain chess).
