"""Refresh an existing development .blend and delivery pair, preserving authored IDs/geometry.
Usage: blender -b source.blend --python this.py -- output.fbx
"""
from pathlib import Path
import runpy
import sys
import bpy

ROOT=Path(__file__).resolve().parents[1]
runpy.run_path(str(ROOT/'run_preview.py'))
from pcg_grid_preview import authoring as a,assets,preview

scene=bpy.context.scene;path=bpy.data.filepath
if not path:raise ValueError('Open a saved development .blend first')
def signature():
 return {o.pcg_dcc_module.uid:(o.pcg_dcc_module.module_id,a.snapshot(o,False)[:3],tuple(tuple(r) for r in o.matrix_world)) for o in a.modules(scene)}
before=signature()
for obj in a.modules(scene):
 if 'flips' in obj.pcg_dcc_module:del obj.pcg_dcc_module['flips']
for obj in scene.objects:
 if obj.get(a.OWNER):
  for name in ('dcc_front_back','dcc_up_down'):
   if name in obj:del obj[name]
for name in ('locked_front_back','locked_up_down'):
 if name in scene.pcg_dcc:del scene.pcg_dcc[name]
a.sync_reverse(scene)
if scene.pcg_dcc.previews:preview.refresh(scene,True)
out=Path(sys.argv[sys.argv.index('--')+1]).resolve()
data=assets.export(scene,out)
bpy.ops.wm.save_as_mainfile(filepath=path)
assert signature()==before,'Authored UUID, ID, geometry or layout changed'
assert all('allowMirrorX' in m and 'allowFlip' not in m for m in data['modules'])
print('DCC_EXISTING_SOURCE_REFRESH_OK: preserved authored UUID/geometry/layout;',len(data['modules']),'source modules',flush=True)
