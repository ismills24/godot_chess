"""Agent-neutral local ComfyUI / Blender command line (Python standard library)."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import time
import urllib.request
import urllib.error
import uuid

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
CONFIG = json.loads((HERE/'config.json').read_text())
COMFY = ROOT / CONFIG['comfy_portable'] / 'ComfyUI'

def request(path, body=None):
    req = urllib.request.Request(CONFIG['comfy_url'] + path,
        data=json.dumps(body).encode() if body is not None else None,
        headers={'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(req, timeout=30) as response:
            return json.load(response)
    except urllib.error.HTTPError as e:
        raise RuntimeError(e.read().decode()) from e

def doctor():
    stats = request('/system_stats')
    schemas = request('/object_info')
    from build_workflows import build
    missing = sorted({n['class_type'] for n in build(True).values()} - schemas.keys())
    from download_models import MODELS
    absent = [name for _, name, _ in MODELS if not (COMFY/'models'/name).is_file()]
    report = {'blender': CONFIG['blender'], 'blender_exists': Path(CONFIG['blender']).is_file(),
              'comfy': str(COMFY), 'endpoint': CONFIG['comfy_url'],
              'version': stats['system']['comfyui_version'], 'devices': stats['devices'],
              'missing_nodes': missing, 'missing_models': absent}
    print(json.dumps(report, indent=2))
    if missing or absent or not report['blender_exists']:
        raise RuntimeError('Harness dependencies are incomplete')

def generate(args):
    reference = Path(args.reference).resolve(strict=True)
    if reference.suffix.lower() not in ('.png', '.jpg', '.jpeg', '.webp'):
        raise ValueError('Use a PNG, JPEG or WebP reference')
    if args.humanoid and not args.pose_confirmed:
        raise ValueError('Humanoid references must already show A/T pose, separated limbs and spread fingers; inspect then pass --pose-confirmed')
    job = uuid.uuid4().hex[:12]
    directory = ROOT / CONFIG['output_dir'] / job
    directory.mkdir(parents=True)
    image_name = f'harness_{job}{reference.suffix.lower()}'
    shutil.copy2(reference, COMFY/'input'/image_name)
    workflow_name = 'trellis2_512_textured' if args.textured else 'trellis2_512_geometry'
    graph = json.loads((HERE/f'{workflow_name}.api.json').read_text())
    graph['1']['inputs']['image'] = image_name
    graph['31']['inputs']['filename_prefix'] = f'asset_harness/{job}/model'
    for node in graph.values():
        if node['class_type'] == 'KSampler': node['inputs']['seed'] = args.seed
    (directory/'prompt.json').write_text(json.dumps(graph, indent=2))
    response = request('/prompt', {'prompt': graph, 'client_id': f'asset-harness-{job}'})
    if response.get('node_errors'): raise RuntimeError(response['node_errors'])
    manifest = {'job': job, 'prompt_id': response['prompt_id'], 'reference': str(reference),
        'workflow': workflow_name, 'seed': args.seed, 'humanoid': args.humanoid,
        'pose_reviewed': args.pose_confirmed, 'directory': str(directory)}
    (directory/'job.json').write_text(json.dumps(manifest, indent=2))
    print(json.dumps(manifest, indent=2), flush=True)
    if args.wait:
        deadline = time.monotonic() + args.timeout
        while not status(str(directory/'job.json')):
            if time.monotonic() > deadline:
                raise TimeoutError('Job still queued/running; use status with job.json. It has not been cancelled.')
            time.sleep(5)

def status(manifest_path):
    manifest = json.loads(Path(manifest_path).read_text())
    history = request('/history/' + manifest['prompt_id']).get(manifest['prompt_id'])
    if not history:
        print('Queued or running:', manifest['prompt_id'], flush=True)
        return False
    directory = Path(manifest['directory'])
    (directory/'history.json').write_text(json.dumps(history, indent=2))
    if history['status'].get('status_str') == 'error':
        raise RuntimeError(json.dumps(history['status'], indent=2))
    if not history['status'].get('completed'): return False
    # This harness runs against the configured local installation, not an arbitrary server.
    source = COMFY/'output'/'asset_harness'/manifest['job']
    files = sorted(source.glob('*.glb'))
    if not files: raise RuntimeError(f'Completed without a GLB in {source}; inspect history.json')
    for path in files: shutil.copy2(path, directory/path.name)
    manifest['generated_models'] = [str(directory/path.name) for path in files]
    manifest['status'] = 'generated'
    Path(manifest_path).write_text(json.dumps(manifest, indent=2))
    print(json.dumps({'status': 'generated', 'models': manifest['generated_models']}, indent=2))
    return True

def process(args):
    output = Path(args.output).resolve()
    if output.exists() and any(output.iterdir()):
        raise ValueError('Choose an empty output directory; existing work will not be overwritten')
    command = [CONFIG['blender'], '--background', '--factory-startup', '--python-exit-code', '1',
        '--python', str(HERE/'blender_process.py'), '--', '--input', str(Path(args.input).resolve(strict=True)),
        '--output', str(output), '--target-faces', str(args.target_faces), '--height', str(args.height)]
    if args.repair: command += ['--repair']
    if args.quad: command += ['--quad']
    if args.rig: command += ['--rig', str(Path(args.rig).resolve(strict=True))]
    subprocess.run(command, check=True)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    sub.add_parser('doctor')
    gen = sub.add_parser('generate')
    gen.add_argument('reference'); gen.add_argument('--textured', action='store_true')
    gen.add_argument('--humanoid', action='store_true'); gen.add_argument('--pose-confirmed', action='store_true')
    gen.add_argument('--seed', type=int, default=42); gen.add_argument('--wait', action='store_true')
    gen.add_argument('--timeout', type=int, default=1800)
    stat = sub.add_parser('status'); stat.add_argument('manifest')
    proc = sub.add_parser('process'); proc.add_argument('input'); proc.add_argument('output')
    proc.add_argument('--repair', action='store_true'); proc.add_argument('--quad', action='store_true')
    proc.add_argument('--target-faces', type=int, default=12000); proc.add_argument('--height', type=float, default=2)
    proc.add_argument('--rig', help='JSON bone landmarks in normalized Blender Z-up coordinates')
    args = parser.parse_args()
    if args.command == 'doctor': doctor()
    elif args.command == 'generate': generate(args)
    elif args.command == 'status': status(args.manifest)
    else: process(args)
