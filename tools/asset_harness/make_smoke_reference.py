"""Synthetic pawn reference and known closed mesh for end-to-end harness checks."""
import bpy
import math
from mathutils import Vector
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = root / 'artifacts/asset_harness/smoke'
out.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
profile = [(0, 0), (0.48, 0), (0.5, 0.08), (0.48, 0.16), (0.34, 0.2), (0.26, 0.35),
           (0.18, 0.75), (0.16, 0.95), (0.23, 1.02), (0.23, 1.1), (0.15, 1.15)]
for i in range(1, 16):
    a = -math.pi / 2 + i * math.pi / 16
    profile.append((0.3 * math.cos(a), 1.4 + 0.3 * math.sin(a)))
profile.append((0, 1.7))
vertices = [(r * math.cos(j * math.tau / 64), r * math.sin(j * math.tau / 64), z)
            for r, z in profile for j in range(64)]
faces = [(i*64+j, i*64+(j+1)%64, (i+1)*64+(j+1)%64, (i+1)*64+j)
         for i in range(len(profile)-1) for j in range(64)]
mesh = bpy.data.meshes.new('Pawn'); mesh.from_pydata(vertices, [], faces); mesh.update()
obj = bpy.data.objects.new('Pawn', mesh); bpy.context.collection.objects.link(obj)
bpy.context.view_layer.objects.active = obj; obj.select_set(True)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=0.00001); bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.object.mode_set(mode='OBJECT')
for p in mesh.polygons: p.use_smooth = True
mat = bpy.data.materials.new('Maple'); mat.diffuse_color = (0.58, 0.35, 0.15, 1); obj.data.materials.append(mat)
bpy.ops.export_scene.gltf(filepath=str(out/'fixture.glb'), export_format='GLB', use_selection=True)
bpy.ops.object.camera_add(location=(3, -6, 2.5))
camera = bpy.context.object; camera.rotation_euler = (Vector((0,0,0.85))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO'; camera.data.ortho_scale=2.3
scene=bpy.context.scene; scene.camera=camera; scene.render.engine='BLENDER_WORKBENCH'
scene.display.shading.light='STUDIO'; scene.display.shading.color_type='MATERIAL'
scene.display.shading.show_shadows=True; scene.display.shading.show_cavity=True
scene.render.resolution_x=512; scene.render.resolution_y=512; scene.render.resolution_percentage=100
scene.render.film_transparent=True; scene.render.image_settings.file_format='PNG'
scene.render.filepath=str(out/'reference.png'); bpy.ops.render.render(write_still=True)
print('SMOKE_REFERENCE', scene.render.filepath)
