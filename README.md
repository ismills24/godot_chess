# godot_chess

Roguelike chess, ported from the TypeScript/Electron prototype to Godot 4.7 (.NET). See [docs/PORT_PLAN.md](docs/PORT_PLAN.md) for the plan and the reasons behind the redesign.

## Layout

| Path | What |
|---|---|
| `src/RogueChess.Engine/` | Rules engine: state, moves, turn resolution, content, AI. Pure C#, **no Godot reference**. |
| `tests/RogueChess.Engine.Tests/` | xUnit tests for the engine. |
| `sandbox/` | Godot debug scene (main scene) that drives the engine. |
| `docs/` | Plan, resolution rules, content guide. |

## Requirements

- Godot **4.7.2 .NET** (the `mono` build). The standard build can't run C# scripts.
- .NET SDK 8 or newer.

## Commands

```bash
dotnet test tests/RogueChess.Engine.Tests/RogueChess.Engine.Tests.csproj
```

```bash
dotnet build GodotChess.csproj
```

Run the sandbox headless (prints `[Sandbox] RogueChess.Engine loaded ...`):

```bash
Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . --quit-after 30
```

## Ziva

The Ziva editor add-on is installed per machine into `addons/ziva_agent/` (gitignored, not redistributable). Copy it from another project or install it from ziva.sh.
