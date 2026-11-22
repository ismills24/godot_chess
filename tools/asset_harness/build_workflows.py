"""Native TRELLIS.2 only; no Pixal3D/MoGe weights or custom nodes required."""
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]

def build(textured=False):
    graph = {}
    def node(id, kind, **inputs):
        graph[str(id)] = {'class_type': kind, 'inputs': inputs}
    node(1, 'LoadImage', image='reference.png')
    node(2, 'LoadBackgroundRemovalModel', bg_removal_name='birefnet.safetensors')
    node(3, 'RemoveBackground', bg_removal_model=['2', 0], image=['1', 0])
    node(4, 'ImageCropToMask', images=['1', 0], masks=['3', 0], width=1024, height=1024, pad_factor=1.0, grow_mask=0, background='#000000')
    node(5, 'CLIPVisionLoader', clip_name='dino_v3_L_naf_fp32.safetensors')
    node(6, 'Trellis2Conditioning', clip_vision_model=['5', 0], image=['4', 0])
    node(7, 'UNETLoader', unet_name='trellis_2_int8_convrot.safetensors', weight_dtype='default')
    node(8, 'VAELoader', vae_name='trellis_2_shape_vae_bf16.safetensors')
    node(9, 'CFGOverride', model=['7', 0], cfg=1, start_percent=0.667, end_percent=1)
    node(10, 'RescaleCFG', model=['9', 0], multiplier=0.7)
    node(11, 'ModelSamplingSD3', model=['10', 0], shift=5)
    node(12, 'EmptyTrellis2LatentStructure', batch_size=1)
    node(13, 'KSampler', model=['11', 0], positive=['6', 0], negative=['6', 1], latent_image=['12', 0], seed=56, steps=12, cfg=7.5, sampler_name='euler', scheduler='normal', denoise=1)
    node(14, 'VaeDecodeStructureTrellis2', samples=['13', 0], vae=['8', 0], resolution='32')
    node(15, 'Trellis2ShapeStage', positive=['6', 0], negative=['6', 1], voxel=['14', 0])
    node(16, 'CFGOverride', model=['7', 0], cfg=1, start_percent=0.769, end_percent=1)
    node(17, 'RescaleCFG', model=['16', 0], multiplier=0.5)
    node(18, 'KSampler', model=['17', 0], positive=['15', 0], negative=['15', 1], latent_image=['15', 2], seed=42, steps=20, cfg=7.5, sampler_name='euler', scheduler='normal', denoise=1)
    node(19, 'VaeDecodeShapeTrellis', samples=['18', 0], vae=['8', 0])
    mesh = ['19', 0]
    if textured:
        node(20, 'VAELoader', vae_name='trellis_2_texture_vae_bf16.safetensors')
        node(21, 'Trellis2TextureStage', positive=['15', 0], negative=['15', 1], shape_latent=['18', 0])
        node(22, 'KSampler', model=['7', 0], positive=['21', 0], negative=['21', 1], latent_image=['21', 2], seed=43, steps=12, cfg=1, sampler_name='euler', scheduler='normal', denoise=1)
        node(23, 'VaeDecodeTextureTrellis', samples=['22', 0], vae=['20', 0], shape_subdivides=['19', 1])
        node(27, 'DecimateMesh', mesh=mesh, target_face_count=25000, placement_mode='midpoint')
        node(24, 'UnwrapMesh', mesh=['27', 0], segmenter='pec', resolution=1024, padding=2, weld_distance=0.0002)
        node(25, 'BakeTextureFromVoxel', mesh=['24', 0], voxel_colors=['23', 0], texture_size=1024, reference_mesh=mesh)
        node(26, 'ApplyTextureToMesh', mesh=['24', 0], base_color=['25', 0], metallic=['25', 1], roughness=['25', 2])
        mesh = ['26', 0]
    node(30, 'MeshToFile3D', mesh=mesh)
    node(31, 'Save3DAdvanced', model_3d=['30', 0], filename_prefix='asset_harness/model', viewport_state='', width=1024, height=1024)
    return graph

def ui_workflow(graph, schemas):
    """Produce a regular editable UI graph as well as the API graph."""
    nodes, links = [], []
    outputs = {}
    depth_counts = {}
    depths = {}
    for id, data in graph.items():
        schema = schemas[data['class_type']]
        inputs, widgets = [], []
        specs = {**schema['input'].get('required', {}), **schema['input'].get('optional', {})}
        parents = [v[0] for v in data['inputs'].values() if isinstance(v, list)]
        depth = max((depths.get(p, 0) + 1 for p in parents), default=0)
        depths[id] = depth
        row = depth_counts.get(depth, 0); depth_counts[depth] = row + 1
        for name, spec in specs.items():
            if name not in data['inputs']:
                continue
            value = data['inputs'][name]
            if isinstance(value, list):
                link_id = len(links) + 1
                links.append([link_id, int(value[0]), value[1], int(id), len(inputs), spec[0]])
                outputs.setdefault((value[0], value[1]), []).append(link_id)
                inputs.append({'name': name, 'type': spec[0], 'link': link_id})
            else:
                widgets.append(value[name] if isinstance(value, dict) and name in value else value)
                if name in ('seed', 'noise_seed'):
                    widgets.append('fixed')
        nodes.append({'id': int(id), 'type': data['class_type'], 'pos': [depth * 340, row * 420],
            'size': [310, 340], 'flags': {}, 'order': len(nodes), 'mode': 0,
            'inputs': inputs,
            'outputs': [{'name': name, 'type': typ, 'links': []} for name, typ in zip(schema.get('output_name', schema['output']), schema['output'])],
            'properties': {'Node name for S&R': data['class_type']}, 'widgets_values': widgets})
    for node in nodes:
        for slot, output in enumerate(node['outputs']):
            output['links'] = outputs.get((str(node['id']), slot), [])
    return {'last_node_id': 31, 'last_link_id': len(links), 'nodes': nodes, 'links': links, 'groups': [], 'config': {}, 'extra': {}, 'version': 0.4}

if __name__ == '__main__':
    schemas = json.loads((ROOT / '.tools/comfy_object_info.json').read_text(encoding='utf-8-sig'))
    for textured in (False, True):
        name = 'trellis2_512_textured' if textured else 'trellis2_512_geometry'
        graph = build(textured)
        for id, data in graph.items():
            required = schemas[data['class_type']]['input'].get('required', {})
            missing = set(required) - set(data['inputs'])
            extra = set(data['inputs']) - set(required) - set(schemas[data['class_type']]['input'].get('optional', {}))
            if missing or extra:
                raise ValueError((id, data['class_type'], missing, extra))
        (HERE / f'{name}.api.json').write_text(json.dumps(graph, indent=2))
        (HERE / f'{name}.workflow.json').write_text(json.dumps(ui_workflow(graph, schemas), indent=2))
        print('Built', name)
