using System;
using UnityEngine;

namespace HexagonalGrids
{
    public class GirdGenerator : MonoBehaviour
    {
        [SerializeField] private int radius;
        [SerializeField] private float cellSize;
        private Grid _grid;

        private void Awake()
        {
            Generate();
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            _grid = new Grid(radius, cellSize, this.transform.position);
        }

        [ContextMenu("Debug")]
        public void Debug()
        {
            if (_grid == null) return;
            UnityEngine.Debug.Log(_grid.vertices.Count);
            foreach (var vertex in _grid.vertices)
            {
                UnityEngine.Debug.Log(vertex.coord.ToString());
            }
        }
        private void OnDrawGizmos()
        {
            if (_grid == null) return;
            foreach (var vertex in _grid.vertices)
            {
                Gizmos.DrawSphere(vertex.coord.worldPosition, 0.1f);
            }
        }
    }
}