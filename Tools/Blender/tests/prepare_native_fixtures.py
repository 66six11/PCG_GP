"""Build isolated saved-update/invalid fixtures from the demo; never save over its source."""
from pathlib import Path
import runpy
import bpy

ROOT=Path(__file__).resolve().parents[1]
runpy.run_path(str(ROOT/'run_preview.py'))
from pcg_grid_preview import authoring as a
folder=ROOT.parents[1]/'Logs/DCCRebuild/native-fixtures';folder.mkdir(exist_ok=True)
roof=next(o for o in a.modules(bpy.context.scene) if o.pcg_dcc_module.module_id=='building.roofs')
cfg=bpy.context.scene.pcg_dcc;cfg.active=roof
bpy.context.view_layer.objects.active=roof;roof.select_set(True)
original_uid=roof.pcg_dcc_module.uid
roof.pcg_dcc_module.weight=2
roof.location=(17,-9,5);roof.rotation_euler=(.2,.4,.7);roof.scale=(1.3,2,.7)
roof.name='屋顶重命名';roof.data.name='屋顶网格改名'
assert bpy.ops.pcg_dcc.rename(new_id='building.roofs.updated.中文')=={'FINISHED'}
assert roof.pcg_dcc_module.uid==original_uid
bpy.ops.wm.save_as_mainfile(filepath=str(folder/'Updated.blend'))
assert not any(o.get('pcg_transport_error') for o in bpy.context.scene.objects)
bpy.ops.pcg_dcc.rule(action='ADD',target='missing.invalid',face='down',weight=1)
bpy.ops.wm.save_as_mainfile(filepath=str(folder/'Invalid.blend'))
assert any(o.get('pcg_transport_error') for o in bpy.context.scene.objects)
print('DCC_NATIVE_FIXTURES_OK: stable UUID, rename/TRS/base-weight update, invalid save marked',flush=True)
