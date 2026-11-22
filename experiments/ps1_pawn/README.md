# Modular textured pawn study

Review-only experiment. No gameplay catalog, board scene or original wooden piece is replaced.
Open `PawnPS1Preview.tscn` and run F6. The existing wood asset is on the left; the new textured
low-poly pawn is on the right. Drag to orbit, scroll to zoom, separate modules, toggle pixel/raw
view, vertex snapping, or continuous orbit. F5 still runs the current game.

## Provenance and reproducibility

`capture_reference.gd` renders **the actual** `assets/pieces/wood/LightPawn.tscn` in Godot,
including isolated Base, Body and Head references. Images and full ComfyUI jobs live under
`artifacts/asset_harness/`. `build_pawn.py` records the three job IDs and rebuilds the experimental
meshes using the harness's Blender cleanup helpers.

The 512-resolution native TRELLIS.2 textured workflow generates each module separately.
The harness now reduces to 25,000 faces before UV unwrapping, while using the original dense
shape as the texture-bake reference. This avoids excessive UV-processing time on generated meshes.

The Blender pass reconstructs convex outer envelopes from the generated vertices (these
three simple forms permit this), discarding TRELLIS's internal sheets and disconnected noise.
It reduces those shapes, fits them to the existing module envelopes, restores the nonconvex
head socket and body dowel, and rebakes the generated albedo with a
stronger authored wood-grain layer and a unified warm 12-color palette. It quantizes/dithers
color into three **64×64** textures.
The preview uses point sampling, faceted shading, optional screen-space vertex snapping,
affine texture mapping, a 480×270 render target, and 15-bit-style color quantization.

## Module contract

- Base: board-ground origin, radius 0.43, height 0.12.
- Body: board-ground coordinates, foot at 0.145 and head seat at 0.91.
- Head: local origin at its mating plane; assembled at Y=0.91 in Godot.
- Dowel: belongs to Body, radius 0.132, length 0.30, recessed groove pitch 0.10.
- Head socket: radius 0.138, cavity depth 0.32, smooth outer sleeve radius 0.185.
- Blender Z-up becomes Godot Y-up through glTF export.

`generated/Base.glb`, `Body.glb`, and `Head.glb` remain independently replaceable.
`PawnPS1.tscn` assembles them, and `generated/PawnPS1.glb` is a combined interchange export.
`generated/report.json` records per-module mesh checks and polygon counts. The authoring file,
including preserved TRELLIS source meshes, is `artifacts/asset_harness/ps1_pawn/PawnPS1.blend`.
This study needs art approval before any gameplay wiring. Mesh checks do not certify absence
of self-intersections or suitability for manufacturing.

## Review checks

The final assembly has **1,256 triangles**, including the exposed threaded dowel and hollow
head socket. All three module reports have zero boundary/non-manifold edges or vertices.
Godot verified separate module nodes, seated/exploded transforms, texture sizes and orbit.
The assembled and exploded views were rendered and inspected without runtime/shader errors.
Review images: `artifacts/asset_harness/ps1_pawn/comparison.png` and `exploded.png`.
