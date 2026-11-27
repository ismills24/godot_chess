# Reinforcements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `Reinforcements` board effect that spawns a random, value-budgeted set of pieces for one side in a
configurable area, drawn only from that side's handmade roster.

**Architecture:** Rosters are JSON files in `rosters/` embedded into the engine assembly and registered in the
catalog by `ContentLibrary.CreateCatalog()`. The effect reuses `EncounterGenerator.GenerateArmy` (with a new piece
allow-list on `ArmySpec`) seeded from the game's deterministic random, and spawns through `Proposal.Spawn`.

**Tech Stack:** C# / .NET 8 engine (`src/RogueChess.Engine`), xUnit (`tests/RogueChess.Engine.Tests`).

**Spec:** `docs/superpowers/specs/2026-11-26-reinforcements-design.md`

## Global Constraints

- Engine must not reference Godot; roster files reach the engine as embedded resources, not via `res://`.
- `TreatWarningsAsErrors` is on in the engine project.
- Malformed effect params and roster files throw `FormatException`.
- Spawn cause string: `"reinforcements"`.
- Effect JSON params: `side` (white|black, default white), `roster` (required), `pieceValue` (required, >= 1),
  `abilityValue` (optional, default 0, >= 0), `squares` (required, non-empty, TidalWave square format).
- Roster JSON: `id` (required), `name` (optional, defaults to id), `pieces` (required, non-empty), `abilities` (optional).
- Generated set: `HasKing = false`, `MinimumPawns = 0`; spawns most valuable first; leftovers that don't fit are dropped.
- Free square = in bounds, no piece, tile does not `BlocksLanding`.
- `ArmySpec.Pieces == null` keeps today's behaviour exactly (existing encounter seeds must not change).

## Review Focus

- Roster whose only pieces are royal or value 0: effect fires, spawns nothing, no exception. (Task 3 test)
- Effect fires twice: second firing gives a set from the same roster, still deterministic per seed. (Task 3 test)
- Undo after a reinforcement turn restores the exact prior state and hash. (Task 3 test + Task 4 fuzz addition)
- Roster ids and piece ids in rosters are case-insensitive like the rest of the catalog. (Task 2 test)
- `pieceValue` far larger than the area: only as many pieces as free squares, the most valuable ones. (Task 3 test)

---

### Task 1: Piece allow-list on `ArmySpec`

**Files:**
- Modify: `src/RogueChess.Engine/Encounters/EncounterSpec.cs` (`ArmySpec`, `EncounterSet.UnknownAbilities` sibling)
- Modify: `src/RogueChess.Engine/Encounters/EncounterGenerator.cs` (`GenerateArmy`)
- Test: `tests/RogueChess.Engine.Tests/EncounterTests.cs`

**Interfaces:**
- Produces: `ArmySpec.Pieces : IReadOnlyList<string>?` (piece ids/aliases; null = all buyable pieces).
  `EncounterSet.UnknownPieces(Catalog) : IReadOnlyList<string>`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Army_pieces_list_limits_what_the_budget_buys()
{
    var spec = new ArmySpec { PieceValue = 20, HasKing = false, MinimumPawns = 0, Pieces = new[] { "Knight", "pawn" } };
    for (ulong seed = 1; seed <= 20; seed++)
    {
        var army = EncounterGenerator.GenerateArmy(spec, Catalog, seed);
        Assert.NotEmpty(army);
        Assert.All(army, p => Assert.True(p.Def == StandardPieces.Knight || p.Def == StandardPieces.Pawn));
        Assert.True(army.Sum(p => p.Def.Value) <= 20);
    }
}

[Fact]
public void Army_pieces_list_without_a_pawn_forces_no_pawns()
{
    var spec = new ArmySpec { PieceValue = 9, MinimumPawns = 2, HasKing = false, Pieces = new[] { "Knight" } };
    Assert.All(EncounterGenerator.GenerateArmy(spec, Catalog, 3), p => Assert.Equal(StandardPieces.Knight, p.Def));
}

[Fact]
public void Unknown_piece_ids_in_an_encounter_set_are_listed()
{
    var set = EncounterSet.Parse("""{ "player": { "pieces": ["Knight", "Dragon"] }, "encounters": [] }""");
    Assert.Equal(new[] { "Dragon" }, set.UnknownPieces(Catalog));
}
```

Also confirm the existing seed-stability tests in `EncounterTests.cs` still pass unchanged (null `Pieces`).

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/RogueChess.Engine.Tests --filter "FullyQualifiedName~EncounterTests"`
Expected: build error, `ArmySpec` has no `Pieces`.

- [ ] **Step 3: Implement**

`ArmySpec.Pieces` with a doc comment. In `GenerateArmy`, when `spec.Pieces != null`, resolve ids with
`catalog.FindPiece` (drop unknowns, `Distinct`), filter `buyable` to that set, and skip the minimum-pawns step
when the set has no pawn (use the set's pawn otherwise). Leave the null path byte-for-byte identical in rng use.
`UnknownPieces` mirrors `UnknownAbilities` over `Player` and every `Enemy`.

- [ ] **Step 4: Run to verify they pass**

Run: `dotnet test tests/RogueChess.Engine.Tests --filter "FullyQualifiedName~EncounterTests"`
Expected: all pass.

- [ ] **Step 5: Commit** — `git commit -m "Let army specs limit which pieces the budget buys"`

---

### Task 2: Rosters

**Files:**
- Create: `src/RogueChess.Engine/Content/RosterDef.cs` (`RosterDef` + `RosterJson`)
- Create: `rosters/soldier.json`
- Modify: `src/RogueChess.Engine/RogueChess.Engine.csproj` (embed `../../rosters/*.json`)
- Modify: `src/RogueChess.Engine/Content/Catalog.cs` (`_rosters`, `AddRoster`, `FindRoster`, `Rosters`)
- Modify: `src/RogueChess.Engine/Content/ContentLibrary.cs` (load embedded rosters after abilities are registered)
- Test: `tests/RogueChess.Engine.Tests/ReinforcementsTests.cs` (new; roster section)

**Interfaces:**
- Produces:
  - `public sealed record RosterDef(string Id, string Name, IReadOnlyList<PieceDef> Pieces, IReadOnlyList<AbilityDef> Abilities)`
  - `public static class RosterJson { public static RosterDef Parse(string json, Catalog catalog); public static Catalog AddEmbeddedRosters(this Catalog catalog); }`
  - `Catalog.AddRoster(RosterDef def) : Catalog`, `Catalog.FindRoster(string id) : RosterDef?` (case-insensitive), `Catalog.Rosters : IEnumerable<RosterDef>`

`rosters/soldier.json`:
```json
{ "id": "soldier", "name": "Soldier", "pieces": ["Pawn", "Knight", "Bishop"], "abilities": ["Marksman", "Piercing"] }
```

csproj item: `<EmbeddedResource Include="../../rosters/*.json" LogicalName="rosters/%(Filename)%(Extension)" />`.
`AddEmbeddedRosters` reads every manifest resource whose name starts with `rosters/` and ends with `.json`, in
ordinal name order; a bad embedded roster throws (it is shipped content, so it should fail loudly in tests).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Soldier_roster_ships_with_the_content_library()
{
    var r = ContentLibrary.CreateCatalog().FindRoster("SOLDIER");
    Assert.NotNull(r);
    Assert.Equal(new[] { StandardPieces.Pawn, StandardPieces.Knight, StandardPieces.Bishop }, r!.Pieces);
    Assert.Equal(new AbilityDef[] { MarksmanAbility.Instance, PiercingAbility.Instance }, r.Abilities);
}

[Fact]
public void Roster_resolves_ids_case_insensitively_and_defaults_its_name()
{
    var r = RosterJson.Parse("""{ "id": "scout", "pieces": ["knight"] }""", ContentLibrary.CreateCatalog());
    Assert.Equal("scout", r.Name);
    Assert.Equal(new[] { StandardPieces.Knight }, r.Pieces);
    Assert.Empty(r.Abilities);
}

[Theory]
[InlineData("""{ "pieces": ["Knight"] }""")]                            // no id
[InlineData("""{ "id": "x" }""")]                                       // no pieces
[InlineData("""{ "id": "x", "pieces": [] }""")]                         // empty pieces
[InlineData("""{ "id": "x", "pieces": ["Dragon"] }""")]                 // unknown piece
[InlineData("""{ "id": "x", "pieces": ["Knight"], "abilities": ["Laser"] }""")] // unknown ability
[InlineData("""{ "id": "x", "pieces": ["Knight"], "colour": "red" }""")] // unknown field
[InlineData("""not json""")]
public void Bad_rosters_throw_format_exceptions(string json)
    => Assert.Throws<FormatException>(() => RosterJson.Parse(json, ContentLibrary.CreateCatalog()));
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/RogueChess.Engine.Tests --filter "FullyQualifiedName~ReinforcementsTests"`
Expected: build error, `RosterJson` not defined.

- [ ] **Step 3: Implement** per the Interfaces block. Parse with `JsonDocument` (comments and trailing commas
allowed, unknown top-level fields rejected), error messages prefixed `roster '<id>':`.

- [ ] **Step 4: Run to verify they pass** (same command). Expected: all pass.

- [ ] **Step 5: Commit** — `git commit -m "Add handmade rosters and the soldier roster"`

---

### Task 3: `ReinforcementsEffect`

**Files:**
- Modify: `src/RogueChess.Engine/Content/BoardEffects/Effects.cs` (append the class)
- Test: `tests/RogueChess.Engine.Tests/ReinforcementsTests.cs` (effect section)

**Interfaces:**
- Consumes: `RosterDef`, `ArmySpec.Pieces`, `EncounterGenerator.GenerateArmy(ArmySpec, Catalog, ulong)`, `Proposal.Spawn`, `TriggerContext.Random(int)`.
- Produces:
  ```csharp
  public sealed class ReinforcementsEffect : BoardEffect
  {
      public const string Cause = "reinforcements";
      public ReinforcementsEffect(Side side, RosterDef roster, int pieceValue, int abilityValue,
          IReadOnlyList<(int X, int Y)> squares, Catalog catalog); // throws ArgumentOutOfRangeException for pieceValue < 1 or abilityValue < 0, ArgumentException for no squares
      public Side Side { get; }  public RosterDef Roster { get; }  public int PieceValue { get; }  public int AbilityValue { get; }
      public IReadOnlyList<(int X, int Y)> Squares { get; } // distinct, sorted by Y then X (as TidalWave)
  }
  ```
  `Key` = `$"Reinforcements({Side}, {Roster.Id}, pieces {PieceValue}, abilities {AbilityValue}, {squares as 'a7 b7'})"`.
  `Describe()` = `$"Sends {Side} reinforcements from the {Roster.Name} roster (piece value {PieceValue}, ability value {AbilityValue}) onto {Squares.Count} square(s)."`

`Apply` algorithm (the order of `ctx.Random` calls is part of determinism; keep it):
1. Seed: `ulong seed = ((ulong)(uint)ctx.Random(int.MaxValue) << 32) | (uint)ctx.Random(int.MaxValue);`
2. `GenerateArmy(new ArmySpec { PieceValue, AbilityValue, HasKing = false, MinimumPawns = 0, Pieces = roster piece ids, Abilities = roster ability ids }, catalog, seed)`.
3. Free squares from `Squares` (in bounds, empty, tile not `BlocksLanding`), shuffled with `ctx.Random` as `WallsPlacementEffect` does.
4. Order the army by `Value` descending, stable on generation order; zip with the free squares; one `Proposal.Spawn(def, Side, sq, Cause, abilities)` each.

Tests use `AddEffect` / `Play` / `Shuffle` helpers copied from `BoardEffectTests` (or move them to a shared internal static class if copying feels wrong; implementer's call). Build effects with a hand-made `RosterDef` and `ContentLibrary.CreateCatalog()`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] Reinforcements_spawn_only_roster_pieces_and_abilities_inside_the_area()
// 8x8, kings a1/h8; roster soldier; Black; pieceValue 8, abilityValue 4; squares a6..h7 (16).
// After Play(s, "a1b1"): every new Black piece is Pawn/Knight/Bishop, on a6..h7, abilities ⊆ {Marksman, Piercing};
// sum of def values <= 8, sum of ability values <= 4, at least one piece spawned.

[Fact] Reinforcements_skip_occupied_and_walled_squares()
// area a6,b6,c6,d6; a White rook on a6, wall on b6; pieceValue 10, roster of Knight only.
// Exactly two knights spawn, on c6 and d6; the rook is untouched.

[Fact] Reinforcements_that_overflow_the_area_keep_the_most_valuable()
// see the note below the test list

[Fact] Reinforcements_with_a_full_area_spawn_nothing_but_still_fire()
// all area squares occupied; after the turn no new pieces, TimesFiredVar == 1.

[Fact] Reinforcements_from_a_royal_only_roster_spawn_nothing()
// roster of King only; fires without exception, no pieces added.

[Fact] Reinforcements_are_reproducible_and_fire_again()
// two states with the same SeedRandom(7) produce identical s.Dump() after the turn; with TurnIntervalCondition(1)
// a second turn adds another batch, all still roster pieces.

[Fact] Reinforcements_undo_exactly()
// record Dump() and Hash, play a turn that spawns, s.UndoTo(0): Dump and Hash equal the start;
// ComputeHashFromScratch() == Hash after the spawn turn.

[Fact] Reinforcements_reject_bad_arguments()
// pieceValue 0 and abilityValue -1 → ArgumentOutOfRangeException; empty squares → ArgumentException.
```

For the overflow test: the set's seed comes from the game's random, so the test can't rebuild it from outside.
Put step 4 in `internal static List<(ArmyPiece Piece, int Square)> Arrange(IReadOnlyList<ArmyPiece> army,
IReadOnlyList<int> freeSquares)` and test it directly: army [Pawn, Bishop, Pawn, Knight(with Piercing)] on 2
squares → Knight+Piercing (value 3 + Piercing's value) on the first square, Bishop on the second. Also run one board
test: roster Pawn + Bishop, pieceValue 20, area 2 squares → exactly 2 spawned.

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/RogueChess.Engine.Tests --filter "FullyQualifiedName~ReinforcementsTests"`
Expected: build error, `ReinforcementsEffect` not defined.

- [ ] **Step 3: Implement** per the Interfaces block.

- [ ] **Step 4: Run to verify they pass** (same command). Expected: all pass.

- [ ] **Step 5: Commit** — `git commit -m "Add the Reinforcements board effect"`

---

### Task 4: Map JSON, fuzz coverage and docs

**Files:**
- Modify: `src/RogueChess.Engine/Content/BoardEffects/BoardEffectJson.cs`
- Modify: `tests/RogueChess.Engine.Tests/BoardEffectTests.cs` (`Random_games_with_board_effects_keep_hash_and_undo_exact`)
- Test: `tests/RogueChess.Engine.Tests/ReinforcementsTests.cs` (map section)
- Modify: `docs/CONTENT_GUIDE.md`, `docs/RESOLUTION_RULES.md`

**Interfaces:**
- Consumes: `ReinforcementsEffect` ctor, `Catalog.FindRoster`.
- Produces: `"Reinforcements"` effect type registered by `AddBoardEffects()`.

`AddBoardEffects` registers `"Reinforcements"` with a closure `entry => Create(entry, catalog)`; add an overload
`Create(JsonElement entry, Catalog catalog)` and `ParseEffect(JsonElement effect, Catalog? catalog)` where
Reinforcements throws `FormatException("Reinforcements needs a catalog")` if the catalog is null. Keep the existing
one-argument `Create`/`ParseEffect` as wrappers passing null. Extract TidalWave's square-list parsing into a shared
`ParseSquares(JsonElement? p, string effectName)` so both use it (messages keep the effect name, e.g.
`"TidalWave: bad square name 'x'"`, `"Reinforcements needs a non-empty list of squares"`).

- [ ] **Step 1: Write the failing tests**

```csharp
private static string ReinforcementsMap(string effectParams) => /* like TidalMap in BoardEffectTests, type "Reinforcements" */;

[Fact]
public void Reinforcements_load_from_a_map()
{
    var s = MapLoader.Load(ReinforcementsMap("""{ "side": "black", "roster": "Soldier", "pieceValue": 6, "abilityValue": 3, "squares": ["b7", "a7"] }"""), ContentLibrary.CreateCatalog());
    Assert.Equal("board-effect:Reinforcements(Black, soldier, pieces 6, abilities 3, a7 b7) when TurnInterval(every 2 from 2)", Assert.Single(s.Globals).Def.Id);
}

[Fact]
public void Reinforcements_default_to_white_and_no_abilities() // { "roster": "soldier", "pieceValue": 2, "squares": ["a3"] } → Side White, AbilityValue 0

[Theory]
[InlineData("""{ "pieceValue": 3, "squares": ["a7"] }""")]                                    // no roster
[InlineData("""{ "roster": "wizard", "pieceValue": 3, "squares": ["a7"] }""")]                // unknown roster
[InlineData("""{ "roster": "soldier", "squares": ["a7"] }""")]                                 // no pieceValue
[InlineData("""{ "roster": "soldier", "pieceValue": 0, "squares": ["a7"] }""")]
[InlineData("""{ "roster": "soldier", "pieceValue": 3, "abilityValue": -1, "squares": ["a7"] }""")]
[InlineData("""{ "roster": "soldier", "pieceValue": 3 }""")]
[InlineData("""{ "roster": "soldier", "pieceValue": 3, "squares": [] }""")]
[InlineData("""{ "roster": "soldier", "pieceValue": 3, "squares": ["9z"] }""")]
public void Bad_reinforcements_entries_are_reported(string effectParams)
// MapLoadException whose Problems contain "Reinforcements" and not "unknown board effect".
```

Also rerun the existing `Bad_tidal_wave_entries_are_reported` after the square-parsing extraction.

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/RogueChess.Engine.Tests --filter "FullyQualifiedName~ReinforcementsTests|FullyQualifiedName~BoardEffectTests"`
Expected: the new map tests fail with "unknown board effect 'Reinforcements'".

- [ ] **Step 3: Implement** the parsing; `pieceValue` is required (missing → `FormatException`), `side` uses `SideParam(p, "side")`.

- [ ] **Step 4: Add to the fuzz test** in `BoardEffectTests.Random_games_with_board_effects_keep_hash_and_undo_exact`:
`new BoardEffectDef(new TurnIntervalCondition(9), new ReinforcementsEffect(Side.Black, catalog.FindRoster("soldier")!, 3, 2, Rows(8, 5, 6), catalog))`
with `var catalog = ContentLibrary.CreateCatalog();`.

- [ ] **Step 5: Docs.** CONTENT_GUIDE.md: a Reinforcements row in the board-effect table (params as in Global
Constraints) and a short "Rosters" section (file location, format, embedded at build, ids case-insensitive).
RESOLUTION_RULES.md: a worked example (soldier roster, Black, value 6, area a7–h7 with one occupied square).

- [ ] **Step 6: Run the whole suite**

Run: `dotnet test tests/RogueChess.Engine.Tests`
Expected: all pass, 0 warnings. Then `dotnet build GodotChess.csproj` succeeds.

- [ ] **Step 7: Commit** — `git commit -m "Load Reinforcements from map JSON and document it"`
