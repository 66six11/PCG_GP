"""DCC v2 contracts, deterministic preview topology and face ports. No bpy."""
from dataclasses import dataclass, field
import json
import math
from pathlib import Path
import re

CORNERS=((-0.5,0.5,0.5),(0.5,0.5,0.5),(0.5,-0.5,0.5),(-0.5,-0.5,0.5),
         (-0.5,0.5,-0.5),(0.5,0.5,-0.5),(0.5,-0.5,-0.5),(-0.5,-0.5,-0.5))
FACES=((0,1,2,3),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7))
FACE_NAMES=('up','down','front','right','back','left')
NORMALS=((0,0,1),(0,0,-1),(0,1,0),(1,0,0),(0,-1,0),(-1,0,0))
OPPOSITE=(1,0,4,5,2,3)
EDGES=((0,1),(1,2),(2,3),(3,0),(4,5),(5,6),(6,7),(7,4),(0,4),(1,5),(2,6),(3,7))
P=10000
RESERVED=('$empty','$solid','$outside')

def add(a,b):return tuple(x+y for x,y in zip(a,b))
def sub(a,b):return tuple(x-y for x,y in zip(a,b))
def mul(a,k):return tuple(x*k for x in a)
def dot(a,b):return sum(x*y for x,y in zip(a,b))
def state_string(state):return ''.join(str((state>>i)&1) for i in range(8))
def state_of(bits):return sum(int(v)<<i for i,v in enumerate(bits))
def valid_id(value):return bool(re.fullmatch(r'[\w-]+(?:\.[\w-]+)*',value,re.UNICODE))
def matches(pattern,value):return pattern==value or (not pattern.startswith('$') and value.startswith(pattern+'.'))
def orient(p,turns=0,mirror=False):
 x,y,z=p
 if mirror:x=-x
 for _ in range(turns%4):x,y=y,-x
 return x,y,z
def transform_state(state,turns=0,mirror=False):
 return sum(1<<CORNERS.index(orient(p,turns,mirror)) for i,p in enumerate(CORNERS) if state&(1<<i))
def transform_face(face,turns=0,mirror=False):return NORMALS.index(orient(NORMALS[face],turns,mirror))
def triangles(faces):return tuple((f[0],f[i],f[i+1]) for f in faces for i in range(1,len(f)-1))

_BASE=None
def base_mesh(state):
 global _BASE
 if state in (0,255):return (),()
 if _BASE is None:_BASE=json.loads(Path(__file__).with_name('base_meshes.json').read_text(encoding='utf-8'))['states']
 entry=_BASE[str(state)]
 return tuple(tuple(p) for p in entry['vertices']),tuple(tuple(f) for f in entry['faces'])

def warp(p,quad,layer=0,height=1,turns=0,mirror=False):
 x,y,z=orient(p,turns,mirror);u=x+.5;v=y+.5
 w=((1-u)*v,u*v,u*(1-v),(1-u)*(1-v))
 return (sum(q[0]*k for q,k in zip(quad,w)),sum(q[1]*k for q,k in zip(quad,w)),(layer+.5+z)*height)

def _merged(segments):
 segments=set(segments)
 while True:
  owners={}
  for edge in segments:
   for point in edge:owners.setdefault(point,[]).append(edge)
  merge=None
  for point,edges in owners.items():
   if len(edges)!=2:continue
   a=next(p for p in edges[0] if p!=point);b=next(p for p in edges[1] if p!=point)
   u=sub(a,point);v=sub(b,point)
   if u[0]*v[1]==u[1]*v[0] and dot(u,v)<0:merge=(edges,a,b);break
  if merge is None:return tuple(sorted(segments))
  edges,a,b=merge;segments.difference_update(edges);segments.add(tuple(sorted((a,b))))

def contour(vertices,triangles,face):
 a=CORNERS[FACES[face][0]];u=sub(CORNERS[FACES[face][1]],a);v=sub(CORNERS[FACES[face][3]],a)
 axis=next(i for i,x in enumerate(NORMALS[face]) if x);plane=round(a[axis]*P)
 points=[tuple(round(x*P) for x in p) for p in vertices]
 coords=[(round(dot(sub(p,mul(a,P)),u)),round(dot(sub(p,mul(a,P)),v))) for p in points]
 edges=set();coplanar={}
 for tri in triangles:
  all_on=all(points[i][axis]==plane for i in tri)
  for i,j in zip(tri,tri[1:]+tri[:1]):
   if points[i][axis]!=plane or points[j][axis]!=plane or coords[i]==coords[j]:continue
   edge=tuple(sorted((coords[i],coords[j])))
   if all_on:coplanar[edge]=coplanar.get(edge,0)+1
   else:edges.add(edge)
 edges.update(e for e,count in coplanar.items() if count==1)
 return _merged(edges)

SQUARE=((0,0),(P,0),(P,P),(0,P))
def orientation(ids):
 start=min(range(4),key=lambda i:ids[i]);return start*2+int(ids[(start+1)%4]>ids[(start+3)%4])
def _target(i,orientation):
 start,reverse=divmod(orientation,2)
 return SQUARE[(start-i if reverse else i-start)%4]
def map_uv(p,orientation):
 a=_target(0,orientation);u=sub(_target(1,orientation),a);v=sub(_target(3,orientation),a)
 return tuple(a[i]+round((u[i]*p[0]+v[i]*p[1])/P) for i in range(2))
def port(state,contour,face,pose):
 bits=[0]*4
 for i,corner in enumerate(FACES[face]):bits[SQUARE.index(_target(i,pose))]=(state>>corner)&1
 return tuple(bits),tuple(sorted(tuple(sorted((map_uv(a,pose),map_uv(b,pose)))) for a,b in contour))
def cube_orientation(face):return orientation(tuple(CORNERS[i] for i in FACES[face]))

@dataclass
class Module:
 uid:str
 id:str
 state:int
 vertices:tuple
 triangles:tuple
 weight:float=1
 turns:tuple=(0,1,2,3)
 mirror:bool=False
 rules:dict=field(default_factory=dict) # Missing face: AUTO. Existing empty list: explicit empty.
 generated:bool=False

@dataclass
class Variant:
 module:Module
 turns:int
 mirror:bool
 state:int
 contours:tuple
 rules:dict
 ports:dict=field(default_factory=dict)
 @property
 def pose(self):return self.turns,self.mirror
 def matches_pose(self,pose):return pose is None or self.pose==tuple(pose)
 @property
 def key(self):return (self.module.uid,)+self.pose
 @property
 def weight(self):return self.module.weight/(len(self.module.turns)*(2 if self.module.mirror else 1))
 def port(self,face,pose):
  key=face,pose
  if key not in self.ports:self.ports[key]=port(self.state,self.contours[face],face,pose)
  return self.ports[key]
 def preference(self,face,target):
  rules=self.rules.get(face)
  if rules is None:return 1.0
  matches_=[r for r in rules if matches(r['id'],target)]
  return max(matches_,key=lambda r:len(r['id']))['weight'] if matches_ else 0.0

def variants(modules):
 result=[]
 for m in sorted(modules,key=lambda m:m.uid):
  for mirror in (False,True) if m.mirror else (False,):
   for turns in m.turns:
    points=[orient(p,turns,mirror) for p in m.vertices]
    result.append(Variant(m,turns,mirror,transform_state(m.state,turns,mirror),
      tuple(contour(points,m.triangles,f) for f in range(6)),
      {transform_face(FACE_NAMES.index(f),turns,mirror):rules for f,rules in m.rules.items()}))
 return result

def semantic(a,fa,b,fb):
 x=a.preference(fa,b.module.id);y=b.preference(fb,a.module.id)
 return x>0 and y>0 and (fa not in a.rules or fb not in b.rules or abs(x-y)<1e-5)

def issues(modules,compiled=None):
 result=[];uids=set();vs=compiled if compiled is not None else variants(modules)
 for m in modules:
  def emit(kind,message,face=-1,vertex=-1):result.append(dict(uid=m.uid,kind=kind,message=message,face=face,vertex=vertex))
  if not valid_id(m.id):emit('INVALID','ID 必须是 主.子.子 形式，各段非空且仅含文字、数字、下划线或连字符')
  if not m.uid or m.uid in uids:emit('INVALID','内部唯一键重复，请创建独立模块')
  uids.add(m.uid)
  if not m.turns:emit('INVALID','至少允许一个旋转方向')
  if not math.isfinite(m.weight) or m.weight<=0:emit('INVALID','模块权重必须为正数')
  conflicts=[x for x in modules if x.uid!=m.uid and x.id==m.id and x.state!=m.state]
  if conflicts:emit('WARNING','同 ID 使用了不同八角状态；允许保留，匹配时仍按 Bool 筛选')
  for i,p in enumerate(m.vertices):
   if any(not math.isfinite(x) or abs(x)>.5001 for x in p):emit('INVALID',f'顶点 {i} 超出标准线框 [-0.5,0.5]',vertex=i)
  if m.state not in (0,255) and not m.triangles:emit('INVALID','非空表面模块没有面')
  if m.state in (0,255) and m.triangles:emit('INVALID','全空或全实状态只需要参考笼，不应包含表面网格')
  if m.state not in (0,255) and m.vertices and not any(any(abs(x)>=.4999 for x in p) for p in m.vertices):emit('INVALID','几何未触及 Cell 边界，请检查注册框尺寸')
  for tri in m.triangles:
   if any(i<0 or i>=len(m.vertices) for i in tri):emit('INVALID','三角面包含无效顶点引用');continue
   u=sub(m.vertices[tri[1]],m.vertices[tri[0]]);v=sub(m.vertices[tri[2]],m.vertices[tri[0]])
   cross=(u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0])
   if dot(cross,cross)<1e-16:emit('INVALID','退化三角面，请检查重叠点或零面积面',vertex=tri[0])
  for f,name in enumerate(FACE_NAMES):
   bits={(m.state>>i)&1 for i in FACES[f]}
   if name not in m.rules:
    if len(bits)>1:emit('MISSING','此面 Bool 空实混合，尚未配置语义邻接；预览暂按几何匹配',face=f)
    continue
   rules=m.rules[name]
   if not rules:emit('MISSING','已限定候选但列表为空，预览可能无解',face=f)
   seen=set()
   for rule in rules:
    target=rule['id'];weight=rule['weight']
    if target in seen or not math.isfinite(weight) or weight<=0:emit('INVALID','候选重复或权重不是正数',face=f)
    seen.add(target)
    targets=[v for v in vs if matches(target,v.module.id)]
    if target=='$outside':
     if contour(m.vertices,m.triangles,f):emit('INVALID','地图外侧接口仍包含几何边界',face=f)
     continue
    if target in RESERVED:
     targets=variants([Module(target,target,255 if target=='$solid' else 0,(),(),turns=(0,))])
    elif not targets:emit('INVALID',f'找不到候选 ID：{target}',face=f);continue
    # Test the declared source face in all of this module's permitted poses.
    support=False
    for a in (v for v in vs if v.module.uid==m.uid):
     fa=transform_face(f,*a.pose);fb=OPPOSITE[fa]
     for b in targets:
      if semantic(a,fa,b,fb) and a.port(fa,cube_orientation(fa))==b.port(fb,cube_orientation(fb)):
       support=True;break
     if support:break
    if not support:emit('INVALID',f'{target} 没有双向兼容的几何、Bool 或朝向',face=f)
 return result

@dataclass
class Preview:
 points:list
 quads:list
 valence:int
 layers:int=3
 core_layer:int=1
 neighbors:list=field(default_factory=list)
 face_poses:list=field(default_factory=list)
 @property
 def count(self):return len(self.quads)*self.layers
 @property
 def core(self):return {self.core_layer*len(self.quads)+i for i in range(self.valence)}
 def quad(self,node):return tuple(self.points[i] for i in self.quads[node%len(self.quads)])
 def layer(self,node):return node//len(self.quads)

def preview_grid(valence,layers=3,core_layer=1,skew=0):
 if valence not in (3,4,5) or layers<3 or not 0<=core_layer<layers:raise ValueError('Invalid preview dimensions')
 radial=[(math.cos(-2*math.pi*i/valence),math.sin(-2*math.pi*i/valence)) for i in range(valence)]
 points=[(0.,0.)]+radial
 for i in range(valence):points.append(add(radial[i],radial[(i+1)%valence]))
 quads=[(0,1+i,1+valence+i,1+(i+1)%valence) for i in range(valence)]
 boundary=[j for i in range(valence) for j in (1+i,1+valence+i)]
 outer=[]
 for i in boundary:outer.append(len(points));points.append(mul(points[i],2))
 for i in range(len(boundary)):
  j=(i+1)%len(boundary);quads.append((boundary[i],outer[i],outer[j],boundary[j]))
 if skew:points=[(x+.12*skew*y*y,y+.08*skew*x) for x,y in points]
 g=Preview(points,quads,valence,layers,core_layer);owners={}
 for node in range(g.count):
  ids=tuple((g.layer(node)+offset)*len(points)+i for offset in (1,0) for i in quads[node%len(quads)])
  neighbors=[None]*6;poses=[]
  for face,corners in enumerate(FACES):
   face_ids=tuple(ids[i] for i in corners);poses.append(orientation(face_ids));key=tuple(sorted(face_ids))
   if key in owners:
    other,of=owners[key];neighbors[face]=(other,of);g.neighbors[other][of]=(node,face)
   else:owners[key]=(node,face)
  g.neighbors.append(neighbors);g.face_poses.append(poses)
 return g
