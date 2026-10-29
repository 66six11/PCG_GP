"""Blender regression: derived assets and UI rule targets stay independent. No files saved."""
from pathlib import Path
from types import SimpleNamespace
import runpy
import bpy
runpy.run_path(str(Path(__file__).resolve().parents[1]/'run_preview.py'))
from pcg_grid_preview import authoring as a,ui
scene=bpy.context.scene
wall,_=a.create(bpy.context,'Defense.CityWall',51)
bpy.ops.pcg_dcc.rule(action='ADD',uid=wall.pcg_dcc_module.uid,face='left',target='Defense.CityWall',weight=2)
tower,parent=a.create(bpy.context,'Defense.CityWall.CityTower',51)
assert parent==wall and tower.data!=wall.data
assert tower.pcg_dcc_module.uid!=wall.pcg_dcc_module.uid
original=tuple(wall.data.vertices[0].co)
tower.data.vertices[0].co.x+=.01
assert tuple(wall.data.vertices[0].co)==original
manual=lambda o:[(r.face,r.target,r.weight) for r in o.pcg_dcc_module.rules if not r.automatic]
wall_rules=manual(wall)
bpy.ops.pcg_dcc.rule(action='ADD',uid=tower.pcg_dcc_module.uid,face='right',index=0,target='Defense.CityWall.CityTower',weight=3)
assert manual(wall)==wall_rules
assert ('right','Defense.CityWall.CityTower',3) in manual(tower)
assert ('left','Defense.CityWall',2) in manual(tower)
class Layout:
 def __init__(self):self.ops=[]
 def operator(self,name,**kwargs):
  result=SimpleNamespace(operator=name);self.ops.append(result);return result
 def __getattr__(self,name):return lambda *args,**kwargs:self
for obj in (wall,tower,wall,tower):
 for face in ('left','right','up'):
  scene.pcg_dcc.active=obj;scene.pcg_dcc.face=face
  layout=Layout();ui.DCC_PT_module.draw(SimpleNamespace(layout=layout),bpy.context)
  for op in layout.ops:
   if op.operator!='pcg_dcc.rule':continue
   assert op.uid==obj.pcg_dcc_module.uid and op.face==face
   if op.action in ('ADD','AUTO'):assert op.index==-1
layout=Layout();ui.draw_registry(layout,bpy.context)
assert all(op.index==-1 for op in layout.ops if op.operator=='pcg_dcc.rule' and op.action in ('ADD','AUTO'))
for name in ('uid','face','index','preset'):
 assert bpy.ops.pcg_dcc.rule.get_rna_type().properties[name].is_skip_save
print('DCC_RULE_BINDING_REGRESSION_OK: independent mesh/UUID/rules; switching modules/faces; registry targets; no historical target reuse')
