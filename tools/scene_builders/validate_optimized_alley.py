import bpy,json
from pathlib import Path
s=bpy.context.scene;p=Path(bpy.data.filepath);images=[i for i in bpy.data.images if i.source=='FILE' and i.type=='IMAGE']
rows=[]
for i in images:
 rows.append({'name':i.name,'size':list(i.size),'packed':bool(i.packed_file),'float':i.is_float});i.buffers_free()
assert s.cycles.device=='CPU'
assert s.render.threads_mode=='FIXED' and s.render.threads<=4
assert all(r['packed'] and max(r['size'])<=1024 for r in rows)
assert not any(m.type=='SUBSURF' and m.show_render for o in s.objects for m in o.modifiers)
assert s.camera and bpy.data.objects.get('Blue green atmospheric fog')
assert any(o.name.startswith('Shed |') for o in s.objects)
r={'reopened_successfully':True,'packed_images':len(rows),'float_images':sum(i['float'] for i in rows),'max_texture_dimension':max(max(i['size']) for i in rows),'CPU_only':True,'threads':s.render.threads,'render_pixels':[round(s.render.resolution_x*s.render.resolution_percentage/100),round(s.render.resolution_y*s.render.resolution_percentage/100)],'bytes_on_disk':p.stat().st_size,'render_performed':False}
(p.parent/'optimization_validation.json').write_text(json.dumps(r,indent=2));print(json.dumps(r,indent=2),flush=True)
