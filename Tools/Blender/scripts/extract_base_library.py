"""Read the project's canonical source library; never save over the input blend."""
import json
from pathlib import Path
import re
import bpy

root=Path(__file__).resolve().parents[1]
corners=((-0.5,0.5,0.5),(0.5,0.5,0.5),(0.5,-0.5,0.5),(-0.5,-0.5,0.5),
         (-0.5,0.5,-0.5),(0.5,0.5,-0.5),(0.5,-0.5,-0.5),(-0.5,-0.5,-0.5))
def orient(p,t,m):
 x,y,z=p
 if m:x=-x
 for _ in range(t):x,y=y,-x
 return x,y,z
sources=[]
for obj in sorted(bpy.context.scene.objects,key=lambda o:o.name):
 match=re.search(r'(?<![01])([01]{8})$',obj.name)
 if obj.type!='MESH' or not match or obj.get('pcg_preview_owner'):continue
 state=sum((c=='1')<<i for i,c in enumerate(match[1]))
 if state in (0,255):continue
 sources.append((state,[tuple(v.co) for v in obj.data.vertices],[list(p.vertices) for p in obj.data.polygons]))
lookup={}
for t in range(4):
 for mirror in (False,True):
  for state,vertices,faces in sources:
   target=sum(1<<corners.index(orient(p,t,mirror)) for i,p in enumerate(corners) if state&(1<<i))
   lookup.setdefault(str(target),{'vertices':[orient(p,t,mirror) for p in vertices],
      'faces':[list(reversed(f)) if mirror else f for f in faces]})
assert len(lookup)==254,len(lookup)
path=root/'pcg_grid_preview/base_meshes.json'
path.write_text(json.dumps({'version':1,'seeds':[s[0] for s in sources],'states':lookup},sort_keys=True,separators=(',',':')),encoding='utf-8')
print('DCC_BASE_LIBRARY_OK: 254 normalized surface templates')
