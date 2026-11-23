"""Resolve harness settings: BLENDER env var > config.local.json (git-ignored) > config.json.

Run directly to print one resolved value, or the whole config as JSON:
  python tools/asset_harness/harness_config.py blender
  python tools/asset_harness/harness_config.py
"""
import json
import os
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent

def load():
    config = json.loads((HERE/'config.json').read_text())
    local = HERE/'config.local.json'
    if local.is_file(): config.update(json.loads(local.read_text()))
    if os.environ.get('BLENDER'): config['blender'] = os.environ['BLENDER']
    return config

if __name__ == '__main__':
    config = load()
    print(config[sys.argv[1]] if len(sys.argv) > 1 else json.dumps(config, indent=2))
