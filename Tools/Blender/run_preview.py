"""Blender Scripting > Open this file > Run Script. No preference changes."""
from pathlib import Path
import importlib.util
import sys

folder=str(Path(__file__).resolve().parent)
if folder not in sys.path:sys.path.insert(0,folder)
old=sys.modules.get('pcg_grid_preview')
if old and getattr(old,'_REGISTERED',False):old.unregister()
# Resolve this workspace explicitly, including when an older installed ZIP has
# already populated sys.modules with a different package path.
for name in list(sys.modules):
    if name=='pcg_grid_preview' or name.startswith('pcg_grid_preview.'):del sys.modules[name]
package=Path(folder)/'pcg_grid_preview'
spec=importlib.util.spec_from_file_location('pcg_grid_preview',package/'__init__.py',submodule_search_locations=[str(package)])
module=importlib.util.module_from_spec(spec);sys.modules[spec.name]=module;spec.loader.exec_module(module)
if old and hasattr(old,'__addon_enabled__'):module.__addon_enabled__=old.__addon_enabled__
module.register()
print('PCG DCC 2.1.4 ready: X mirror + native Unity / FBX+JSON; 3D View > N > PCG DCC')
