"""Blender --background --factory-startup --python render_preview.py -- model.glb preview.png"""
import sys
from pathlib import Path
import bpy
from mathutils import Vector

source, destination = sys.argv[sys.argv.index('--')+1:]
destination=Path(destination).resolve()
if destination.exists(): raise ValueError('Preview already exists; choose a new path')
destination.parent.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(Path(source).resolve()))
points=[o.matrix_world @ Vector(c) for o in bpy.context.scene.objects if o.type=='MESH' for c in o.bound_box]
lo=Vector(tuple(min(p[i] for p in points) for i in range(3))); hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
center=(lo+hi)/2; extent=max(hi-lo)
bpy.ops.object.camera_add(location=center+Vector((1.8,-3,1.5))*extent)
camera=bpy.context.object; camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO'; camera.data.ortho_scale=extent*1.4
scene=bpy.context.scene; scene.camera=camera; scene.render.engine='BLENDER_WORKBENCH'
scene.display.shading.light='STUDIO'; scene.display.shading.color_type='MATERIAL'
scene.display.shading.show_cavity=True; scene.display.shading.show_shadows=True
scene.render.resolution_x=768; scene.render.resolution_y=768; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'; scene.render.filepath=str(destination)
bpy.ops.render.render(write_still=True)
