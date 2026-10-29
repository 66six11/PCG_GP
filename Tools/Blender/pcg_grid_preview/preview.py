"""Three isolated finite previews and linked-mesh deformation editing."""
import hashlib
import json
import math
import time
import bpy
import blf
import gpu
from gpu_extras.batch import batch_for_shader
from mathutils import Matrix,Vector
from bpy_extras import view3d_utils
from . import authoring as a,core,solver

_HANDLES=[]
_ENABLED=False
_BASE_VARIANTS=None
_VARIANT_CACHE={}

def uid(scene):
 if not scene.get('pcg_dcc_owner'):
  import uuid
  scene['pcg_dcc_owner']=uuid.uuid4().hex
 return scene['pcg_dcc_owner']

def generated_collection(scene):
 name='PCG Previews / '+uid(scene)[:8];col=bpy.data.collections.get(name)
 if col is None:col=bpy.data.collections.new(name)
 if col.name not in scene.collection.children:scene.collection.children.link(col)
 return col

def tag(obj,scene,role):obj[a.OWNER]=uid(scene);obj[a.ROLE]=role

def remove_object(obj):
 data=obj.data;bpy.data.objects.remove(obj,do_unlink=True)
 if data and data.users==0:
  if isinstance(data,bpy.types.Mesh):bpy.data.meshes.remove(data)
  elif isinstance(data,bpy.types.Lattice):bpy.data.lattices.remove(data)

def clear(scene):
 a.object_mode(bpy.context)
 for obj in list(scene.objects):
  if obj.get(a.OWNER)==scene.get('pcg_dcc_owner') and obj.get(a.ROLE)!='transport':remove_object(obj)
 cfg=scene.pcg_dcc;cfg.edit_instance=None
 a._CACHE.pop(scene.as_pointer(),None)

def _object(scene,name,role,mesh=None):
 obj=bpy.data.objects.new(name,mesh);generated_collection(scene).objects.link(obj);tag(obj,scene,role)
 obj.hide_render=True;return obj

def _native_core(scene,source,key,quad,layer,height,offset,variant,existing):
 obj=existing.get(key)
 if obj is None or obj.data!=source.data:
  if obj:remove_object(obj)
  obj=source.copy();obj.data=source.data;obj.animation_data_clear();obj.name='编辑 | '+key
  obj.parent=None
  for constraint in list(obj.constraints):obj.constraints.remove(constraint)
  generated_collection(scene).objects.link(obj);tag(obj,scene,'core');obj['dcc_key']=key
  obj['dcc_source_uid']=source.pcg_dcc_module.uid
  obj.hide_render=True;obj.hide_select=False;obj.hide_viewport=False;obj.hide_set(False)
  obj.lock_location=(True,)*3;obj.lock_rotation=(True,)*3;obj.lock_scale=(True,)*3
  frame=_object(scene,'编辑坐标框 | '+key,'frame');frame.hide_set(True);frame.hide_select=True
  obj.parent=frame
  data=bpy.data.lattices.new('线性变形笼 | '+key);data.points_u=2;data.points_v=2;data.points_w=2
  for axis in 'uvw':setattr(data,'interpolation_type_'+axis,'KEY_LINEAR')
  cage=_object(scene,'变形笼 | '+key,'lattice',data);cage.parent=frame;cage.hide_set(True);cage.hide_select=True
  modifier=obj.modifiers.new('DCC 预览变形（不要应用）','LATTICE');modifier.object=cage;modifier.show_in_editmode=True;modifier.show_on_cage=True
  obj['dcc_cage']=cage.name;obj['dcc_frame']=frame.name
 cage=bpy.data.objects.get(obj.get('dcc_cage',''))
 if cage is None:return obj
 center=core.warp((0,0,0),quad,layer,height,*variant.pose)
 def deform(p):return Vector(core.sub(core.warp(p,quad,layer,height,*variant.pose),center))
 columns=[deform(tuple(.5 if j==axis else 0 for j in range(3)))-deform(tuple(-.5 if j==axis else 0 for j in range(3))) for axis in range(3)]
 linear=Matrix(tuple(zip(*columns))).to_4x4();inverse=linear.inverted()
 matrix=Matrix.Translation(Vector(core.add(center,offset)))@linear
 obj.matrix_parent_inverse=matrix;obj.matrix_basis=Matrix.Identity(4)
 cage.matrix_parent_inverse=matrix;cage.matrix_basis=Matrix.Identity(4)
 for p in cage.data.points:p.co_deform=inverse.to_3x3()@deform(tuple(p.co))
 for modifier in obj.modifiers:
  if modifier.type=='LATTICE' and modifier.object==cage:continue
  modifier.show_in_editmode=False;modifier.show_viewport=not source.data.is_editmode
 obj['dcc_turns']=variant.turns;obj['dcc_mirror']=variant.mirror
 obj['dcc_valence']=int(key.split(':')[0]);obj['dcc_node']=int(key.split(':')[1])
 return obj

def _helper(scene,key,module,variant,quad,layer,height,offset,sources,existing):
 obj=existing.get(key)
 if obj is not None and obj.get(a.ROLE)!='helper':remove_object(obj);obj=None
 if obj is None:obj=_object(scene,'辅助 | '+key,'helper',bpy.data.meshes.new('DCC helper'));obj['dcc_key']=key
 mesh=obj.data
 faces=module.triangles
 mesh.clear_geometry();mesh.from_pydata([core.add(core.warp(p,quad,layer,height,*variant.pose),offset) for p in module.vertices],[],
                                      [tuple(reversed(f)) if variant.mirror else f for f in faces]);mesh.update()
 mesh.materials.clear()
 # Helpers are intentionally subdued; source detail materials stay on core instances.
 mat=bpy.data.materials.get('PCG DCC Auxiliary') or bpy.data.materials.new('PCG DCC Auxiliary')
 mat.diffuse_color=(.32,.42,.5,1);mesh.materials.append(mat)
 obj.color=(.32,.42,.5,1);obj.hide_select=True;obj.hide_set(False)
 return obj

def _catalog(descriptions,include_base):
 global _BASE_VARIANTS
 result=[]
 for d in descriptions:
  key=(d.id,d.state,d.vertices,d.triangles,d.weight,d.turns,d.mirror,json.dumps(d.rules,sort_keys=True))
  old=_VARIANT_CACHE.get(d.uid)
  if old is None or old[0]!=key:_VARIANT_CACHE[d.uid]=(key,core.variants([d]))
  result.extend(_VARIANT_CACHE[d.uid][1])
 if _BASE_VARIANTS is None:_BASE_VARIANTS=solver.catalog([],True)
 result.extend(_BASE_VARIANTS if include_base else _BASE_VARIANTS[-2:])
 return result

def refresh(scene,force=False):
 cfg=scene.pcg_dcc;source=cfg.active
 if not cfg.previews or source is None or not source.pcg_dcc_module.enabled:return
 if cfg.busy:return
 cache=a._CACHE.setdefault(scene.as_pointer(),{})
 cfg.busy=True
 try:
  objects=a.modules(scene);descriptions=[a.descriptor(o) for o in objects]
  selected=next(d for d in descriptions if d.uid==source.pcg_dcc_module.uid)
  settings=(selected.uid,cfg.layers,cfg.core_layer,cfg.height,cfg.spacing,cfg.skew,cfg.seed,cfg.include_base,cfg.lock_pose,cfg.locked_turn,cfg.locked_mirror)
  signature=hash((settings,tuple((d.uid,d.id,d.state,d.vertices,d.triangles,d.weight,d.turns,d.mirror,json.dumps(d.rules,sort_keys=True)) for d in descriptions)))
  editing=bpy.context.object if bpy.context.scene==scene and bpy.context.object and bpy.context.object.mode=='EDIT' else None
  editing_key=editing.name if editing else ''
  if not force and signature==cache.get('signature') and editing_key==cache.get('editing_key'):
   cache['dirty']=False;return
  vs=_catalog(descriptions,cfg.include_base);engine=solver.Solver(vs)
  source_issues=[r for r in core.issues(descriptions,vs) if r['uid']==selected.uid]
  existing={o.get('dcc_key'):o for o in scene.objects if o.get(a.OWNER)==uid(scene) and o.get(a.ROLE) in ('core','helper')}
  used=set();grids={};records={};status=[];lines=[];overlays=[];labels=[];hits=[]
  previous=cache.get('previous',{}) if cache.get('seed')==cfg.seed else {}
  for k,n in enumerate((3,4,5)):
   grid=core.preview_grid(n,cfg.layers,min(cfg.core_layer,cfg.layers-1),cfg.skew)
   # The full fan is a wire guide; exactly one cell holds the edited module.
   class Single(core.Preview):
    @property
    def core(self):return {self.core_layer*len(self.quads)}
   grid.__class__=Single
   offset=((k-1)*cfg.spacing,0,0);pins={}
   if editing and editing.get('dcc_valence')==n:pins[editing['dcc_node']]=(editing.get('dcc_turns',0),editing.get('dcc_mirror',False))
   locked=(cfg.locked_turn,cfg.locked_mirror) if cfg.lock_pose else None
   result=engine.solve_neighbors(grid,selected,cfg.seed+n*397,previous.get(n),locked,pins=pins)
   grids[n]=(grid,result,offset)
   if result['success']:
    records[n]={i:vs[c].key for i,c in enumerate(result['choices']) if c>=0}
    status.append(f'{n}价：直接邻接通过 ({result["observations"]}次选择)')
   else:status.append(f'{n}价：'+result['error'])
   labels.append((core.add(offset,(0,-4,0)),f'{n}价点  '+('通过' if result['success'] else '无解'),(0.4,1,0.65,1) if result['success'] else (1,.25,.15,1)))
   drawn=set()
   for node in range(grid.count):
    quad=grid.quad(node);layer=grid.layer(node);key=f'{n}:{node}'
    corners=[core.add(core.warp(p,quad,layer,cfg.height),offset) for p in core.CORNERS]
    for i,j in core.EDGES:
     edge=tuple(sorted((corners[i],corners[j])))
     if edge not in drawn:lines.append((edge,(.24,.32,.3,1)));drawn.add(edge)
    index=result['choices'][node] if result['success'] else next((i for i,v in enumerate(vs) if v.module.uid==selected.uid),None)
    if node in grid.core:
     if index is None:continue
     obj=_native_core(scene,source,key,quad,layer,cfg.height,offset,vs[index],existing);used.add(obj.name)
     used.update((obj.get('dcc_cage',''),obj.get('dcc_frame','')))
     for issue in source_issues:
      if issue['face']>=0:
       face=core.transform_face(issue['face'],*vs[index].pose)
       points=[corners[i] for i in core.FACES[face]]
       overlays.append((points,(1,.1,.05,.3) if issue['kind']=='INVALID' else (1,.6,.05,.19)))
       hits.append((tuple(sum(p[i] for p in points)/4 for i in range(3)),issue['message']))
     if node==min(grid.core):
      for i,p in enumerate(core.CORNERS):
       location=core.add(core.warp(p,quad,layer,cfg.height,*vs[index].pose),offset)
       labels.append((location,'V'+str(i+1),(1,.55,.1,1) if selected.state&(1<<i) else (.55,.6,.62,1)))
    elif result['success'] and index>=0 and vs[index].state not in (0,255):
     obj=_helper(scene,key,vs[index].module,vs[index],quad,layer,cfg.height,offset,objects,existing);used.add(obj.name)
   if not result['success']:
    for node,face in result['conflicts']:
     if face<0:continue
     points=[core.add(core.warp(core.CORNERS[i],grid.quad(node),grid.layer(node),cfg.height),offset) for i in core.FACES[face]]
     overlays.append((points,(1,.1,.05,.4)));hits.append((tuple(sum(p[i] for p in points)/4 for i in range(3)),result['error']))
  # Delete stale generated objects only, keeping source meshes and active edits.
  for obj in list(scene.objects):
   if obj.get(a.OWNER)==uid(scene) and obj.get(a.ROLE) in ('core','helper','lattice','frame') and obj.name not in used:
    if obj==editing:continue
    remove_object(obj)
  cache.update(signature=signature,editing_key=editing_key,dirty=False,grids=grids,variants=vs,previous=records,seed=cfg.seed,
               lines=lines,overlays=overlays,labels=labels,hits=hits)
  # Compute user-facing diagnostics independently from a solve's success.
  cache['source_issues']=source_issues
  cfg.status='；'.join(status)
  names=a.names(scene)
 finally:cfg.busy=False

def draw_3d():
 if not _ENABLED or not bpy.context.scene or not hasattr(bpy.context.scene,'pcg_dcc'):return
 scene=bpy.context.scene;cfg=scene.pcg_dcc;cache=a._CACHE.get(scene.as_pointer(),{})
 lines=list(cache.get('lines',[])) if cfg.previews else [];overlays=list(cache.get('overlays',[])) if cfg.previews else []
 source=cfg.active
 if cfg.pending_frame:
  points=[tuple(cfg.pending_frame.matrix_world@Vector(p)) for p in core.CORNERS]
  lines.extend(((points[i],points[j]),(.25,.65,1,1)) for i,j in core.EDGES)
 if source and source.pcg_dcc_module.enabled:
  points=[tuple(source.matrix_world@Vector(p)) for p in core.CORNERS]
  lines.extend(((points[i],points[j]),(.45,.8,.55,1)) for i,j in core.EDGES)
  found=[dict(kind=r.kind,message=r.message,face=r.face,vertex=r.vertex) for r in cfg.issues if r.uid==source.pcg_dcc_module.uid]
  if not found:found=cache.get('source_issues',[])
  general=[r for r in found if r['face']<0 and r['vertex']<0]
  if general:
   color=(1,.12,.06,1) if any(r['kind']=='INVALID' for r in general) else (1,.8,.1,1)
   lines.extend(((points[i],points[j]),color) for i,j in core.EDGES)
  face=core.FACE_NAMES.index(cfg.face)
  indices=core.FACES[face]
  lines.extend(((points[i],points[j]),(.2,.6,1,1)) for i,j in zip(indices,indices[1:]+indices[:1]))
  for r in found:
   if r['face']>=0:overlays.append(([points[i] for i in core.FACES[r['face']]],(1,.15,.05,.3) if r['kind']=='INVALID' else (1,.65,.05,.22)))
  shader=gpu.shader.from_builtin('UNIFORM_COLOR');gpu.state.blend_set('ALPHA');gpu.state.depth_test_set('LESS_EQUAL')
  for i,p in enumerate(points):
   gpu.state.point_size_set(8);shader.bind();shader.uniform_float('color',(.25,1,.35,1) if source.pcg_dcc_module.corners[i] else (.65,.68,.7,1))
   batch_for_shader(shader,'POINTS',{'pos':[p]}).draw(shader)
  vertices=a.snapshot(source,False)[0]
  for r in found:
   if 0<=r['vertex']<len(vertices):
    gpu.state.point_size_set(11);shader.bind();shader.uniform_float('color',(1,.05,.03,1))
    batch_for_shader(shader,'POINTS',{'pos':[tuple(source.matrix_world@Vector(vertices[r['vertex']]))]}).draw(shader)
 shader=gpu.shader.from_builtin('UNIFORM_COLOR');gpu.state.blend_set('ALPHA');gpu.state.depth_test_set('LESS_EQUAL')
 for edge,color in lines:
  shader.bind();shader.uniform_float('color',color);batch_for_shader(shader,'LINES',{'pos':edge}).draw(shader)
 for points,color in overlays if cfg.show_diagnostic_faces else ():
   shader.bind();shader.uniform_float('color',color);batch_for_shader(shader,'TRIS',{'pos':points},indices=((0,1,2),(0,2,3))).draw(shader)
 gpu.state.depth_test_set('NONE');gpu.state.blend_set('NONE');gpu.state.point_size_set(1)

def draw_2d():
 if not _ENABLED or not bpy.context.region or not bpy.context.region_data:return
 scene=bpy.context.scene;cfg=scene.pcg_dcc;cache=a._CACHE.get(scene.as_pointer(),{});labels=list(cache.get('labels',[])) if cfg.previews else []
 source=cfg.active
 if cfg.pending_frame:
  labels.extend((tuple(cfg.pending_frame.matrix_world@Vector(p)),f'V{i+1}',(.3,.75,1,1)) for i,p in enumerate(core.CORNERS))
 if source and source.pcg_dcc_module.enabled:
  labels.extend((tuple(source.matrix_world@Vector(p)),f'V{i+1}  {int(source.pcg_dcc_module.corners[i])}',(.3,1,.4,1) if source.pcg_dcc_module.corners[i] else (.7,.7,.7,1)) for i,p in enumerate(core.CORNERS))
 for p,text,color in labels:
  xy=view3d_utils.location_3d_to_region_2d(bpy.context.region,bpy.context.region_data,Vector(p))
  if xy:
   blf.size(0,12);blf.color(0,*color);blf.position(0,xy.x+5,xy.y+5,0);blf.draw(0,text)
 hover=cache.get('hover')
 if hover and time.monotonic()-hover[3]<2 and (len(hover)<5 or hover[4]==bpy.context.area.as_pointer()):
  x,y,text,_=hover[:4];blf.size(0,13);blf.color(0,1,.8,.3,1)
  # Wrap diagnostic text for narrow viewports.
  for i in range(0,len(text),38):blf.position(0,x+14,y-18-(i//38)*18,0);blf.draw(0,text[i:i+38])

def register():
 global _ENABLED
 _ENABLED=True
 if not bpy.app.background:
  _HANDLES.extend([bpy.types.SpaceView3D.draw_handler_add(draw_3d,(),'WINDOW','POST_VIEW'),
                   bpy.types.SpaceView3D.draw_handler_add(draw_2d,(),'WINDOW','POST_PIXEL')])

def unregister():
 global _ENABLED
 _ENABLED=False
 for handle in _HANDLES:bpy.types.SpaceView3D.draw_handler_remove(handle,'WINDOW')
 _HANDLES.clear();_VARIANT_CACHE.clear()
