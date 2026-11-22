"""Convert only the alley working PNGs to 8-bit; source downloads stay untouched."""
from PIL import Image
from pathlib import Path
root=Path(__file__).resolve().parents[2]/'artifacts/victorian_alley_v2/optimized_textures'
for p in root.glob('*.png'):
 with Image.open(p) as im:
  out=im.convert('RGBA' if 'A' in im.getbands() else 'RGB');out.save(p,optimize=True)
print('Converted working textures to 8-bit PNG',flush=True)
