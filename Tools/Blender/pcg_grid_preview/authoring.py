"""Persistent Blender document model and undoable authoring operations."""
import json
import re
import uuid
import bpy
import bmesh
from mathutils import Matrix,Vector
from bpy.props import (BoolProperty,BoolVectorProperty,CollectionProperty,EnumProperty,FloatProperty,
                       IntProperty,PointerProperty,StringProperty)
from . import core

OWNER='pcg_dcc_generated'
ROLE='pcg_dcc_role'
_CACHE={}
FACE_ITEMS=[(n,t,'') for n,t in zip(core.FACE_NAMES,('上 +Y','下 -Y','前 +Z','右 +X','后 -Z','左 -X'))]

def changed(self,context):
 if context and context.scene:_CACHE.setdefault(context.scene.as_pointer(),{})['dirty']=True

def redraw_viewports(self,context):
 if not context:return
 for window in context.window_manager.windows:
  for area in window.screen.areas:
   if area.type=='VIEW_3D':area.tag_redraw()

def dimensions_changed(self,context):
 if self.core_layer>=self.layers:self.core_layer=self.layers-1
 changed(self,context)

def source_poll(self,obj):return obj.type=='MESH' and not obj.get(OWNER)

class DCCRule(bpy.types.PropertyGroup):
 face:EnumProperty(items=FACE_ITEMS)
 target:StringProperty(name='候选 ID')
 weight:FloatProperty(name='组合权重',default=1,min=.0001,max=10000)
 automatic:BoolProperty(default=False)
 origin_uid:StringProperty()
 origin_face:StringProperty()
 origin_target:StringProperty()

class DCCModule(bpy.types.PropertyGroup):
 enabled:BoolProperty(default=False)
 uid:StringProperty()
 module_id:StringProperty(name='层级 ID',default='building.module',update=changed)
 corners:BoolVectorProperty(name='启用顶点',size=8,update=changed)
 weight:FloatProperty(name='基础权重',default=1,min=.0001,max=10000,update=changed)
 turns:BoolVectorProperty(name='允许旋转',size=4,default=(True,True,True,True),update=changed)
 mirror:BoolProperty(name='允许镜像',default=False,update=changed)
 explicit:BoolVectorProperty(size=6,update=changed)
 rules:CollectionProperty(type=DCCRule)
 built_state:IntProperty(default=-1)

class DCCName(bpy.types.PropertyGroup):
 collapsed:BoolProperty(default=False)

class DCCIssue(bpy.types.PropertyGroup):
 uid:StringProperty()
 kind:StringProperty()
 message:StringProperty()
 face:IntProperty(default=-1)
 vertex:IntProperty(default=-1)

class DCCSettings(bpy.types.PropertyGroup):
 active:PointerProperty(name='当前模块',type=bpy.types.Object,poll=source_poll,update=changed)
 names:CollectionProperty(type=DCCName)
 issues:CollectionProperty(type=DCCIssue)
 face:EnumProperty(name='邻接面',items=FACE_ITEMS,update=changed)
 live:BoolProperty(name='实时预览',default=True)
 show_diagnostic_faces:BoolProperty(name='显示诊断面',description='显示约束与几何诊断的红黄半透明面；关闭不影响检查、文字与线框',default=True,update=redraw_viewports)
 registry_search:StringProperty(name='搜索模块',description='按 ID、对象名或唯一键筛选',options={'SKIP_SAVE'})
 registry_raw:BoolProperty(name='显示原始规则 JSON',default=False,options={'SKIP_SAVE'})
 seed:IntProperty(name='组合种子',default=42,min=0,update=changed)
 layers:IntProperty(name='预览层数',default=3,min=3,max=5,update=dimensions_changed)
 core_layer:IntProperty(name='模块所在层',default=1,min=0,max=4,update=dimensions_changed)
 height:FloatProperty(name='层高',default=1,min=.1,max=3,update=changed)
 spacing:FloatProperty(name='三组间距',default=9,min=6,max=30,update=changed)
 skew:FloatProperty(name='网格偏斜',default=0,min=0,max=1,update=changed)
 include_base:BoolProperty(name='辅助区使用内置灰模',default=True,update=changed)
 lock_pose:BoolProperty(name='固定模块朝向',default=False,update=changed)
 locked_turn:IntProperty(name='固定旋转 ×90°',default=0,min=0,max=3,update=changed)
 locked_mirror:BoolProperty(name='固定镜像',default=False,update=changed)
 previews:BoolProperty(name='显示三组预览',default=False,update=changed)
 status:StringProperty(default='创建或注册模块，再生成三组预览。')
 pending_frame:PointerProperty(type=bpy.types.Object)
 pending_target:PointerProperty(type=bpy.types.Object)
 edit_instance:PointerProperty(type=bpy.types.Object)
 edit_reference:PointerProperty(type=bpy.types.Object)
 edit_lattice:PointerProperty(type=bpy.types.Object)
 edit_frame:PointerProperty(type=bpy.types.Object)
 edit_valence:IntProperty(default=4)
 edit_node:IntProperty(default=0)
 busy:BoolProperty(default=False)

def modules(scene):
 return sorted((o for o in scene.objects if o.type=='MESH' and not o.get(OWNER) and o.pcg_dcc_module.enabled),key=lambda o:o.pcg_dcc_module.uid)

def current(context):
 obj=context.object
 if obj and obj.get(OWNER):return context.scene.pcg_dcc.active
 if obj and obj.type=='MESH' and obj.pcg_dcc_module.enabled:return obj
 return context.scene.pcg_dcc.active

def snapshot(obj,evaluated=True):
 if obj.data.is_editmode:
  bm=bmesh.from_edit_mesh(obj.data);bm.verts.ensure_lookup_table();bm.verts.index_update()
  v=tuple(tuple(p.co) for p in bm.verts);faces=tuple(tuple(p.index for p in f.verts) for f in bm.faces)
  tri=tuple(tuple(loop.vert.index for loop in t) for t in bm.calc_loop_triangles())
  return v,faces,tri,tuple(f.material_index for f in bm.faces),tuple(obj.data.materials)
 cleanup=None;mesh=obj.data
 if evaluated and obj.modifiers:
  deps=bpy.context.evaluated_depsgraph_get();ev=obj.evaluated_get(deps);mesh=ev.to_mesh(preserve_all_data_layers=True,depsgraph=deps);cleanup=ev.to_mesh_clear
 try:
  mesh.calc_loop_triangles()
  return (tuple(tuple(p.co) for p in mesh.vertices),tuple(tuple(p.vertices) for p in mesh.polygons),
          tuple(tuple(t.vertices) for t in mesh.loop_triangles),tuple(p.material_index for p in mesh.polygons),tuple(mesh.materials))
 finally:
  if cleanup:cleanup()

def effective_rules(obj):
 meta=obj.pcg_dcc_module;result={}
 for face,name in enumerate(core.FACE_NAMES):
  rules=[r for r in meta.rules if r.face==name]
  if meta.explicit[face] or rules:
   targets={}
   for r in rules:targets.setdefault(r.target,dict(id=r.target,weight=float(r.weight)))
   result[name]=list(targets.values())
 return result

def descriptor(obj):
 m=obj.pcg_dcc_module;v,f,t,_,_=snapshot(obj)
 return core.Module(m.uid,m.module_id,core.state_of(m.corners),v,t,m.weight,
                    tuple(i for i,b in enumerate(m.turns) if b),m.mirror,effective_rules(obj))

def collection(scene,path):
 parent=scene.collection;full='PCG Modules'
 col=bpy.data.collections.get(full)
 if col is None:col=bpy.data.collections.new(full)
 if col.name not in parent.children:parent.children.link(col)
 parent=col
 for part in path.split('.'):
  full+=' / '+part;child=bpy.data.collections.get(full)
  if child is None:child=bpy.data.collections.new(full)
  if child.name not in parent.children:parent.children.link(child)
  parent=child
 return parent

def organize(scene,obj):
 target=collection(scene,obj.pcg_dcc_module.module_id)
 if obj.name not in target.objects:target.objects.link(obj)
 for col in list(obj.users_collection):
  if col!=target:col.objects.unlink(obj)

def names(scene):
 settings=scene.pcg_dcc;ids={o.pcg_dcc_module.module_id for o in modules(scene)}
 expanded=set()
 for name in ids:
  parts=name.split('.')
  expanded.update('.'.join(parts[:i]) for i in range(1,len(parts)+1))
 old={i.name:i.collapsed for i in settings.names}
 if set(old)==expanded:return
 settings.names.clear()
 for name in sorted(expanded):item=settings.names.add();item.name=name;item.collapsed=old.get(name,False)

def activate(context,obj):
 object_mode(context)
 for item in context.selected_objects:item.select_set(False)
 obj.hide_set(False);obj.hide_select=False;obj.select_set(True);context.view_layer.objects.active=obj
 context.scene.pcg_dcc.active=obj

def object_mode(context):
 if context.object and context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')

def assign(obj,module_id,state):
 m=obj.pcg_dcc_module;m.enabled=True;m.uid=uuid.uuid4().hex;m.module_id=module_id
 m.corners=tuple(bool(state&(1<<i)) for i in range(8));m.built_state=state
 return m

def create(context,module_id,state,reuse=True):
 if not core.valid_id(module_id):raise ValueError('ID 格式应为 主.子.子，各段非空')
 scene=context.scene;object_mode(context)
 possible=[o for o in modules(scene) if core.state_of(o.pcg_dcc_module.corners)==state
           and (module_id==o.pcg_dcc_module.module_id or module_id.startswith(o.pcg_dcc_module.module_id+'.'))]
 parent=max(possible,key=lambda o:len(o.pcg_dcc_module.module_id)) if possible and reuse else None
 if parent:
  obj=parent.copy();obj.data=parent.data.copy();obj.name=module_id
  obj.animation_data_clear();obj.parent=None;obj.matrix_world=Matrix.Identity(4)
  # A child inherits manual rules, not generated reverse cache rows.
  for i in range(len(obj.pcg_dcc_module.rules)-1,-1,-1):
   if obj.pcg_dcc_module.rules[i].automatic:obj.pcg_dcc_module.rules.remove(i)
 else:
  v,f=core.base_mesh(state);mesh=bpy.data.meshes.new(module_id);mesh.from_pydata(v,[],f);mesh.update()
  obj=bpy.data.objects.new(module_id,mesh)
 assign(obj,module_id,state);collection(scene,module_id).objects.link(obj)
 count=len(modules(scene))-1;obj.location=((count%8)*1.5,6+(count//8)*1.5,0)
 scene.pcg_dcc.active=obj;names(scene);activate(context,obj)
 return obj,parent

def matching_faces(a,b,source_face):
 output=set();av=core.variants([a]);bv=core.variants([b])
 for x in av:
  fa=core.transform_face(source_face,*x.pose);fb=core.OPPOSITE[fa]
  for y in bv:
   if x.port(fa,core.cube_orientation(fa))!=y.port(fb,core.cube_orientation(fb)):continue
   for local in range(6):
    if core.transform_face(local,*y.pose)==fb:output.add(core.FACE_NAMES[local])
 return output

def sync_reverse(scene,edited=None):
 objects=modules(scene)
 # Derived rows are regenerated from authored declarations. Removing one does
 # not accidentally remove another declaration with the same logical ID.
 for obj in objects:
  rows=obj.pcg_dcc_module.rules
  for i in range(len(rows)-1,-1,-1):
   if rows[i].automatic:rows.remove(i)
 descriptions={o:descriptor(o) for o in objects}
 declarations=[(o,r.face,r.target,float(r.weight)) for o in objects for r in o.pcg_dcc_module.rules]
 for source,face,target,weight in declarations:
  a=descriptions[source]
  for obj in objects:
   if not core.matches(target,obj.pcg_dcc_module.module_id):continue
   for other_face in matching_faces(a,descriptions[obj],core.FACE_NAMES.index(face)):
    manual=next((r for r in obj.pcg_dcc_module.rules if r.face==other_face and r.target==a.id and not r.automatic),None)
    if manual:
     if edited==(source.pcg_dcc_module.uid,face,target):manual.weight=weight
     continue
    rows=obj.pcg_dcc_module.rules
    if any(r.face==other_face and r.target==a.id and r.origin_uid==a.uid for r in rows):continue
    r=rows.add();r.face=other_face;r.target=a.id;r.weight=weight;r.automatic=True
    r.origin_uid=a.uid;r.origin_face=face;r.origin_target=target
 changed(None,bpy.context)

def validate(scene):
 objects=modules(scene);descriptions=[descriptor(o) for o in objects]
 # Include virtual base IDs as usable helper candidates, but do not validate
 # their implicit geometry-only rules as authored content.
 from . import solver
 result=core.issues(descriptions,solver.catalog(descriptions,True))
 for obj in objects:
  expected=bpy.data.collections.get('PCG Modules / '+' / '.join(obj.pcg_dcc_module.module_id.split('.')))
  if expected not in obj.users_collection:result.append(dict(uid=obj.pcg_dcc_module.uid,kind='WARNING',message='模型不在对应 ID 集合中，可点击整理集合',face=-1,vertex=-1))
 settings=scene.pcg_dcc;settings.issues.clear()
 for issue in result:
  item=settings.issues.add()
  for key,value in issue.items():setattr(item,key,value)
 names(scene)
 return result

CLASSES=(DCCRule,DCCModule,DCCName,DCCIssue,DCCSettings)
