# Maple and walnut chess pieces

Twelve reusable Godot scenes: `LightPawn.tscn` through `LightKing.tscn`, plus
the six `Dark*.tscn` equivalents. Instantiate copies for the usual 16 pieces
per side. `piece_kind` and `dark_wood` can also be set on `wood_piece.gd` before
adding a node to the scene tree. No chess rules or picking/collision are included.

Open `res://experiments/wood_set/WoodSetPreview.tscn` and press F6 to inspect the
12 designs, the full 32-piece inventory, or individual pairs. Pixel rendering,
head lifting, camera orbit, and zoom are available. Display discs are neutral
preview stands, not a chessboard. The main sandbox now uses these assets for gameplay.

## Asset contract

- Origin is at the center of the foot on the ground; +Y is up. The knight faces +X.
- Base radii range from 0.43 to 0.56 units. The preview uses 1.35-unit minimum
  spacing; choose a board square size that also accommodates later modifications.
- `Base` and `Body` are separate splat meshes; `Head` is a detachable Node3D with
  `Carving` and `SmoothSocket` children. `ThreadedDowel` stays with the body.
- `seat_height` is the local head mating plane, varying by piece type.
  All heads share the pawn prototype's wooden dowel (radius 0.132, height 0.30,
  pitch 0.10) and smooth open sleeve (inner radius 0.138, outer radius 0.185).
- `set_head_lift(distance)` couples rotation to axial travel for the first 0.30
  units and lifts clear thereafter. Zero restores the seated pose. The preview
  toggle jumps between states for inspection; it is not a new swap animation.
- Optional `base_kind`, `body_kind`, and `head_kind` select modules independently.
  Empty overrides use `piece_kind`, preserving the original scenes. The body supplies
  the head's mating height. Gameplay recipes live in `assets/pieces/visual_catalog.json`.
- In a normal 3D viewport, the active Camera3D determines splat depth sorting.
  Geometry is generated once per archetype/module and shared across instances
  and wood finishes. The first load has a short generation cost.
- Set `depth_tested_splats = true` before adding a piece to the tree for board-scale
  rendering. It uses GPU depth-tested Gaussian cutouts and disables the piece's
  sorting process. The default remains soft alpha blending for close-up studies.

## Rendering and construction

`wood_shapes.gd` uses sampled surfaces of revolution for simple tapered bodies and
thin disc feet, with signed distance functions for the six head carvings. The
reference-led revision replaces ornate rings with one foot seam; the knight is a
broad horse cutout with throat negative space and no face or mane detailing.
A rook with three broad raised battlements and a recessed center, a bishop with a
short diagonal side slit and intact tip, a ball-topped queen capital, and a king
cross provide the other identifying shapes. The grain and sheen are subdued.
Sampling is deterministic. Gaussian-opacity surface splats retain the painted
finish; smooth sockets and recessed dowel threads use conventional geometry.

`wood_surface.gdshader` and `wood_solid.gdshader` share procedural grain, directional
shading, and satin highlights through `wood_lighting.gdshaderinc`. Maple and walnut
use the same geometry with distinct material colors, independent of gameplay side.
The current assets intentionally reuse `experiments/pawn/workshop_parts.gd` and
its dowel shader; keep those dependencies when moving or packaging the set.

`wood_splats.gd` caches geometry and up to four depth-order meshes per module.
Identically oriented pieces share an order, including the two wood finishes.
The cache quantizes the local camera direction; transparent surface splats still
have approximate overlap sorting. Lighting is stylized, and splats do not cast
dynamic shadows. A second simultaneous viewport with a different camera has not
been validated. Prebaking and a production sorting path can be added when needed.

## Validation

`res://experiments/wood_set/verify_wood_set.gd` checks all 12 scene assets, finite
geometry, module hierarchy, head seating/clearance, both 16-piece inventories,
and the pair inspector. Run it with Godot's headless `--script` option.

The original twelve-design collection and bishop/knight close-ups were visually checked
under Godot 4.7.2 GL Compatibility. A 4.2-second 32-piece debug run on an RTX 3080 Ti
averaged 120 FPS with 116 FPS 1% low after shared socket/dowel caching; the worst
frame was 12.4 ms. This includes switching to 32 pieces, not continuous orbit or a
complete game/board benchmark.

The minimalist revision was separately checked in the collection preview and
with the geometry/module verifier. The timings above refer to the earlier shapes.

The subsequent rook/bishop refinement uses denser bishop sampling and clips splat
footprints against the slit in `wood_surface.gdshader`. Keep its cut transform in
sync with `wood_shapes.gd`. Geometry checks cover the three raised rook sectors
and the bishop's intact tip. Both revised pieces were visually checked as close-up
maple/walnut pairs in Godot, with no runtime or shader errors.

The same bishop clipping transform also lives in `wood_surface_depth.gdshader`;
update both surface shaders when changing the cut. Board-scale cutouts reject
back-facing splats and have crisper edges than the blended gallery renderer.
