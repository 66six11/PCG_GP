# Procedural model deformation

`ModelBuilder` resolves the cell state through `ModelLibrary`, then updates the
`MeshDeformation` component on the target `MeshFilter` object.

- The library borrows source meshes. It owns shared, persistent interpolation
  samples keyed by source mesh and template rotation. Inspector changes, explicit
  invalidation, and library disable dispose those samples.
- `MeshDeformation` owns its output mesh and persistent vertex buffer. Rebuilding
  with the same source and cache revision reuses both. A different source or an
  explicitly invalidated library cache replaces the output;
  destruction, pool clearing, and editor assembly reload release owned resources.
- Pool release hides the output and clears cell/library bindings while retaining
  reusable geometry. A null pool prefab creates a plain model object, never a
  clone of the pool itself.
- Empty cell states (0 and 255) have no displayed surface. Missing nonempty states
  clear the previous display and report an error.

Source meshes must be readable and use the existing normalized template domain
`[-0.5, 0.5]` in X/Z. Template rotation is applied before interpolation into the
cell; only the cell rotation is assigned to the object. Submeshes, index format,
colors, and UV channel dimensions are preserved. Normals are recalculated after
warping; tangents are recalculated when UV0 is available.

When changing source mesh contents or replacing entries of the authoring lists in
code, call `ModelLibrary.InvalidateRuntimeCache()`. List replacement and count
changes also invalidate the state lookup automatically. For the standalone
`MeshDeformation.Apply` API, call `ReleaseResources()` before reusing a modified
source mesh. Source meshes are otherwise treated as immutable.

The compatibility API `MeshHelper.TransformMesh` returns a new mesh owned by its
caller. Repeated updates should use `ModelBuilder` or `MeshDeformation.Apply`.
The current entry points complete synchronously with Burst jobs via `Run`; a
future batch API can schedule multiple models without an immediate per-model wait.

Regression coverage lives in `../Tests/Editor/MeshDeformationTests.cs` and includes
mesh reuse and destruction, irregular-cell rotation, empty states, cache
invalidation, submesh/UV preservation, reflection winding/tangent handedness, and
pool reuse. Collider and navigation data are outside this component's ownership.

## State derivation and editor binding

Source names end in eight binary digits, ordered V1 through V8 (the leftmost
character is bit 0). The PCG file contains 54 such mesh subassets. Bind the mesh
subassets directly; the FBX scene nodes contain layout offsets/scales and are not
the normalized deformation templates. Material submeshes are never split into
separate state models.

`CubeTransform` defines the corner convention once for state and geometry
transforms. A derived model applies local X reflection first, then 0/90/180/270
degrees around Y. `ModelInfo.mirrorX` stores reflection metadata; source Mesh
references remain persistent FBX subassets. Runtime output reverses triangle
winding when mirrored. Derivation never creates transient source meshes.

`ModelStateTable.Build` validates all sources before committing. Explicit source
states win over rotated variants, which win over mirrored variants. Candidates
are processed in state order. Duplicate source states, malformed names, or
incomplete coverage reject the update and preserve the previous library.

Use **Tools > Marching Cube > Batch Mesh Importer** to scan, bind/save, validate
state references and transforms, or check shared-face contours. The window starts
with `Assets/Art/BaseModel/PCG.fbx` and the adjacent `ModelLibrary.asset`.
**Tools > Marching Cube > Rebind PCG Library** repeats the default binding directly.
Binding enables Read/Write on the source model when required by CPU deformation.

Shared-face validation compares all six face orientations for each state, grouped
by their four shared corner bits. It snaps coordinates to 1e-4 template units and
merges collinear edge subdivisions; it detects contour disagreements, not shading
seams or intersections inside a cell. Validation results and source corrections are
recorded beside the model library in `ModelLibraryValidation.md`.
