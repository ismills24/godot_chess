# Local image-to-3D asset harness

Agent-neutral commands for Codex, Claude, or a person. No Claude dependency, paid service,
custom ComfyUI node pack, or Blender add-on is required. This does not replace gameplay assets automatically.

## Installed on this machine

- Blender: 5.3.0, path set per machine (see below).
- ComfyUI portable: `<repo>/.tools/ComfyUI_windows_portable` (pinned 0.46.0, Python 3.13, PyTorch 2.14.1 CUDA 13.0).
- ComfyUI UI/API: `http://127.0.0.1:8188` (loopback only).
- Paths and endpoint: `tools/asset_harness/config.json`.
- Per-machine overrides: an optional, git-ignored `tools/asset_harness/config.local.json`
  whose keys replace those in `config.json` (e.g. `{"blender": "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe"}`).
  A `BLENDER` environment variable overrides both. Priority: `BLENDER` > `config.local.json` > `config.json`.
  `python tools/asset_harness/harness_config.py blender` prints the resolved path.
- Weights: `<portable>/ComfyUI/models`; five SHA-256-verified safetensors files, approximately 9 GB.
- Jobs, generated GLBs, logs and working Blender files: `artifacts/asset_harness/`.
- The large tools/weights and outputs are ignored by Git and Godot. Keep source scripts/workflows in Git.

From the repository root, start the service and check dependencies:

```powershell
./tools/asset_harness/start_comfy.ps1
python tools/asset_harness/harness.py doctor
```

The service runs in a hidden background process. Its PID is in `.tools/comfy.pid` and logs
are `.tools/comfy.stdout.log` and `.tools/comfy.stderr.log`. Startup takes a little time.
The launcher reuses a responding endpoint; `doctor` checks its node and model availability.
Do not stop an unrelated ComfyUI instance or cancel someone else's queued work.

To reproduce the installation, run `install_portable.ps1`. It downloads a pinned official
archive, checks its SHA-256, extracts it, then downloads and verifies the model files.
It reuses the existing Blender path; on another machine set it in `config.local.json`, not `config.json`.
An existing archive/model with the wrong checksum causes an error, not silent replacement.
The embedded Python at `<portable>/python_embeded/python.exe` can run these CLI scripts too.

## Reference requirements

Use one centered subject, fully visible, with a clean background and enough margin.
TRELLIS.2 is image-to-3D: there is no text prompt in this workflow that can force a new pose.
For humanoids, first create or edit the reference to an A- or T-pose, palms visible, fingers
clearly spread, limbs separated, feet apart, no props occluding hands, and no cropped extremities.
Inspect the reference before acknowledging `--pose-confirmed`. Acknowledgement is a human/agent
review gate, not a pose classifier. Inspect the resulting mesh's hands as well.
For chess pieces, generate each detachable module separately when practical. Preserve the
project's physical socket/seat contract when refining and assembling them in Blender/Godot.

## Package the 3D chess menu props

The alley uses the weathered barrel from TRELLIS.2 job `8c2492536794` and existing
Poly Haven assets from `artifacts/polyhaven_sources/`. It reuses the Hidden Alley
brick and plaster materials, Cobblestone Floor 01, and Handsaw Wood. The optimized
alley package under `artifacts/victorian_alley_v2/optimized_textures/` supplies the
portable brick normal and roughness maps. Package those files with the portable Python:

```powershell
& ./.tools/ComfyUI_windows_portable/python_embeded/python.exe tools/asset_harness/prepare_menu_props.py
```

Godot imports the packaged files from `menu/props/` and renders and lights the scene
in real time. The source Blender scenes and ComfyUI jobs stay under `artifacts/`.

The factory frontage, fire escape, stove, boards, wicker basket, and spade come
from `artifacts/victorian_alley_v2/victorian_alley_optimized.blend`. The exporter
appends only those meshes, keeps the architectural assemblies in source coordinates,
and trims the prop meshes and textures for the runtime scene:

```powershell
$blender = python tools/asset_harness/harness_config.py blender
& $blender --background --factory-startup --disable-autoexec --threads 1 --python-exit-code 1 --python tools/asset_harness/export_menu_alley.py
```

The factory export cuts the two camera-side arches into the brick mesh and adds
shallow masonry reveals around the recessed panes. The menu reuses the PS1 pawn
study and applies a lightly filtered low-resolution image and subtle 5-bit color
dithering to the live scene.

## Generate

### Inn sign metalwork

The Hanging Pawn's beveled brass lettering and pierced pawn plaque are authored precisely
in Blender, with the existing harness configuration. Rebuild the small runtime GLB with:

```powershell
$blender = python tools/asset_harness/harness_config.py blender
& $blender --background --factory-startup --disable-autoexec --threads 1 --python-exit-code 1 --python tools/asset_harness/export_inn_sign.py
```

This writes `inn/props/hanging_pawn_sign.glb` and retains the source `.blend` and report in
`artifacts/asset_harness/inn_sign/`. Georgia Bold is used by default; on another machine,
append `-- --font /path/to/bold-serif.ttf`. The mesh contains the lettering, a fixed nail,
and `PawnPivot`, whose origin is the hole in the pawn's base. `inn/street.gd` supplies the
level wooden board and rotates only `PawnPivot` around local Z, centered on PI radians so
the pawn hangs upside down. No font file or Blender installation is needed at runtime.

### Image-to-3D

```powershell
python tools/asset_harness/harness.py generate C:/path/reference.png --wait
python tools/asset_harness/harness.py generate C:/path/reference.png --textured --wait
python tools/asset_harness/harness.py generate C:/path/character.png --humanoid --pose-confirmed --wait
```

Without `--wait`, submission returns immediately with a job manifest. Resume inspection with:

```powershell
python tools/asset_harness/harness.py status artifacts/asset_harness/JOB_ID/job.json
```

Every job stores the exact submitted API prompt, seed, input path, ComfyUI history and model output.
The default seed is 42. `--timeout` limits waiting, not the job itself; never resubmit solely because
a client wait timed out. Errors return nonzero and preserve the history for diagnosis.

Two editable UI graphs and matching API graphs are included:

- `trellis2_512_geometry.workflow.json` / `.api.json`: base shape at 512 resolution.
- `trellis2_512_textured.workflow.json` / `.api.json`: shape plus PBR texture generation and 1024 atlas.

Open the `.workflow.json` file in ComfyUI and select your reference in LoadImage. Both are also
copied into this installation's `ComfyUI/user/default/workflows/Asset Harness` folder.
The native int8 TRELLIS.2 model is selected directly. No unused Pixal3D diffusion or MoGe model
is required. The DINO image encoder comes from Comfy-Org's compatible Pixal3D repository.
The 512 shape pass, one-image batches and reserved VRAM were tested on this 12 GB RTX 3080 Ti.
This is a baseline, not a promise that every complex image fits. Raw meshes can exceed a million
triangles. The textured workflow now reduces to 25,000 faces before UV unwrapping and
bakes against the original dense shape. This avoids the excessive unwrap cost observed
on the modular pawn references. Geometry-only output preserves the dense source.

## Refine, repair, retopologize

```powershell
python tools/asset_harness/harness.py process raw.glb artifacts/asset_harness/my_refinement --repair --quad --target-faces 6000 --height 2
```

The output directory must be empty. The script imports into a factory-startup background Blender,
preserves the original high-resolution source hidden in `asset.blend`, centers the result over its
foot at zero, and normalizes height. It cleans duplicate/degenerate geometry, optionally voxel-remeshes
an open/non-manifold input, and optionally runs QuadriFlow. Without `--quad`, the reduction uses
decimation; this is explicitly not manual animation retopology. `--target-faces` is a polygon budget,
not a triangle budget. Quads approximately double their count when triangulated.

Output: `asset.blend`, `report.json`, and, on validation success, `asset.glb`.
Reports check boundary and non-manifold edges/vertices, loose vertices, degenerate faces and volume.
Self-intersections and internal cavities are not automatically certified. Inspect the shape before
calling it production-ready or fabrication-ready. Thin fingers and sockets can fuse during voxel repair;
keep the source and adjust/repair those features deliberately. QuadriFlow cancellation fails validation.

Voxel/quad remeshing invalidates inherited UVs. In that case the output uses a neutral material and
the report sets `texture_rebake_required=true`; textures must be rebaked from the preserved high-poly
source or replaced with authored materials in Blender. The harness does not pretend to complete that
art pass. Review silhouette, socket fit, detail loss, scale and shading before copying a GLB into assets.

For a geometry preview:

```powershell
$blender = python tools/asset_harness/harness_config.py blender
& $blender --background --factory-startup --python tools/asset_harness/render_preview.py -- processed/asset.glb preview.png
```

## Optional rigging

Fit a bone landmark JSON to the actual normalized mesh. `rig.example.json` illustrates the schema;
its generic T-pose joints are not a fitted rig. Coordinates are Blender Z-up fractions of final height,
with the feet at zero. Supply `name`, `head`, `tail`, optional `parent`, and optional `deform` for each bone.
Add explicit finger chains for humanoid hands and fit them to each generated finger.

```powershell
python tools/asset_harness/harness.py process raw.glb artifacts/asset_harness/rigged --repair --quad --rig fitted_rig.json
```

The script builds the armature, applies Blender's automatic weights, and rejects export if vertices
have no deform-bone weights. It checks invalid/cyclic bone hierarchies. Automatic weights are only
a starting point: bend every joint, inspect shoulders/hips/fingers, fix weights and animation edge loops,
then validate the exported skeleton in Godot. A pawn fixture verified armature/skin export; no humanoid
deformation quality has been validated yet. For rigid chess modules, do not add a humanoid rig.

## Verified smoke tests (2026-11-20)

- Official portable archive and all five model files downloaded; weight SHA-256 checks passed.
- Portable Blender starts in background; ComfyUI sees the 12 GB GPU and all required nodes/models.
- Synthetic pawn reference → native TRELLIS.2 geometry GLB (about 13 seconds for this sample).
- Same reference → textured GLB (about 91 seconds for this sample).
- Generated geometry → voxel repair → QuadriFlow: 1,999 quads, no boundary/non-manifold edges or vertices.
- Fixture → 2-bone skin → GLB: zero unweighted vertices. This tests mechanics, not humanoid anatomy.
- Original source remains in the .blend; failed attempts retain reports and do not export a passing GLB.

Source instructions and upstream architecture:

- [Official portable ComfyUI release](https://github.com/Comfy-Org/ComfyUI/releases/tag/v0.46.0)
- [Official TRELLIS.2 workflow documentation](https://docs.comfy.org/tutorials/3d/trellis2)
- [Official combined template used as the pipeline reference](https://github.com/Comfy-Org/workflow_templates/blob/main/templates/3d_pixal3d_trellis2_image_to_model.json)
- [Microsoft TRELLIS.2](https://github.com/microsoft/TRELLIS.2)
- [Comfy-Org native model weights](https://huggingface.co/Comfy-Org/TRELLIS.2)

`build_workflows.py` rebuilds the graphs from the installed `/object_info` schema saved to
`.tools/comfy_object_info.json`. Model download metadata is in `.tools/model_manifest.json`.
