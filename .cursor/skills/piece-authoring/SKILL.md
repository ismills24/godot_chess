---
name: piece-authoring
description: Create or refine modular chess-piece visuals and workshop assembly animations in godot_chess. Use for piece shapes, material changes, ability-related physical modifications, attachment interfaces, surface splats, or head-swap previews. Does not define gameplay rules or balance.
---

# Piece authoring

Build on this project's working Godot prototypes. Preserve the distinction between
user-established art direction, current implementation choices, and undecided game
design. Later explicit user feedback can revise any preference below.

## Established preferences

- Favor minimalist, impressionistic forms for the base wooden set: broad tapered
  bodies, a single quiet foot seam, restrained tops, and subtle grain. The ornate
  turned profiles, busy crowns, and detailed first knight were rejected. Use a
  simple horse silhouette with clear throat negative space; avoid tiny facial
  details, serrated mane grooves, or separate spiky ears. Preserve recognition
  through silhouette rather than surface ornament.
- Abilities are diegetic: people cheat by physically modifying pieces in workshops.
  Designs should suggest how an alteration was made and fitted to the original piece.
- Cheating becomes more sophisticated in more affluent areas. Express this through
  workmanship, fit, materials, and concealment; this does not establish a power curve.
- Players should be able to modify pieces modularly. A head that visibly screws off
  and is replaced is an established interaction. Base/body/head is the working
  decomposition, not a requirement that every future piece use exactly three modules.
- Keep the head's outer neck/sleeve smooth. The stacked-ring, ribbed sleeve was rejected.
- For the current pawn, use a wooden dowel with shallow recessed threading. The thick
  raised spiral looked like vines and was rejected. Preserve a solid dowel silhouette.
- Preserve readable mechanical seams and clean mating surfaces. Avoid wispy splats at
  the collar and sharp edges; tighten footprints/falloff locally or use crisp geometry.
- The current replacement is a plain steel head with a cool metallic highlight.
  The bomb, fuse, patch, and rivets were superseded for this prototype. They are not
  forbidden for a future explicitly requested design.
- Pieces should belong in the menu's rainy, lantern-lit Victorian alley: lit by scene
  lights rather than baked shading, with a felt foot. Bodies use a gentle concave sweep
  with a collar bead, not straight cones.
- Match the menu's PS1-era aesthetic: flat, matte, softly varied painted colour. A
  realistic finish (prominent ring grain, glossy varnish, crisp highlights) was rejected
  as too sharp and distracting, especially on maple. Review through the menu's low-res
  stage and pixel finish rather than full-resolution renders.
- Special-piece status is diegetic material, not UI: e.g. a gilt (gold leaf) finish for
  a VIP-style piece, a smoldering wick for an explosive one. `finish` on `wood_piece.gd`
  is the material slot; such ideas are art direction, not established rules.
- The painted surface-splat appearance and pixel treatment were received positively.
  Keep the ability to inspect the underlying rendering without postprocessing.

## Open gameplay ideas — not requirements

- A heavier metal-headed piece might require a larger piece, such as a rook or above,
  to capture it. No threshold, ranking, balance, or capture restriction is established.
- Specific ability-to-module mappings, multiple abilities competing for an attachment,
  cosmetic freedom versus required ability cues, and progression benefits remain open.
- The exact material palette and construction details for each wealth tier remain to
  be designed. Earlier examples of tiered bomb designs were proposals, not specifications.

Do not turn these ideas into engine behavior, UI promises, or mandatory art conventions
while fulfilling a visual request. If gameplay implementation is requested, resolve the
relevant semantics then; visual iteration can proceed independently.

## Start from the maintained examples

Read [the experiment notes](../../../experiments/pawn/README.md) and the relevant
scripts before editing. Paths below are relative to the repository root:

- `assets/pieces/wood/README.md`, `wood_shapes.gd`, and `wood_lighting.gdshaderinc`:
  the reusable maple/walnut set, minimalist geometry, and quiet wood finish.
  `experiments/wood_set/WoodSetPreview.tscn` previews all designs and individual pairs.
- `experiments/pawn/WorkshopPreview.tscn` and `workshop_preview.gd`: current modular
  ivory-to-steel swap, controls, camera, and deterministic timeline poses.
- `experiments/pawn/workshop_parts.gd` and `wooden_dowel.gdshader`: smooth hollow
  sleeves, manufactured interfaces, and shallow threading cut into the wooden dowel.
- `experiments/pawn/splat_part.gd` and `workshop_splat.gdshader`: rigid head modules,
  cached local samples, depth sorting, world-space lighting, and metallic highlights.
- `experiments/pawn/workshop_palette.gdshader`: cool steel shades and warm wood hues;
  avoid mapping shadowed ivory or iron to rusty brown.
- `experiments/pawn/pawn_shape.gd`, `pawn_preview.gd`, and `PawnPreview.tscn`: original
  SDF pawn and the separate rendering study. Keep it useful as a comparison.

Reusable geometry and animation logic belong in project code, not duplicated inside
this skill. Keep presentation separate from the pure C# engine in `src/RogueChess.Engine/`.
For an isolated experiment, preserve the existing sandbox main scene; launch the selected
preview with F6. Blender is optional, and this workflow requires no Blender connector.

## Construction and animation

Use the current hybrid approach where appropriate: sampled mathematical surfaces with
Gaussian-opacity splats for broad shapes, conventional geometry for clean joints and
mechanical details. These are surface splats, not a trained volumetric 3DGS model.

Keep detachable modules in local coordinates with a defined seating height, axis,
clearance, and mating radius. Preserve an actual open socket where it will be visible.
During threaded engagement, couple translation and rotation:
`axial_travel = pitch * angle_radians / TAU`, with signs chosen for the thread handedness.
The current three turns, 0.10-unit pitch, and 13.5-second sequence are prototype values,
not universal proportions or timings. Lift clear before moving sideways; align the
replacement before engaging. Derive poses from timeline time so backward scrubbing,
pause, and replay do not accumulate rotation or position drift.

Cache rigid modules' local samples and geometry. Re-sort transparency when orientation
changes rather than resampling shapes per frame. Use world-space shading for moving
parts so highlights do not rotate as painted marks; steel can add view-dependent shine.
Profile before replacing the current renderer. A successful single-piece benchmark
does not establish full-board performance.
Measure continuous camera motion as well as idle frames: the initial 32-piece
preview appeared fast at rest but stalled on CPU sorting during orbit. The board
now uses `depth_tested_splats` (crisper GPU cutouts, no per-frame sorting); the
piece gallery retains soft blending. Inspect the visual tradeoff when choosing a
mode, and do not report an idle FPS measurement as interactive performance.

## Verification and handoff

- Inspect assembled and detached states, the exposed dowel, and intermediate clearance.
  Check silhouette, socket fit, edge fuzz, and material identity at the intended game scale.
- Run the full animation in Godot and inspect pixel/palette and raw rendering as relevant.
  Check changed controls, pause/replay, backward scrubbing, and phase continuity.
- For attachment or timing changes, verify thread-pitch coupling and final seated poses
  after the scene is ready. Headless checks cannot substitute for visual shader validation.
- Use available Godot connector tools to inspect APIs, run scenes, and measure performance.
  If the connector reports a user-owned running game, do not replace it or retry until
  the user confirms it has stopped. Continue independent checks and state any remaining
  visual-validation limitation.
- Report the concrete change, checks actually completed, and relevant limitations.
  Leave the intended preview scene selected when practical. Update experiment notes for
  implementation changes; update this skill only for new durable preferences or lessons.
