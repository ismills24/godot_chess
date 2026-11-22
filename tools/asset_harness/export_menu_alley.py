"""Export the selected runtime modules from the optimized Blender alley.

Run with an empty Blender scene in background mode. This script appends selected
objects and writes GLBs. It does not open the source scene or invoke a renderer.
"""
from pathlib import Path
import json

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.geometry import convex_hull_2d


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "artifacts/victorian_alley_v2/victorian_alley_optimized.blend"
DESTINATION = ROOT / "menu/props"

FACTORY = (
    "wall_tripple_standard_window_02.003",
    "window_double_half_03.001",
    "window_tripple_standard_02.001",
    "window_tripple_standard_02.002",
    "window_tripple_standard_02.003",
    "window_single_standard_02.001",
    "window_single_standard_02.002",
    "door_single_standard_01.001",
    "door_double_standard_03.001",
    "door_quad_standard_01.001",
    "dado_double_door_03.001",
    "dado_quad_01.001",
    "pier_corner_cornice_02.001",
    "pier_corner_crown_02.001",
    "pier_single_cornice_01.005",
    "pier_single_cornice_01.007",
    "pier_single_crown_01.004",
    "pier_single_crown_01.003",
    "crown_01_double_01.002",
    "crown_01_double_01.003",
    "crown_01_double_01.004",
    "crown_01_tripple_01.001",
    "crown_01_tripple_01.002",
    "crown_01_tripple_01.003",
    "base_corner_02.001",
    "base_double_01.003",
    "base_double_01.005",
    "base_double_01.007",
    "cornice_01_double_01.003",
    "cornice_01_double_01.004",
    "cornice_01_double_01.011",
    "cornice_01_double_01.012",
    "pier_single_cornice_01.003",
    "pier_single_cornice_01.004",
)
GROUPS = {
    "factory_bay": FACTORY,
    "fire_escape": (
        "modular_fire_escape_railing_middle.001",
        "modular_fire_escape_platform_railing.001",
        "modular_fire_escape_platform_middle.001",
        "modular_fire_escape_stairs.001",
        "modular_fire_escape_ladder_bottom.001",
        "modular_fire_escape_railing_bottom.001",
        "modular_fire_escape_platform_bottom.001",
    ),
    "barrel_stove": ("barrel_stove.002",),
    "wicker_basket": ("Shed | wicker_basket_01",),
    "rusted_spade": ("Shed | rusted_spade_01",),
    "salvaged_boards": ("wooden_boards.001",),
}
REDUCTION = {
    FACTORY[0]: 0.49,
    "barrel_stove.002": 0.48,
    "Shed | wicker_basket_01": 0.36,
    "Shed | rusted_spade_01": 0.30,
}
MATERIALS = {}
SCALED_IMAGES = {}


def scaled_image(source, cap):
    key = (source.name, cap)
    if key not in SCALED_IMAGES:
        image = source.copy()
        image.name = source.name + f"_{cap}"
        width, height = image.size
        if max(width, height) > cap:
            ratio = cap / max(width, height)
            image.scale(round(width * ratio), round(height * ratio))
        # Material-only copies keep image packing and resizing away from the source scene.
        image.pack()
        SCALED_IMAGES[key] = image
    return SCALED_IMAGES[key]


def material_for(source, is_brick, lit):
    if source is None:
        return None
    name = source.name
    is_glass = "glass" in name.lower()
    key = (name, lit and is_glass)
    if key in MATERIALS:
        return MATERIALS[key]

    material = bpy.data.materials.new(name + (" | oil lit" if key[1] else " | runtime"))
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    surface = nodes.get("Principled BSDF")
    surface.inputs["Base Color"].default_value = (0.17, 0.13, 0.10, 1.0)
    surface.inputs["Roughness"].default_value = 0.78
    if is_glass:
        surface.inputs["Base Color"].default_value = (0.14, 0.081, 0.035, 1.0) if lit else (0.018, 0.024, 0.023, 1.0)
        surface.inputs["Roughness"].default_value = 0.55
        if lit:
            surface.inputs["Emission Color"].default_value = (0.50, 0.21, 0.068, 1.0)
            surface.inputs["Emission Strength"].default_value = 0.52
    elif source.node_tree:
        images = [n.image for n in source.node_tree.nodes if n.type == "TEX_IMAGE" and n.image]
        diffuse = next((i for i in images if "_diff" in i.name.lower()), None)
        normal = next((i for i in images if "_nor_gl" in i.name.lower()), None)
        cap = 768 if is_brick else 384
        if diffuse:
            tex = nodes.new("ShaderNodeTexImage")
            tex.image = scaled_image(diffuse, cap)
            links.new(tex.outputs["Color"], surface.inputs["Base Color"])
        if normal:
            tex = nodes.new("ShaderNodeTexImage")
            tex.image = scaled_image(normal, min(cap, 512))
            bump = nodes.new("ShaderNodeNormalMap")
            bump.inputs["Strength"].default_value = 0.82
            links.new(tex.outputs["Color"], bump.inputs["Color"])
            links.new(bump.outputs["Normal"], surface.inputs["Normal"])
    MATERIALS[key] = material
    return material


def prepare_object(obj):
    world = obj.matrix_world.copy()
    obj.data = obj.data.copy()
    obj.data.transform(world)
    obj.parent = None
    obj.matrix_world = Matrix.Identity(4)
    is_brick = obj.name == FACTORY[0]
    lit = obj.name in {
        "window_tripple_standard_02.002",
        "window_double_half_03.001",
        "window_single_standard_02.001",
    }
    if is_brick:
        # The Blend file uses a world-space material graph. Give glTF an explicit UV.
        uv = obj.data.uv_layers.active or obj.data.uv_layers.new()
        for face in obj.data.polygons:
            axis = 1 if abs(face.normal.x) > abs(face.normal.y) else 0
            for index in face.loop_indices:
                co = obj.data.vertices[obj.data.loops[index].vertex_index].co
                uv.data[index].uv = (co[axis] * 0.32, co.z * 0.32)

    for index, original in enumerate(list(obj.data.materials)):
        replacement = material_for(original, is_brick, lit)
        if replacement:
            obj.data.materials[index] = replacement

    ratio = REDUCTION.get(obj.name)
    if ratio is not None:
        bpy.context.view_layer.objects.active = obj
        modifier = obj.modifiers.new("Runtime triangle budget", "DECIMATE")
        modifier.ratio = ratio
        bpy.ops.object.modifier_apply(modifier=modifier.name)


def window_outline(window):
    glass_slots = {i for i, m in enumerate(window.data.materials) if m and "glass" in m.name.lower()}
    points = [
        window.matrix_world @ window.data.vertices[i].co
        for face in window.data.polygons if face.material_index in glass_slots
        for i in face.vertices
    ]
    plane = [Vector((p.y, p.z)) for p in points]
    outline = [plane[i] for i in convex_hull_2d(plane)]
    if (outline[1] - outline[0]).cross(outline[2] - outline[1]) < 0:
        outline.reverse()
    center = sum(outline, Vector((0.0, 0.0))) / len(outline)
    return [center + (p - center) * 1.032 for p in outline], points[0].x


def cut_window_opening(wall, outline, face_x):
    """Cut the arch out of the street face, including an open, single-sided wall."""
    mesh = bmesh.new()
    mesh.from_mesh(wall.data)
    low = (face_x - 0.10, min(p.x for p in outline), min(p.y for p in outline))
    high = (face_x + 0.28, max(p.x for p in outline), max(p.y for p in outline))

    def touches_opening(face):
        coords = [v.co for v in face.verts]
        return all(min(p[i] for p in coords) <= high[i] + 0.001 and
                   max(p[i] for p in coords) >= low[i] - 0.001 for i in range(3))

    edges = [(a, outline[(i + 1) % len(outline)] - a) for i, a in enumerate(outline)]
    for a, direction in edges:
        faces = [f for f in mesh.faces if touches_opening(f)]
        geometry = set(faces)
        for face in faces:
            geometry.update(face.edges)
            geometry.update(face.verts)
        bmesh.ops.bisect_plane(
            mesh, geom=list(geometry), dist=0.0001,
            plane_co=(face_x, a.x, a.y), plane_no=(0.0, -direction.y, direction.x),
        )

    removed = []
    for face in mesh.faces:
        midpoint = face.calc_center_median()
        if not low[0] <= midpoint.x <= high[0]:
            continue
        center = Vector((midpoint.y, midpoint.z))
        if all(direction.cross(center - a) >= -0.00008 for a, direction in edges):
            removed.append(face)
    bmesh.ops.delete(mesh, geom=removed, context="FACES")
    mesh.to_mesh(wall.data)
    mesh.free()
    return len(removed)


def add_window_reveal(window, wall, face_x):
    outline, glass_x = window_outline(window)
    removed = cut_window_opening(wall, outline, face_x)
    vertices = [(x, p.x, p.y) for x in (face_x - 0.002, glass_x + 0.012) for p in outline]
    count = len(outline)
    faces = []
    for i in range(count):
        next_i = (i + 1) % count
        faces.append((i, i + count, next_i + count, next_i))
    mesh = bpy.data.meshes.new("Brick window reveal")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(wall.data.materials[0])
    uv = mesh.uv_layers.new()
    for poly in mesh.polygons:
        for i in poly.loop_indices:
            at = mesh.vertices[mesh.loops[i].vertex_index].co
            uv.data[i].uv = ((at.y + at.x) * 0.32, (at.z + at.x) * 0.32)
    reveal = bpy.data.objects.new("Brick window reveal", mesh)
    bpy.context.collection.objects.link(reveal)
    reveal.select_set(True)
    print(f"Window opening {window.name}: {removed} clipped wall faces", flush=True)


def dress_camera_side(objects):
    wall = objects[FACTORY[0]]
    # Shift the stock cornice out of the opening and keep its authentic profile as a sill.
    sill = objects["cornice_01_double_01.004"]
    sill.location = Vector((-0.01, 0.02, -0.37))
    placements = (
        ("window_tripple_standard_02.002", 2.63),
        ("window_tripple_standard_02.001", 5.13),
    )
    for name, bottom in placements:
        source = objects[name]
        detail = source.copy()
        bpy.context.collection.objects.link(detail)
        center_y = 8.5 if name.endswith(".002") else 5.5
        detail.scale = Vector((0.91, 0.82, 1.0))
        detail.location = Vector((2.385 - 3.97 * detail.scale.x, -1.01 - center_y * detail.scale.y, bottom - 6.475))
        detail.select_set(True)
        bpy.context.view_layer.update()
        add_window_reveal(detail, wall, 2.4)

    upper_sill = sill.copy()
    bpy.context.collection.objects.link(upper_sill)
    upper_sill.location.z += 2.50
    upper_sill.select_set(True)
    for suffix in (".001", ".002"):
        add_window_reveal(objects["window_single_standard_02" + suffix], wall, 2.0)


def main():
    names = {name for group in GROUPS.values() for name in group}
    with bpy.data.libraries.load(str(SOURCE), link=False) as (source, selected):
        missing = names.difference(source.objects)
        if missing:
            raise RuntimeError(f"Missing source meshes: {sorted(missing)}")
        selected.objects = sorted(names)
    objects = dict(zip(sorted(names), selected.objects))
    for obj in objects.values():
        if obj.type != "MESH":
            raise TypeError(obj.name)
        bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.update()

    DESTINATION.mkdir(parents=True, exist_ok=True)
    summary = {}
    for group, sources in GROUPS.items():
        bpy.ops.object.select_all(action="DESELECT")
        batch = [objects[name] for name in sources]
        for obj in batch:
            # Appended meshes can carry source-scene modifiers with dependencies.
            obj.modifiers.clear()
            prepare_object(obj)
            obj.select_set(True)

        if group == "factory_bay":
            dress_camera_side(objects)
        bpy.context.view_layer.objects.active = batch[0]
        if len(batch) > 1:
            bpy.ops.object.join()
        result = bpy.context.view_layer.objects.active
        result.name = group
        result.data.calc_loop_triangles()
        summary[group] = len(result.data.loop_triangles)
        bpy.ops.export_scene.gltf(
            filepath=str(DESTINATION / (group + ".glb")),
            export_format="GLB",
            use_selection=True,
            export_animations=False,
            export_cameras=False,
            export_lights=False,
        )
        result.select_set(False)
    print("Runtime triangles:", json.dumps(summary, sort_keys=True), flush=True)


if __name__ == "__main__":
    main()
