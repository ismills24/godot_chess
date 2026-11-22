"""Download official native ComfyUI weights, resume partial files, verify SHA-256."""
import concurrent.futures
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
COMFY = ROOT / '.tools/ComfyUI_windows_portable/ComfyUI'
MODELS = [
    ('Comfy-Org/TRELLIS.2', 'diffusion_models/trellis_2_int8_convrot.safetensors', 'd01952ad137213f6a868f86b6b877026276f84af5eec23069217475a0bad3a31'),
    ('Comfy-Org/TRELLIS.2', 'vae/trellis_2_shape_vae_bf16.safetensors', 'de0cb4949a76c59ee5c091a995a69bcc8c51d5aeda939f0c641a50d2a72341f4'),
    ('Comfy-Org/TRELLIS.2', 'vae/trellis_2_texture_vae_bf16.safetensors', '714e5ebf094a610e12a8e3b5175c18a62f37f6ea4218acb6073644456b73ab0e'),
    ('Comfy-Org/Pixal3D', 'clip_vision/dino_v3_L_naf_fp32.safetensors', '4ad2ec4e0879a5b5b04cd97325cc37da954a7b6edca5170b86510f17f2b2290f'),
    ('Comfy-Org/BiRefNet', 'background_removal/birefnet.safetensors', '9ab37426bf4de0567af6b5d21b16151357149139362e6e8992021b8ce356a154'),
]

def digest(path):
    with path.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def download(item):
    repo, name, expected = item
    target = COMFY / 'models' / name
    target.parent.mkdir(parents=True, exist_ok=True)
    if target.exists():
        if digest(target) != expected:
            raise RuntimeError(f'Existing file hash mismatch; inspect before replacing: {target}')
    else:
        part = target.with_suffix(target.suffix + '.part')
        url = f'https://huggingface.co/{repo}/resolve/main/{name}'
        print(f'Downloading {name}', flush=True)
        subprocess.run(['curl.exe', '-fL', '--silent', '--show-error', '--retry', '4', '-C', '-', '-o', str(part), url], check=True)
        if digest(part) != expected:
            raise RuntimeError(f'Download hash mismatch: {part}')
        part.rename(target)
    print(f'Verified {name}', flush=True)
    return {'repository': repo, 'file': name, 'sha256': expected, 'bytes': target.stat().st_size}

if __name__ == '__main__':
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        results = list(pool.map(download, MODELS))
    (ROOT / '.tools/model_manifest.json').write_text(json.dumps(results, indent=2))
