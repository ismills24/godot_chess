import json,urllib.request,hashlib,subprocess
from pathlib import Path
root=Path('artifacts/polyhaven_sources'); catalog=json.loads((root/'model_catalog.json').read_text(encoding='utf-8'))
records=[]
for asset in ['wicker_basket_01','rusted_spade_01','hand_plane_no4','handsaw_wood']:
 d=root/asset; d.mkdir(exist_ok=True)
 subprocess.run(['curl.exe','-L','--fail','--silent','--show-error','https://api.polyhaven.com/files/'+asset,'-o',str(d/'manifest.json')],check=True)
 manifest=json.loads((d/'manifest.json').read_text())
 (d/'manifest.json').write_text(json.dumps(manifest,indent=2))
 f=manifest['gltf']['2k']['gltf']; files={asset+'_2k.gltf':f,**f.get('include',{})}
 for rel,info in files.items():
  p=d/rel; p.parent.mkdir(parents=True,exist_ok=True)
  if not p.exists(): subprocess.run(['curl.exe','-L','--fail','--silent','--show-error',info['url'],'-o',str(p)],check=True)
  assert hashlib.md5(p.read_bytes()).hexdigest()==info['md5'],p
 records.append({'id':asset,'url':'https://polyhaven.com/a/'+asset,'license':'CC0-1.0','authors':catalog[asset].get('authors'),'files':list(files)})
 print('Downloaded and verified',asset,flush=True)
(root/'shed_sources.json').write_text(json.dumps(records,indent=2))
