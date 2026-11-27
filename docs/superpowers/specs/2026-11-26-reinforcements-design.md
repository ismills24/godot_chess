# Reinforcements board effect — design

## Intent

A new board effect: when its condition fires, a set of fresh pieces spawns for one side (the beneficiary) inside a
configurable area of the board. The set is random but constrained: it can only contain the pieces and abilities
the beneficiary's **roster** allows. A soldier with pawns, knights and bishops and Marksman + Piercing only ever
gets reinforcements built from those. The size of the set is set by a piece value and an ability value, the same
budgets encounter armies use.

Rosters are handmade JSON configs, one per character, written once and referenced by id from any map. Reuse
existing content and logic as much as possible: the army generator, spawn proposals, the board-effect plumbing.

## Approaches

- **A. Named rosters in their own files (chosen).** `rosters/soldier.json` lists the character's pieces and
  abilities. The effect names the roster by id. One source of truth per character, and encounters can reuse
  rosters later. Costs a small loader and a catalog registry.
- **B. Lists inline in each effect's params.** Nothing new to load, but every map using the soldier repeats its
  lists, and they drift.

The user picked A.

## Roster files

```json
// rosters/soldier.json
{
  "id": "soldier",
  "name": "Soldier",
  "pieces": ["Pawn", "Knight", "Bishop"],
  "abilities": ["Marksman", "Piercing"]
}
```

- Ids resolve through the catalog (`FindPiece` / `FindAbility`, so the prototype aliases work too).
- An unknown piece or ability id, a missing `id`, or an empty `pieces` list throws `FormatException` when the
  roster is loaded. `abilities` may be empty or missing (no abilities).
- The catalog gets `AddRoster(RosterDef)` / `FindRoster(id)`. Roster files are loaded from `rosters/*.json` at the
  same point the catalog is built for play.

## Effect params (map JSON)

```json
"effect": { "type": "Reinforcements", "params": {
  "side": "black",                       // beneficiary: white | black (default white, like other effects)
  "roster": "soldier",                   // roster id (required)
  "pieceValue": 6,                       // piece budget (required, >= 1)
  "abilityValue": 3,                     // ability budget (optional, default 0)
  "squares": ["a7", "b7", "c7", "d7"]    // starting area (required, non-empty), same format as TidalWave
} }
```

Malformed params (unknown roster, bad square names, `pieceValue` < 1, `abilityValue` < 0, no squares) throw
`FormatException`, as the other effects do. Squares off the board are ignored when the effect fires.

## Behaviour

When the condition is met, in one wave:

1. **Generate the set** with `EncounterGenerator.GenerateArmy`, using an `ArmySpec` with `PieceValue` /
   `AbilityValue` from the params, `HasKing = false`, `MinimumPawns = 0`, `Abilities` = the roster's abilities and
   a new `Pieces` allow-list = the roster's pieces. Royal pieces in a roster are never bought (the generator
   already skips them). The seed comes from the trigger context's deterministic random, so replays, undo and
   hashing stay consistent.
2. **Pick squares:** the free squares in the area (no piece, tile doesn't block landing), in random order.
3. **Spawn** each piece with its abilities via `Proposal.Spawn(def, side, square, "reinforcements", abilities)`,
   most valuable first. If the set is bigger than the free area, the cheapest leftovers don't arrive. If no
   square is free, nothing spawns (the effect still counts as fired).

Spawn conflicts with other proposals in the same wave go through the engine's normal rules.

## Changes to existing code

- `ArmySpec.Pieces`: optional piece-id allow-list (null = every buyable piece, today's behaviour), applied to the
  generator's `buyable` list and to the minimum-pawns step (no pawn in the list = no forced pawns).
  `EncounterSet.UnknownAbilities` gets a sibling check for unknown piece ids.
- The `BoardEffectFactory` only receives JSON. Reinforcements needs the catalog to resolve the roster, so
  `AddBoardEffects` registers it with a closure over the catalog. Builder's call if a cleaner seam shows up.

## Out of scope

- Godot visuals or a spawn animation.
- Run integration (rosters that grow with unlocks). Rosters are fixed, handmade files for now.
- Encounters building armies from rosters (possible later, not part of this).

## Deliverables

- `RosterDef` + roster JSON parsing + catalog registry, and an example `rosters/soldier.json`.
- `ArmySpec.Pieces` support in `GenerateArmy`.
- `ReinforcementsEffect` in `Content/BoardEffects/Effects.cs`, registered and parsed in `BoardEffectJson`.
- Tests: only roster pieces/abilities appear, budgets respected, spawns land only on free area squares, overflow
  drops the cheapest, full area spawns nothing, deterministic for the same state, undo/hash consistency, parse
  errors (effect and roster).
- Docs: a row in CONTENT_GUIDE.md's effect table, a roster section, and a worked example in RESOLUTION_RULES.md.
