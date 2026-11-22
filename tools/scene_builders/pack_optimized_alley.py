import bpy,json
from pathlib import Path
root=Path(bpy.data.filepath).parent;s=bpy.context.scene;s.cycles.device='CPU'
for im in bpy.data.images:
 if im.source!='FILE' or im.type!='IMAGE':continue
 path=root/'optimized_textures'/Path(im.filepath).name
 assert path.exists(),path
 if im.packed_file:im.unpack(method='REMOVE')
 im.filepath=str(path);im.reload();assert not im.is_float,im.name;im.pack();im.buffers_free()
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(root/'victorian_alley_optimized.blend'),compress=True)
print('8-bit textures packed. No render performed.',flush=True)
