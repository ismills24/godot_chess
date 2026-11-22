import bpy,json
from pathlib import Path
from mathutils import Vector
out=Path('artifacts/victorian_alley_v2')
s=bpy.context.scene
rows=[]
for o in s.objects:
 pts=[o.matrix_world@Vector(v) for v in o.bound_box] if o.type=='MESH' else [o.location]
 rows.append({'name':o.name,'type':o.type,'loc':list(o.location),'size':list(o.dimensions),'min':[min(v[i] for v in pts) for i in range(3)],'max':[max(v[i] for v in pts) for i in range(3)],'collection':[c.name for c in o.users_collection],'hide':o.hide_render,'materials':[m.name for m in o.data.materials if m] if o.type=='MESH' else []})
(out/'source_inventory.json').write_text(json.dumps({'objects':rows,'collections':[c.name for c in bpy.data.collections],'camera':s.camera.name if s.camera else None},indent=2))
print('CAMERAS',[(o.name,list(o.location),list(o.rotation_euler),o.data.lens) for o in s.objects if o.type=='CAMERA'])
print('COLLECTIONS',[(c.name,len(c.objects)) for c in bpy.data.collections])
s.render.engine='CYCLES'; s.cycles.samples=12; s.cycles.use_denoising=True
p=bpy.context.preferences.addons['cycles'].preferences;p.compute_device_type='OPTIX';p.get_devices()
for d in p.devices:d.use=d.type=='OPTIX'
s.cycles.device='GPU'; s.render.resolution_x=1000;s.render.resolution_y=650;s.render.resolution_percentage=70;s.render.filepath=str(out/'source_preview.png');s.render.image_settings.file_format='PNG'
s.render.use_simplify=True;s.render.simplify_subdivision_render=1;s.render.simplify_child_particles_render=.1
bpy.ops.render.render(write_still=True)
