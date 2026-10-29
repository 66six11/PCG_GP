"""Actual Blender v2 acceptance tests and independent demo output."""
from pathlib import Path
import runpy
import sys
import json
import bpy
import bmesh
from mathutils import Vector,Matrix

ROOT=Path(__file__).resolve().parents[1];PROJECT=ROOT.parents[1]
runpy.run_path(str(ROOT/'run_preview.py'))
import pcg_grid_preview as addon
from pcg_grid_preview import authoring as a,core,preview,assets,transport
addon.register();scene=bpy.context.scene;cfg=scene.pcg_dcc
for obj in list(scene.objects):bpy.data.objects.remove(obj,do_unlink=True)

assert bpy.ops.pcg_dcc.create(module_id='building.roof',corners=tuple(bool(240&(1<<i)) for i in range(8)))=={'FINISHED'}
roof=cfg.active;roof_uid=roof.pcg_dcc_module.uid
assert not roof.pcg_dcc_module.mirror
assert bpy.ops.pcg_dcc.create(module_id='building.wall',corners=tuple(bool(31&(1<<i)) for i in range(8)))=={'FINISHED'}
wall=cfg.active
cfg.active=roof;bpy.context.view_layer.objects.active=roof
assert bpy.ops.pcg_dcc.rule(action='ADD',target='building.wall',face='down',weight=1)=={'FINISHED'}
assert any(r.automatic and r.target=='building.roof' and r.face=='up' for r in wall.pcg_dcc_module.rules)
assert bpy.ops.pcg_dcc.preview(action='BUILD')=={'FINISHED'}
cache=a._CACHE[scene.as_pointer()]
assert all(result['success'] for g,result,offset in cache['grids'].values()),cfg.status
cores=[o for o in scene.objects if o.get(a.ROLE)=='core']
assert len(cores)==3 and all(o.data==roof.data and not o.hide_select for o in cores)
for g,result,offset in cache['grids'].values():
 center=min(g.core);variant=cache['variants'][result['choices'][center]]
 expected={center}|{pair[0] for face,pair in enumerate(g.neighbors[center]) if pair and any(variant.state&(1<<i) for i in core.FACES[face])}
 assert len(g.core)==1 and {i for i,j in enumerate(result['choices']) if j>=0}==expected
 assert result['propagations']==0
assert all(o.hide_select for o in scene.objects if o.get(a.ROLE)=='helper')

# Every editable native lattice agrees with canonical deformation.
deps=bpy.context.evaluated_depsgraph_get();bpy.context.view_layer.update()
for obj in cores:
 n=obj['dcc_valence'];node=obj['dcc_node'];g,result,offset=cache['grids'][n]
 evaluated=obj.evaluated_get(deps);mesh=evaluated.to_mesh()
 for source_vertex,out in zip(roof.data.vertices,mesh.vertices):
  expected=Vector(core.add(core.warp(source_vertex.co,g.quad(node),g.layer(node),cfg.height,obj['dcc_turns'],obj['dcc_mirror']),offset))
  assert (obj.matrix_world@out.co-expected).length<1e-5,(obj.name,expected,obj.matrix_world@out.co)
 evaluated.to_mesh_clear()
print('PASS native editable 3/4/5 previews match actual cell deformation',flush=True)
cfg.lock_pose=False

# Edit the preview directly. Interior poking leaves shared contours unchanged.
assert bpy.ops.pcg_dcc.edit(valence=4)=={'FINISHED'}
edit=bpy.context.object;assert edit.data==roof.data and edit.mode=='EDIT'
bm=bmesh.from_edit_mesh(edit.data);bmesh.ops.poke(bm,faces=list(bm.faces),offset=.07);bmesh.update_edit_mesh(edit.data)
preview.refresh(scene,True)
assert bpy.context.object==edit and edit.mode=='EDIT' and roof.data.is_editmode
assert all(r['success'] for g,r,o in a._CACHE[scene.as_pointer()]['grids'].values())
bpy.ops.object.mode_set(mode='OBJECT')
assert len(roof.data.vertices)>4
print('PASS preview edits share source topology and keep valid helpers stable',flush=True)

# Derived child copies geometry independently; conflict under same ID is allowed.
cfg.active=roof;bpy.context.view_layer.objects.active=roof
assert bpy.ops.pcg_dcc.create(module_id='building.roof.child',corners=tuple(roof.pcg_dcc_module.corners),reuse=True)=={'FINISHED'}
child=cfg.active;assert child.data!=roof.data and len(child.data.vertices)==len(roof.data.vertices)
assert bpy.ops.pcg_dcc.create(module_id='building.roof',corners=tuple(bool(15&(1<<i)) for i in range(8)))=={'FINISHED'}
conflict=cfg.active
assert any(i['kind']=='WARNING' and i['uid']==conflict.pcg_dcc_module.uid for i in a.validate(scene))
# Remove only the temporary conflict/child modules through the actual commands.
bpy.ops.pcg_dcc.delete();cfg.active=child;bpy.context.view_layer.objects.active=child;bpy.ops.pcg_dcc.delete()
print('PASS hierarchy reuse, unique keys, allowed Bool conflicts and registry warnings',flush=True)

# Register an existing transformed mesh inside its own reference cage.
bpy.ops.mesh.primitive_cube_add(size=1,location=(4,5,6));external=bpy.context.object
external.scale=(2,3,4);external.rotation_euler.z=.3
world=external.matrix_world.copy();bpy.context.view_layer.update();world=external.matrix_world.copy()
assert bpy.ops.pcg_dcc.register_begin()=={'FINISHED'}
assert cfg.pending_target==external and cfg.pending_frame is not None
assert bpy.ops.pcg_dcc.register_finish(module_id='props.registered',corners=(False,)*8)=={'FINISHED'}
assert external.pcg_dcc_module.enabled and any(o.get('dcc_registration_cage') for o in external.children)
assert all(max(abs(x) for x in v.co)<=.50001 for v in external.data.vertices)
assert (external.matrix_world.translation-world.translation).length<1e-5
bpy.ops.pcg_dcc.delete()
print('PASS transformed model registration, canonical coordinates and bound guide',flush=True)

# Bool changes rebuild in place without changing identity; constraints are rechecked.
cfg.active=roof;bpy.context.view_layer.objects.active=roof
roof.pcg_dcc_module.corners=tuple(bool(240&(1<<i)) for i in range(8));old_uid=roof.pcg_dcc_module.uid
assert bpy.ops.pcg_dcc.rebuild(reuse=False)=={'FINISHED'}
assert roof.pcg_dcc_module.uid==old_uid
# No solution keeps the current source in all core positions, with no helpers/fallback.
roof.pcg_dcc_module.explicit[1]=True
for i in range(len(roof.pcg_dcc_module.rules)-1,-1,-1):
 if roof.pcg_dcc_module.rules[i].face=='down':roof.pcg_dcc_module.rules.remove(i)
a.sync_reverse(scene);preview.refresh(scene,True)
assert all(not r['success'] for g,r,o in a._CACHE[scene.as_pointer()]['grids'].values())
assert len([o for o in scene.objects if o.get(a.ROLE)=='core'])==3
assert not any(o.get(a.ROLE)=='helper' for o in scene.objects)
bpy.ops.pcg_dcc.rule(action='ADD',target='building.wall',face='down',weight=1)
print('PASS rebuild identity and no-solution diagnostics without replacing the target',flush=True)

# Repeated direct-neighbor refresh must never mutate the shared source mesh.
before=tuple(tuple(v.co) for v in roof.data.vertices)
preview.refresh(scene,True);preview.refresh(scene,True)
assert tuple(tuple(v.co) for v in roof.data.vertices)==before

# Explicit rename updates the group and all incoming references.
assert bpy.ops.pcg_dcc.rename(new_id='building.roofs')=={'FINISHED'}
assert roof.pcg_dcc_module.module_id=='building.roofs'
assert any(r.target=='building.roofs' for r in wall.pcg_dcc_module.rules)
assert not any(i['kind']=='INVALID' for i in a.validate(scene))

out=PROJECT/'Assets/Art/Buildings/DCC/DCCModules.fbx'
data=assets.export(scene,out)
assert data['schemaVersion']==1 and len(data['modules'])>=55
assert len({m['id'] for m in data['modules']})==len(data['modules'])
assert all(not m['id'].startswith('$') for m in data['modules'])
print('PASS canonical source-only FBX/JSON export with hierarchical ID resolution',flush=True)

# Compact library layout, selected model and preview display saved independently.
assert bpy.ops.pcg_dcc.edit(valence=4)=={'FINISHED'}
assert bpy.context.object.mode=='EDIT' and bpy.context.object.get('dcc_valence')==4
bpy.ops.object.mode_set(mode='OBJECT')
for obj in bpy.context.selected_objects:obj.select_set(False)
roof.select_set(True);bpy.context.view_layer.objects.active=roof
roof.location=(0,6,1);wall.location=(2,6,1);cfg.active=roof;cfg.previews=True;preview.refresh(scene,True)
for area in bpy.context.screen.areas:
 if area.type=='VIEW_3D':
  area.spaces.active.show_region_ui=True;area.spaces.active.region_3d.view_location=Vector((0,1,1));area.spaces.active.region_3d.view_distance=24
path=ROOT/'PCG_DCC_Demo.blend';bpy.ops.wm.save_as_mainfile(filepath=str(path))
ids={o.pcg_dcc_module.uid for o in a.modules(scene)}
bpy.ops.wm.open_mainfile(filepath=str(path));scene=bpy.context.scene
assert {o.pcg_dcc_module.uid for o in a.modules(scene)}==ids
transfer=[o for o in scene.objects if o.get('pcg_kind')=='module']
assert len(transfer)==55 and {o['pcg_uid'] for o in transfer}=={m['id'] for m in data['modules']}
roots=[o for o in scene.objects if o.get('pcg_kind')=='manifest']
assert len(roots)==1 and not roots[0].get('pcg_transport_error')
assert json.loads(roots[0]['pcg_manifest_json'])['modules']==data['modules']
names={o['pcg_uid']:(o.name,o.data.name) for o in transfer}
transport.prepare(scene);transport.prepare(scene)
assert {o['pcg_uid']:(o.name,o.data.name) for o in scene.objects if o.get('pcg_kind')=='module'}==names
# Export after a native snapshot exists: canonical delivery names must not collide.
again=assets.export(scene,out);assert again['modules']==data['modules']
preview.refresh(scene,True);assert all(r['success'] for g,r,o in a._CACHE[scene.as_pointer()]['grids'].values())
addon.unregister();addon.register();preview.refresh(scene,True)
assert all(r['success'] for g,r,o in a._CACHE[scene.as_pointer()]['grids'].values())
print('DCC_BLENDER_ACCEPTANCE_OK: three single-cell direct-neighbor previews, shared-mesh editing, registry, registration, save/reload/export',flush=True)
