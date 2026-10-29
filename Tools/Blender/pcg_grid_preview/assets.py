"""Export explicit source assets, never the generated preview scene."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile
import bpy
from bpy.props import StringProperty
from . import authoring as a,core

def _export_sources(scene):
 descriptions=[a.descriptor(o) for o in a.modules(scene) if core.state_of(o.pcg_dcc_module.corners) not in (0,255)]
 data=json.loads(Path(__file__).with_name('base_meshes.json').read_text(encoding='utf-8'))
 # Canonical source families, with all poses, keep the same semantics on the
 # irregular grid when actual shared local faces are not nominal opposites.
 for state in data['seeds']:
  v,f=core.base_mesh(state);descriptions.append(core.Module('builtin.base.s'+str(state).zfill(3),'base.s'+str(state).zfill(3),
             state,v,core.triangles(f),weight=.25,mirror=True,generated=True))
 return descriptions

def manifest(scene,fbx_name):
 sources=_export_sources(scene)
 boundaries=[core.Module('$empty','$empty',0,(),(),turns=(0,)),core.Module('$solid','$solid',255,(),(),turns=(0,))]
 variants=core.variants(sources+boundaries);by_uid={m.uid:[v for v in variants if v.module.uid==m.uid] for m in sources}
 records=[]
 for source in sources:
  faces={}
  for face,name in enumerate(core.FACE_NAMES):
   allowed={}
   for left in by_uid[source.uid]:
    fa=core.transform_face(face,*left.pose);fb=core.OPPOSITE[fa]
    key=left.port(fa,core.cube_orientation(fa))
    for right in variants:
     if key!=right.port(fb,core.cube_orientation(fb)) or not core.semantic(left,fa,right,fb):continue
     w=left.preference(fa,right.module.id) if fa in left.rules else right.preference(fb,left.module.id)
     allowed.setdefault(right.module.uid,w)
   # A preview crop is open; a real runtime map boundary must be explicit and
   # have no contour. Auto faces get the geometry-safe outside boundary too.
   if not core.contour(source.vertices,source.triangles,face):
    rules=source.rules.get(name)
    if rules is None or any(r['id']=='$outside' for r in rules):allowed['$outside']=next((r['weight'] for r in rules or [] if r['id']=='$outside'),1)
   faces[name]=[dict(id=target,weight=weight) for target,weight in sorted(allowed.items())]
  records.append(dict(id=source.uid,logicalId=source.id,displayName=source.id,
     meshName='dcc_'+hashlib.sha256(source.uid.encode()).hexdigest()[:24],corners=[(source.state>>i)&1 for i in range(8)],
     weight=source.weight,yawDegrees=[t*90 for t in source.turns],allowMirrorX=source.mirror,neighbors=faces))
 return dict(schemaVersion=1,tilesetId='pcg.dcc.v2',coordinateSystem='Unity_Y_Up',cornerOrder='V1_V2_V3_V4_V5_V6_V7_V8',modelFile=fbx_name,modules=records),sources

def export(scene,filename):
 active=bpy.context.view_layer.objects.active;mode=active.mode if active else 'OBJECT'
 a.object_mode(bpy.context)
 try:return _export_object_mode(scene,filename)
 finally:
  if active and active.name in bpy.context.view_layer.objects:
   bpy.context.view_layer.objects.active=active
   if mode=='EDIT':bpy.ops.object.mode_set(mode='EDIT')

def source_mesh(scene,source,entry):
 """One canonical mesh snapshot shared by native-development and FBX delivery."""
 obj=next((o for o in a.modules(scene) if o.pcg_dcc_module.uid==source.uid),None)
 if obj:
  deps=bpy.context.evaluated_depsgraph_get();ev=obj.evaluated_get(deps)
  mesh=bpy.data.meshes.new_from_object(ev,preserve_all_data_layers=True,depsgraph=deps)
 else:
  mesh=bpy.data.meshes.new(entry['meshName']);mesh.from_pydata(source.vertices,[],source.triangles);mesh.update()
 mesh.name=entry['meshName'];return mesh

def _export_object_mode(scene,filename):
 result=a.validate(scene)
 invalid=[r for r in result if r['kind']=='INVALID']
 if invalid:raise ValueError('先修复注册表失效项：'+invalid[0]['message'])
 path=Path(filename).with_suffix('.fbx');path.parent.mkdir(parents=True,exist_ok=True)
 data,sources=manifest(scene,path.name);selected=list(bpy.context.selected_objects);active=bpy.context.view_layer.objects.active
 mode=active.mode if active else 'OBJECT';temporary=[]
 try:
  a.object_mode(bpy.context)
  for obj in bpy.context.selected_objects:obj.select_set(False)
  for source,entry in zip(sources,data['modules']):
   mesh=source_mesh(scene,source,entry)
   temp=bpy.data.objects.new(entry['meshName'],mesh);scene.collection.objects.link(temp)
   temp.select_set(True);bpy.context.view_layer.objects.active=temp;temporary.append(temp)
  with tempfile.TemporaryDirectory(prefix='dcc-export-',dir=path.parent) as folder:
   staged=Path(folder)/path.name
   bpy.ops.export_scene.fbx(filepath=str(staged),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',
      use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=False,global_scale=1,apply_unit_scale=False,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True)
   descriptor=staged.with_suffix('.tileset.json');descriptor.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
   out_json=path.with_suffix('.tileset.json');backup=Path(folder)/'previous.fbx';backup_json=Path(folder)/'previous.json'
   existed=path.exists();had_json=out_json.exists()
   if existed:shutil.copy2(path,backup)
   if had_json:shutil.copy2(out_json,backup_json)
   try:os.replace(staged,path);os.replace(descriptor,out_json)
   except Exception:
    if existed:os.replace(backup,path)
    elif path.exists():path.unlink()
    if had_json:os.replace(backup_json,out_json)
    elif out_json.exists():out_json.unlink()
    raise
 finally:
  for obj in temporary:
   mesh=obj.data;bpy.data.objects.remove(obj,do_unlink=True)
   if mesh.users==0:bpy.data.meshes.remove(mesh)
  for obj in selected:
   if obj.name in bpy.context.view_layer.objects:obj.select_set(True)
  if active and active.name in bpy.context.view_layer.objects:
   bpy.context.view_layer.objects.active=active
   if mode=='EDIT':bpy.ops.object.mode_set(mode='EDIT')
 return data

class DCC_OT_export(bpy.types.Operator):
 bl_idname='pcg_dcc.export';bl_label='导出源模块 FBX + JSON'
 filepath:StringProperty(subtype='FILE_PATH',default='DCCModules.fbx')
 def invoke(self,context,event):context.window_manager.fileselect_add(self);return {'RUNNING_MODAL'}
 def execute(self,context):
  try:
   data=export(context.scene,self.filepath);context.scene.pcg_dcc.status=f"已导出 {len(data['modules'])} 个源模块；不包含预览副本。"
   return {'FINISHED'}
  except Exception as e:self.report({'ERROR'},str(e));return {'CANCELLED'}

CLASSES=(DCC_OT_export,)
