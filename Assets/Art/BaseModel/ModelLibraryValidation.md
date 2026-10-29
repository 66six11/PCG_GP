# PCG model library validation

The corrected library has been rebound from `PCG.fbx`:

- 54 source mesh subassets.
- 256 unique states; the 254 nonempty states have persistent FBX references.
- 60 states use reflection metadata instead of generated source meshes.
- States 0 and 255 intentionally have no surface Mesh.
- All state/source transforms pass validation.
- All shared-face contours pass: **0 conflicts** (previously 15).
- Read/Write remains enabled for runtime CPU deformation.

## Source correction

The object names `base_00001110` and `base_00001111` had been assigned to each
other's geometry. Their object names were exchanged in both `PCG.fbx` and
`PCG.blend`, preserving the existing geometry and object transforms:

- `base_00001110` (state 112) now identifies the partial bottom surface.
- `base_00001111` (state 240) now identifies the complete horizontal surface.

Only those two object-name strings were changed in each source file. The
files retain their lengths, datablock/node structure, geometry, and asset GUIDs.
The Blender object-to-mesh associations were checked against its DNA schema;
Unity successfully reimported the corrected FBX before the library was rebound.

The correction resolves all previously reported face discrepancies in states
112, 176, 208, 224, and 240. Shared-face validation checks geometry contours at
1e-4 template-unit precision; it does not certify shading continuity or
self-intersections inside a cell.

Use **Tools > Marching Cube > Batch Mesh Importer** to scan/bind/validate again,
or **Tools > Marching Cube > Rebind PCG Library** to repeat the default binding.
`ActualPcgModelsAgreeOnEverySharedFaceContour` provides an automated regression
check against the real source model.

```text
sources=54, states=256, mirrored=60, errors=0, seamConflicts=0
```
