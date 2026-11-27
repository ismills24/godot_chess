# Tidal Wave board effect — design

## Intent

A new board effect: a tidal wave rolls in from one edge of the board and, as it recedes, drags every piece on the
affected squares toward that edge. Pieces dragged past the edge are washed out of bounds and destroyed. It is
configured per map like the other board effects (condition + effect).

Example: from the north, affecting the top 3 rows, strength 1. Pieces on rank 6 move to rank 7, rank 7 to rank 8,
and pieces on rank 8 are destroyed.

## Parameters (map JSON)

```json
"effect": { "type": "TidalWave", "params": {
  "direction": "north",            // north | south | east | west: the edge the wave comes from (and drags toward)
  "squares":   ["a6", "b6", "..."], // affected squares
  "strength":  1                    // optional, default 1: how many one-square steps
} }
```

North = toward the highest rank (Black's home side), south = toward rank 1, east = toward the h-file, west = toward
the a-file. Malformed params (unknown direction, bad square names, strength < 1, no squares) throw
`FormatException`, as the other effects do.

## Approaches

- **A. N successive one-square steps (chosen).** Each step is its own resolution wave: every piece on an affected
  square is pulled one square toward the edge. Collisions, walls and tile reactions (Slippery, Fire...) go through
  the engine's normal rules on every step, and the Godot layer can animate each step as a beat.
- **B. Single jump to the final square.** The effect computes where each piece ends up and moves it there in one
  wave. Simpler, but the effect would have to resolve collisions itself (which RESOLUTION_RULES.md puts in the
  engine), and pieces would skip the squares in between, including walls.

The user picked A.

## Behaviour

Per step, in one wave:
- Every piece (both sides) currently on an affected square gets `Move` one square toward the edge (cause
  `tidal-wave`).
- A piece whose next square would be off the board gets `Destroy` (cause `tidal-wave`, no killer: nobody is
  credited).
- Walls hold pieces back: the effect proposes no move into a square whose tile blocks passage or landing (the engine
  alone would let the piece onto a wall and break it, so the effect checks this itself, as Slippery does).
- The engine's sliding-block rules resolve the rest: a whole column shifts together because each square is vacated
  in the same wave. A move into a piece that stays put (held by a wall, or not on an affected square) is blocked,
  and the blocked piece just stays where it is (no freeze, unlike Wind Gust).
- Each step reads the board as it is then: a piece that moved onto an affected square is pulled by the next step,
  and a piece that left the area is not.

Strength N runs N steps, one per wave, within the same turn-end resolution.

## How the steps chain (builder's call, sketched)

Board effects currently fire once per `TurnEnded`. Proposal: give `BoardEffect` an optional step count (default 1)
and `BoardEffectDef` a second global variable, "steps left". When the condition is met, the def applies step 1 and
sets steps left to N-1. It also listens to its own `VarChanged`: while steps left > 0, it applies the next step and
decrements. `TimesFired` goes up once per firing, not once per step. All state stays in global variables, so it
is hashed and undone with the game.

## Out of scope

- Visuals or animation in Godot (the resolution log already provides one beat per step).
- Changing tiles (no "flooded" tiles left behind).
- Pieces immune to the wave.

## Deliverables

- `TidalWaveEffect` in `Content/BoardEffects/Effects.cs`, registered and parsed in `BoardEffectJson`.
- Multi-step support in `BoardEffectDef`, with existing effects unchanged (steps = 1).
- Tests: a column shift with the edge piece destroyed, a blocked piece (wall and non-affected piece), all four
  directions, strength 2+, parse errors, and undo/hash consistency.
- Docs: a row in CONTENT_GUIDE.md's effect table and a worked example in RESOLUTION_RULES.md.
