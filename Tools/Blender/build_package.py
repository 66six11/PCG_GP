"""Build the v2 add-on, including canonical Marching Cubes authoring templates."""
from pathlib import Path
from zipfile import ZipFile,ZIP_DEFLATED

root=Path(__file__).resolve().parent
with ZipFile(root/'pcg_grid_preview.zip','w',ZIP_DEFLATED) as bundle:
    for path in sorted(p for p in (root/'pcg_grid_preview').iterdir() if p.suffix in ('.py','.json')):
        bundle.write(path,'pcg_grid_preview/'+path.name)
    bundle.write(root/'README.md','pcg_grid_preview/README.md')
    bundle.write(root/'BUILDING_MODULES.md','pcg_grid_preview/BUILDING_MODULES.md')
with ZipFile(root/'pcg_grid_preview.zip') as bundle:
    assert bundle.testzip() is None
    print('Package ready:',', '.join(bundle.namelist()))
