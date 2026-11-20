# Pawn surface-splat experiment

Open `PawnPreview.tscn` in Godot and press **F6** (run current scene).
F5 still runs the existing chess sandbox.

Drag the preview to orbit; scroll to zoom. The panel controls pixel rendering,
the 12-color palette, turntable rotation, stroke size, and sampling density.
Disable both pixel rendering and the palette to inspect the underlying splats.

## Implementation

- `pawn_shape.gd`: signed distance to a revolved polygon profile, unioned with a
  sphere. Seeded, jittered grid samples are projected onto the zero surface with
  numerical gradients. Each sample receives a normal and baked diffuse color.
- `pawn_preview.gd`: tangent-aligned elliptical quads, sorted back-to-front by
  camera depth. Vertex geometry is cached; camera movement updates triangle
  ordering using native integer sorting. Default spacing produces 5,664 splats.
- `splat.gdshader`: truncated Gaussian opacity over each elliptical quad.
- `palette.gdshader`: nearest-color mapping to a shared 12-color palette.
  A reduced-resolution SubViewport supplies the pixelation independently.

This is a surface-splat approximation of the linked SDF painting technique,
not a trained 3D Gaussian Splatting model or a volumetric covariance renderer.
No Blender, downloaded model, or image generator is required.
The display tile and contact-shadow disc are ordinary meshes. Lighting is baked;
splats do not cast dynamic shadows. Center-depth sorting can show artifacts with
large overlapping strokes. Density changes synchronously regenerate samples.
Full-board rendering, deformation, exports, and production GPU sorting remain
outside this first visual experiment.

## Verification

Loaded and instantiated through Godot 4.7.2 .NET, then visually checked under GL
Compatibility on an RTX 3080 Ti. Pixel/palette toggles and turntable inputs were
delivered successfully. After caching geometry, the 2.5-second raw-splat turntable
check averaged 120 FPS with 115 FPS 1% low (debug build, vsync on). This short
single-pawn measurement is not a full-board performance guarantee.

## Workshop head-swap prototype

Open `WorkshopPreview.tscn` and press **F6**. The 13.5-second swap starts
automatically; use Pause/Play, Replay head swap, the timeline slider, or
Inspect exposed thread. Drag to orbit and scroll to zoom. Pixel rendering and
the workshop palette can be toggled separately. The original pawn scene and
the project's main sandbox remain unchanged.

The base and shaft retain their SDF-derived splats. Ivory and solid steel heads are
separate rigid splat modules with open undersides and smooth hollow sleeves.
The neck is a wooden dowel with shallow recessed threading, rather than a raised
spiral wrapped around a shaft: three revolutions at 0.10 units per
revolution. During engagement, vertical travel is coupled to angular travel;
parts only translate sideways once they have cleared the neck. The old head
rests on the left pad after removal. The replacement is a plain, slightly larger
steel sphere with a cool specular highlight. Its sleeve is smooth steel; the
original head has a smooth ivory sleeve. There are no bomb details. The idea of
requiring a larger capturing piece is not implemented as a gameplay rule.

`workshop_parts.gd` creates crisp conventional geometry for the mechanical
interfaces and bench. `splat_part.gd` caches each head's local surface samples
and re-sorts its triangles as the camera or head rotates. The moving splats use
world-space directional shading so highlights do not rotate with the heads;
the steel finish adds a view-dependent highlight. `wooden_dowel.gdshader` cuts
the shallow helical groove inward and adds subtle longitudinal wood grain.
`workshop_preview.gd` computes poses directly from timeline time, making replay
and backward scrubbing deterministic without accumulating rotations.

This is an assembly animation study: parts are moved by a presentation timeline,
without hands/tools, physics, or gameplay ability changes. The threads are visual
geometry, not a manufacturing specification or simulated threaded joint. The
previous bomb-head version ran without errors at 120 FPS average / 116 FPS 1% low
in a 16-second GL Compatibility debug run on the same RTX 3080 Ti; that measurement
does not validate the revised metal-head rendering.
