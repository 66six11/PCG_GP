using System.Collections.Generic;
using HexagonalGrids;
using UnityEngine;

namespace MarchingCube
{
    public class Model
    {
        //TODO: 材质未绑定
        private MeshFilter _meshFilter;

        public Model(ModelLibrary modelLibrary, Cell cell, MeshFilter meshFilter)
        {
            Library = modelLibrary;
            Cell = cell;
            _meshFilter = meshFilter;
        }

        public Model()
        {
        }
        
        public ModelLibrary Library { get; set; }

        public Cell Cell { get; set; }

        private MeshDeformation deformation;
        public MeshDeformation Deformation
        {
            get
            {
                if (deformation == null || deformation.gameObject != _meshFilter.gameObject)
                {
                    deformation = _meshFilter.GetComponent<MeshDeformation>();
                    if (deformation == null) deformation = _meshFilter.gameObject.AddComponent<MeshDeformation>();
                }
                return deformation;
            }
        }
        public Mesh Mesh => deformation == null ? null : deformation.Mesh;

        public MeshFilter MeshFilter
        {
            get => _meshFilter;
            set => _meshFilter = value;
        }
        public MeshRenderer MeshRenderer { get; set; }

        public GameObject gameObject => _meshFilter == null ? null : _meshFilter.gameObject;
    }

    public class ModelBuilder
    {
        private readonly Model _model;


        public ModelBuilder(ModelLibrary modelLibrary, Cell cell, MeshFilter meshFilter)
        {
            _model = new Model(modelLibrary, cell, meshFilter);
        }

        public ModelBuilder(Model model)
        {
            _model = model;
        }

        public ModelBuilder SetLibrary(ModelLibrary modelLibrary)
        {
            _model.Library = modelLibrary;
            return this;
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
            if (_model.Cell == null || _model.MeshFilter == null || _model.Library == null)
            {
                Debug.LogError("Cell, MeshFilter or ModelLibrary is null");
                return;
            }

            byte state = _model.Cell.GetCellByte();
            if (state == 0 || state == 255)
            {
                _model.Deformation.Hide();
                return;
            }
            var model = _model.Library.GetModel(state);
            if (model == null)
            {
                _model.Deformation.Hide();
                Debug.LogError($"Model state {state} not found in library");
                return;
            }

            var mesh = model.Value.mesh;
            if (mesh == null)
            {
                _model.Deformation.Hide();
                Debug.LogError($"Mesh not found for model state {state}");
                return;
            }

            Build(model.Value);
        }

        /// <summary>Build a WFC-selected source variant without changing the Cell's occupancy.</summary>
        public void Build(ModelInfo selected)
        {
            if (_model.Cell == null || _model.MeshFilter == null || _model.Library == null)
                throw new System.InvalidOperationException("Cell, MeshFilter and ModelLibrary are required.");
            byte state = _model.Cell.GetCellByte();
            if (state == 0 || state == 255) { _model.Deformation.Hide(); return; }
            if (selected.stateCode != state || selected.mesh == null)
                throw new System.ArgumentException("The selected module must match the occupied Cell state.");
            TransformMesh(selected, _model.Cell, _model.MeshFilter);
        }

        private void TransformMesh(ModelInfo model, Cell cell, MeshFilter meshFilter)
        {
            cell.BuildTransformMatrix();
            _model.Deformation.Apply(_model.Library.GetTemplate(model), cell.localV1, cell.localV2,
                cell.localV3, cell.localV4, cell.V1.y - cell.V5.y);
            meshFilter.transform.position = cell.Center;
            // Template orientation is applied before deformation, in the template's sample cache.
            meshFilter.transform.rotation = cell.rotation;
        }
    }
}
