using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HexagonalGrids
{
    public class GirdGenerator : MonoBehaviour
    {
        [SerializeField] private int radius;
        [SerializeField] private float cellSize;
        [SerializeField] private bool debug;
        private HexGrid _hexGrid;

        private void Awake()
        {
            Generate();
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            _hexGrid = new HexGrid(radius, cellSize, this.transform.position);
        }

        [ContextMenu("随机合并三角")]
        public void RandomMergeTriangles()
        {
            if (_hexGrid == null) return;
            _hexGrid.RandomMergeTriangles();
        }

        [ContextMenu("Debug")]
        public void Debug()
        {
            if (_hexGrid == null) return;
            UnityEngine.Debug.Log("Veritex Count: " + _hexGrid.vertices.Count);
            
            if (_hexGrid.triangles.Count > 0)
            {
                UnityEngine.Debug.Log("Triangle Count: " + _hexGrid.triangles.Count);
            }
           
            UnityEngine.Debug.Log("Edge Count: " + _hexGrid.edges.Count);
            string s = "";
            foreach (var vertex in _hexGrid.vertices)
            {
                s += vertex.coord.ToString() + " " + vertex.position.ToString() + "\n";
            }

            UnityEngine.Debug.Log(s);
        }

        private void OnDrawGizmos()
        {
            if (_hexGrid == null) return;

            if (!debug) return;
            for (int i = 0; i < _hexGrid.triangles.Count; i++)
            {
                var triangle = _hexGrid.triangles[i];
                var center = triangle.a.position + triangle.b.position + triangle.c.position;
                center /= 3;
                // Gizmos.DrawSphere(center, 0.1f);
                Handles.Label(center, $"{i}");
            }

            for (int i = 0; i < _hexGrid.edges.Count; i++)
            {
                Gizmos.color = Color.yellow;
                var edge = _hexGrid.edges[i];
                var endpointsAsList = new List<HexVertex>(edge.endpoints);

                Gizmos.DrawLine(endpointsAsList[0].position, endpointsAsList[1].position);
            }

            Gizmos.color = Color.blue;
            foreach (var vertex in _hexGrid.vertices)
            {
                Gizmos.DrawSphere(vertex.position, 0.1f);
            }
        }
    }
}