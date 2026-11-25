# Stage 1: the street

The first stage of a run: the player starts in the gutter. Presentation only — no rules.
`sandbox/visuals/StageVisuals.cs` names a stage's board scene and setting scene;
`BoardView3D.Stage` defaults to `StageVisuals.Street`. `StageVisuals.Workshop` keeps the
clean maple/walnut board under neutral lights.

## Board — `StreetBoard.tscn`

A salvaged tea-crate lid with squares painted on by hand. It follows the board contract
(`board_width`/`board_height` set before entering the tree, a `Squares` child with one
marker per square, Y=0 playing surface, 1.45-unit squares, centred on the origin).

- Planks run along the files with real gaps; paint crosses them. One plank lost its end
  (inside the margin), cleats underneath show at the edges, nails, and a tin patch over a split.
- `street_board.gdshader`: greyed pine, soot-black paint with broad chips (worst at square
  corners and the busy middle ranks), a wobbly painted border, a few scratches, a tea ring,
  a candle scorch, damp blotches and a tide line. It works in world XZ, so the board must sit
  at the origin unrotated — the gameplay view guarantees this.
- A worn crate stencil ("J. HOLLIS & SON · FINE TEAS · LONDON") along White's margin.
- `wear_seed` fixes the plank layout and weathering.

## Setting — `StreetSetting.tscn`

Lit like the main menu's alley: warm gas lamp (key, shadows), cool night fill, teal ambient,
AgX, glow. `fit(width, height)` lays out around the board's edges.

- Scale is true to the pieces (one square ≈ 5 cm): cobbles (`menu/props/cobbles.jpg`, 24-unit
  tiles) are about two squares wide, and the kerb towers over the board along Black's edge.
- `street_ground.gdshader` (from `menu/wet_cobbles.gdshader`): pooled water, a running gutter
  at the kerb's foot, lamp/candle glints and rain rings.
- `street_stone.gdshader` (kerb, pavement) dithers away any fragment between the camera and
  the board, so the orbiting camera never loses squares behind stonework.
- Props sit within the camera's margin: a candle stub in a tobacco tin (flickering light) and
  spent matches by White's right hand, the stake of coppers by Black's left, a sodden card,
  a drain grate in the gutter.

Review through the gameplay view (pixel stage), not full-resolution renders.
