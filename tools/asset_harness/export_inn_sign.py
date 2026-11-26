"""Author the inn sign's stamped brass lettering and loose pawn, then export to Godot.

Uses the harness's configured Blender, without an image-to-3D inference step:
  $blender = python tools/asset_harness/harness_config.py blender
  & $blender --background --factory-startup --python-exit-code 1 --python tools/asset_harness/export_inn_sign.py

Optional font override: append -- --font /path/to/a/bold-serif.ttf.
The wooden board is still built by street.gd. PawnPivot is authored upright around
the nail through its base; Godot rotates it by PI and animates that pivot alone.
"""
import argparse
import json
import math
from pathlib import Path
import sys

import bpy
import bmesh
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "inn/props/hanging_pawn_sign.glb"
WORK = ROOT / "artifacts/asset_harness/inn_sign"
PIVOT = (0.66, -0.26, 0.068)


def point(x, y, z):
    """Author in Godot coordinates: x right, y up, z out of the sign."""
    return Vector((x, -z, y))


def material(name, color, metallic, roughness, fill=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    rgb = [int(color[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    linear = [v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in rgb]
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*linear, 1)
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    # A small baked-light fill keeps the far end legible in the game's low-res night.
    shader.inputs["Emission Color"].default_value = (*linear, 1)
    shader.inputs["Emission Strength"].default_value = fill
    return mat


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def bevel(obj, width):
    activate(obj)
    modifier = obj.modifiers.new("Stamped edge", "BEVEL")
    modifier.width = width
    modifier.segments = 1
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def clean_mesh(obj):
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    bmesh.ops.remove_doubles(mesh, verts=list(mesh.verts), dist=0.000001)
    bmesh.ops.dissolve_degenerate(mesh, edges=list(mesh.edges), dist=0.000001)
    bmesh.ops.recalc_face_normals(mesh, faces=list(mesh.faces))
    mesh.to_mesh(obj.data)
    mesh.free()
    obj.data.update()


def validate_pawn(obj):
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    problems = {
        "nonmanifold_edges": sum(not e.is_manifold for e in mesh.edges),
        "degenerate_edges": sum(e.calc_length() < 0.000001 for e in mesh.edges),
        "degenerate_faces": sum(f.calc_area() < 1e-10 for f in mesh.faces),
    }
    mesh.free()
    if any(problems.values()):
        raise ValueError(f"Pawn mesh failed export validation: {problems}")
    return problems


def cylinder(name, at, radius, depth, mat=None, parent=None, vertices=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth)
    obj = bpy.context.object
    obj.name = name
    obj.parent = parent
    obj.location = point(*at)
    obj.rotation_euler.x = math.pi / 2
    if mat:
        obj.data.materials.append(mat)
    return obj


def punch(obj, name, x, y, radius):
    cutter = cylinder(name, (x, y, 0), radius, 0.10, parent=obj.parent)
    activate(obj)
    modifier = obj.modifiers.new(name, "BOOLEAN")
    modifier.operation = "DIFFERENCE"
    modifier.solver = "EXACT"
    modifier.object = cutter
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.data.objects.remove(cutter, do_unlink=True)


def finish_stamp(obj, brass, edge, side, front):
    """Keep the chamfer readable after the game's low-resolution color reduction."""
    obj.data.materials.clear()
    for mat in (brass, edge, side):
        obj.data.materials.append(mat)
    obj.data.update()
    for polygon in obj.data.polygons:
        facing = polygon.normal.dot(front)
        polygon.material_index = 0 if facing > 0.99 else (1 if facing > 0.15 else 2)


def lettering(name, text, center, height, max_width, font, brass, edge, side):
    curve = bpy.data.curves.new(name, "FONT")
    curve.body = text
    curve.font = font
    curve.resolution_u = 4
    curve.size = 1
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.update()
    curve.size *= min(height / obj.dimensions.y, max_width / obj.dimensions.x)
    # Shallow extrusion and a chamfer make a physical stamped edge in lamplight.
    curve.extrude = 0.009
    curve.bevel_depth = 0.0025
    curve.bevel_resolution = 0
    activate(obj)
    bpy.ops.object.convert(target="MESH")
    finish_stamp(obj, brass, edge, side, Vector((0, 0, 1)))
    low = Vector(tuple(min(v.co[i] for v in obj.data.vertices) for i in range(3)))
    high = Vector(tuple(max(v.co[i] for v in obj.data.vertices) for i in range(3)))
    middle = (low + high) / 2
    for vertex in obj.data.vertices:
        vertex.co = point(vertex.co.x - middle.x + center[0],
                          vertex.co.y - middle.y + center[1],
                          vertex.co.z - low.z + 0.031)
    obj.data.update()


def pawn(brass, edge, side, iron, dark, worn):
    pivot = bpy.data.objects.new("PawnPivot", None)
    bpy.context.collection.objects.link(pivot)
    pivot.location = point(*PIVOT)
    # Exaggerate the round head and narrow neck so the inverted pawn still reads at
    # the game's 704px render size. Arc endpoints are included only once: the old
    # near-duplicate points pinched the bevel into almost zero-width slivers.
    right = [(0.112, -0.025), (0.112, 0.008), (0.095, 0.008),
             (0.095, 0.037), (0.078, 0.052), (0.063, 0.078),
             (0.050, 0.112), (0.038, 0.150), (0.028, 0.180),
             (0.028, 0.190), (0.058, 0.190), (0.058, 0.212),
             (0.028, 0.212), (0.028, 0.232)]
    outline = right + [(0.072 * math.cos(math.radians(angle)),
                        0.300 + 0.072 * math.sin(math.radians(angle)))
                       for angle in range(-60, 241, 20)]
    outline.extend((-x, y) for x, y in reversed(right))
    count = len(outline)
    vertices = [point(x, y, depth) for depth in (-0.009, 0.009) for x, y in outline]
    faces = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
    faces.extend((i, (i + 1) % count, (i + 1) % count + count, i + count)
                 for i in range(count))
    mesh = bpy.data.meshes.new("Pawn plaque")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("LoosePawn", mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = pivot
    obj.data.materials.append(brass)
    punch(obj, "Base nail hole", 0, 0, 0.011)
    punch(obj, "Lost head nail hole", 0, 0.300, 0.006)
    clean_mesh(obj)
    bevel(obj, 0.003)
    clean_mesh(obj)
    # Resolve boolean ngons here, before glTF export and Godot's LOD generation.
    activate(obj)
    triangles = obj.modifiers.new("Stable plate triangles", "TRIANGULATE")
    bpy.ops.object.modifier_apply(modifier=triangles.name)
    finish_stamp(obj, brass, edge, side, Vector((0, -1, 0)))
    validation = validate_pawn(obj)
    # Keep the nail fixed to the board, outside PawnPivot's moving hierarchy.
    cylinder("NailShank", (PIVOT[0], PIVOT[1], 0.059), 0.008, 0.075, iron)
    head = cylinder("NailHead", (PIVOT[0], PIVOT[1], 0.101), 0.024, 0.014, iron, vertices=8)
    bevel(head, 0.002)
    # The empty mounting point where the pawn's head used to be attached upright.
    old_hole = (PIVOT[0], PIVOT[1] + 0.300)
    cylinder("OldNailScar", (*old_hole, 0.030), 0.013, 0.002, worn)
    cylinder("OldNailHole", (*old_hole, 0.032), 0.007, 0.002, dark)
    return validation


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--font", type=Path, default=Path("C:/Windows/Fonts/georgiab.ttf"))
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    if not args.font.is_file():
        raise FileNotFoundError("Pass -- --font with an installed bold serif TTF.")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    brass = material("Stamped aged brass", "d8a458", 0.30, 0.48, 0.27)
    edge = material("Worn brass bevel", "f0c781", 0.30, 0.40, 0.28)
    side = material("Tarnished brass edge", "77512b", 0.30, 0.62, 0.10)
    iron = material("Worn nail iron", "99928a", 0.30, 0.58, 0.16)
    dark = material("Empty nail hole", "100c08", 0.0, 1.0)
    worn = material("Exposed wood at old nail", "63412a", 0.0, 0.95)
    font = bpy.data.fonts.load(str(args.font))
    lettering("TheHanging", "THE HANGING", (0, 0.155), 0.122, 1.49, font, brass, edge, side)
    lettering("PawnWord", "PAWN", (-0.16, -0.095), 0.235, 1.13, font, brass, edge, side)
    # The moving plaque should pick up the lantern, rather than glow like a label.
    pawn_brass = material("Pawn brass", "d8a458", 0.25, 0.52, 0.08)
    pawn_edge = material("Pawn bevel", "e7b970", 0.25, 0.46, 0.08)
    pawn_side = material("Pawn edge patina", "876139", 0.25, 0.65, 0.04)
    pawn_validation = pawn(pawn_brass, pawn_edge, pawn_side, iron, dark, worn)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    WORK.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK / "sign.blend"))
    bpy.ops.export_scene.gltf(filepath=str(OUTPUT), export_format="GLB",
                              export_apply=True, export_yup=True, export_cameras=False,
                              export_lights=False)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    report = {"asset": str(OUTPUT.relative_to(ROOT)), "font": args.font.name,
              "triangles": sum(sum(len(face.vertices) - 2 for face in obj.data.polygons) for obj in meshes),
              "bytes": OUTPUT.stat().st_size, "pawn_pivot": PIVOT,
              "pawn_mesh_validation": pawn_validation,
              "animation": "Rotate PawnPivot around local Z, centered on PI radians."}
    (WORK / "report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


main()
