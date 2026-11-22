"""Refine three TRELLIS jobs into a textured, modular PS1 pawn; never touches gameplay assets."""
import bpy, bmesh, json, math, sys
import numpy as np
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tools/asset_harness'))
from blender_process import active, cleanup, inspect
OUT=ROOT/'experiments/ps1_pawn/generated'
OUT.mkdir(parents=True,exist_ok=True)
JOBS={'Base':'1a0089b08749','Body':'25e4ab550007','Head':'7cf8a7d74b8d'}
SPECS={'Base':((.86,.86,.12),0,140),'Body':((.817,.817,.765),.145,220),'Head':((.56,.56,.59),.91,260)}
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
report={'source':'Godot LightPawn.tscn module captures','jobs':JOBS,'texture_size':64,'head_seat':.91,'socket_radius':.138,'dowel_radius':.132,'modules':{}}
parts=[]; originals=[]

def boolean(obj, cutter, operation):
    active(obj); mod=obj.modifiers.new('Precision joint','BOOLEAN'); mod.operation=operation; mod.solver='EXACT'; mod.object=cutter
    bpy.ops.object.modifier_apply(modifier=mod.name); bpy.data.objects.remove(cutter,do_unlink=True); cleanup(obj)

def cylinder(radius,depth,z,vertices=16):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=(0,0,z))
    obj=bpy.context.object; bpy.ops.object.transform_apply(location=True,rotation=True,scale=True); return obj

def grain_materials(obj):
    for material in obj.data.materials:
        if not material or not material.use_nodes: continue
        nodes=material.node_tree.nodes; links=material.node_tree.links
        bsdf=next((n for n in nodes if n.type=='BSDF_PRINCIPLED'),None)
        if not bsdf: continue
        color=bsdf.inputs['Base Color']; old=color.links[0].from_socket if color.is_linked else None
        coord=nodes.new('ShaderNodeTexCoord'); mapping=nodes.new('ShaderNodeVectorMath'); mapping.operation='MULTIPLY'; mapping.inputs[1].default_value=(16,16,.08)
        links.new(coord.outputs['Generated'],mapping.inputs[0])
        noise=nodes.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value=3; noise.inputs['Detail'].default_value=1.5
        links.new(mapping.outputs[0],noise.inputs['Vector'])
        ramp=nodes.new('ShaderNodeValToRGB'); ramp.color_ramp.elements[0].position=.28; ramp.color_ramp.elements[0].color=(.16,.12,.08,1)
        ramp.color_ramp.elements[1].position=.68; ramp.color_ramp.elements[1].color=(1,.94,.78,1)
        links.new(noise.outputs['Fac'],ramp.inputs[0])
        mix=nodes.new('ShaderNodeMixRGB'); mix.blend_type='MULTIPLY'; mix.inputs[0].default_value=.65
        unify=nodes.new('ShaderNodeMixRGB'); unify.blend_type='MIX'; unify.inputs[0].default_value=.72
        unify.inputs[2].default_value=(.48,.27,.11,1)
        if old: links.new(old,unify.inputs[1])
        else: unify.inputs[1].default_value=color.default_value
        links.new(unify.outputs[0],mix.inputs[1])
        links.new(ramp.outputs[0],mix.inputs[2]); links.new(mix.outputs[0],color)

def bake(source, target, name):
    active(target)
    while target.data.uv_layers: target.data.uv_layers.remove(target.data.uv_layers[0])
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.035)
    bpy.ops.object.mode_set(mode='OBJECT')
    target.data.materials.clear()
    mat=bpy.data.materials.new(name+'_PS1_64px'); mat.use_nodes=True
    target.data.materials.append(mat)
    image=bpy.data.images.new(name+'_64',width=64,height=64,alpha=False)
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=image; tex.interpolation='Closest'
    mat.node_tree.nodes.active=tex
    bsdf=mat.node_tree.nodes.get('Principled BSDF'); bsdf.inputs['Roughness'].default_value=1
    bsdf.inputs['Specular IOR Level'].default_value=0
    source.hide_set(False); source.hide_render=False; source.select_set(True)
    scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.device='CPU'; scene.cycles.samples=1
    scene.render.bake.use_selected_to_active=True; scene.render.bake.cage_extrusion=.08
    scene.render.bake.max_ray_distance=.2; scene.render.bake.margin=3
    scene.render.bake.use_pass_direct=False; scene.render.bake.use_pass_indirect=False; scene.render.bake.use_pass_color=True
    bpy.ops.object.bake(type='DIFFUSE')
    pixels=np.array(image.pixels[:],dtype=np.float32).reshape(64,64,4)
    # 15-bit color with ordered dither, intentionally visible in nearest-filtered texels.
    bayer=np.array([[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]],dtype=np.float32)/16-.5
    rgb=np.clip(pixels[:,:,:3],0,1)
    srgb=np.where(rgb<=.0031308,rgb*12.92,1.055*np.power(rgb,1/2.4)-.055)
    # A unified warm wood palette prevents independent generations drifting toward green/yellow.
    palette=np.array([[46,28,22],[64,39,27],[82,49,31],[99,61,35],[116,72,42],[133,85,49],
                      [150,100,59],[165,115,70],[181,132,83],[196,149,98],[209,167,116],[223,187,140]],dtype=np.float32)/255
    luminance=srgb[:,:,0]*.2126+srgb[:,:,1]*.7152+srgb[:,:,2]*.0722
    index=np.clip(np.floor((luminance-.12)/.65*11+np.tile(bayer,(16,16))*.6+.5),0,11).astype(int)
    srgb=palette[index]
    pixels[:,:,:3]=np.where(srgb<=.04045,srgb/12.92,np.power((srgb+.055)/1.055,2.4)); pixels[:,:,3]=1
    image.pixels.foreach_set(pixels.ravel()); image.update()
    image.filepath_raw=str(OUT/(name.lower()+'_64.png')); image.file_format='PNG'; image.save(); image.pack()
    mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
    source.hide_set(True); source.hide_render=True
    for poly in target.data.polygons: poly.use_smooth=False

for name,job in JOBS.items():
    before=set(bpy.data.objects)
    path=ROOT/'artifacts/asset_harness'/job/'model_00001.glb'
    bpy.ops.import_scene.gltf(filepath=str(path))
    meshes=[o for o in bpy.data.objects if o not in before and o.type=='MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes: o.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0]
    if len(meshes)>1: bpy.ops.object.join()
    high=bpy.context.object; high.name=name+'_TrellisSource'
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    dim,seat,budget=SPECS[name]
    coords=[v.co for v in high.data.vertices]
    lo=Vector(tuple(min(v[i] for v in coords) for i in range(3))); hi=Vector(tuple(max(v[i] for v in coords) for i in range(3)))
    for v in high.data.vertices:
        v.co=Vector(((v.co.x-(lo.x+hi.x)/2)*dim[0]/(hi.x-lo.x),(v.co.y-(lo.y+hi.y)/2)*dim[1]/(hi.y-lo.y),(v.co.z-lo.z)*dim[2]/(hi.z-lo.z)))
    grain_materials(high)
    low=high.copy(); low.data=high.data.copy(); low.name=name; bpy.context.collection.objects.link(low); active(low)
    high.hide_set(True); high.hide_render=True
    cleanup(low)
    initial=inspect(low)
    # These three minimalist outer forms are convex. Rebuild their envelope from
    # TRELLIS vertices to discard internal sheets / disconnected reconstruction noise.
    # Restore the nonconvex manufactured socket explicitly below.
    hull=bmesh.new()
    for vertex in low.data.vertices: hull.verts.new(vertex.co)
    hull.verts.ensure_lookup_table()
    result=bmesh.ops.convex_hull(hull,input=list(hull.verts),use_existing_faces=False)
    unused=[v for v in hull.verts if not v.link_faces]
    if unused: bmesh.ops.delete(hull,geom=unused,context='VERTS')
    hull.to_mesh(low.data); hull.free(); cleanup(low)
    mod=low.modifiers.new('PS1 polygon budget','DECIMATE'); mod.ratio=min(1,budget/max(1,len(low.data.polygons)))
    bpy.ops.object.modifier_apply(modifier=mod.name); cleanup(low)
    # Restore planar interfaces after reconstruction/decimation.
    for v in low.data.vertices:
        if v.co.z < dim[2]*.05: v.co.z=0
        if name=='Base' and v.co.z>dim[2]*.95: v.co.z=dim[2]
    if name=='Head':
        boolean(low,cylinder(.185,.15,.075),'UNION')
        boolean(low,cylinder(.138,.43,.105),'DIFFERENCE')
    cleanup(low)
    bake(high,low,name)
    if name=='Body':
        # Exact mating dowel, recessed helical groove; belongs to the body module.
        vertices=[]; faces=[]; sectors=12; rings=19
        for row in range(rings):
            z=.30*row/(rings-1)
            for col in range(sectors):
                a=math.tau*col/sectors
                groove=max(0,math.cos(math.tau*z/.10-a))**8
                r=.132-.008*groove
                vertices.append((r*math.cos(a),r*math.sin(a),dim[2]+z))
        for row in range(rings-1):
            for col in range(sectors):
                a=row*sectors+col; b=row*sectors+(col+1)%sectors
                faces.append((a,b,b+sectors,a+sectors))
        faces.extend([tuple(reversed(range(sectors))),tuple((rings-1)*sectors+i for i in range(sectors))])
        mesh=bpy.data.meshes.new('ThreadedDowel'); mesh.from_pydata(vertices,[],faces); mesh.update()
        dowel=bpy.data.objects.new('ThreadedDowel',mesh); bpy.context.collection.objects.link(dowel)
        mat=bpy.data.materials.new('DowelWood'); mat.use_nodes=True
        mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.32,.16,.055,1)
        mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=1
        dowel.data.materials.append(mat)
        active(low); dowel.select_set(True); bpy.ops.object.join(); cleanup(low)
    low.location.z=seat; high.location.z=seat
    # Base/body keep board-ground coordinates. Head's origin remains its mating plane.
    if name!='Head':
        active(low); bpy.ops.object.transform_apply(location=True,rotation=False,scale=False)
    report['modules'][name]={'source':str(path),'initial':initial,'final':inspect(low),'seat':seat,'bounds':dim}
    assert report['modules'][name]['final']['closed_manifold'], name+' failed mesh validation'
    parts.append(low); originals.append(high)
    print('PS1_MODULE',name,json.dumps(report['modules'][name]),flush=True)

bpy.ops.object.select_all(action='DESELECT')
for part in parts: part.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.export_scene.gltf(filepath=str(OUT/'PawnPS1.glb'),export_format='GLB',use_selection=True)
for part in parts:
    active(part); old=part.location.copy()
    if part.name=='Head': part.location.z=0
    bpy.ops.export_scene.gltf(filepath=str(OUT/(part.name+'.glb')),export_format='GLB',use_selection=True)
    part.location=old
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'artifacts/asset_harness/ps1_pawn/PawnPS1.blend'))
report['triangles']=sum(inspect(p)['triangles'] for p in parts)
(OUT/'report.json').write_text(json.dumps(report,indent=2))
print('PS1_COMPLETE',report['triangles'],flush=True)
