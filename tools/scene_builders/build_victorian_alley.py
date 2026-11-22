"""Deterministic Victorian alley art scene. Run with portable Blender --background --python."""
import bpy, math, random, json, sys
from pathlib import Path
from mathutils import Vector
R=random.Random(1879)
ROOT=Path(__file__).resolve().parents[2]; OUT=ROOT/'artifacts/victorian_alley'; OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for c in list(bpy.data.collections):
 if c.name != 'Collection' and c.users==0: bpy.data.collections.remove(c)
scene=bpy.context.scene

def collection(name):
 c=bpy.data.collections.new(name); scene.collection.children.link(c); return c
arch=collection('01 | Tenements and chimney skyline'); street=collection('02 | Wet cobbles and gutters'); props=collection('03 | Poverty and household props'); chess=collection('04 | Street chess'); lights=collection('05 | Practical lights'); atmosphere=collection('06 | Atmosphere'); current=arch

def move(o,name,mat=None):
 o.name=name
 for c in list(o.users_collection): c.objects.unlink(o)
 current.objects.link(o)
 if mat: o.data.materials.append(mat)
 return o

def mat(name,col,rough=.65,metal=0,noise=True):
 m=bpy.data.materials.new(name); m.diffuse_color=(*col,1); m.use_nodes=True
 n=m.node_tree.nodes; l=m.node_tree.links; p=n.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*col,1); p.inputs['Roughness'].default_value=rough; p.inputs['Metallic'].default_value=metal
 if noise:
  tex=n.new('ShaderNodeTexNoise'); tex.inputs['Scale'].default_value=6; tex.inputs['Detail'].default_value=3
  geo=n.new('ShaderNodeNewGeometry'); l.new(geo.outputs['Position'],tex.inputs['Vector'])
  ramp=n.new('ShaderNodeValToRGB'); ramp.color_ramp.elements[0].color=(*[v*.32 for v in col],1); ramp.color_ramp.elements[1].color=(*[v*1.35 for v in col],1)
  l.new(tex.outputs['Fac'],ramp.inputs[0]); l.new(ramp.outputs[0],p.inputs['Base Color'])
  bump=n.new('ShaderNodeBump'); bump.inputs['Strength'].default_value=.35; bump.inputs['Distance'].default_value=.045; l.new(tex.outputs['Fac'],bump.inputs['Height']); l.new(bump.outputs[0],p.inputs['Normal'])
 return m
brick=[mat('Soot brick %02d'%i,(.10+i*.012,.058+i*.005,.037+i*.003)) for i in range(9)]
stone=mat('Damp eroded sandstone',(.21,.22,.18)); mortar=mat('Coal dust mortar',(.035,.049,.046)); wood=mat('Splintered old oak',(.105,.072,.038)); iron=mat('Oxidised iron',(.032,.045,.043),.48,.65); glass=mat('Unlit smoky glass',(.015,.035,.035),.22,.25)
cobbles=[mat('Wet basalt %02d'%i,(.043+i*.008,.061+i*.009,.062+i*.008),.22,.08) for i in range(7)]
water=mat('Standing rainwater',(.018,.044,.046),.075,.35,False)
cloth=[mat('Faded washing %d'%i,c) for i,c in enumerate([(.17,.19,.14),(.14,.11,.075),(.08,.13,.13)])]
def emissive(name,col,power):
 m=mat(name,col,.4,0,False); p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Emission Color'].default_value=(*col,1); p.inputs['Emission Strength'].default_value=power; return m
amber=emissive('Amber window glow',(1,.22,.035),.8); flame=emissive('Fire amber',(1,.12,.008),2); yellow=emissive('Fire hot core',(1,.38,.035),3)
def cube(name,loc,size,ma,bevel=0):
 o=batch(name,[(loc,size,0)],[ma],bevel)
 # Preserve object-local rotations for leaning boards.
 for v in o.data.vertices: v.co-=Vector(loc)
 o.location=loc
 return o

def cyl(name,loc,r,depth,ma,vertices=12):
 vs=[(r*math.cos(i*math.tau/vertices),r*math.sin(i*math.tau/vertices),z) for z in [-depth/2,depth/2] for i in range(vertices)]
 fs=[tuple(reversed(range(vertices))),tuple(range(vertices,vertices*2))]
 fs.extend((i,(i+1)%vertices,(i+1)%vertices+vertices,i+vertices) for i in range(vertices))
 me=bpy.data.meshes.new(name); me.from_pydata(vs,[],fs); me.materials.append(ma); o=bpy.data.objects.new(name,me); current.objects.link(o); o.location=loc; return o

def beam(name,a,b,r,ma):
 a,b=Vector(a),Vector(b); o=cyl(name,(a+b)/2,r,(b-a).length,ma); o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler(); return o

def batch(name,boxes,mats,bevel=0):
 vs=[]; fs=[]; mi=[]
 for (x,y,z),(a,b,c),idx in boxes:
  k=len(vs); vs.extend([(x+dx*a/2,y+dy*b/2,z+dz*c/2) for dx,dy,dz in [(-1,-1,-1),(-1,-1,1),(-1,1,-1),(-1,1,1),(1,-1,-1),(1,-1,1),(1,1,-1),(1,1,1)]])
  fs.extend([tuple(k+i for i in f) for f in [(0,4,6,2),(1,3,7,5),(0,1,5,4),(2,6,7,3),(0,2,3,1),(4,5,7,6)]]); mi.extend([idx]*6)
 mesh=bpy.data.meshes.new(name); mesh.from_pydata(vs,[],[tuple(reversed(f)) for f in fs]); mesh.update(); o=bpy.data.objects.new(name,mesh); current.objects.link(o)
 for m in mats: mesh.materials.append(m)
 for p,i in zip(mesh.polygons,mi): p.material_index=i
 if bevel: mod=o.modifiers.new('Chipped edge bevels','BEVEL'); mod.width=bevel; mod.segments=1
 return o

def light(name,loc,col,power,size=1,kind='POINT',target=None):
 d=bpy.data.lights.new(name,kind); d.energy=power; d.color=col
 if kind=='AREA': d.shape='DISK'; d.size=size
 else: d.shadow_soft_size=size
 o=bpy.data.objects.new(name,d); lights.objects.link(o); o.location=loc
 if target: o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
 return o
# Side walls, facades staggered in depth, chimney silhouettes.
for side in [-1,1]:
 for j in range(7):
  start=-5+j*5; h=R.uniform(8.3,12.3); x=side*(3.65+R.uniform(-.15,.18)); length=4.95
  cube('Tenement shell', (x+side*.8,start+2.5,h/2),(1.6,length,h),mortar)
  boxes=[]
  for row in range(int(h/.23)):
   for k in range(11):
    y=start+k*.48+(row%2)*.24
    boxes.append(((x,y,row*.23+.12),(.16,.45,.207),R.randrange(9)))
  batch('Individual weathered facade bricks',boxes,brick,.012)
  for z in [3.2,6.35]: cube('String course',(x-side*.10,start+2.4,z),(.29,5.05,.13),stone,.02)
  cube('Overhanging cornice',(x-side*.16,start+2.4,h),(.65,5.25,.28),wood,.03)
  for yy in [start+1.25,start+3.65]:
   for zz in [1.8,4.65,7.65]:
    if zz+1>h: continue
    lit=R.random()<.42; wm=amber if lit else glass; wx=x-side*.14
    cube('Recessed sash window',(wx,yy,zz),(.12,1.02,1.73),wood,.025)
    cube('Window glass',(wx-side*.075,yy,zz),(.03,.84,1.51),wm)
    for off in [-.47,0,.47]: cube('Sash vertical',(wx-side*.12,yy+off,zz),(.12,.055,1.76),wood,.01)
    for off in [-.83,0,.83]: cube('Sash rail',(wx-side*.12,yy,zz+off),(.12,1.05,.065),wood,.01)
    cube('Stone window sill',(wx-side*.22,yy,zz-.94),(.5,1.27,.16),stone,.025)
    cube('Lintel',(wx-side*.1,yy,zz+.96),(.28,1.22,.20),stone,.02)
    if lit and j<3: light('Window spill',(wx-side*.35,yy,zz),(1,.34,.10),32,.6)
  # tall door, boarded and water-stained
  yy=start+2.5
  cube('Door surround',(x-side*.12,yy,1.15),(.22,1.05,2.3),stone)
  for k in range(6): cube('Door plank',(x-side*.25,yy-.41+k*.165,1.1),(.12,.155,2.1),wood,.015)
  for z in [.55,1.65]: cube('Door strap',(x-side*.33,yy,z),(.05,.95,.065),iron)
  beam('Rain downpipe',(x-side*.3,start+.1,.15),(x-side*.3,start+.1,h),.055,iron)
  for z in [1,3.7,6.5]: cube('Pipe bracket',(x-side*.25,start+.1,z),(.28,.19,.07),iron)
  cube('Roof',(x+side*.9,start+2.5,h+.25),(2.7,5.35,.28),iron)
  for yy in [start+1,start+3.5]:
   cube('Chimney stack',(x+side*.7,yy,h+.9),(.85,.85,1.4),brick[2]); cube('Chimney crown',(x+side*.7,yy,h+1.65),(1.05,1.0,.18),stone)
   for dy in [-.23,.23]: cyl('Terracotta chimney pot',(x+side*.7,yy+dy,h+2),.14,.7,brick[5])
# Remnants of peeling lime plaster and moss at street level.
plaster=mat('Flaking dirty lime plaster',(.19,.19,.145),.92)
for side in [-1,1]:
 for j in range(7):
  yy=-4+j*5
  # Organic irregular islands exposing brick below.
  for patch in range(5):
   cyy=yy+R.uniform(-.3,3.7); cz=R.uniform(.7,8); ry=R.uniform(.22,.65); rz=R.uniform(.35,1.4)
   vs=[(side*3.40,cyy+math.cos(i*math.tau/18)*ry*R.uniform(.65,1.2),cz+math.sin(i*math.tau/18)*rz*R.uniform(.65,1.2)) for i in range(18)]
   me=bpy.data.meshes.new('Plaster remnant'); me.from_pydata(vs,[],[tuple(range(18))]); ob=bpy.data.objects.new('Broken lime plaster',me); arch.objects.link(ob); me.materials.append(plaster)
  if j%2==0:
   for k in range(3):
    ob=cube('Boarded abandoned window',(side*3.24,yy+1.25,4.2+k*.26),(.09,1.12,.18),wood,.015); ob.rotation_euler.x=R.uniform(-.14,.14)
# distant closing building
cube('Foggy end of lane',(0,31,6),(9,2,12),mortar)
for xx in [-2,0,2]:
 for zz in [3,6,9]: cube('Distant window',(xx,29.98,zz),(.7,.04,1.2),amber if xx==0 and zz==6 else glass)
current=street
cube('Road substrate',(0,11,-.18),(7.5,39,.3),mortar)
boxes=[]
for row in range(142):
 for k in range(20):
  x=-3.55+k*.37+(row%2)*.185; y=-8+row*.28
  boxes.append(((x+R.uniform(-.018,.018),y,R.uniform(-.02,.015)),(.34+R.uniform(-.03,.015),.245,R.uniform(.09,.13)),R.randrange(7)))
batch('Uneven hand laid wet cobbles',boxes,cobbles,.025)
for s in [-1,1]:
 batch('Raised gutter kerb',[((s*3.4,-7+i*.65,.12),(.28,.61,.22),0) for i in range(58)],[stone],.025)
# puddle patches shallow irregular polygons
for i in range(65):
 x=R.uniform(-3.2,3.2); y=R.uniform(-7,30); rx=R.uniform(.15,.85); ry=R.uniform(.2,1.2)
 vs=[(x+math.cos(a*math.tau/12)*rx*R.uniform(.7,1.1),y+math.sin(a*math.tau/12)*ry*R.uniform(.7,1.1),.067) for a in range(12)]
 me=bpy.data.meshes.new('Puddle'); me.from_pydata(vs,[],[tuple(range(12))]); o=bpy.data.objects.new('Rainwater in low cobbles',me); street.objects.link(o); me.materials.append(water)
current=props
# sagging ropes and ragged cloth
for cy,z in [(7,6.1),(15,7.8),(23,8.0)]:
 def rope_z(x): return z-.75*(1-(x/3.5)**2)
 for i in range(24):
  a=-3.5+i*7/24; b=a+7/24; beam('Sagging washing line',(a,cy,rope_z(a)),(b,cy,rope_z(b)),.016,wood)
 for idx in range(6):
  cx=-2.7+idx*1.03; width=R.uniform(.5,.85); drop=R.uniform(.7,1.45); vs=[]; fs=[]
  for v in range(9):
   for u in range(7):
    x=cx+(u/6-.5)*width; zz=rope_z(x)-v/8*drop
    if v==8: zz+=R.uniform(0,.19)
    vs.append((x,cy+.10*math.sin(u*1.5+v*.4)*v/8,zz))
  for v in range(8):
   for u in range(6):
    a=v*7+u; fs.append((a,a+1,a+8,a+7))
  me=bpy.data.meshes.new('Tattered fabric'); me.from_pydata(vs,[],fs); o=bpy.data.objects.new('Ragged laundry',me); props.objects.link(o); me.materials.append(cloth[idx%3])
# plank piles and rubble
for side,y in [(-1,-2),(1,3),(1,-3),(-1,13)]:
 for k in range(5):
  o=cube('Leaning salvaged board',(side*(2.6+k*.13),y+k*.19,.95),(.16,.25,2.1),wood,.022); o.rotation_euler=(.15,side*.32,R.uniform(-.12,.12))
for i in range(160):
 x=R.choice([-1,1])*R.uniform(2.55,3.25); y=R.uniform(-5,25)
 o=cube('Rubble and coal',(x,y,.1),(R.uniform(.07,.23),R.uniform(.09,.28),R.uniform(.06,.18)),R.choice([stone,brick[4],wood]),.02); o.rotation_euler.z=R.random()*6
# crates
for x,y in [(-2.7,4),(-2.65,4.8),(2.6,11),(-2.6,17)]:
 for z in [.15,.38,.61]:
  for s in [-1,1]: cube('Crate slat',(x+s*.38,y,z),(.065,.8,.18),wood,.01); cube('Crate slat',(x,y+s*.38,z),(.8,.065,.18),wood,.01)
 for dx in [-.32,.32]:
  for dy in [-.32,.32]: cube('Crate corner',(x+dx,y+dy,.38),(.09,.09,.8),wood)
# lanterns
for side,y,z in [(-1,1.8,3.3),(1,10,3.8),(-1,20,4.0)]:
 x=side*2.9
 beam('Lantern wall bracket',(side*3.5,y,z+.7),(x,y,z+.7),.04,iron); beam('Lantern chain',(x,y,z+.7),(x,y,z+.36),.026,iron)
 cyl('Lantern foot',(x,y,z-.35),.23,.09,iron,6); cyl('Lantern warm glass',(x,y,z),.17,.6,amber,6)
 for a in range(6):
  ang=a*math.tau/6; beam('Lantern cage',(x+.19*math.cos(ang),y+.19*math.sin(ang),z-.3),(x+.25*math.cos(ang),y+.25*math.sin(ang),z+.32),.018,iron)
 bpy.ops.mesh.primitive_cone_add(vertices=6,radius1=.34,radius2=.08,depth=.22,location=(x,y,z+.4)); move(bpy.context.object,'Lantern cap',iron)
 light('Gas lantern',(x-side*.17,y-.08,z),(1,.40,.12),140,.32)
# brazier: open iron cage and faceted flames
bx,by=2.25,3.1
for z in [.18,.35,1.1]:
 bpy.ops.mesh.primitive_torus_add(major_radius=.43,minor_radius=.035,major_segments=16,minor_segments=4,location=(bx,by,z)); move(bpy.context.object,'Brazier iron band',iron)
for i in range(16):
 a=i*math.tau/16; beam('Brazier upright',(bx+.42*math.cos(a),by+.42*math.sin(a),.15),(bx+.42*math.cos(a),by+.42*math.sin(a),1.12),.027,iron)
cyl('Coal bed',(bx,by,.55),.41,.22,wood)
for i in range(18):
 h=R.uniform(.3,1.1); x=bx+R.uniform(-.28,.28); y=by+R.uniform(-.28,.28)
 bpy.ops.mesh.primitive_cone_add(vertices=5,radius1=R.uniform(.06,.16),radius2=0,depth=h,location=(x,y,.72+h/2)); o=move(bpy.context.object,'Faceted fire',yellow if i%3==0 else flame); o.rotation_euler.y=R.uniform(-.3,.3)
light('Brazier fire',(bx,by,1.35),(1,.23,.035),370,.55)
# street chess on battered low timber table
current=chess
ivory=mat('Dirty ivory chessmen',(.5,.36,.17)); black=mat('Ebony chessmen',(.035,.026,.017)); tilelight=mat('Worn chalk squares',(.30,.32,.27)); tiledark=mat('Dark board squares',(.055,.076,.072))
cx,cy=.65,-3.1
for x in [-.85,.85]:
 for y in [-.85,.85]: cube('Table leg',(cx+x,cy+y,.38),(.13,.13,.76),wood,.02)
for i in range(9): cube('Battered table plank',(cx-.96+i*.24,cy,.78),(.23,2.2,.13),wood,.015)
for row in range(8):
 for col in range(8): cube('Chess square',(cx+(col-3.5)*.23,cy+(row-3.5)*.23,.854),(.226,.226,.012),tilelight if (row+col)%2 else tiledark)
def piece(x,y,kind,ma):
 h={'pawn':.21,'rook':.29,'king':.43,'bishop':.32}[kind]; z=.87
 cyl(kind+' foot',(x,y,z+.026),.085,.052,ma,8)
 bpy.ops.mesh.primitive_cone_add(vertices=8,radius1=.068,radius2=.035,depth=h*.65,location=(x,y,z+h*.38)); move(bpy.context.object,kind+' body',ma)
 cyl(kind+' collar',(x,y,z+h*.72),.055,.035,ma,8)
 if kind=='rook':
  cyl('Rook crown',(x,y,z+h*.88),.075,.065,ma,8)
  for a in range(4):
   ang=a*math.tau/4; cube('Rook crenel',(x+.055*math.cos(ang),y+.055*math.sin(ang),z+h+.02),(.04,.04,.055),ma)
 else:
  bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.06,location=(x,y,z+h*.91)); move(bpy.context.object,kind+' head',ma)
  if kind=='king': cube('King cross',(x,y,z+h+.08),(.035,.035,.14),ma); cube('King crossbar',(x,y,z+h+.10),(.11,.035,.03),ma)
for c,r,k,m in [(0,0,'rook',ivory),(4,0,'king',ivory),(2,2,'pawn',ivory),(6,1,'pawn',ivory),(5,3,'bishop',ivory),(7,5,'rook',black),(3,6,'king',black),(1,4,'pawn',black),(4,5,'pawn',black),(6,6,'bishop',black)]: piece(cx+(c-3.5)*.23,cy+(r-3.5)*.23,k,m)
# Generated ComfyUI barrel, normalized and placed as dressed props.
current=props
paths=list((ROOT/'artifacts/asset_harness/8c2492536794').glob('*.glb'))
if paths:
 before=set(bpy.data.objects); bpy.ops.import_scene.gltf(filepath=str(paths[0])); imported=[o for o in bpy.data.objects if o not in before and o.type=='MESH']
 for o in imported:
  bpy.context.view_layer.objects.active=o; o.select_set(True)
  pts=[o.matrix_world@Vector(p) for p in o.bound_box]; lo=Vector(tuple(min(v[i] for v in pts) for i in range(3))); hi=Vector(tuple(max(v[i] for v in pts) for i in range(3))); factor=1.1/(hi.z-lo.z)
  # Bake imported transforms into the mesh, then normalize foot at zero.
  matrix=o.matrix_world.copy()
  for v in o.data.vertices: v.co=(matrix@v.co-Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z)))*factor
  o.matrix_world.identity(); o.location=(-2.7,.2,.08); move(o,'TRELLIS weathered barrel')
  for loc in [(-2.6,8,.08),(2.7,16,.08)]:
   cp=o.copy(); cp.data=o.data; props.objects.link(cp); cp.location=loc
# teal mist volume
current=atmosphere
fog=bpy.data.materials.new('Blue green suspended fog'); fog.use_nodes=True; ns=fog.node_tree.nodes; ns.clear(); out=ns.new('ShaderNodeOutputMaterial'); vol=ns.new('ShaderNodeVolumePrincipled'); vol.inputs['Density'].default_value=.047; vol.inputs['Color'].default_value=(.24,.45,.47,1); vol.inputs['Anisotropy'].default_value=.22; fog.node_tree.links.new(vol.outputs['Volume'],out.inputs['Volume']); cube('Atmospheric fog volume',(0,13,7),(22,65,27),fog)
scene.world.use_nodes=True; scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.10,.22,.25,1); scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.35
light('Moonlit cloud ceiling',(0,8,14),(.22,.57,.65),1500,12,'AREA',(0,7,0)); light('Distant cyan haze',(0,27,7),(.20,.58,.65),1800,7,'AREA',(0,8,4)); light('Foreground soft night fill',(-1,-6,7),(.24,.51,.58),400,8,'AREA',(0,-1,1))
bpy.ops.object.camera_add(location=(-.9,-9.8,2.65)); cam=bpy.context.object; cam.name='Menu camera | 16 by 9'; cam.rotation_euler=(Vector((.2,7.8,3.35))-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.lens=24; scene.camera=cam
scene.render.engine='CYCLES'; scene.cycles.samples=80; scene.cycles.use_denoising=True
try:
 prefs=bpy.context.preferences.addons['cycles'].preferences; prefs.compute_device_type='OPTIX'; prefs.get_devices()
 for d in prefs.devices: d.use=d.type=='OPTIX'
 scene.cycles.device='GPU'
except Exception: pass
scene.render.resolution_x=1536; scene.render.resolution_y=864; scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'; scene.view_settings.look='AgX - Medium High Contrast'; scene.view_settings.exposure=.1
scene.render.image_settings.file_format='PNG'; scene.render.filepath=str(OUT/'alley_preview.png')
scene['art_direction']='Poor Victorian alley, teal blue fog, warm sparse practicals, no people. Reference supplied by user.'
scene['godot_note']='Meters, Z-up Blender. Recreate volumetric fog and procedural shaders in Godot; emissive meshes need local lights. No game scene changed.'
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'victorian_alley.blend'))
bpy.ops.render.render(write_still=True)
print('ALLEY_COMPLETE',str(OUT))
