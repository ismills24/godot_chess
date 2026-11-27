# Tidal Wave Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `TidalWave` board effect that drags pieces on chosen squares toward one board edge, N one-square steps per firing, destroying pieces pulled off the board.

**Architecture:** `BoardEffect` gains a virtual `Steps` (default 1). `BoardEffectDef` allocates a second global variable "steps left" only when `Steps > 1`, and chains the extra steps by reacting to its own `VarChanged` fact, one wave per step. `TidalWaveEffect` proposes one `Move`/`Destroy` per piece per step and leaves collisions to the resolver's sliding-block rules.

**Tech Stack:** C# / .NET 8, xUnit (`tests/RogueChess.Engine.Tests`).

**Spec:** `docs/superpowers/specs/2026-11-26-tidal-wave-design.md`

## Global Constraints

- Existing effects (WallsPlacement, WindGust, CheckerboardWipe) must keep exactly their current ids, var count (1) and behaviour, so saved runs and hashes don't change (no `RunSession.ContentVersion` bump needed).
- Effect JSON type name: `TidalWave`; params `direction` (`north|south|east|west`, any case), `squares` (array of names like `"a6"`), `strength` (int, default 1, must be ≥ 1).
- North = +rank (toward the highest rank), South = −rank, East = +file, West = −file.
- Cause string for both moves and destroys: `tidal-wave`.
- Blocked pieces stay put; no freeze.
- Every state change goes through proposals (hash + undo exact).
- Test command: `dotnet test tests/RogueChess.Engine.Tests/RogueChess.Engine.Tests.csproj --filter "FullyQualifiedName~BoardEffectTests"`; full suite before finishing: `dotnet test tests/RogueChess.Engine.Tests/RogueChess.Engine.Tests.csproj` and `dotnet test tests/RogueChess.Runs.Tests/RogueChess.Runs.Tests.csproj`.

## Review Focus

1. A piece on an affected square adjacent to a wall in the wave's direction: it stays, the wall survives, and the piece behind it in the same column is blocked too (test in Task 2).
2. A piece pulled onto an affected square during step 1 is pulled again in step 2; a piece pulled off the affected area is not (test in Task 2).
3. Affected squares that are off the board for the loaded map (e.g. `"h8"` on a 4×4 map) are ignored rather than crashing (test in Task 2).
4. A king washed off the board: the game ends through the normal outcome evaluation, no exception (test in Task 2).
5. Undo across a multi-step firing restores the exact hash and dump (test in Task 2, using strength 2 in the random-games test).

---

### Task 1: Multi-step board effects

**Files:**
- Modify: `src/RogueChess.Engine/Content/BoardEffects/BoardEffectDef.cs`
- Test: `tests/RogueChess.Engine.Tests/BoardEffectTests.cs`

**Interfaces:**
- Produces: `public virtual int Steps => 1;` on `BoardEffect` (doc: how many times `Apply` runs per firing, one wave each). `public const int StepsLeftVar = 1;` on `BoardEffectDef`. `BoardEffectDef` constructor passes `varCount: effect.Steps > 1 ? 2 : 1`. `Triggers => Effect.Steps > 1 ? FactKinds.TurnEnded | FactKinds.VarChanged : FactKinds.TurnEnded`.

Chaining rule in `OnFact`:
- `TurnEnded`, condition met: `Effect.Apply(ctx, timesFired)`, `AddVar(TimesFiredVar, +1)` (as today), and if `Steps > 1`, `SetVar(StepsLeftVar, Steps - 1, "board-effect-step")`.
- `VarChanged` where `fact.Var == VarTarget.OfGlobal(ctx.Self.GlobalIndex)`, `fact.Slot == StepsLeftVar` and `fact.NewValue > 0`: `Effect.Apply(ctx, timesFired)` (the current, already-incremented count minus 1, so every step of one firing sees the same value) and `AddVar(StepsLeftVar, -1, "board-effect-step")`.

With N = 3 that runs Apply in three consecutive waves and ends with steps left 0.

- [ ] **Step 1: Write the failing tests** (add a private test effect at the bottom of the test class)

```csharp
/// <summary>Each step turns the first non-wall square of rank 1 (from a1) into a wall.</summary>
private sealed class WallPerStepEffect(int steps) : BoardEffect
{
    public override string Key => $"WallPerStep({steps})";
    public override string Describe() => "test";
    public override int Steps => steps;
    public override void Apply(TriggerContext ctx, int timesFired)
    {
        for (int x = 0; x < ctx.Board.Width; x++)
            if (ctx.Board.TileAt(ctx.Board.Sq(x, 0)).Def != WallTile.Instance)
            { ctx.Propose(Proposal.ChangeTile(ctx.Board.Sq(x, 0), WallTile.Instance, "test")); return; }
    }
}

[Fact]
public void Multi_step_effect_applies_once_per_wave_within_one_firing()
{
    var s = Rogue("7k/8/8/8/8/8/8/7K w - - 0 1");
    var g = AddEffect(s, new TurnDelayCondition(1), new WallPerStepEffect(3));
    Play(s, "h1g1");
    Assert.Equal(3, Walls(s));
    Assert.Equal(1, g.Var(BoardEffectDef.TimesFiredVar));
    Assert.Equal(0, g.Var(BoardEffectDef.StepsLeftVar));
    Play(s, "h8g8"); // TurnDelay fires once only
    Assert.Equal(3, Walls(s));
}

[Fact]
public void Single_step_effects_keep_one_variable()
{
    Assert.Equal(1, new BoardEffectDef(new TurnIntervalCondition(1), new WindGustEffect(Side.White)).VarCount);
    Assert.Equal(2, new BoardEffectDef(new TurnIntervalCondition(1), new WallPerStepEffect(2)).VarCount);
}
```

(If `GlobalInstance.Var` has a different name, use the accessor the existing tests/def use: `ctx.Board.Globals[i].Var(slot)`.)

- [ ] **Step 2: Run and verify failure** — `dotnet test ... --filter "FullyQualifiedName~BoardEffectTests"`; expected: compile error (`Steps`/`StepsLeftVar` missing).
- [ ] **Step 3: Implement** per the Interfaces block. Update the `BoardEffectDef` and `BoardEffect` doc comments to describe steps.
- [ ] **Step 4: Run** the filter; expected: all BoardEffectTests pass (existing legacy-map id tests unchanged).
- [ ] **Step 5: Commit** — `git commit -m "Let board effects run several steps per firing"`

---

### Task 2: TidalWaveEffect

**Files:**
- Modify: `src/RogueChess.Engine/Content/BoardEffects/Effects.cs`
- Test: `tests/RogueChess.Engine.Tests/BoardEffectTests.cs`

**Interfaces:**
- Consumes: `BoardEffect.Steps` (Task 1).
- Produces:
  - `public enum WaveDirection { North, South, East, West }`
  - `public sealed class TidalWaveEffect : BoardEffect` with `public const string Cause = "tidal-wave";`, constructor `TidalWaveEffect(WaveDirection direction, IReadOnlyList<(int X, int Y)> squares, int strength = 1)` (throws `ArgumentOutOfRangeException` if `strength < 1`), properties `Direction`, `Squares` (deduplicated, sorted by Y then X), `Strength`; `Steps => Strength`.
  - `Key` = `$"TidalWave({Direction}, {names}, strength {Strength})"` where names are the sorted squares as `a6 b6 c6` (letter = 'a'+X, number = Y+1), space-separated.
  - `Describe()` = `$"Drags pieces on {Squares.Count} square(s) {Strength} square(s) toward the {direction lower-case} edge; pieces pulled off the board are destroyed."`

`Apply` per step: for each `(x, y)` in `Squares` that is in bounds and has a piece: target `(x+dx, y+dy)` (North 0,+1 · South 0,−1 · East +1,0 · West −1,0). Off the board → `Proposal.Destroy(piece, Cause)`. Else, if the target tile's def `BlocksPassage || BlocksLanding` → propose nothing. Else `Proposal.Move(piece, target, Cause)`.

- [ ] **Step 1: Write the failing tests**

Helper: `static (int, int)[] Rows(int width, params int[] ranks)` returning every `(x, rank-1)` for those ranks (ranks given 1-based).

```csharp
[Fact] // the user's example: north wave on the top 3 rows
public void Tidal_wave_pulls_the_top_rows_north_and_washes_out_the_edge()
{
    // a8 black rook, a7 white pawn, a6 white knight, a5 white bishop (outside the area)
    var s = Rogue("r6k/P7/N7/B7/8/8/8/7K w - - 0 1");
    AddEffect(s, new TurnDelayCondition(1), new TidalWaveEffect(WaveDirection.North, Rows(8, 6, 7, 8)));
    Play(s, "h1g1");
    Assert.Equal(StandardPieces.Pawn, At(s, "a8")?.Def);   // a7 → a8 (rook washed out)
    Assert.Equal(StandardPieces.Knight, At(s, "a7")?.Def); // a6 → a7
    Assert.Null(At(s, "a6"));
    Assert.Equal(StandardPieces.Bishop, At(s, "a5")?.Def); // outside the area: untouched
}
```

Further tests (same style, assertions named):
- `Tidal_wave_works_in_every_direction` — `[Theory]` over the four directions on a lone white rook at d4 with area = {d4}: lands on d5 / d3 / e4 / c4.
- `Tidal_wave_destroys_pieces_pulled_off_the_board_without_crediting_anyone` — a white knight on a8, area {a8}, North: a8 empty; the turn's trace/facts have a `Died` with cause `tidal-wave` and no `Killed` fact whose source is a piece. (If the trace isn't easy to read, assert the piece is gone and the captured/kill counters used elsewhere in the tests are unchanged.)
- `Tidal_wave_is_held_back_by_walls` — wall on a8, pieces on a7 and a6, area = ranks 6–8, North: a7 and a6 stay put, a8 is still a wall.
- `Tidal_wave_is_blocked_by_a_piece_outside_the_area` — area {a6}, piece on a6 and a7, North: both stay.
- `Tidal_wave_strength_two_rechecks_the_area_each_step` — area = ranks 6–7 only, North, strength 2; pieces on a5 (outside), a6: after one firing a6's piece is on a8 (step 1 → a7 still in area, step 2 → a8), a5's piece is still on a5.
- `Tidal_wave_strength_two_washes_out_two_rows` — area ranks 7–8, strength 2, pieces on a7 and a8: both destroyed.
- `Tidal_wave_ignores_affected_squares_off_the_board` — 4×4 board via `MapLoader`-free setup (`new GameState(4, 4, RuleSet.Rogue)` + `Fen`-equivalent piece placement used elsewhere in tests, or an 8×8 board with a square `(10, 10)` in the list): no exception, other squares still pulled.
- `Tidal_wave_washing_out_a_king_ends_the_game` — black king h8, area {h8}, North: after the turn `s.Rules.Evaluate(s) != GameOutcome.Ongoing`.
- Add `s.AddGlobal(new BoardEffectDef(new TurnIntervalCondition(7), new TidalWaveEffect(WaveDirection.West, Rows(8, 3, 4), strength: 2)));` to `Random_games_with_board_effects_keep_hash_and_undo_exact` (it still has to pass `played > 10`; drop it to strength 1 or interval 9 if the game ends early).

- [ ] **Step 2: Run and verify failure** — filter as above; expected: compile error (`TidalWaveEffect` missing).
- [ ] **Step 3: Implement** `WaveDirection` and `TidalWaveEffect` in `Effects.cs` per the Interfaces block, with a doc comment in the style of the other effects.
- [ ] **Step 4: Run** the filter; expected: all pass, including the random-games hash/undo test.
- [ ] **Step 5: Commit** — `git commit -m "Add the Tidal Wave board effect"`

---

### Task 3: Map JSON and docs

**Files:**
- Modify: `src/RogueChess.Engine/Content/BoardEffects/BoardEffectJson.cs`
- Modify: `docs/CONTENT_GUIDE.md` (effect table near line 182), `docs/RESOLUTION_RULES.md` (§11 worked examples after "Wind gust", and the content table near line 229)
- Test: `tests/RogueChess.Engine.Tests/BoardEffectTests.cs`

**Interfaces:**
- Consumes: `TidalWaveEffect`, `WaveDirection` (Task 2).
- Produces: `.AddBoardEffect("TidalWave", Create)` in `AddBoardEffects`; a `"TidalWave"` case in `ParseEffect`.

Parsing: `direction` required, case-insensitive match to `WaveDirection`, else `FormatException("TidalWave needs a direction of north, south, east or west")`. `squares` required, non-empty array of strings; each parsed with the same rules as `MoveText.ParseSquare` minus the bounds check (letter a–z then a positive integer rank → `(letter-'a', rank-1)`), else `FormatException($"TidalWave: bad square name '{name}'")`. `strength` via `Int(p, "strength", 1)`, `< 1` → `FormatException("TidalWave needs a strength of at least 1")`.

- [ ] **Step 1: Write the failing tests**
  - `Tidal_wave_loads_from_a_map` — inline map JSON (8×8, kings on a1/h8, one `boardEffects` entry `TurnInterval` interval 2 + `TidalWave` `{ "direction": "North", "squares": ["c6","a6","b6"] }`): the single global's `Def.Id == "board-effect:TidalWave(North, a6 b6 c6, strength 1) when TurnInterval(every 2)"` (copy the exact `TurnIntervalCondition.Key` text the existing tests produce for interval 2 without start turn).
  - `Bad_tidal_wave_entries_are_reported` — `[Theory]` over params `{ "squares": ["a1"] }` (no direction), `{ "direction": "up", "squares": ["a1"] }`, `{ "direction": "north", "squares": [] }`, `{ "direction": "north", "squares": ["9z"] }`, `{ "direction": "north", "squares": ["a1"], "strength": 0 }`: `MapLoadException` whose `Problems` contain `"TidalWave"`.
- [ ] **Step 2: Run and verify failure** — expected: the load test fails with an unknown board effect problem.
- [ ] **Step 3: Implement** the parsing and registration.
- [ ] **Step 4: Docs** — CONTENT_GUIDE row: `| TidalWave | direction (required: north/south/east/west), squares (required: ["a6", ...]), strength (1): every piece on those squares moves one square toward that edge, strength times; pieces pulled off the board are destroyed; walls and pieces that stay put block |`. RESOLUTION_RULES worked example: one `Move` per piece per step, all in one wave; the column shifts because each square is vacated in the same wave; off-board pulls are `Destroy`; strength N chains N waves through the effect's own steps-left variable. Add TidalWave to the content table row with the other board effects, noting `VarChanged` for multi-step.
- [ ] **Step 5: Run the full suites** — both `dotnet test` commands from Global Constraints; expected: all pass. Also `dotnet build GodotChess.csproj` succeeds.
- [ ] **Step 6: Commit** — `git commit -m "Load Tidal Wave from map JSON and document it"`
