"""Trim the downloaded Poly Haven inn props into runtime GLBs under inn/props/.

Run in headless Blender after tools/scene_builders/download_inn_props.py:
  $blender = python tools/asset_harness/harness_config.py blender
  & $blender --background --factory-startup --python-exit-code 1 --python tools/asset_harness/export_inn_props.py

Each model is decimated to a triangle budget and exported with its textures embedded. The
textures come pre-shrunk from the download step's `small/` copy; Blender must not re-encode
them, because Godot cannot decode the JPEGs Blender writes. Placement and scale happen in Godot (`_place_prop` scales to a height).
"""
import json
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[2]
SOURCES = ROOT / 'artifacts/polyhaven_sources'
DESTINATION = ROOT / 'inn/props'
DEFAULT_BUDGET = 4000
# Runtime name -> (Poly Haven id, triangle budget)
PROPS = {
    'round_table': ('round_wooden_table_01', 3000),
    'plank_table': ('gallinera_table', 2500),
    'stool': ('wooden_stool_01', 2500),
    'stool_low': ('wooden_stool_02', 2500),
    'bench': ('painted_wooden_bench', DEFAULT_BUDGET),
    'wine_barrel': ('wine_barrel_01', 3000),
    'barrel': ('Barrel_01', 2500),
    'bottles': ('wine_bottles_01', 6000),
    'jug': ('jug_01', 2000),
    'goblets': ('brass_goblets', 3000),
    'candlestick': ('wooden_candlestick', 1500),
    'shelf': ('Shelf_01', DEFAULT_BUDGET),
    'bucket': ('wooden_bucket_01', 2000),
    'crate': ('wooden_crate_01', 2000),
    'chandelier': ('lantern_chandelier_01', 4000),
    'bowl': ('wooden_bowl_02', 1500),
    'plate': ('carved_wooden_plate', 1200),
    'oil_lamp': ('vintage_oil_lamp', 2500),
}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def triangles(obj):
    return sum(len(polygon.vertices) - 2 for polygon in obj.data.polygons)


def export(name, asset, budget):
    reset()
    bpy.ops.import_scene.gltf(filepath=str(SOURCES / asset / 'small' / f'{asset}_1k.gltf'))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    total = sum(triangles(obj) for obj in meshes)
    if total > budget:
        ratio = budget / total
        for obj in meshes:
            modifier = obj.modifiers.new('Budget', 'DECIMATE')
            modifier.ratio = ratio
            bpy.context.view_layer.objects.active = obj
            bpy.ops.object.modifier_apply(modifier=modifier.name)
    output = DESTINATION / f'{name}.glb'
    bpy.ops.export_scene.gltf(filepath=str(output), export_format='GLB', export_image_format='AUTO',
                              export_apply=True, export_yup=True)
    after = sum(triangles(obj) for obj in bpy.context.scene.objects if obj.type == 'MESH')
    return {'name': name, 'source': asset, 'triangles_before': total, 'triangles_after': after,
            'bytes': output.stat().st_size}


def main():
    DESTINATION.mkdir(parents=True, exist_ok=True)
    report = [export(name, asset, budget) for name, (asset, budget) in PROPS.items()]
    for row in report:
        print(f"{row['name']:12} {row['triangles_before']:>7} -> {row['triangles_after']:>6} tris  {row['bytes'] // 1024:>5} KB")
    (SOURCES / 'inn_export_report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')


main()
