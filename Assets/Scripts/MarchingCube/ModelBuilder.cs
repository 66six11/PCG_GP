
using HexagonalGrids;
using UnityEngine;
using Utility;

namespace MarchingCube
{
    public class ModelBuilder
    {
        private ModelLibrary _modelLibrary;
        private Cell _cell;
        private MeshFilter _meshFilter;
        private Mesh _mesh;

        public ModelLibrary Library => _modelLibrary;
        public Cell Cell => _cell;
        public Mesh Mesh => _mesh;
        public MeshFilter MeshFilter => _meshFilter;

        public ModelBuilder()
        {
        }

        public ModelBuilder(ModelLibrary modelLibrary)
        {
            _modelLibrary = modelLibrary;
        }

        public ModelBuilder SetLibrary(ModelLibrary modelLibrary)
        {
            _modelLibrary = modelLibrary;
            return this;
        }

        public ModelBuilder SetCell(Cell cell)
        {
            _cell = cell;
            return this;
        }

        public ModelBuilder SetMeshFilter(MeshFilter meshFilter)
        {
            _meshFilter = meshFilter;
            return this;
        }

        public void Build()
        {
            if (_cell == null || _meshFilter == null)
            {
                Debug.LogError("Cell or MeshFilter is null");
                return;
            }

            var model = _modelLibrary.GetModel(_cell.GetCellByte());
            if (model == null)
            {
                Debug.LogError("Model not found in library");
                return;
            }

            var mesh = model.Value.mesh;
            if (mesh == null)
            {
                Debug.LogError("Mesh not found in model");
                return;
            }

            TransformMesh(model.Value, _cell, _meshFilter);
        }

        private void TransformMesh(ModelInfo model, Cell cell, MeshFilter meshFilter)
        {
            var rotation = model.rotation;
            _mesh = model.mesh.TransformMesh(cell.localV1, cell.localV2, cell.localV3, cell.localV4, cell.V1.y - cell.V5.y);
            meshFilter.transform.position = cell.Center;
            meshFilter.transform.rotation = cell.rotation * rotation;
            meshFilter.mesh = _mesh;
        }
    }
}