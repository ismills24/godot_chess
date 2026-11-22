import bpy,json,os
from pathlib import Path
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.device='CPU';s.render.threads_mode='FIXED';s.render.threads=2
rows=[]
for o in s.objects:
 if o.type=='MESH': rows.append({'name':o.name,'vertices':len(o.data.vertices),'faces':len(o.data.polygons),'mesh':o.data.name,'users':o.data.users,'modifiers':[{'name':m.name,'type':m.type,'render':m.show_render,'levels':getattr(m,'render_levels',None)} for m in o.modifiers]})
r={'objects':len(s.objects),'meshes':len(bpy.data.meshes),'unique_vertices':sum(len(m.vertices) for m in bpy.data.meshes),'unique_faces':sum(len(m.polygons) for m in bpy.data.meshes),'largest':sorted(rows,key=lambda x:x['vertices'],reverse=True)[:30],'modifier_counts':{},'images':[{'name':i.name,'packed_bytes':i.packed_file.size if i.packed_file else 0,'users':i.users,'float':i.is_float} for i in bpy.data.images]}
for row in rows:
 for m in row['modifiers']:r['modifier_counts'][m['type']]=r['modifier_counts'].get(m['type'],0)+1
Path('artifacts/victorian_alley_v2/optimization_audit.json').write_text(json.dumps(r,indent=2));print(json.dumps({k:v for k,v in r.items() if k!='images'},indent=2),flush=True)
