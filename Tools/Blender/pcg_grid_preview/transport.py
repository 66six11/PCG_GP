"""Persist native Unity import snapshots on save; no external converter or sidecar."""
import json
import uuid
import bpy
from bpy.app.handlers import persistent
from . import authoring as a,assets

MARKER='pcg_dcc_transport'

def discard(collection):
 for obj in list(collection.objects):
  mesh=obj.data;bpy.data.objects.remove(obj,do_unlink=True)
  if isinstance(mesh,bpy.types.Mesh) and not mesh.users:bpy.data.meshes.remove(mesh)
 bpy.data.collections.remove(collection)

def prepare(scene):
 """Atomically replace hidden canonical snapshots, leaving authoring layout alone."""
 if not a.modules(scene):
  for col in list(scene.collection.children):
   if col.get(MARKER):discard(col)
  return
 invalid=[r for r in a.validate(scene) if r['kind']=='INVALID']
 if invalid:raise ValueError(invalid[0]['message'])
 data,sources=assets.manifest(scene,'native.blend')
 col=bpy.data.collections.new('PCG Unity Transfer '+uuid.uuid4().hex[:8]);col[MARKER]=True
 scene.collection.children.link(col)
 root=bpy.data.objects.new('PCG Unity Manifest',None);col.objects.link(root)
 root['pcg_kind']='manifest';root['pcg_manifest_json']=json.dumps(data,ensure_ascii=False,separators=(',',':'))
 root['pcg_transport_version']=1
 try:
  for source,entry in zip(sources,data['modules']):
   mesh=assets.source_mesh(scene,source,entry)
   mesh.name='transfer_'+entry['meshName']
   obj=bpy.data.objects.new('transfer_'+entry['meshName'],mesh);col.objects.link(obj);obj.parent=root
   obj['pcg_kind']='module';obj['pcg_uid']=source.uid
  for obj in col.objects:
   obj[a.OWNER]='unity-transfer';obj[a.ROLE]='transport';obj.hide_select=True;obj.hide_render=True
  col.hide_viewport=True;col.hide_render=True
 except Exception:
  discard(col);raise
 for old in list(scene.collection.children):
  if old!=col and old.get(MARKER):discard(old)
 # Replacement creation happens while the old names still exist. Restore stable
 # names after swapping, otherwise Blender's .001 suffix would alternate on save.
 root.name='PCG Unity Manifest'
 by_uid={entry['id']:entry for entry in data['modules']}
 for obj in col.objects:
  if obj.get('pcg_kind')=='module':
   name='transfer_'+by_uid[obj['pcg_uid']]['meshName'];obj.name=name;obj.data.name=name
 col.name='PCG Unity Transfer'

@persistent
def before_save(_):
 # Evaluate modifiers in Object Mode exactly as the formal exporter does.
 scene=bpy.context.scene
 if scene is None or (not a.modules(scene) and not any(col.get(MARKER) for col in scene.collection.children)):return
 active=bpy.context.view_layer.objects.active;mode=active.mode if active else 'OBJECT'
 a.object_mode(bpy.context)
 try:
  for scene in bpy.data.scenes:
   # Only the active scene has the matching evaluated dependency graph.
   if scene!=bpy.context.scene:continue
   try:prepare(scene)
   except Exception as error:
    # Never publish an old snapshot as though it represented the new save.
    for col in scene.collection.children:
     if col.get(MARKER):
      for obj in col.objects:
       if obj.get('pcg_kind')=='manifest':obj['pcg_transport_error']=str(error)
    if not any(col.get(MARKER) for col in scene.collection.children):
     col=bpy.data.collections.new('PCG Unity Transfer');col[MARKER]=True;scene.collection.children.link(col)
     root=bpy.data.objects.new('PCG Unity Manifest',None);col.objects.link(root)
     root['pcg_kind']='manifest';root['pcg_transport_error']=str(error)
    scene.pcg_dcc.status='Unity 规则快照失败：'+str(error)
    print('DCC_UNITY_TRANSFER_FAILED:',error,flush=True)
 finally:
  if active and active.name in bpy.context.view_layer.objects:
   bpy.context.view_layer.objects.active=active
   if mode=='EDIT':bpy.ops.object.mode_set(mode='EDIT')
