"""Blender batch refinement: preserve source, clean/remesh, optional quads and landmark rig, validate/export."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys
import bpy
import bmesh
from mathutils import Vector

def inspect(obj):
    bm = bmesh.new(); bm.from_mesh(obj.data)
    stats = {'vertices': len(bm.verts), 'faces': len(bm.faces),
        'triangles': sum(max(0, len(f.verts)-2) for f in bm.faces),
        'boundary_edges': sum(e.is_boundary for e in bm.edges),
        'nonmanifold_edges': sum(not e.is_manifold for e in bm.edges),
        'nonmanifold_vertices': sum(not v.is_manifold for v in bm.verts),
        'loose_vertices': sum(not v.link_edges for v in bm.verts),
        'degenerate_faces': sum(f.calc_area() < 1e-12 for f in bm.faces),
        'signed_volume': bm.calc_volume(signed=True)}
    stats['closed_manifold'] = (stats['faces'] > 0 and stats['nonmanifold_edges'] == 0
        and stats['nonmanifold_vertices'] == 0 and stats['loose_vertices'] == 0 and stats['degenerate_faces'] == 0 and abs(stats['signed_volume']) > 1e-9)
    bm.free()
    return stats

def active(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True); bpy.context.view_layer.objects.active = obj

def cleanup(obj):
    bm=bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.00001)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=0.000001)
    loose=[v for v in bm.verts if not v.link_faces]
    if loose: bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    if bm.calc_volume(signed=True) < 0: bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data); bm.free(); obj.data.update()

def attach_rig(obj, path, height):
    specification=json.loads(Path(path).read_text())
    bones=specification['bones']
    if not bones or len({b['name'] for b in bones}) != len(bones): raise ValueError('Bones must have unique names')
    names={b['name'] for b in bones}
    for bone in bones:
        if bone.get('parent') and bone['parent'] not in names: raise ValueError('Unknown parent: '+bone['parent'])
        for key in ('head','tail'):
            if len(bone[key]) != 3 or not all(math.isfinite(v) for v in bone[key]): raise ValueError('Invalid joint position')
        if (Vector(bone['tail'])-Vector(bone['head'])).length < 0.0001: raise ValueError('Zero-length bone')
        chain=set(); current=bone
        while current.get('parent'):
            if current['name'] in chain: raise ValueError('Cyclic bone hierarchy')
            chain.add(current['name']); current=next(b for b in bones if b['name']==current['parent'])
    data=bpy.data.armatures.new('AssetRig'); rig=bpy.data.objects.new('AssetRig',data)
    bpy.context.collection.objects.link(rig); active(rig); bpy.ops.object.mode_set(mode='EDIT')
    for bone in bones:
        b=data.edit_bones.new(bone['name']); b.head=Vector(bone['head'])*height; b.tail=Vector(bone['tail'])*height
        b.use_deform=bone.get('deform',True)
    for bone in bones:
        if bone.get('parent'): data.edit_bones[bone['name']].parent=data.edit_bones[bone['parent']]
    bpy.ops.object.mode_set(mode='OBJECT')
    active(obj); rig.select_set(True); bpy.context.view_layer.objects.active=rig
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    deform={g.index for g in obj.vertex_groups if g.name in names and data.bones[g.name].use_deform}
    unweighted=sum(not any(g.group in deform and g.weight > 0.00001 for g in v.groups) for v in obj.data.vertices)
    return rig, {'bones':len(bones),'unweighted_vertices':unweighted,'deformation_review_required':True}

def main():
    parser=argparse.ArgumentParser(); parser.add_argument('--input',required=True); parser.add_argument('--output',required=True)
    parser.add_argument('--repair',action='store_true'); parser.add_argument('--quad',action='store_true')
    parser.add_argument('--target-faces',type=int,default=12000); parser.add_argument('--height',type=float,default=2)
    parser.add_argument('--rig'); args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    if args.height <= 0 or not math.isfinite(args.height) or args.target_faces < 100: raise ValueError('Invalid size or face budget')
    source=Path(args.input).resolve(); output=Path(args.output).resolve()
    if output.exists() and any(output.iterdir()): raise ValueError('Output must be empty')
    output.mkdir(parents=True,exist_ok=True)
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    if source.suffix.lower() in ('.glb','.gltf'): bpy.ops.import_scene.gltf(filepath=str(source))
    elif source.suffix.lower()=='.obj': bpy.ops.wm.obj_import(filepath=str(source))
    else: raise ValueError('Input must be GLB, glTF, or OBJ')
    imported=[o for o in bpy.context.scene.objects if o.type=='MESH']
    if not imported: raise ValueError('No mesh objects in source')
    bpy.ops.object.select_all(action='DESELECT')
    copies=[]
    for obj in imported:
        clone=obj.copy(); clone.data=obj.data.copy(); clone.matrix_world=obj.matrix_world.copy(); clone.parent=None
        bpy.context.collection.objects.link(clone); clone.select_set(True); copies.append(clone)
        obj.hide_render=True; obj.hide_set(True)
    bpy.context.view_layer.objects.active=copies[0]
    if len(copies)>1: bpy.ops.object.join()
    obj=bpy.context.object; obj.name='GameAsset'
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    coords=[v.co for v in obj.data.vertices]
    lo=Vector(tuple(min(v[i] for v in coords) for i in range(3))); hi=Vector(tuple(max(v[i] for v in coords) for i in range(3)))
    if hi.z-lo.z < 1e-8: raise ValueError('Source has zero height')
    scale=args.height/(hi.z-lo.z); center=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
    for v in obj.data.vertices: v.co=(v.co-center)*scale
    # Keep the preserved source aligned for later texture projection / retopology.
    for original in imported:
        original.matrix_world.translation=(original.matrix_world.translation-center)*scale
        original.scale *= scale
    cleanup(obj)
    report={'source':str(source),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
        'before':inspect(obj),'height':args.height,'source_preserved_in_blend':True,
        'self_intersections_checked':False,'texture_rebake_required':False,'rig':None}
    if args.repair and not report['before']['closed_manifold']:
        modifier=obj.modifiers.new('Watertight voxel repair','REMESH'); modifier.mode='VOXEL'
        modifier.voxel_size=args.height/256; modifier.use_smooth_shade=True
        bpy.ops.object.modifier_apply(modifier=modifier.name); cleanup(obj)
        report['texture_rebake_required']=True
        report['repair_voxel_size']=args.height/256
    if args.quad:
        active(obj)
        # Dense voxel quads can contain triangulation pathologies which QuadriFlow rejects.
        # Precondition with a modest reduction, preserving the original in the .blend.
        if len(obj.data.polygons)>args.target_faces*6:
            modifier=obj.modifiers.new('QuadriFlow precondition','DECIMATE')
            modifier.ratio=args.target_faces*6/len(obj.data.polygons)
            bpy.ops.object.modifier_apply(modifier=modifier.name); cleanup(obj)
        result=bpy.ops.object.quadriflow_remesh(target_faces=args.target_faces, use_mesh_symmetry=False, use_preserve_sharp=True, use_preserve_boundary=True)
        report['quad_operator_result']=list(result)
        report['quad_succeeded']='FINISHED' in result and len(obj.data.polygons) <= args.target_faces*2
        report['texture_rebake_required']=True; report['topology']='automatic_quad_remesh_requires_art_review'
    elif len(obj.data.polygons)>args.target_faces:
        modifier=obj.modifiers.new('Game mesh reduction','DECIMATE')
        modifier.ratio=args.target_faces/len(obj.data.polygons)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        report['topology']='decimated_not_manual_retopology'
    cleanup(obj)
    if report['texture_rebake_required']:
        # Do not export invalid inherited UV textures as if they survived remeshing.
        obj.data.materials.clear()
        material=bpy.data.materials.new('Preview_needs_texture_rebake'); material.diffuse_color=(0.65,0.65,0.65,1)
        obj.data.materials.append(material)
    report['after']=inspect(obj)
    rig=None
    if args.rig: rig, report['rig']=attach_rig(obj,args.rig,args.height)
    report['export_passed']=report['after']['closed_manifold'] and report.get('quad_succeeded',True) and (report['rig'] is None or report['rig']['unweighted_vertices']==0)
    bpy.ops.wm.save_as_mainfile(filepath=str(output/'asset.blend'))
    (output/'report.json').write_text(json.dumps(report,indent=2))
    if not report['export_passed']: raise RuntimeError('Mesh/skin validation failed; inspect preserved asset.blend and report.json')
    active(obj)
    if rig: rig.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(output/'asset.glb'),export_format='GLB',use_selection=True,export_apply=False)
    print('HARNESS_RESULT',json.dumps(report))

if __name__=='__main__': main()
