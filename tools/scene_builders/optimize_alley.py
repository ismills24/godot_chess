"""Bounded-memory, CPU-only optimization and save. Never renders."""
import bpy,json,os,time,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'artifacts/victorian_alley_v2';TEX=OUT/'optimized_textures';TEX.mkdir(exist_ok=True)
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.device='CPU';s.render.threads_mode='FIXED';s.render.threads=max(1,min(4,(os.cpu_count() or 4)//4))
def stats():
 meshes={o.data for o in s.objects if o.type=='MESH'}
 return {'objects':len(s.objects),'unique_meshes':len(meshes),'vertices':sum(len(m.vertices) for m in meshes),'faces':sum(len(m.polygons) for m in meshes),'triangles':sum(sum(len(p.vertices)-2 for p in m.polygons) for m in meshes)}
before=stats();removed=[];subdivisions=0;reductions=[]
for o in list(s.objects):
 if o.name in ['grass','ivy','weed_plants'] or 'potted_plant_01_pebbles' in o.name:
  removed.append(o.name);bpy.data.objects.remove(o,do_unlink=True)
for o in s.objects:
 for mod in list(o.modifiers):
  if mod.type=='SUBSURF':o.modifiers.remove(mod);subdivisions+=1
  elif mod.type=='BEVEL':mod.segments=1
  elif mod.type=='PARTICLE_SYSTEM':mod.show_render=False;mod.show_viewport=False
# Reduce only dense organic dressing; retain authored facade topology.
for o in list(s.objects):
 if o.type!='MESH':continue
 target=12000 if o.name=='stones' else 7000
 if (o.name=='stones' or 'tree_stump' in o.name or o.name.startswith('Shed |')) and len(o.data.polygons)>target*1.5:
  old=len(o.data.polygons);o.data=o.data.copy();m=o.modifiers.new('Optimized dressing','DECIMATE');m.ratio=target/old
  bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=m.name);reductions.append({'object':o.name,'before_faces':old,'after_faces':len(o.data.polygons)})
# Remove now-unreferenced source materials/textures, including legacy fake-user assets.
for m in bpy.data.materials:m.use_fake_user=False
for i in bpy.data.images:i.use_fake_user=False
bpy.ops.outliner.orphans_purge(do_recursive=True)
images=[]
s.render.image_settings.file_format='PNG';s.render.image_settings.color_depth='8';s.render.image_settings.color_mode='RGBA'
for idx,im in enumerate(list(bpy.data.images)):
 if im.source!='FILE' or im.type!='IMAGE':continue
 # Decode and resample one texture at a time, then release the decoded buffer.
 old=list(im.size)
 if not old[0] or not old[1]:raise RuntimeError('Unreadable texture: '+im.name)
 was_float=im.is_float;cap=1024
 if max(old)>cap:
  ratio=cap/max(old);im.scale(max(1,round(old[0]*ratio)),max(1,round(old[1]*ratio)))
 new=list(im.size);safe=''.join(c if c.isalnum() or c in '_-' else '_' for c in Path(im.name).stem)
 path=TEX/(str(idx).zfill(3)+'_'+safe+'.png')
 if im.packed_file:im.unpack(method='REMOVE')
 im.filepath_raw=str(path);im.file_format='PNG';im.save();im.reload()
 images.append({'name':im.name,'before':old,'after':new,'was_float':was_float,'is_float':im.is_float,'bytes':path.stat().st_size})
 im.buffers_free()
 if idx%20==0:print('Processed textures',idx,flush=True)
# Preserve fog/color/light design. Reduce bounce work, resolution, and concurrency.
s.cycles.samples=24;s.cycles.preview_samples=8;s.cycles.use_denoising=True;s.cycles.denoiser='OPENIMAGEDENOISE'
if hasattr(s.cycles,'denoising_use_gpu'):s.cycles.denoising_use_gpu=False
s.cycles.use_adaptive_sampling=True;s.cycles.adaptive_threshold=.08
s.cycles.max_bounces=4;s.cycles.diffuse_bounces=2;s.cycles.glossy_bounces=2;s.cycles.transmission_bounces=2;s.cycles.transparent_max_bounces=4;s.cycles.volume_bounces=0
s.render.use_persistent_data=False

s.render.use_simplify=True;s.render.simplify_subdivision_render=0;s.render.simplify_subdivision=0
if hasattr(s.cycles,'texture_limit_render'):s.cycles.texture_limit_render='1024'
if hasattr(s.cycles,'texture_limit'):s.cycles.texture_limit='1024'
s.render.resolution_x=1440;s.render.resolution_y=960;s.render.resolution_percentage=50
s.view_settings.view_transform='AgX';s.view_settings.look='AgX - Medium High Contrast';s.view_settings.exposure=.65
s.render.filepath=str(OUT/'optimized_cpu_preview.png')
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':area.spaces.active.shading.type='SOLID'
s['optimization_note']='No render performed. CPU only, limited threads, 720x480 initial render. Original downloads retained. Materials capped at 1K.'
report={'before':before,'after':stats(),'removed_dense_dressing':removed,'subdivision_modifiers_removed':subdivisions,'mesh_reductions':reductions,'images':images,'render_settings':{'device':s.cycles.device,'threads':s.render.threads,'resolution':[720,480],'samples':s.cycles.samples,'denoiser':s.cycles.denoiser},'render_executed':False}
(OUT/'optimization_report.json').write_text(json.dumps(report,indent=2))
subprocess.run([str(ROOT/'.tools/ComfyUI_windows_portable/python_embeded/python.exe'),str(ROOT/'tools/scene_builders/quantize_alley_textures.py')],check=True)
for im in bpy.data.images:
 if im.source=='FILE' and im.type=='IMAGE':
  im.reload();assert not im.is_float,im.name;im.buffers_free()
bpy.ops.file.pack_all()
# Compressed save is smaller on disk; image reductions reduce actual working memory.
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'victorian_alley_optimized.blend'),compress=True)
print('OPTIMIZATION_COMPLETE',json.dumps({'before':before,'after':stats(),'textures':len(images),'texture_bytes':sum(i['bytes'] for i in images),'float_images':sum(i['is_float'] for i in images)}),flush=True)
