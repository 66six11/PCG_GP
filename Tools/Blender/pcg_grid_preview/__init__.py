bl_info={
 'name':'PCG DCC Module Authoring','author':'PCG Tools','version':(2,1,4),'blender':(4,5,0),
 'location':'3D View > N > PCG DCC','category':'Mesh',
 'description':'Hierarchical module registry and editable single-cell direct-neighbor previews'}

import bpy
from bpy.app.handlers import persistent
from bpy.props import PointerProperty
from . import authoring,preview,ui,assets,transport

_REGISTERED=False

@persistent
def _reset(*args):
 authoring._CACHE.clear()

@persistent
def _dirty(scene,depsgraph):
 if not hasattr(scene,'pcg_dcc') or scene.pcg_dcc.busy:return
 watched={o.data.as_pointer() for o in authoring.modules(scene)}
 for item in depsgraph.updates:
  data=item.id.original
  if data.as_pointer() in watched:
   authoring._CACHE.setdefault(scene.as_pointer(),{})['dirty']=True;return

def _tick():
 if not _REGISTERED:return None
 try:
  scene=bpy.context.scene
  if scene and hasattr(scene,'pcg_dcc'):
   cfg=scene.pcg_dcc;obj=bpy.context.object
   if obj and obj.type=='MESH' and not obj.get(authoring.OWNER) and obj.pcg_dcc_module.enabled and cfg.active!=obj:cfg.active=obj
   if cfg.previews and cfg.live:preview.refresh(scene)
   authoring.names(scene)
 except (ReferenceError,RuntimeError,ValueError) as e:
  if bpy.context.scene and hasattr(bpy.context.scene,'pcg_dcc'):bpy.context.scene.pcg_dcc.status='预览更新失败：'+str(e)
 return .2

def register():
 global _REGISTERED
 if _REGISTERED:return
 for cls in authoring.CLASSES:bpy.utils.register_class(cls)
 bpy.types.Object.pcg_dcc_module=PointerProperty(type=authoring.DCCModule)
 bpy.types.Scene.pcg_dcc=PointerProperty(type=authoring.DCCSettings)
 for cls in ui.CLASSES:bpy.utils.register_class(cls)
 for cls in assets.CLASSES:bpy.utils.register_class(cls)
 preview.register()
 for collection,handler in ((bpy.app.handlers.depsgraph_update_post,_dirty),(bpy.app.handlers.undo_post,_reset),
   (bpy.app.handlers.redo_post,_reset),(bpy.app.handlers.load_post,_reset),(bpy.app.handlers.save_pre,transport.before_save)):
  if handler not in collection:collection.append(handler)
 _REGISTERED=True;bpy.app.timers.register(_tick,first_interval=.3,persistent=True)

def unregister():
 global _REGISTERED
 if not _REGISTERED:return
 _REGISTERED=False
 if bpy.app.timers.is_registered(_tick):bpy.app.timers.unregister(_tick)
 for collection,handler in ((bpy.app.handlers.depsgraph_update_post,_dirty),(bpy.app.handlers.undo_post,_reset),
   (bpy.app.handlers.redo_post,_reset),(bpy.app.handlers.load_post,_reset),(bpy.app.handlers.save_pre,transport.before_save)):
  if handler in collection:collection.remove(handler)
 preview.unregister();authoring._CACHE.clear()
 for cls in reversed(assets.CLASSES):bpy.utils.unregister_class(cls)
 for cls in reversed(ui.CLASSES):bpy.utils.unregister_class(cls)
 del bpy.types.Scene.pcg_dcc;del bpy.types.Object.pcg_dcc_module
 for cls in reversed(authoring.CLASSES):bpy.utils.unregister_class(cls)
