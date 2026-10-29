"""Contract and local-solver tests, runnable without Blender."""
import importlib.util
from pathlib import Path
import sys
import types
import unittest
from collections import Counter

root=Path(__file__).parents[1]/'pcg_grid_preview'
package=types.ModuleType('dcc_test');package.__path__=[str(root.resolve())];sys.modules['dcc_test']=package
for name in ('core','solver'):
 spec=importlib.util.spec_from_file_location('dcc_test.'+name,root/(name+'.py'))
 module=importlib.util.module_from_spec(spec);sys.modules[spec.name]=module;spec.loader.exec_module(module);setattr(package,name,module)
c=package.core;s=package.solver

def module(uid,name,state,**kw):
 v,f=c.base_mesh(state);return c.Module(uid,name,state,v,c.triangles(f),**kw)

class ContractTests(unittest.TestCase):
 def test_hierarchical_ids_match_only_complete_segments(self):
  self.assertTrue(c.valid_id('建筑.屋顶.端头'));self.assertTrue(c.matches('roof','roof.end'))
  self.assertFalse(c.matches('roof','roofing'));self.assertFalse(c.valid_id('a..b'));self.assertFalse(c.valid_id('$empty'))
 def test_all_surface_templates_are_normalized(self):
  for state in range(1,255):
   v,f=c.base_mesh(state);self.assertTrue(v and f)
   self.assertTrue(all(abs(x)<=.5001 for p in v for x in p))
  self.assertEqual(c.base_mesh(0),((),()));self.assertEqual(c.state_string(240),'00001111')
 def test_three_fans_have_expected_valence_and_one_full_ring(self):
  for n in (3,4,5):
   g=c.preview_grid(n);self.assertEqual(len(g.quads),n*3);self.assertEqual(g.count,n*9);self.assertEqual(len(g.core),n)
   self.assertEqual(sum(0 in q for q in g.quads),n)
   for q in g.quads:
    points=[g.points[i] for i in q]
    area=sum(a[0]*b[1]-a[1]*b[0] for a,b in zip(points,points[1:]+points[:1]))
    self.assertLess(area,0)
   for node,faces in enumerate(g.neighbors):
    for face,other in enumerate(faces):
     if other:self.assertEqual(g.neighbors[other[0]][other[1]],(node,face))
 def test_bool_is_part_of_port_even_when_both_meshes_are_empty(self):
  empty=c.variants([c.Module('e','$empty',0,(),(),turns=(0,))])[0]
  solid=c.variants([c.Module('s','$solid',255,(),(),turns=(0,))])[0]
  for face in range(6):self.assertNotEqual(empty.port(face,0),solid.port(face,0))
 def test_missing_rules_highlight_only_mixed_bool_faces(self):
  wall=module('a','wall',51)
  issues=c.issues([wall]);self.assertEqual({i['face'] for i in issues if i['kind']=='MISSING'},{0,1,3,5})
 def test_same_logical_id_bool_conflict_warns_but_is_not_invalid(self):
  result=c.issues([module('a','roof',240),module('b','roof',15)])
  self.assertTrue(any(i['kind']=='WARNING' for i in result));self.assertFalse(any(i['kind']=='INVALID' for i in result))
 def test_missing_reference_is_localized_to_the_declared_face(self):
  m=module('r','roof',240,rules={'down':[{'id':'missing','weight':1}]})
  result=[i for i in c.issues([m]) if i['kind']=='INVALID']
  self.assertEqual(len(result),1);self.assertEqual(result[0]['face'],1)
 def test_outside_geometry_reports_actual_vertex(self):
  m=module('a','roof',240);m.vertices=((.8,0,0),)+m.vertices[1:]
  self.assertTrue(any(i['vertex']==0 and i['kind']=='INVALID' for i in c.issues([m])))

class PreviewSolverTests(unittest.TestCase):
 def setUp(self):
  self.roof=module('roof','building.roof',240,rules={'down':[{'id':'building.wall','weight':1}]})
  self.wall=module('wall','building.wall',31)
  self.vs=s.catalog([self.roof,self.wall]);self.solver=s.Solver(self.vs)
 def test_roof_in_all_three_fans_requires_real_wall_below(self):
  for n in (3,4,5):
   g=c.preview_grid(n);r=self.solver.solve(g,self.roof)
   self.assertTrue(r['success'],r['error']);self.assertTrue(self.solver.validate(g,r['choices']))
   for node in g.core:
    self.assertEqual(self.vs[r['choices'][node]].module.uid,'roof')
    self.assertEqual(self.vs[r['choices'][node-len(g.quads)]].module.uid,'wall')
   for node in set(range(g.count))-g.core:self.assertNotEqual(self.vs[r['choices'][node]].module.id,self.roof.id)
 def test_wrong_bool_wall_cannot_be_forced_by_weight(self):
  bad=module('wall','building.wall',51,weight=10000)
  engine=s.Solver(s.catalog([self.roof,bad]));r=engine.solve(c.preview_grid(4),self.roof)
  self.assertFalse(r['success']);self.assertIsNone(r['choices']);self.assertTrue(r['conflicts'])
 def test_current_module_never_replaced_when_rules_are_unsatisfiable(self):
  self.roof.rules={'down':[]};engine=s.Solver(s.catalog([self.roof,self.wall]))
  r=engine.solve(c.preview_grid(3),self.roof)
  self.assertFalse(r['success']);self.assertIsNone(r['choices'])
 def test_previous_valid_helper_layout_is_reused(self):
  g=c.preview_grid(5);r=self.solver.solve(g,self.roof)
  previous={node:self.vs[index].key for node,index in enumerate(r['choices'])}
  again=self.solver.solve(g,self.roof,previous=previous)
  self.assertEqual(r['choices'],again['choices']);self.assertEqual(again['observations'],0)
 def test_seed_reproduces_and_invalid_locked_pose_has_no_solution(self):
  g=c.preview_grid(3)
  a=self.solver.solve(g,self.roof,seed=22);b=self.solver.solve(g,self.roof,seed=22)
  self.assertEqual(a['choices'],b['choices'])
  m=module('one','roof',240,turns=(0,));r=s.Solver(s.catalog([m])).solve(g,m,locked_pose=(1,False))
  self.assertFalse(r['success'])
 def test_skewed_fans_share_consistent_bool_and_contours(self):
  for n in (3,4,5):
   g=c.preview_grid(n,skew=1);r=self.solver.solve(g,self.roof)
   self.assertTrue(r['success']);self.assertTrue(self.solver.validate(g,r['choices']))
 def test_explicit_search_budget_is_a_failure_not_partial_output(self):
  r=self.solver.solve(c.preview_grid(4),self.roof,budget=1)
  self.assertFalse(r['success']);self.assertTrue(r['budget']);self.assertIsNone(r['choices'])
 def test_core_support_beyond_crop_reports_insufficient_range(self):
  r=self.solver.solve(c.preview_grid(4,core_layer=0),self.roof)
  self.assertFalse(r['success']);self.assertIn('范围不足',r['error']);self.assertIsNone(r['choices'])

class DirectNeighborTests(unittest.TestCase):
 def test_self_adjacency_repeats_asset_without_second_hop(self):
  m=c.Module('wall','Defense.CityWall',255,(),(),turns=(0,),rules={f:[{'id':'Defense.CityWall','weight':1}] for f in c.FACE_NAMES})
  vs=s.catalog([m],False);engine=s.Solver(vs)
  for n in (3,4,5):
   g=c.preview_grid(n);center=min(g.core);r=engine.solve_neighbors(g,m)
   self.assertTrue(r['success'],r['error'])
   occupied={i for i,j in enumerate(r['choices']) if j>=0}
   self.assertEqual(occupied,{center}|{p[0] for p in g.neighbors[center] if p})
   self.assertTrue(all(vs[r['choices'][i]].module.uid==m.uid for i in occupied))
   self.assertEqual(r['propagations'],0)
 def test_self_adjacency_cannot_override_incompatible_bool(self):
  m=module('roof','building.roof',240,rules={'down':[{'id':'building.roof','weight':1}]})
  self.assertFalse(s.Solver(s.catalog([m])).solve_neighbors(c.preview_grid(4),m,locked_pose=(0,False))['success'])
 def test_only_one_center_and_active_face_neighbors_are_filled(self):
  roof=module('roof','roof',240)
  vs=s.catalog([roof]);engine=s.Solver(vs)
  for n in (3,4,5):
   grid=c.preview_grid(n);center=min(grid.core);r=engine.solve_neighbors(grid,roof)
   self.assertTrue(r['success'],r['error'])
   variant=vs[r['choices'][center]]
   expected={center}|{pair[0] for face,pair in enumerate(grid.neighbors[center]) if pair and any(variant.state&(1<<i) for i in c.FACES[face])}
   self.assertEqual({i for i,j in enumerate(r['choices']) if j>=0},expected)
   self.assertEqual(vs[r['choices'][center]].module.uid,roof.uid)
   self.assertEqual(r['propagations'],0)
 def test_helpers_other_faces_do_not_trigger_second_hop(self):
  roof=module('roof','roof',240,rules={'down':[{'id':'wall','weight':1}]})
  wall=module('wall','wall',31,turns=(0,),rules={'down':[],'front':[]})
  vs=s.catalog([roof,wall]);engine=s.Solver(vs);g=c.preview_grid(4)
  r=engine.solve_neighbors(g,roof,locked_pose=(0,False))
  self.assertTrue(r['success'],r['error'])
  bottom=g.neighbors[min(g.core)][1][0]
  self.assertEqual(vs[r['choices'][bottom]].module.uid,'wall')
  self.assertFalse(engine.solve(g,roof,locked_pose=(0,False))['success'])
 def test_direct_face_still_enforces_bool_and_reciprocal_rule(self):
  roof=module('roof','roof',240,rules={'down':[{'id':'wall','weight':1}]})
  for wall in (module('wall','wall',51,weight=10000),module('wall','wall',31,turns=(0,),rules={'up':[]})):
   r=s.Solver(s.catalog([roof,wall])).solve_neighbors(c.preview_grid(4),roof,locked_pose=(0,False))
   self.assertFalse(r['success']);self.assertIsNone(r['choices']);self.assertTrue(r['conflicts'])
 def test_direct_previous_layout_is_stable(self):
  roof=module('roof','roof',240);vs=s.catalog([roof]);engine=s.Solver(vs);g=c.preview_grid(5)
  r=engine.solve_neighbors(g,roof,seed=123)
  previous={n:vs[j].key for n,j in enumerate(r['choices']) if j>=0}
  again=engine.solve_neighbors(g,roof,previous=previous)
  self.assertEqual(r['choices'],again['choices']);self.assertEqual(again['observations'],0)
 def test_inactive_faces_skip_even_explicit_rules_and_old_helpers(self):
  for n in (3,4,5):
   g=c.preview_grid(n);center=min(g.core)
   # Only V5 is occupied: top, right and back must stay blank.
   m=module('corner','corner',16,rules={'up':[],'right':[{'id':'missing','weight':1}],'back':[]})
   vs=s.catalog([m]);engine=s.Solver(vs)
   previous={pair[0]:vs[0].key for pair in g.neighbors[center] if pair}
   r=engine.solve_neighbors(g,m,locked_pose=(0,False),previous=previous)
   self.assertTrue(r['success'],r['error'])
   for face,pair in enumerate(g.neighbors[center]):
    self.assertEqual(r['choices'][pair[0]]>=0,face in (1,2,5))
 def test_inactive_face_gate_uses_rotated_mirrored_bool(self):
  m=module('corner','corner',16,mirror=True);vs=s.catalog([m]);engine=s.Solver(vs)
  g=c.preview_grid(5);center=min(g.core)
  for turn in range(4):
   for mirror in (False,True):
    r=engine.solve_neighbors(g,m,locked_pose=(turn,mirror))
    self.assertTrue(r['success'],r['error']);state=c.transform_state(16,turn,mirror)
    for face,pair in enumerate(g.neighbors[center]):
     self.assertEqual(r['choices'][pair[0]]>=0,any(state&(1<<i) for i in c.FACES[face]))
 def test_all_inactive_has_no_helpers(self):
  m=module('blank','blank',0,rules={name:[] for name in c.FACE_NAMES})
  vs=s.catalog([m]);g=c.preview_grid(4);r=s.Solver(vs).solve_neighbors(g,m)
  self.assertTrue(r['success']);self.assertEqual(sum(j>=0 for j in r['choices']),1)

if __name__=='__main__':unittest.main()
