"""DCC v2 operators and panels. Persistent edits use Blender undo commands."""
import json
import time
import bpy
from mathutils import Matrix,Vector
from bpy.props import BoolProperty,BoolVectorProperty,EnumProperty,FloatProperty,IntProperty,StringProperty
from bpy_extras import view3d_utils
from . import authoring as a,core,preview

def selected(context):
 obj=a.current(context)
 if obj is None or not obj.pcg_dcc_module.enabled:raise ValueError('请先创建或选择一个已注册模块')
 return obj

def error(operator,e):operator.report({'ERROR'},str(e));return {'CANCELLED'}

def preset_changed(self,context):
 if self.preset:
  if hasattr(self,'module_id'):self.module_id=self.preset
  elif hasattr(self,'target'):self.target=self.preset

def update(context,force=True):
 a.names(context.scene);a.changed(None,context)
 if context.scene.pcg_dcc.previews:preview.refresh(context.scene,force)

class DCC_OT_create(bpy.types.Operator):
 bl_idname='pcg_dcc.create';bl_label='创建模块';bl_options={'REGISTER','UNDO'}
 module_id:StringProperty(name='ID',default='building.module')
 preset:StringProperty(name='预选已有 ID',update=preset_changed)
 corners:BoolVectorProperty(name='启用顶点',size=8,default=(False,False,False,False,True,True,True,True))
 reuse:BoolProperty(name='复用相同 Bool 的已有或父模块灰模',default=True)
 def invoke(self,context,event):
  a.names(context.scene)
  return context.window_manager.invoke_props_dialog(self,width=440)
 def draw(self,context):
  l=self.layout;l.prop(self,'module_id');l.prop_search(self,'preset',context.scene.pcg_dcc,'names')
  for row in (0,4):
   r=l.row(align=True)
   for i in range(row,row+4):r.prop(self,'corners',index=i,text='V'+str(i+1),toggle=True)
  l.prop(self,'reuse');state=core.state_of(self.corners)
  same=[o for o in a.modules(context.scene) if o.pcg_dcc_module.module_id==self.module_id]
  if any(core.state_of(o.pcg_dcc_module.corners)!=state for o in same):l.label(text='同 ID 已存在不同 Bool；允许创建，将标记冲突。',icon='ERROR')
  elif same:l.label(text='将创建独立模型，内部唯一键不同。',icon='INFO')
 def execute(self,context):
  try:
   state=core.state_of(self.corners)
   conflict=any(o.pcg_dcc_module.module_id==self.module_id and core.state_of(o.pcg_dcc_module.corners)!=state for o in a.modules(context.scene))
   obj,parent=a.create(context,self.module_id,state,self.reuse);a.sync_reverse(context.scene)
   context.scene.pcg_dcc.status='已创建 '+self.module_id+('；复用 '+parent.pcg_dcc_module.module_id+' 的几何' if parent else '；使用标准灰模')
   if conflict:self.report({'WARNING'},'同 ID 的 Bool 不一致；已按要求创建，可在注册表检查')
   update(context);return {'FINISHED'}
  except Exception as e:return error(self,e)

class DCC_OT_rebuild(bpy.types.Operator):
 bl_idname='pcg_dcc.rebuild';bl_label='按当前 Bool 重建标准灰模';bl_options={'REGISTER','UNDO'}
 reuse:BoolProperty(name='使用匹配父模块灰模',default=True)
 def invoke(self,context,event):return context.window_manager.invoke_props_dialog(self,width=400)
 def draw(self,context):
  self.layout.label(text='替换当前网格；保留 ID 和规则，支持 Ctrl+Z。')
  self.layout.label(text='旧规则将重新校验，不保证仍然有效。');self.layout.prop(self,'reuse')
 def execute(self,context):
  try:
   obj=selected(context);a.object_mode(context);m=obj.pcg_dcc_module;state=core.state_of(m.corners)
   parents=[o for o in a.modules(context.scene) if o!=obj and m.module_id.startswith(o.pcg_dcc_module.module_id+'.') and core.state_of(o.pcg_dcc_module.corners)==state]
   if parents and self.reuse:mesh=max(parents,key=lambda o:len(o.pcg_dcc_module.module_id)).data.copy()
   else:
    v,f=core.base_mesh(state);mesh=bpy.data.meshes.new(m.module_id);mesh.from_pydata(v,[],f);mesh.update()
   obj.data=mesh
   for modifier in list(obj.modifiers):obj.modifiers.remove(modifier)
   m.built_state=state;a.sync_reverse(context.scene);a.validate(context.scene);update(context)
   return {'FINISHED'}
  except Exception as e:return error(self,e)

class DCC_OT_register_begin(bpy.types.Operator):
 bl_idname='pcg_dcc.register_begin';bl_label='放置注册线框';bl_options={'REGISTER','UNDO'}
 def execute(self,context):
  obj=context.object
  if obj is None or obj.type!='MESH' or obj.get(a.OWNER):return error(self,ValueError('先选择需要注册的源网格'))
  a.object_mode(context);cfg=context.scene.pcg_dcc
  if cfg.pending_frame and cfg.pending_frame.name in context.scene.objects:preview.remove_object(cfg.pending_frame)
  frame=bpy.data.objects.new('DCC 注册参考框（移动缩放框以包围模型）',None)
  frame.empty_display_type='CUBE';frame.empty_display_size=.5
  frame.matrix_world=obj.matrix_world.copy();context.collection.objects.link(frame)
  cfg.pending_target=obj;cfg.pending_frame=frame
  for o in context.selected_objects:o.select_set(False)
  frame.select_set(True);context.view_layer.objects.active=frame
  cfg.status='调整参考框的位置、旋转和缩放，或将模型放入框内，再点击确认注册。'
  return {'FINISHED'}

class DCC_OT_register_finish(bpy.types.Operator):
 bl_idname='pcg_dcc.register_finish';bl_label='确认注册';bl_options={'REGISTER','UNDO'}
 module_id:StringProperty(name='ID',default='building.registered')
 preset:StringProperty(name='预选已有 ID',update=preset_changed)
 corners:BoolVectorProperty(name='启用顶点',size=8,default=(False,False,False,False,True,True,True,True))
 def invoke(self,context,event):a.names(context.scene);return context.window_manager.invoke_props_dialog(self,width=420)
 def draw(self,context):
  l=self.layout;l.prop(self,'module_id');l.prop_search(self,'preset',context.scene.pcg_dcc,'names')
  for start in (0,4):
   row=l.row(align=True)
   for i in range(start,start+4):row.prop(self,'corners',index=i,text='V'+str(i+1),toggle=True)
 def execute(self,context):
  try:
   cfg=context.scene.pcg_dcc;obj=cfg.pending_target;frame=cfg.pending_frame
   if obj is None or frame is None:raise ValueError('请先放置注册线框')
   if not core.valid_id(self.module_id):raise ValueError('ID 格式无效')
   a.object_mode(context)
   if abs(frame.matrix_world.determinant())<1e-8:raise ValueError('注册框缩放不能为零')
   # Bake evaluated source geometry into the frame's canonical local space.
   deps=context.evaluated_depsgraph_get();ev=obj.evaluated_get(deps)
   mesh=bpy.data.meshes.new_from_object(ev,preserve_all_data_layers=True,depsgraph=deps)
   transform=frame.matrix_world.inverted()@obj.matrix_world;mesh.transform(transform)
   obj.data=mesh;obj.matrix_world=frame.matrix_world.copy()
   for modifier in list(obj.modifiers):obj.modifiers.remove(modifier)
   a.assign(obj,self.module_id,core.state_of(self.corners));a.organize(context.scene,obj)
   # Keep the actual registration cage bound to the module, as requested.
   frame.parent=obj;frame.matrix_parent_inverse=Matrix.Identity(4);frame.matrix_basis=Matrix.Identity(4)
   frame.hide_select=True;frame['dcc_registration_cage']=obj.pcg_dcc_module.uid
   cfg.pending_frame=None;cfg.pending_target=None;a.activate(context,obj)
   a.sync_reverse(context.scene);a.validate(context.scene);update(context)
   return {'FINISHED'}
  except Exception as e:return error(self,e)

class DCC_OT_register_cancel(bpy.types.Operator):
 bl_idname='pcg_dcc.register_cancel';bl_label='取消注册';bl_options={'REGISTER','UNDO'}
 def execute(self,context):
  cfg=context.scene.pcg_dcc
  if cfg.pending_frame:preview.remove_object(cfg.pending_frame)
  cfg.pending_frame=None;cfg.pending_target=None;return {'FINISHED'}

class DCC_OT_select(bpy.types.Operator):
 bl_idname='pcg_dcc.select';bl_label='选择模块';bl_options={'REGISTER','UNDO'}
 uid:StringProperty();message:StringProperty()
 @classmethod
 def description(cls,context,properties):return properties.message or '选择并定位源模块'
 def execute(self,context):
  obj=next((o for o in a.modules(context.scene) if o.pcg_dcc_module.uid==self.uid),None)
  if obj:a.activate(context,obj);update(context)
  return {'FINISHED'}

class DCC_OT_rename(bpy.types.Operator):
 bl_idname='pcg_dcc.rename';bl_label='重命名 ID 分组';bl_options={'REGISTER','UNDO'}
 new_id:StringProperty(name='新 ID')
 def invoke(self,context,event):
  try:self.new_id=selected(context).pcg_dcc_module.module_id
  except Exception as e:return error(self,e)
  return context.window_manager.invoke_props_dialog(self,width=400)
 def execute(self,context):
  try:
   obj=selected(context);old=obj.pcg_dcc_module.module_id;new=self.new_id
   if not core.valid_id(new):raise ValueError('ID 格式无效')
   for o in a.modules(context.scene):
    m=o.pcg_dcc_module
    if core.matches(old,m.module_id):m.module_id=new+m.module_id[len(old):];a.organize(context.scene,o)
    for r in m.rules:
     if core.matches(old,r.target):r.target=new+r.target[len(old):]
   a.sync_reverse(context.scene);update(context);return {'FINISHED'}
  except Exception as e:return error(self,e)

class DCC_OT_delete(bpy.types.Operator):
 bl_idname='pcg_dcc.delete';bl_label='移除当前模型';bl_options={'REGISTER','UNDO'}
 bl_description='删除源模型及绑定参考框；保留失效引用供检查，Ctrl+Z 可恢复'
 def execute(self,context):
  try:
   obj=selected(context);a.object_mode(context);cfg=context.scene.pcg_dcc
   # Source geometry is removed only through this explicit undoable command.
   for child in list(obj.children):
    if child.get('dcc_registration_cage')==obj.pcg_dcc_module.uid:bpy.data.objects.remove(child,do_unlink=True)
   bpy.data.objects.remove(obj,do_unlink=True);cfg.active=None
   preview.clear(context.scene);a.sync_reverse(context.scene);a.validate(context.scene);update(context)
   return {'FINISHED'}
  except Exception as e:return error(self,e)

class DCC_OT_rule(bpy.types.Operator):
 bl_idname='pcg_dcc.rule';bl_label='邻接规则';bl_options={'REGISTER','UNDO'}
 action:EnumProperty(items=[('ADD','添加候选',''),('EDIT','编辑候选',''),('REMOVE','移除候选',''),('AUTO','恢复自动几何匹配','')])
 # Blender must not reuse an earlier operation's source, face or row index.
 uid:StringProperty(options={'SKIP_SAVE'});face:StringProperty(options={'SKIP_SAVE'});index:IntProperty(default=-1,options={'SKIP_SAVE'})
 target:StringProperty(name='候选 ID',default='base')
 preset:StringProperty(name='预选已有 ID',update=preset_changed,options={'SKIP_SAVE'})
 weight:FloatProperty(name='权重',default=1,min=.0001,max=10000)
 def _source(self,context):return next((o for o in a.modules(context.scene) if o.pcg_dcc_module.uid==self.uid),None) if self.uid else selected(context)
 def invoke(self,context,event):
  try:
   obj=self._source(context)
   if obj is None:raise ValueError('源模块不存在')
   self.uid=obj.pcg_dcc_module.uid;self.face=self.face or context.scene.pcg_dcc.face
   if self.action in ('ADD','AUTO'):self.index=-1
   if self.index>=0:
    r=obj.pcg_dcc_module.rules[self.index]
    self.face=r.face
    if r.automatic:
     origin=next((o for o in a.modules(context.scene) if o.pcg_dcc_module.uid==r.origin_uid),None)
     if origin is None:raise ValueError('自动反向规则的来源已丢失')
     self.uid=origin.pcg_dcc_module.uid;self.face=r.origin_face
     self.index=next(i for i,q in enumerate(origin.pcg_dcc_module.rules) if not q.automatic and q.face==r.origin_face and q.target==r.origin_target)
     obj=origin;r=obj.pcg_dcc_module.rules[self.index]
    self.target=r.target;self.weight=r.weight
   if self.action in ('REMOVE','AUTO'):return self.execute(context)
   a.names(context.scene);return context.window_manager.invoke_props_dialog(self,width=420)
  except Exception as e:return error(self,e)
 def draw(self,context):
  l=self.layout;l.prop(self,'target',text='ID（父 ID 包含子项）');l.prop_search(self,'preset',context.scene.pcg_dcc,'names')
  l.label(text='特殊值：$empty 空气 / $solid 实心 / $outside 地图外侧')
  l.prop(self,'weight',slider=True)
 def execute(self,context):
  try:
   obj=self._source(context)
   if obj is None:raise ValueError('规则源模块不存在')
   face=self.face or context.scene.pcg_dcc.face;m=obj.pcg_dcc_module;f=core.FACE_NAMES.index(face)
   if self.action=='ADD':self.index=-1
   if self.action=='AUTO':
    for i in range(len(m.rules)-1,-1,-1):
     if m.rules[i].face==face and not m.rules[i].automatic:m.rules.remove(i)
    m.explicit[f]=False
   elif self.action=='REMOVE':
    if self.index<0:raise ValueError('没有选中候选')
    m.rules.remove(self.index)
   else:
    if not core.valid_id(self.target) and self.target not in core.RESERVED:raise ValueError('候选 ID 格式无效')
    if self.index>=0:r=m.rules[self.index]
    else:
     r=next((r for r in m.rules if r.face==face and r.target==self.target and not r.automatic),None)
     if r is None:r=m.rules.add()
    r.face=face;r.target=self.target;r.weight=self.weight;r.automatic=False;m.explicit[f]=True
   a.sync_reverse(context.scene,(m.uid,face,self.target));a.validate(context.scene);update(context)
   return {'FINISHED'}
  except Exception as e:return error(self,e)

class DCC_OT_preview(bpy.types.Operator):
 bl_idname='pcg_dcc.preview';bl_label='预览操作';bl_options={'REGISTER','UNDO'}
 action:EnumProperty(items=[('BUILD','生成 / 刷新三组预览',''),('REROLL','换一组辅助组合',''),('CLEAR','清理预览',''),('FOCUS','定位全部预览','')])
 def execute(self,context):
  try:
   cfg=context.scene.pcg_dcc
   if self.action=='CLEAR':cfg.previews=False;preview.clear(context.scene);return {'FINISHED'}
   if cfg.active is None:cfg.active=selected(context)
   if self.action=='REROLL':cfg.seed+=101
   cfg.previews=True;preview.refresh(context.scene,True)
   if self.action=='FOCUS' and context.area and context.area.type=='VIEW_3D':
    context.space_data.region_3d.view_location=Vector((0,0,cfg.height*1.5));context.space_data.region_3d.view_distance=cfg.spacing*2.4
   return {'FINISHED'}
  except Exception as e:return error(self,e)

class DCC_OT_edit(bpy.types.Operator):
 bl_idname='pcg_dcc.edit';bl_label='编辑模型';bl_options={'REGISTER','UNDO'}
 valence:IntProperty(default=4)
 source:BoolProperty(default=False)
 def execute(self,context):
  try:
   obj=selected(context);cfg=context.scene.pcg_dcc
   if not self.source:
    cfg.active=obj;cfg.previews=True;preview.refresh(context.scene,True)
    obj=next((o for o in context.scene.objects if o.get(a.ROLE)=='core' and o.get('dcc_valence')==self.valence),None)
    if obj is None:raise ValueError('没有可编辑的核心 Cell')
   a.object_mode(context)
   for item in context.selected_objects:item.select_set(False)
   obj.hide_select=False;obj.select_set(True);context.view_layer.objects.active=obj
   context.view_layer.update()
   evaluated=obj.evaluated_get(context.evaluated_depsgraph_get())
   bounds=[evaluated.matrix_world@Vector(p) for p in evaluated.bound_box]
   center=sum(bounds,Vector())/len(bounds)
   radius=max((p-center).length for p in bounds)
   bpy.ops.object.mode_set(mode='EDIT')
   if context.area and context.area.type=='VIEW_3D':
    view=context.space_data.region_3d
    view.view_location=center;view.view_distance=max(4.0,radius*4)
   return {'FINISHED'}
  except Exception as e:return error(self,e)

class DCC_OT_fold(bpy.types.Operator):
 bl_idname='pcg_dcc.fold';bl_label='展开 / 折叠 ID';bl_options={'INTERNAL'}
 path:StringProperty()
 def execute(self,context):
  item=context.scene.pcg_dcc.names.get(self.path)
  if item:item.collapsed=not item.collapsed
  return {'FINISHED'}

class DCC_OT_organize(bpy.types.Operator):
 bl_idname='pcg_dcc.organize';bl_label='按 ID 整理集合';bl_options={'REGISTER','UNDO'}
 def execute(self,context):
  for obj in a.modules(context.scene):a.organize(context.scene,obj)
  return {'FINISHED'}

class DCC_OT_check(bpy.types.Operator):
 bl_idname='pcg_dcc.check';bl_label='检查注册表'
 def execute(self,context):
  try:
   result=a.validate(context.scene);update(context)
   context.scene.pcg_dcc.status=f'检查完成：{len(result)} 条提示；橙色为未配置，红色为失效，黄色为 ID 状态提醒。'
   if not bpy.app.background and context.area and context.area.type=='VIEW_3D':bpy.ops.pcg_dcc.inspect('INVOKE_DEFAULT')
   return {'FINISHED'}
  except Exception as e:return error(self,e)

class DCC_OT_inspect(bpy.types.Operator):
 bl_idname='pcg_dcc.inspect';bl_label='悬停查看检查信息'
 _running=set()
 def invoke(self,context,event):
  self.area=context.area
  self.region=next(r for r in self.area.regions if r.type=='WINDOW')
  self.view=context.space_data.region_3d
  if self.area.as_pointer() in self._running:return {'CANCELLED'}
  self._running.add(self.area.as_pointer());context.window_manager.modal_handler_add(self);return {'RUNNING_MODAL'}
 def modal(self,context,event):
  if event.type=='ESC' or not preview._ENABLED:
   self._running.discard(self.area.as_pointer());return {'FINISHED'}
  if event.type=='MOUSEMOVE' and context.area==self.area:
   mouse=(event.mouse_x-self.region.x,event.mouse_y-self.region.y)
   cfg=context.scene.pcg_dcc;cache=a._CACHE.setdefault(context.scene.as_pointer(),{});hits=list(cache.get('hits',[]));obj=cfg.active
   if obj:
    for issue in cfg.issues:
     if issue.uid!=obj.pcg_dcc_module.uid:continue
     if issue.face>=0:p=Vector(tuple(sum(core.CORNERS[i][j] for i in core.FACES[issue.face])/4 for j in range(3)))
     elif issue.vertex>=0 and issue.vertex<len(obj.data.vertices):p=obj.data.vertices[issue.vertex].co
     else:p=Vector((0,0,0))
     hits.append((tuple(obj.matrix_world@p),issue.message))
   nearest=None;distance=24
   for p,message in hits:
    xy=view3d_utils.location_3d_to_region_2d(self.region,self.view,Vector(p))
    if xy:
     d=(xy-Vector(mouse)).length
     if d<distance:distance=d;nearest=message
   cache['hover']=(*mouse,nearest,time.monotonic(),self.area.as_pointer()) if nearest else None
   self.area.tag_redraw()
  return {'PASS_THROUGH'}

class DCC_PT_module(bpy.types.Panel):
 bl_label='模块编辑';bl_idname='DCC_PT_module';bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='PCG DCC'
 def draw(self,context):
  l=self.layout;cfg=context.scene.pcg_dcc
  row=l.row(align=True);row.operator('pcg_dcc.create',icon='ADD');row.operator('pcg_dcc.register_begin',text='注册已有模型')
  if cfg.pending_frame:
   box=l.box();box.label(text='调整注册框后确认');row=box.row(align=True);row.operator('pcg_dcc.register_finish');row.operator('pcg_dcc.register_cancel')
  l.prop(cfg,'active')
  obj=cfg.active
  if obj and obj.pcg_dcc_module.enabled:
   m=obj.pcg_dcc_module;row=l.row(align=True);row.label(text='ID：'+m.module_id);row.operator('pcg_dcc.rename',text='',icon='GREASEPENCIL')
   for start in (0,4):
    row=l.row(align=True)
    for i in range(start,start+4):row.prop(m,'corners',index=i,text='V'+str(i+1),toggle=True)
   l.label(text='状态：'+core.state_string(core.state_of(m.corners)))
   if m.built_state!=core.state_of(m.corners):l.label(text='Bool 已变；可重建灰模，或自行调整几何。',icon='ERROR')
   l.operator('pcg_dcc.rebuild');l.prop(m,'weight',slider=True)
   row=l.row(align=True)
   for i in range(4):row.prop(m,'turns',index=i,text=str(i*90)+'°',toggle=True)
   l.prop(m,'mirror');row=l.row(align=True);row.operator('pcg_dcc.edit',text='编辑原模').source=True
   for n in (3,4,5):row.operator('pcg_dcc.edit',text=f'{n}价').valence=n
   box=l.box();box.label(text='邻接约束')
   for group in (('up','down','front'),('left','right','back')):
    row=box.row(align=True)
    for name in group:row.prop_enum(cfg,'face',name,text=dict((x[0],x[1].split()[0]) for x in a.FACE_ITEMS)[name])
   face=cfg.face;by_target={}
   if not any(m.corners[i] for i in core.FACES[core.FACE_NAMES.index(face)]):
    box.label(text='本面四角未启用，不推导邻格',icon='INFO')
   for i,r in enumerate(m.rules):
    if r.face==face:by_target.setdefault(r.target,(i,r))
   rules=list(by_target.values())
   if not rules:box.label(text='自动几何匹配；混合 Bool 面标记未配置。',icon='INFO')
   total=sum(r.weight for _,r in rules)
   for i,r in rules:
    row=box.row(align=True);row.label(text=r.target+(' ↔' if r.automatic else ''))
    op=row.operator('pcg_dcc.rule',text=f'{r.weight:g} ({r.weight/total:.0%})');op.action='EDIT';op.index=i;op.uid=m.uid;op.face=r.face
    op=row.operator('pcg_dcc.rule',text='',icon='X');op.action='REMOVE';op.index=i;op.uid=m.uid;op.face=r.face
   row=box.row(align=True)
   op=row.operator('pcg_dcc.rule',text='添加候选');op.action='ADD';op.uid=m.uid;op.face=cfg.face;op.index=-1
   op=row.operator('pcg_dcc.rule',text='恢复自动');op.action='AUTO';op.uid=m.uid;op.face=cfg.face;op.index=-1
   l.operator('pcg_dcc.delete',text='删除模型（可撤销）',icon='TRASH')

class DCC_PT_preview(bpy.types.Panel):
 bl_label='三组局部预览';bl_idname='DCC_PT_preview';bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='PCG DCC'
 def draw(self,context):
  l=self.layout;cfg=context.scene.pcg_dcc
  l.operator('pcg_dcc.preview',text='生成 / 刷新预览',icon='VIEW3D').action='BUILD'
  row=l.row(align=True);row.operator('pcg_dcc.preview',text='换一组').action='REROLL';row.operator('pcg_dcc.preview',text='定位').action='FOCUS';row.operator('pcg_dcc.preview',text='清理').action='CLEAR'
  l.label(text='单格填入，仅匹配六面直接邻接')
  l.prop(cfg,'show_diagnostic_faces')
  for prop in ('live','layers','core_layer','height','skew','include_base','lock_pose'):l.prop(cfg,prop)
  if cfg.lock_pose:l.prop(cfg,'locked_turn');l.prop(cfg,'locked_mirror')
  for line in cfg.status.split('；'):
   for i in range(0,len(line),25):l.label(text=line[i:i+25])

class DCC_OT_registry_window(bpy.types.Operator):
 bl_idname='pcg_dcc.registry_window';bl_label='打开模块注册表';bl_description='在独立窗口中按 ID 分类浏览全部模块和六面规则'
 def execute(self,context):
  try:
   for window in context.window_manager.windows:
    if window.screen.name.startswith('PCG DCC Registry'):
     self.report({'INFO'},'模块注册表窗口已打开');return {'FINISHED'}
   a.names(context.scene)
   before={w.as_pointer() for w in context.window_manager.windows}
   bpy.ops.screen.area_dupli('INVOKE_DEFAULT')
   window=next((w for w in context.window_manager.windows if w.as_pointer() not in before),None)
   if window is None:raise RuntimeError('无法创建注册表窗口')
   window.scene=context.scene;window.screen.name='PCG DCC Registry'
   area=max(window.screen.areas,key=lambda ar:ar.width*ar.height);area.type='PROPERTIES'
   area.spaces.active.context='SCENE';area.spaces.active.pin_id=context.scene
   return {'FINISHED'}
  except Exception as e:return error(self,e)

def draw_registry(layout,context):
 cfg=context.scene.pcg_dcc;objects=a.modules(context.scene);query=cfg.registry_search.strip().casefold()
 shown=[o for o in objects if not query or query in (o.name+' '+o.pcg_dcc_module.module_id+' '+o.pcg_dcc_module.uid).casefold()]
 row=layout.row(align=True);row.operator('pcg_dcc.check',text='检查全部模块',icon='CHECKMARK');row.operator('pcg_dcc.organize',text='整理集合');row.operator('pcg_dcc.export',text='导出 FBX + JSON')
 layout.prop(cfg,'registry_search',icon='VIEWZOOM');layout.prop(cfg,'registry_raw')
 layout.label(text=f'已注册 {len(objects)} 个模块，当前显示 {len(shown)} 个（不含预览实例与内置辅助灰模）')
 if not shown:layout.label(text='没有符合条件的模块');return
 hidden=[]
 for group in cfg.names:
  if not any(core.matches(group.name,o.pcg_dcc_module.module_id) for o in shown):continue
  if any(group.name.startswith(p+'.') for p in hidden):continue
  row=layout.row(align=True)
  for _ in range(group.name.count('.')):row.separator(factor=.6)
  op=row.operator('pcg_dcc.fold',text=group.name,icon='TRIA_RIGHT' if group.collapsed else 'TRIA_DOWN',emboss=False);op.path=group.name
  if group.collapsed:hidden.append(group.name);continue
  for obj in shown:
   m=obj.pcg_dcc_module
   if m.module_id!=group.name:continue
   box=layout.box();box.label(text=f'{obj.name}  |  {m.module_id}',icon='MESH_DATA')
   box.label(text='唯一键：'+m.uid);box.label(text='网格：'+obj.data.name)
   vertices,faces,triangles,_,materials=a.snapshot(obj,False)
   box.label(text=f'顶点 {len(vertices)} · 面 {len(faces)} · 三角面 {len(triangles)} · 材质 {len(materials)}')
   row=box.row(align=True)
   for i in range(8):row.label(text=f'V{i+1}: {int(m.corners[i])}')
   box.label(text=f'Bool：{core.state_string(core.state_of(m.corners))}  |  基础权重：{m.weight:g}')
   box.label(text='允许旋转：'+(', '.join(f'{i*90}°' for i,v in enumerate(m.turns) if v) or '无')+'  |  局部 X 镜像：'+('允许' if m.mirror else '禁止'))
   rules=a.effective_rules(obj)
   for face,(name,label,_) in enumerate(a.FACE_ITEMS):
    facebox=box.box();rows=[(i,r) for i,r in enumerate(m.rules) if r.face==name]
    active=any(m.corners[i] for i in core.FACES[face])
    mode='显式候选' if name in rules else '自动几何匹配'
    facebox.label(text=f'{label} · {mode}'+(' · 四角全关，预览不推导' if not active else ''))
    if name in rules and not rows:facebox.label(text='候选为空：此方向禁止邻接',icon='ERROR')
    total=sum(r.weight for _,r in rows)
    for i,r in rows:
     row=facebox.row(align=True);row.label(text=r.target+'  ['+('自动反向' if r.automatic else '手动')+']')
     op=row.operator('pcg_dcc.rule',text=f'权重 {r.weight:g} ({r.weight/total:.1%})' if total else f'权重 {r.weight:g}');op.action='EDIT';op.index=i;op.uid=m.uid;op.face=name
     op=row.operator('pcg_dcc.rule',text='',icon='X');op.action='REMOVE';op.index=i;op.uid=m.uid;op.face=name
     if r.automatic:facebox.label(text=f'来源：{r.origin_uid} / {r.origin_face} → {r.origin_target}')
    row=facebox.row(align=True)
    op=row.operator('pcg_dcc.rule',text='添加候选');op.action='ADD';op.uid=m.uid;op.face=name;op.index=-1
    op=row.operator('pcg_dcc.rule',text='恢复自动');op.action='AUTO';op.uid=m.uid;op.face=name;op.index=-1
   related=[i for i in cfg.issues if i.uid==m.uid]
   if related:
    box.label(text=f'上次检查：{len(related)} 条提示',icon='INFO')
    messages=list(dict.fromkeys(i.kind+'：'+i.message for i in related))
    for message in messages:
     for i in range(0,len(message),60):box.label(text=message[i:i+60])
   if cfg.registry_raw:
    record=dict(uid=m.uid,id=m.module_id,corners=[int(v) for v in m.corners],weight=m.weight,yawDegrees=[i*90 for i,v in enumerate(m.turns) if v],allowMirrorX=m.mirror,neighbors=rules)
    raw=box.box();raw.label(text='源规则 JSON（缺省面为自动匹配）')
    for line in json.dumps(record,ensure_ascii=False,indent=2).splitlines():
     for i in range(0,len(line),90):raw.label(text=line[i:i+90])

class DCC_PT_registry_window(bpy.types.Panel):
 bl_label='PCG DCC 模块注册表';bl_idname='DCC_PT_registry_window';bl_space_type='PROPERTIES';bl_region_type='WINDOW';bl_context='scene';bl_order=-1000
 @classmethod
 def poll(cls,context):return context.screen and context.screen.name.startswith('PCG DCC Registry')
 def draw(self,context):draw_registry(self.layout,context)

class DCC_PT_registry(bpy.types.Panel):
 bl_label='模块注册表';bl_idname='DCC_PT_registry';bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='PCG DCC'
 def draw(self,context):
  l=self.layout;cfg=context.scene.pcg_dcc;row=l.row(align=True);row.operator('pcg_dcc.check',icon='CHECKMARK');row.operator('pcg_dcc.organize',text='整理集合')
  l.operator('pcg_dcc.export',icon='EXPORT')
  l.operator('pcg_dcc.registry_window',text='打开完整注册表（独立窗口）',icon='WINDOW')
  l.label(text=f'已注册 {len(a.modules(context.scene))} 个模块')

CLASSES=(DCC_OT_create,DCC_OT_rebuild,DCC_OT_register_begin,DCC_OT_register_finish,DCC_OT_register_cancel,
         DCC_OT_select,DCC_OT_rename,DCC_OT_delete,DCC_OT_rule,DCC_OT_preview,DCC_OT_edit,DCC_OT_fold,
         DCC_OT_organize,DCC_OT_check,DCC_OT_inspect,DCC_OT_registry_window,DCC_PT_module,DCC_PT_preview,DCC_PT_registry,DCC_PT_registry_window)
