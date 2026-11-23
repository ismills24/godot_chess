# Inn scene: design

A second 3D location where the player plays chess against the bartender. It follows the
alley main menu (`menu/`) in tone and rendering: dark, a bit crass, low-res pixel finish.

## Scope of v1 (visual first pass)

- The room, lighting, fireplace and props, rendered through the same pipeline as the menu.
- A low-poly bartender built from primitives. Short sequence: they stand at the chess table
  holding a board, set it down, sit, and the camera pushes in to the play shot.
- The served board is decorative (wooden set, starting position). It is not yet connected
  to the engine. `BoardView3D` and an AI opponent profile come in a later pass.
- Menu "Play" loads the inn. The sandbox stays reachable through the existing command-line
  game arguments (`--scenario=...` and so on), which still skip the menu.

Out of scope for v1: real gameplay, result screen and rematch, walking paths, audio.

## Rendering

The same setup as `menu/MainMenu.tscn`: a `SubViewport` stage with a 704px long edge, shown
through `menu/pixel_finish.gdshader`; AgX tonemapping, glow, fog; 60 FPS cap; animation
stepped at 30 Hz; a reduced-motion setting honored.

## Room

A small, low room, about 10 m wide x 12 m deep x 3 m high.

- **Chess table:** against the left wall, a third of the way in. The bartender sits on the
  wall side (bench) and the player on the near side (stool).
- **Fireplace:** centered on the back wall. It is the key light and the focal point.
- **Bar:** along the right wall, with shelves, bottles and a lamp.
- **Other tables:** four or five, empty, with leftover mugs, a tipped chair and candle wax.
- **Materials:** the alley's brick (masonry shader) for the hearth and walls, dark timber
  beams and floorboards, plaster with soot.

## Camera

1. **Establishing:** from near the door, angled left to frame the chess table, the
   bartender and the fireplace.
2. **Play:** a slow push in to a seated view over the board, bartender across, fire behind.

## Code structure

- `common/scene_kit.gd`: shared builder helpers taken out of `menu/alley_world.gd`
  (batched boxes, prop placement scaled to a height, primitive meshes, lights, materials,
  animated-material stepping, flame mesh). `alley_world.gd` extends it, and its behavior
  must not change.
- `inn/Inn.tscn` + `inn/inn.gd`: stage, camera, environment, sequence controller.
- `inn/inn_world.gd`: builds the room (extends the scene kit).
- `inn/bartender.gd`: builds and animates the figure, with a `serve()` sequence and a signal
  when seated.
- `inn/props/`: packaged runtime props.

## Props pipeline

CC0 Poly Haven models are downloaded into `artifacts/polyhaven_sources/` (git-ignored) by
`tools/scene_builders/download_inn_props.py`. Then `tools/asset_harness/export_inn_props.py`
runs in headless Blender. It decimates each model to a budget, shrinks textures, and writes
GLBs to `inn/props/`. Only the packaged GLBs are committed.

## Verification

- The menu renders the same before and after the scene-kit refactor (frame capture).
- The inn renders, and the sequence plays from start to the play shot (frame capture via
  Godot's movie writer).
- No errors in the Godot output.
