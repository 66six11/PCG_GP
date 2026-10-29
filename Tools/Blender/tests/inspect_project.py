"""Read a DCC .blend and report persisted modules/embedded JSON, without saving it."""
from pathlib import Path
import json
import runpy
import bpy

ROOT=Path(__file__).resolve().parents[1]
runpy.run_path(str(ROOT/'run_preview.py'))
from pcg_grid_preview import authoring as a

report={
 'project':bpy.data.filepath,
 'embeddedJsonTexts':[text.name for text in bpy.data.texts if text.name.lower().endswith('.json')],
 'modules':[],
 'generatedPreviewObjects':sum(bool(obj.get(a.OWNER)) for obj in bpy.context.scene.objects),
}
for obj in a.modules(bpy.context.scene):
 m=obj.pcg_dcc_module
 report['modules'].append(dict(uid=m.uid,id=m.module_id,corners=[int(v) for v in m.corners],weight=m.weight,
  allowMirrorX=bool(m.mirror),
  rules=[dict(face=r.face,id=r.target,weight=r.weight,automatic=r.automatic) for r in m.rules]))
exported=json.loads((ROOT.parents[1]/'Assets/Art/Buildings/DCC/DCCModules.tileset.json').read_text(encoding='utf-8'))
by_uid={m['id']:m for m in exported['modules']}
for m in report['modules']:
 entry=by_uid[m['uid']]
 assert (entry['logicalId'],entry['corners'],entry['weight'])==(m['id'],m['corners'],m['weight'])
 assert entry['allowMirrorX']==m['allowMirrorX']
 for rule in m['rules']:
  targets={r['id']:r['weight'] for r in entry['neighbors'][rule['face']]}
  eligible=[v for v in by_uid.values() if v['logicalId']==rule['id'] or v['logicalId'].startswith(rule['id']+'.')]
  assert any(targets.get(v['id'])==rule['weight'] for v in eligible),(m['id'],rule)
report['exportedJsonMatchesPersistedModules']=True
report['exportedModuleCount']=len(exported['modules'])
path=ROOT.parents[1]/'Logs/DCCRebuild/blender-project-data-report.json'
path.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('DCC_PROJECT_INSPECT_OK:',len(report['modules']),'registered modules,',len(report['embeddedJsonTexts']),'embedded JSON texts; source not saved')
