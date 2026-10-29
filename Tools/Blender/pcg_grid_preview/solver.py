"""Finite local WFC. Bool, contours and semantic rules are all hard constraints.
Preview cut boundaries stay open; there is no silent fallback or seed replacement.
"""
from collections import deque
import math
import random
from . import core

def bits(mask):
 while mask:
  low=mask&-mask;yield low.bit_length()-1;mask^=low

def catalog(modules,include_base=True):
 sources=list(modules)
 if include_base:
  for state in range(1,255):
   v,f=core.base_mesh(state)
   sources.append(core.Module('$base:'+str(state),'base.s'+str(state).zfill(3),state,v,core.triangles(f),
                              weight=.25,turns=(0,),generated=True))
 sources.extend([core.Module('$empty','$empty',0,(),(),turns=(0,)),core.Module('$solid','$solid',255,(),(),turns=(0,))])
 return core.variants(sources)

class Solver:
 def __init__(self,variants):
  self.variants=variants;self.supports={};self.buckets={}
 def support(self,fa,oa,fb,ob):
  key=fa,oa,fb,ob
  if key not in self.supports:
   if (fb,ob) not in self.buckets:
    index={}
    for i,b in enumerate(self.variants):index.setdefault(b.port(fb,ob),[]).append(i)
    self.buckets[fb,ob]=index
   index=self.buckets[fb,ob];masks=[]
   for a in self.variants:
    mask=0
    for j in index.get(a.port(fa,oa),()):
     if core.semantic(a,fa,self.variants[j],fb):mask|=1<<j
    masks.append(mask)
   self.supports[key]=masks
  return self.supports[key]

 def solve_neighbors(self,grid,selected,seed=42,previous=None,locked_pose=None,pins=None):
  """One pinned cell and its face neighbors; never propagate from a helper."""
  vs=self.variants;rng=random.Random(seed);node=min(grid.core)
  result=dict(success=False,choices=None,error='',conflicts=[],observations=0,propagations=0,backtracks=0,budget=False)
  pose=(pins or {}).get(node,locked_pose)
  centers=[i for i,v in enumerate(vs) if v.module.uid==selected.uid and v.matches_pose(pose)]
  lookup={v.key:i for i,v in enumerate(vs)}
  old=lookup.get((previous or {}).get(node))
  centers.sort(key=lambda i:(i!=old,i))
  if not centers:
   result.update(error='当前 Cell 没有允许的朝向',conflicts=[(node,-1)]);return result
  for index in centers:
   center=vs[index];choices=[-1]*grid.count;choices[node]=index;conflicts=[];observations=0
   for face,pair in enumerate(grid.neighbors[node]):
    # A fully inactive face has no building connection to preview. Do not
    # sample an arbitrary module merely because its touching face is empty.
    if not any(center.state&(1<<corner) for corner in core.FACES[face]):continue
    if pair is None:
     if face in center.rules:conflicts.append((node,face))
     continue
    other,of=pair
    masks=self.support(face,grid.face_poses[node][face],of,grid.face_poses[other][of])
    # Neighbor cells are independent instances; the source asset may repeat.
    candidates=list(bits(masks[index]))
    if not candidates:conflicts.extend(((node,face),(other,of)));continue
    previous_index=lookup.get((previous or {}).get(other))
    if previous_index in candidates:choice=previous_index
    else:
     weights=[]
     for j in candidates:
      v=vs[j];factor=center.preference(face,v.module.id) if face in center.rules else v.preference(of,center.module.id)
      weights.append(v.weight*factor)
     choice=rng.choices(candidates,weights=weights,k=1)[0];observations+=1
    choices[other]=choice
   if not conflicts:
    result.update(success=True,choices=choices,observations=observations);return result
   result['conflicts']=conflicts
  missing=[face for n,face in result['conflicts'] if n==node and grid.neighbors[node][face] is None]
  result['error']='预览范围不足：当前格的显式邻接超出裁切范围' if missing else '当前格的直接邻接没有合法候选；不继续推导邻格的其他面'
  return result

 def solve(self,grid,selected,seed=42,previous=None,locked_pose=None,budget=4096,pins=None):
  vs=self.variants;rng=random.Random(seed);domains=[]
  result=dict(success=False,choices=None,error='',conflicts=[],observations=0,propagations=0,backtracks=0,budget=False)
  for node in range(grid.count):
   mask=0
   for i,v in enumerate(vs):
    if node in grid.core:
     pose=(pins or {}).get(node,locked_pose)
     allowed=v.module.uid==selected.uid and v.matches_pose(pose)
    else:allowed=v.module.uid!=selected.uid and v.module.id!=selected.id
    if allowed:mask|=1<<i
   domains.append(mask)
   if not mask:
    result.update(error=f'Cell {node} 没有初始候选；检查当前模块允许的朝向',conflicts=[(node,-1)])
    return result
  for node in grid.core:
   for face,pair in enumerate(grid.neighbors[node]):
    if pair is not None:continue
    permitted=0
    for value in bits(domains[node]):
     if face not in vs[value].rules:permitted|=1<<value
    if not permitted:
     result.update(error=f'预览范围不足：核心 Cell {node}/{core.FACE_NAMES[face]} 的显式邻接超出裁切范围；调整模块所在层或增加层数',conflicts=[(node,face)])
     return result
    domains[node]=permitted
  # Reuse a still-valid layout, rather than changing helpers during every edit.
  if previous and len(previous)==grid.count:
   lookup={v.key:i for i,v in enumerate(vs)}
   choices=[lookup.get(previous.get(i),-1) for i in range(grid.count)]
   if all(i>=0 and domains[n]&(1<<i) for n,i in enumerate(choices)) and self.validate(grid,choices):
    result.update(success=True,choices=choices);return result

  def propagate(ds):
   queue=deque(range(grid.count));queued=set(queue)
   while queue:
    source=queue.popleft();queued.discard(source)
    for sf,pair in enumerate(grid.neighbors[source]):
     if pair is None:continue
     target,tf=pair;masks=self.support(tf,grid.face_poses[target][tf],sf,grid.face_poses[source][sf])
     before=ds[target];after=0
     for value in bits(before):
      if masks[value]&ds[source]:after|=1<<value
     if after==before:continue
     result['propagations']+=(before^after).bit_count();ds[target]=after
     if not after:
      result.update(error=f'Cell {target}/{core.FACE_NAMES[tf]} 与 Cell {source}/{core.FACE_NAMES[sf]} 无共同合法候选',
                    conflicts=[(target,tf),(source,sf)])
      return False
     if target not in queued:queue.append(target);queued.add(target)
   return True
  def weights(node,ds):
   output=[]
   for value in bits(ds[node]):
    a=vs[value];log=math.log(a.weight)
    for face,pair in enumerate(grid.neighbors[node]):
     if pair is None:continue
     other,of=pair
     if ds[other].bit_count()!=1:continue
     b=vs[ds[other].bit_length()-1]
     factor=a.preference(face,b.module.id) if face in a.rules else b.preference(of,a.module.id)
     if factor>0:log+=math.log(factor)
    output.append((value,math.exp(max(-50,min(50,log)))))
   return output
  def choose(items):
   t=rng.random()*sum(w for _,w in items)
   for value,w in items:
    t-=w
    if t<0:return value
   return items[-1][0]
  stack=[];consistent=propagate(domains)
  while True:
   if consistent:
    best=-1;entropy=float('inf')
    for node,mask in enumerate(domains):
     if mask.bit_count()<=1:continue
     items=weights(node,domains);total=sum(w for _,w in items)
     h=math.log(total)-sum(w*math.log(w) for _,w in items)/total+rng.random()*1e-9
     if h<entropy:best=node;entropy=h
    if best<0:
     choices=[m.bit_length()-1 for m in domains]
     if not self.validate(grid,choices):raise AssertionError('Solver published inconsistent shared Bool/ports')
     result.update(success=True,choices=choices,error='',conflicts=[]);return result
    stack.append([domains.copy(),best,domains[best]])
   else:
    result['backtracks']+=1
    while stack and not stack[-1][2]:stack.pop()
    if not stack:return result
   if result['observations']>=budget:
    result.update(budget=True,error='预览搜索达到上限；当前编辑模块保持不变');return result
   snapshot,node,remaining=stack[-1];domains=snapshot.copy()
   choice=choose([(v,w) for v,w in weights(node,domains) if remaining&(1<<v)])
   stack[-1][2]&=~(1<<choice);domains[node]=1<<choice;result['observations']+=1
   consistent=propagate(domains)

 def validate(self,grid,choices):
  vertex_bits={}
  for node,index in enumerate(choices):
   variant=self.variants[index];q=grid.quads[node%len(grid.quads)]
   ids=tuple((grid.layer(node)+offset)*len(grid.points)+i for offset in (1,0) for i in q)
   for corner,key in enumerate(ids):
    value=(variant.state>>corner)&1
    if key in vertex_bits and vertex_bits[key]!=value:return False
    vertex_bits[key]=value
   for face,pair in enumerate(grid.neighbors[node]):
    if pair is None:continue
    other,of=pair;b=self.variants[choices[other]]
    if not core.semantic(variant,face,b,of) or variant.port(face,grid.face_poses[node][face])!=b.port(of,grid.face_poses[other][of]):return False
  return True
