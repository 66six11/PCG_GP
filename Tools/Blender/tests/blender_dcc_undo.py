"""Native undo/redo and data-link lifecycle acceptance."""
from pathlib import Path
import runpy
import bpy
ROOT=Path(__file__).resolve().parents[1]
runpy.run_path(str(ROOT/'run_preview.py'))
from pcg_grid_preview import authoring as a,preview
scene=bpy.context.scene
for obj in list(scene.objects):bpy.data.objects.remove(obj,do_unlink=True)
bpy.ops.pcg_dcc.create(module_id='building.roof')
scene.pcg_dcc.previews=True;preview.refresh(scene,True)
uid=scene.pcg_dcc.active.pcg_dcc_module.uid
bpy.ops.ed.undo_push(message='DCC before child')
bpy.ops.pcg_dcc.create(module_id='building.roof.window',corners=tuple(scene.pcg_dcc.active.pcg_dcc_module.corners))
child_uid=scene.pcg_dcc.active.pcg_dcc_module.uid
bpy.ops.ed.undo_push(message='DCC after child')
assert len(a.modules(bpy.context.scene))==2
assert bpy.ops.ed.undo()=={'FINISHED'}
scene=bpy.context.scene;preview.refresh(scene,True)
assert {o.pcg_dcc_module.uid for o in a.modules(scene)}=={uid}
assert scene.pcg_dcc.active.pcg_dcc_module.uid==uid
assert all(o.data==scene.pcg_dcc.active.data for o in scene.objects if o.get(a.ROLE)=='core')
assert bpy.ops.ed.redo()=={'FINISHED'}
scene=bpy.context.scene;preview.refresh(scene,True)
assert {o.pcg_dcc_module.uid for o in a.modules(scene)}=={uid,child_uid}
assert scene.pcg_dcc.active.pcg_dcc_module.uid==child_uid
print('DCC_UNDO_REDO_OK: module identity, source geometry and editable preview links restored',flush=True)
