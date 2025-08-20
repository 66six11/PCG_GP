using System.Collections;
using System.Collections.Generic;
using HexagonalGrids;
using UnityEngine;
using Utility;

namespace MarchingCube
{
    public class Model
    {
        private ModelLibrary _modelLibrary;
        private Cell _cell;
        private Mesh _mesh;
        private MeshFilter _meshFilter;

        public Model(ModelLibrary modelLibrary, Cell cell, MeshFilter meshFilter)
        {
            _modelLibrary = modelLibrary;
            _cell = cell;
            _meshFilter = meshFilter;
        }

        public ModelLibrary Library
        {
            get => _modelLibrary;
            set => _modelLibrary = value;
        }

        public Cell Cell
        {
            get => _cell;
            set => _cell = value;
        }

        public Mesh Mesh
        {
            get => _mesh;
            set => _mesh = value;
        }

        public MeshFilter MeshFilter
        {
            get => _meshFilter;
            set => _meshFilter = value;
        }
    }

    public class ModelBuilder
    {
        private readonly Model _model;


        public ModelBuilder(ModelLibrary modelLibrary, Cell cell, MeshFilter meshFilter)
        {
            _model = new Model(modelLibrary, cell, meshFilter);
        }


        public ModelBuilder SetCell(Cell cell)
        {
            _model.Cell = cell;
            return this;
        }

        public ModelBuilder SetMeshFilter(MeshFilter meshFilter)
        {
            _model.MeshFilter = meshFilter;
            return this;
        }

        public ModelBuilder RegisterModel(IDictionary<Cell, Model> models)
        {
            models.Add(_model.Cell, _model);
            return this;
        }

        public void Build()
        {
            if (_model.Cell == null || _model.MeshFilter == null)
            {
                Debug.LogError("Cell or MeshFilter is null");
                return;
            }

            var model = _model.Library.GetModel(_model.Cell.GetCellByte());
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

            TransformMesh(model.Value, _model.Cell, _model.MeshFilter);
        }

        private void TransformMesh(ModelInfo model, Cell cell, MeshFilter meshFilter)
        {
            var rotation = model.rotation;
            _model.Mesh = model.mesh.TransformMesh(cell.localV1, cell.localV2, cell.localV3, cell.localV4, cell.V1.y - cell.V5.y);
            meshFilter.transform.position = cell.Center;
            meshFilter.transform.rotation = cell.rotation * rotation;
            meshFilter.mesh = _model.Mesh;
        }
    }
}