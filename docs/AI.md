# AI

The AI lives in the engine (`src/RogueChess.Engine/AI/`) and has no Godot dependency. It is built to be injected: a level asks for an opponent by name or by description, and an `AgentFactory` builds it.

## The pieces

| Piece | What it is | Swap it with |
|---|---|---|
| `IChessAgent` | Anything that picks a move: `Decide(state, cancel) → AgentDecision` | `factory.RegisterKind("my-kind", (profile, f) => new MyAgent(profile))` |
| `IEvaluator` | Scores a position for the side to move | `factory.RegisterEvaluator("my-eval", profile => new MyEval(profile.Weights))` |
| `AgentProfile` | Plain data: strength limits plus personality | JSON (`ai/profiles.json`, or inline in a map) or C# `with` expressions |

Built in: the `search` kind (`SearchAgent`), the `random` kind, and the `weighted` evaluator.

## How the search works

`SearchAgent` is a negamax alpha-beta search with iterative deepening. It adds a transposition table keyed by the state's Zobrist hash, capture-only quiescence, and TT, MVV-LVA, killer and history move ordering.

**Every node is a real turn**: `Game.PlayTurn`, then `UndoTo`. The AI therefore sees exactly what the game would do: explosions, scapegoats, guardian tiles and turn-start effects. New content needs **no AI code**. Its material value comes from `PieceDef.Value` / `AbilityDef.Value`, its moves from its movement primitives and `ModifyMoves`, and its consequences from the resolver.

A side loses when it started the search with a royal piece and has none left at the end of a turn (the win rule, [RESOLUTION_RULES §7.1](RESOLUTION_RULES.md)). Under standard rules, checkmate and stalemate also count.

**Determinism.** The same profile in the same position always picks the same move. All randomness is seeded from `profile.Seed` and the position hash, and the search tables are reset for every decision. `NodeBudget` is the reproducible strength limit. `TimeBudgetMs` is only a wall-clock safety cap, and a search it cuts short is not reproducible across machines.

**Threads.** An agent may play moves on the state you pass but restores it before returning. To think in the background, pass `state.Clone()` (as the sandbox does) and cancel with the token. Give each AI player its own agent.

## Profile fields

Strength:

| Field | Default | Meaning |
|---|---|---|
| `maxDepth` | 3 | Plies of full search |
| `nodeBudget` | 300000 | Nodes per decision (0 = unlimited). The reproducible limit |
| `timeBudgetMs` | 3000 | Wall-clock cap (0 = none) |
| `quiescenceDepth` | 6 | Plies of captures followed past the horizon. 0 means the agent misses recaptures |
| `tableBits` | 18 | Transposition table size, 2^n entries |

Personality:

| Field | Meaning |
|---|---|
| `weights.material` / `weights.abilityMaterial` | % of the content's own values (100 = as authored) |
| `weights.mobility`, `center`, `pawnAdvance` | Centipawns per move, per step toward the centre, per rank advanced |
| `weights.threats` | % of an attacked enemy piece's value (biggest threat per attacker) |
| `weights.kingPressure` | Per own piece within two squares of an enemy royal (×10 for attacking it) |
| `weights.kingShelter` | Per own piece next to our royal |
| `weights.tempo` | Bonus for having the move |
| `choiceMargin` | Any move within this many centipawns of the best may be chosen at random |
| `rootNoise` | A fixed random offset per root move, ±centipawns |
| `blunderChance` | Chance of a random move. Never one that loses outright, and never instead of a forced win |
| `contempt` | Positive = avoids draws, negative = happy to draw |
| `seed` | Randomness seed: vary it per run or per level for variety |
| `kind` / `evaluator` | Which registered implementation to use |

## Presets

| Id | Strength | Personality |
|---|---|---|
| `random` | none | Any legal move |
| `novice` | depth 1 | Wide choice margin, 20% blunders |
| `casual` | depth 2 | Some randomness, 5% blunders |
| `club` | depth 3 | Near-best moves |
| `master` | depth 8, 2M nodes | Always the best move it finds |
| `aggressive` | club | King pressure and threats, avoids draws |
| `defensive` | club | King shelter, accepts draws |
| `greedy` | club | Overvalues material and abilities |
| `chaotic` | casual | Lots of noise |

`ai/profiles.json` adds `berserker`, `turtle` and `collector` as examples of extending presets.

## Choosing the AI for a level

A map file names the players in its `"ai"` field:

```json
"ai": { "black": "aggressive" }
"ai": { "black": { "extends": "club", "maxDepth": 2, "weights": { "kingPressure": 40 } } }
"ai": { "white": "human", "black": "turtle" }
```

In code:

```csharp
var factory = new AgentFactory();                 // built-ins + presets
factory.LoadProfiles(File.ReadAllText("ai/profiles.json"));
var (white, black) = factory.ParseMapPlayers(mapJson);
IChessAgent enemy = factory.Create(black!);
AgentDecision d = enemy.Decide(state.Clone(), cancelToken);
Game.PlayTurn(state, d.Move!.Value);
```

Unknown fields, ids, kinds or evaluators throw `AgentConfigException`, so typos in level files fail loudly.

## Debugging

`AgentDecision.Format(state)` gives a summary: the move, why it was picked (`best`, `candidate`, `blunder`...), the score, depth, nodes and speed, the expected line, and the top root moves with their noise. The sandbox shows this above each AI move's trace.

In the sandbox, the Players row picks Human or any profile per side. "Pause AI" stops the AI from moving, and "AI move" plays one move (on a human's turn, Club suggests and plays it). Undo against an AI goes back to your own turn.

Headless AI-vs-AI run:

```
godot --headless --path . --quit-after 3000 -- --scenario=07 --white=club --black=berserker
```

## Speed

Measured in Release by `AiTests.Benchmark_nodes_per_second`. Each node is a full resolved turn with undo.

| Position | Nodes/s |
|---|---|
| Start position | about 55–110k |
| Kiwipete | about 110k |
| Start with content mix | about 300k (smaller trees) |

A depth-3 `club` move typically takes 20–150 ms in the sandbox.

## Known limits / next steps

- **Converting won positions.** At depth 3 the AI builds large leads but can be slow to hunt the king down. A "drive the enemy king" term for the side that is ahead would fix this.
- **No game-history repetition detection.** Only repetitions within the search line are scored as draws.
- **No fast path yet** for positions without hooked content. Every node runs the full resolver.
- **Not yet compared with the TypeScript `EfficientAI`** on the same positions.
