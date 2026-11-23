# Procedural slum containers

Open `res://experiments/shop/ShopPreview.tscn` and run F6. Select any of the five
containers, then adjust width, height and depth. The preview also has a wear seed,
animated resizing, reset, right-drag orbit and wheel zoom. It uses the main menu's
704-pixel render stage and `menu/pixel_finish.gdshader`.

## Reusable assets

- `shop_containers.gd` exposes `create_wine_crate(size, seed, wear)` and
  `create_cardboard_box(size, seed, wear)`. The preview uses these same helpers.
  Both return an unparented container with its mesh and anchors already built.
- `WineCrate.tscn` and `CardboardBox.tscn` are standalone, editor-visible Node3D scenes.
- `shop_container.gd` builds low-poly geometry with fixed material thickness and
  dimension-based UVs. Changing dimensions rebuilds the mesh at unit node scale;
  wood grain does not stretch. Crates add boards as dimensions increase.
- Local origin is bottom centre, +Y up, with the display opening facing +Z.
- `dimensions` describes the main shell; cardboard flaps project beyond it.
- `ItemAnchor` is just above the interior floor; `PriceTagAnchor` sits at the
  front lower lip. The preview offsets tags farther forward to clear its shelves.
- `usable_size()` returns the interior dimensions. The preview fits existing chess
  piece scenes and two placeholder artifacts into these bounds.
- `wear_seed` determines panel tints, UV offsets, broken slats and bent flaps.
  It is independent of inventory. No physics or purchase/run logic is included.
- Supported shell dimensions: X 0.55–3.2, Y 0.45–2.6, Z 0.45–2.0 preview units.
  Invalid and out-of-range dimensions are sanitized.

```gdscript
const Containers = preload("res://assets/shop/shop_containers.gd")

var crate = Containers.create_wine_crate(Vector3(2.0, 1.4, 1.1), 42)
add_child(crate)
var box = Containers.create_cardboard_box(Vector3(1.6, 0.8, 1.0), 43, 0.8)
add_child(box)

# Optional: attach your own item and tag nodes to the stable anchors.
crate.item_anchor.add_child(item_scene.instantiate())
crate.tag_anchor.add_child(price_tag_scene.instantiate())

# Immediate rebuilds preserve attached children, node transforms and texture scale.
crate.resize(Vector3(2.5, 1.4, 1.1))
box.set_wear(44, 0.9)
var available_space = crate.usable_size()
```

Factory arguments are width/height/depth, an integer wear seed, and wear in 0–1.
The default sizes are `(1.8, 1.3, 1.0)` for crates and `(1.6, 0.8, 1.0)` for boxes;
both default to seed 17 and wear 0.6. Containers can be configured before parenting.
Direct edits to exported properties still coalesce into a deferred rebuild when
inside the scene tree. The helpers have no dependency on the preview or game rules.

## Textures

`textures/wine_crate_wood.png` and `textures/worn_cardboard.png` are generated
albedo sources. Their checked-in Godot import settings limit the runtime textures
to **128 × 128**, lossless, no mipmaps; the materials use nearest filtering.
The original source resolution is retained for future revisions. The built-in
imagegen prompts are recorded in `textures/generation-prompts.json`.

The material tiles contain grain/paper and stains only. Panel edges, slat gaps,
thickness, dents and flaps are actual geometry, so the assets can change size.

## Verification and captures

Run with a local Godot 4.7 executable:

```text
godot --headless --path . --script res://experiments/shop/verify_containers.gd
godot --headless --path . res://experiments/shop/ShopPreview.tscn -- --verify-shop
godot --path . res://experiments/shop/ShopPreview.tscn --resolution 1600x900 -- --capture-shop
```

The first check covers runtime texture sizes, extreme dimensions, finite geometry,
triangle winding, anchors, deterministic rebuilds, invalid input, both factory
helpers and preservation of attached children through resizing/reseeding. The second
exercises the actual UI signals, reseeding, reset and resizing animation. Capture
mode writes `artifacts/shop/shop-default.png` and `artifacts/shop/shop-resized.png`
before exiting. These are real engine renders. The environment is an art workbench;
the bridge and artifact props are placeholders for the eventual shop stage.
