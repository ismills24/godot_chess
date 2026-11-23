"""Download the CC0 Poly Haven models used by the inn into artifacts/polyhaven_sources/.

Re-running skips files that are already present and verified. Each asset also gets a
`small/` copy with 512px textures (needs Pillow) for export_inn_props.py. Shrinking happens
here, not in Blender, because Godot cannot decode the JPEGs Blender writes.
  python tools/scene_builders/download_inn_props.py
"""
import hashlib
import json
from pathlib import Path
import shutil
import urllib.request

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCES = ROOT / 'artifacts/polyhaven_sources'
RESOLUTION = '1k'
SMALL_TEXTURE = 512
ASSETS = [
    'round_wooden_table_01', 'gallinera_table', 'wooden_stool_01', 'wooden_stool_02',
    'painted_wooden_bench', 'wine_barrel_01', 'Barrel_01', 'wine_bottles_01', 'jug_01',
    'brass_goblets', 'wooden_candlestick', 'Shelf_01', 'wooden_bucket_01', 'wooden_crate_01',
    'lantern_chandelier_01', 'wooden_bowl_02', 'carved_wooden_plate', 'vintage_oil_lamp',
]


def fetch(url):
    request = urllib.request.Request(url, headers={'User-Agent': 'godot-chess-tools'})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def shrink(folder, files):
    small = folder / 'small'
    for relative in files:
        source, target = folder / relative, small / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        if source.suffix.lower() in ('.jpg', '.jpeg', '.png'):
            with Image.open(source) as image:
                image.thumbnail((SMALL_TEXTURE, SMALL_TEXTURE), Image.Resampling.LANCZOS)
                if source.suffix.lower() == '.png':
                    image.save(target)
                else:
                    image.convert('RGB').save(target, quality=90)
        else:
            shutil.copyfile(source, target)


def main():
    records = []
    for asset in ASSETS:
        folder = SOURCES / asset
        folder.mkdir(parents=True, exist_ok=True)
        manifest = json.loads(fetch('https://api.polyhaven.com/files/' + asset))
        info = json.loads(fetch('https://api.polyhaven.com/info/' + asset))
        gltf = manifest['gltf'][RESOLUTION]['gltf']
        files = {f'{asset}_{RESOLUTION}.gltf': gltf, **gltf.get('include', {})}
        for relative, entry in files.items():
            path = folder / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            if not path.is_file() or hashlib.md5(path.read_bytes()).hexdigest() != entry['md5']:
                path.write_bytes(fetch(entry['url']))
            if hashlib.md5(path.read_bytes()).hexdigest() != entry['md5']:
                raise RuntimeError(f'Checksum mismatch: {path}')
        shrink(folder, files)
        records.append({'id': asset, 'url': 'https://polyhaven.com/a/' + asset, 'license': 'CC0-1.0',
                        'authors': info.get('authors'), 'gltf': f'{asset}/{asset}_{RESOLUTION}.gltf'})
        print('Downloaded and verified', asset, flush=True)
    (SOURCES / 'inn_sources.json').write_text(json.dumps(records, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
