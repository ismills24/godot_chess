"""Rebuild Poly Haven Hidden Alley as an unpopulated Victorian menu environment.
Source assets remain untouched. Run with the downloaded ph_hidden_alley.blend open.
"""
import bpy,math,random,json
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[2]; OUT=ROOT/'artifacts/victorian_alley_v2'; SRC=ROOT/'artifacts/polyhaven_sources'; scene=bpy.context.scene; R=random.Random(1888)
scene.render.engine='CYCLES'; scene.cycles.device='CPU'; scene.render.threads_mode='FIXED'; scene.render.threads=2
# Bake relative source image paths before saving into the output folder.
for im in bpy.data.images:
 if im.source=='FILE':
  im.filepath=bpy.path.abspath(im.filepath)
  if not Path(im.filepath).exists():
   candidate=SRC/'hidden_alley/textures'/Path(im.filepath).name
   if candidate.exists():im.filepath=str(candidate)
   else: print('MISSING SOURCE IMAGE',im.name,im.filepath,flush=True)
removed=[]
remove_col={'Background Buildings','high_rise_building','airducts','modular_chainlink_fence','Modular_utility_poles','power_box_01.001','security_light','roller_shutters','Fog','Sky','Light Blockers','factory_building_05','factory_building_06','Paralax Interiors','Tree'}
remove_terms=['covered_car','boombox','cassette','spray_paint','aircon','old_tyre','security_camera','plastic_','wheel_rim','multi_cleaner','television','utility_box','road_barrier','large_iron_gate','fire_hydrant','baseball','street_rat','street_lamp','outdoor_table_chair','barrel_02','barrel_03','saber','power_box','electri']
for o in list(scene.objects):
 if o.type=='LIGHT' or any(c.name in remove_col for c in o.users_collection) or any(t in o.name.lower() for t in remove_terms):
  removed.append(o.name); bpy.data.objects.remove(o,do_unlink=True)
# Use tested simple mesh helpers from the first study, without executing that scene.
helper=(ROOT/'tools/scene_builders/build_victorian_alley.py').read_text()
exec(helper[helper.index('def collection('):helper.index('# Side walls,')])
print('Modern elements removed:',len(removed),flush=True)
# Existing PVC forms become blackened iron service pipes.
for o in scene.objects:
 if o.type=='MESH' and any(c.name=='pipes' for c in o.users_collection):
  o.data=o.data.copy(); o.data.materials.clear();o.data.materials.append(iron)
# No imported animation or sunlight rigs; cinematic static camera.
scene.animation_data_clear();scene.use_nodes=False
for ob in scene.objects:
 if ob.animation_data: ob.animation_data_clear()
# Worn cobblestone PBR ground with shallow physical relief.
current=street
m=bpy.data.materials.new('PH cobblestones | rain darkened');m.use_nodes=True;n=m.node_tree.nodes;l=m.node_tree.links;p=n.get('Principled BSDF'); p.inputs['Roughness'].default_value=.3
coord=n.new('ShaderNodeTexCoord');mapping=n.new('ShaderNodeVectorMath');mapping.operation='SCALE';mapping.inputs[3].default_value=.45;l.new(coord.outputs['Object'],mapping.inputs[0])
for key in ['Diffuse','nor_gl','Rough','Displacement']:
 path=next((SRC/'cobbles').glob(key+'.*')); im=bpy.data.images.load(str(path),check_existing=True)
 if key!='Diffuse':im.colorspace_settings.name='Non-Color'
 tex=n.new('ShaderNodeTexImage');tex.image=im;l.new(mapping.outputs[0],tex.inputs['Vector'])
 if key=='Diffuse':
  mix=n.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=(.44,.53,.51,1);l.new(tex.outputs['Color'],mix.inputs[1]);l.new(mix.outputs[0],p.inputs['Base Color'])
 elif key=='nor_gl':
  normal=n.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=1.1;l.new(tex.outputs[0],normal.inputs['Color']);l.new(normal.outputs[0],p.inputs['Normal'])
 elif key=='Rough':
  mathn=n.new('ShaderNodeMath');mathn.operation='MULTIPLY_ADD';mathn.inputs[1].default_value=.42;mathn.inputs[2].default_value=.08;l.new(tex.outputs[0],mathn.inputs[0]);l.new(mathn.outputs[0],p.inputs['Roughness'])
# Retain authored ground topology, replace modern asphalt.
floor=bpy.data.objects.get('Floor_main')
if floor:
 floor.data.materials.clear();floor.data.materials.append(m)
 for mod in list(floor.modifiers):floor.modifiers.remove(mod)
# Close camera-side ground gaps with matching surface.
cube('Cobble ground extension',(0,-5,-.12),(5,8,.10),m)
# Shallow puddles with irregular edges, not mirror-smooth covering the whole alley.
for i in range(30):
 x=R.uniform(-1.65,1.7);y=R.uniform(-5,13);rx=R.uniform(.12,.45);ry=R.uniform(.18,.85)
 vs=[(x+math.cos(a*math.tau/16)*rx*R.uniform(.65,1.15),y+math.sin(a*math.tau/16)*ry*R.uniform(.65,1.15),-.045) for a in range(16)]
 me=bpy.data.meshes.new('Rain puddle');me.from_pydata(vs,[],[tuple(range(16))]);ob=bpy.data.objects.new('Rain gathered in low paving',me);street.objects.link(ob);me.materials.append(water)
# Warm a minority of existing windows, preserving all their aged frames.
windows=[o for o in list(scene.objects) if o.type=='MESH' and o.name.startswith('window')]
for i,o in enumerate(windows):
 if R.random()<.23:
  o.data=o.data.copy()
  for idx,ma in enumerate(o.data.materials):
   if ma and 'glass' in ma.name.lower():o.data.materials[idx]=amber
# Real wooden plank texture on new timber dressing.
wood=bpy.data.materials.get('ph_gate_boards') or wood
current=arch
# Cover roller doors with narrow salvaged boards; keep old masonry frame.
for x,y,w in [(1.96,1.5,1.2),(3.96,9.9,1.9)]:
 for i in range(int(w/.13)):
  cube('Boarded workshop entrance',(x,y-w/2+i*.13,1.05),(.08,.12,2.18),wood,.008)
 for z in [.48,1.5]:cube('Old iron hinge strap',(x-.06,y,z),(.035,w,.045),iron)
# Chimneys break the flat roofline and establish the industrial Victorian silhouette.
chimney_mat=bpy.data.materials.get('modular_factory_facade_brick_worldspace_building_01') or brick[3]
for x,y,z in [(-3.1,-1,9.3),(-3.5,3.5,9.3),(3,-3,9.5),(4.7,6,9.3),(4.7,11,9.3),(-12,9,9.4),(7.5,15,15.2),(-8,18,13.5)]:
 cube('Brick chimney stack',(x,y,z+.7),(.85,1.15,1.4),chimney_mat,.015);cube('Chimney crown',(x,y,z+1.45),(1.0,1.3,.18),stone,.025)
 for dy in [-.34,.34]:
  cyl('Sooted chimney pot',(x,y+dy,z+1.87),.17,.7,brick[4]);cyl('Chimney dark opening',(x,y+dy,z+2.23),.135,.012,iron)
# Keep source texture detail but dim overly reflective glazing.
for ma in bpy.data.materials:
 if 'glass' in ma.name.lower() and ma.use_nodes:
  for node in ma.node_tree.nodes:
   if node.type=='BSDF_PRINCIPLED': node.inputs['Roughness'].default_value=.35
current=props
# Washing lines, shaped cloth with irregular hems.
for cy,z,left,right in [(2.8,5.5,-2.18,2),(8.2,6.6,-3.4,3.9),(12.2,7.5,-3.4,3.9)]:
 def rope_z(x):return z-.52*(1-((x-(left+right)/2)/((right-left)/2))**2)
 for i in range(25):
  a=left+i*(right-left)/25;b=left+(i+1)*(right-left)/25;beam('Sagging hemp washing line',(a,cy,rope_z(a)),(b,cy,rope_z(b)),.012,wood)
 for idx in range(5):
  cx=left+.55+idx*(right-left-.9)/5;width=R.uniform(.4,.68);drop=R.uniform(.6,1.05);vs=[];fs=[]
  for v in range(10):
   for u in range(9):
    x=cx+(u/8-.5)*width;zz=rope_z(x)-v/9*drop+(R.uniform(0,.09) if v==9 else 0);vs.append((x,cy+.09*math.sin(u*1.3+v*.4)*v/9,zz))
  for v in range(9):
   for u in range(8):a=v*9+u;fs.append((a,a+1,a+10,a+9))
  me=bpy.data.meshes.new('Threadbare linen');me.from_pydata(vs,[],fs);ob=bpy.data.objects.new('Patched hanging washing',me);props.objects.link(ob);me.materials.append(cloth[idx%3])
  sol=ob.modifiers.new('Cloth thickness','SOLIDIFY');sol.thickness=.002
# Lanterns with metal cage, aged glass and an actual small flame inside.
for side,y,z,wall in [(-1,-.8,2.8,2.2),(1,4.5,3.1,4),(-1,10.8,2.9,3.5)]:
 x=side*(wall-.4);wx=side*wall
 beam('Lantern wall bracket',(wx,y,z+.55),(x,y,z+.55),.025,iron);beam('Lantern suspension',(x,y,z+.55),(x,y,z+.3),.018,iron)
 cyl('Lantern foot',(x,y,z-.25),.18,.07,iron,6);cyl('Warm gaslight glass',(x,y,z),.135,.45,amber,6)
 for a in range(6):
  ang=a*math.tau/6;beam('Lantern cage',(x+.16*math.cos(ang),y+.16*math.sin(ang),z-.24),(x+.21*math.cos(ang),y+.21*math.sin(ang),z+.25),.012,iron)
 bpy.ops.mesh.primitive_cone_add(vertices=6,radius1=.27,radius2=.04,depth=.18,location=(x,y,z+.31));move(bpy.context.object,'Gas lantern hood',iron)
 light('Amber gas practical',(x-side*.12,y,z),(1,.37,.095),95,.26)
# Append real Shed models; normalize geometry with world transforms and preserve PBR maps.
added=[]
def prop(asset,loc,height,rot=0):
 before=set(bpy.data.objects);bpy.ops.import_scene.gltf(filepath=str(SRC/asset/(asset+'_2k.gltf')));obs=[o for o in bpy.data.objects if o not in before and o.type=='MESH']
 bpy.context.view_layer.update();points=[o.matrix_world@Vector(v) for o in obs for v in o.bound_box];lo=Vector(tuple(min(v[i] for v in points) for i in range(3)));hi=Vector(tuple(max(v[i] for v in points) for i in range(3)));scale=height/(hi.z-lo.z);center=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z));rz=Matrix.Rotation(rot,4,'Z')
 for ob in obs:
  mw=ob.matrix_world.copy();ob.data=ob.data.copy()
  for v in ob.data.vertices:v.co=rz@((mw@v.co-center)*scale)
  ob.parent=None;ob.matrix_world=Matrix.Identity(4);ob.location=loc;move(ob,'Shed | '+asset);ob['source']='https://polyhaven.com/a/'+asset;added.append(ob.name)
 return obs
prop('wicker_basket_01',(-1.48,-1.05,-.05),.48,.35)
prop('wicker_basket_01',(2.8,6.1,-.05),.40,1.5)
sp=prop('rusted_spade_01',(-1.99,.25,-.04),1.18,-.3)
for ob in sp:ob.rotation_euler.y=-.13
prop('hand_plane_no4',(-1.42,-.7,.49),.16,1.1)
prop('handsaw_wood',(-1.85,.8,.06),.55,-.8)
# Rescue the old street chess collection as an intentional menu focal point.
with bpy.data.libraries.load(str(ROOT/'artifacts/victorian_alley/victorian_alley.blend'),link=False) as (a,b):b.collections=[n for n in a.collections if n.startswith('04 |')]
for col in b.collections:
 scene.collection.children.link(col)
 for ob in col.objects:
  ob.location+=Vector((.25,1.1,-.08))
  if ob.type=='MESH' and 'Table' in ob.name or 'plank' in ob.name.lower():
   if ob.type=='MESH':ob.data.materials.clear();ob.data.materials.append(wood)
# Source barrel stove becomes the warming fire, tucked into the right recess.
stove=bpy.data.objects.get('barrel_stove.002')
if stove:
 stove.location.x=2.75;stove.location.y=4.0
light('Stove ember glow',(2.5,3.6,.55),(1,.20,.035),110,.38)
# Small discarded timber and rubble emphasize neglect without modern litter.
for side,y in [(-1,1.4),(1,5.2)]:
 for k in range(4):
  ob=cube('Salvaged timber',(side*(1.9 if side<0 else 3.6)-side*k*.08,y+k*.13,.7),(.10,.19,1.65),wood,.008);ob.rotation_euler=(.12,side*.28,R.uniform(-.12,.12))
# Replace source sky and lighting with a cool, damp night.
world=bpy.data.worlds.new('Blue green night sky');scene.world=world;world.use_nodes=True;world.node_tree.nodes.get('Background').inputs[0].default_value=(.095,.19,.21,1);world.node_tree.nodes.get('Background').inputs[1].default_value=.30
light('Clouded moon | broad teal',(0,5,13),(.25,.58,.66),1650,9,'AREA',(0,5,0));light('Far blue green mist',(0,13,8),(.20,.57,.61),1700,5,'AREA',(0,0,3));light('Camera side night fill',(0,-6,6),(.28,.50,.57),270,7,'AREA',(0,1,1))
light('Chess warm bounce',(1.4,-1.5,2.5),(1,.48,.21),35,2,'AREA',(.8,-2,.8))
current=atmosphere
fog=bpy.data.materials.new('Night mist | teal');fog.use_nodes=True;nn=fog.node_tree.nodes;nn.clear();output=nn.new('ShaderNodeOutputMaterial');vol=nn.new('ShaderNodeVolumePrincipled');vol.inputs['Density'].default_value=.035;vol.inputs['Color'].default_value=(.35,.56,.57,1);vol.inputs['Anisotropy'].default_value=.25;fog.node_tree.links.new(vol.outputs['Volume'],output.inputs['Volume']);cube('Blue green atmospheric fog',(0,7,8),(44,60,32),fog)
cam=scene.camera;cam.location=(-.35,-6.3,2.0);cam.rotation_euler=(Vector((.15,7.5,3.25))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=26;cam.data.dof.use_dof=False;cam.name='Victorian alley | menu camera'
# The optimizer owns memory limits and saving. This builder never renders.
exec(compile((ROOT/'tools/scene_builders/optimize_alley.py').read_text(),str(ROOT/'tools/scene_builders/optimize_alley.py'),'exec'))
