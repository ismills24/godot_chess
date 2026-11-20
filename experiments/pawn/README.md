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
