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


        [ContextMenu("细分网格")]
        public void SubdivideGrid()
        {
            if (_hexGrid == null) return;
            _hexGrid.SubdivideGrid();
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
            for (int i = 0; i < _hexGrid.midVertices.Count; i++)
            {
                Gizmos.color = Color.green;
                var midVertex = _hexGrid.midVertices[i];
                // Gizmos.DrawSphere(center, 0.1f);\
                Gizmos.DrawSphere(midVertex.position, 0.1f);
            }

            for (int i = 0; i < _hexGrid.edges.Count; i++)
            {
                Gizmos.color = Color.yellow;
                var edge = _hexGrid.edges[i];
                var endpointsAsList = new List<HexVertex>(edge.endpoints);

                Gizmos.DrawLine(endpointsAsList[0].position, endpointsAsList[1].position);
            }

            foreach (var center in _hexGrid.centerVertices)
            {
                Gizmos.color = Color.aquamarine;
                Gizmos.DrawSphere(center.position, 0.1f);
            }

            if (_hexGrid.subQuads.Count > 0)
            {
                foreach (var subQuad in _hexGrid.subQuads)
                {
                    Gizmos.color = Color.coral;

                    Gizmos.DrawLine(subQuad.a.position, subQuad.b.position);
                    Gizmos.DrawLine(subQuad.b.position, subQuad.c.position);
                    Gizmos.DrawLine(subQuad.c.position, subQuad.d.position);
                    Gizmos.DrawLine(subQuad.d.position, subQuad.a.position);
                    
                    Gizmos.color = Color.cornsilk;
                    Gizmos.DrawSphere(subQuad.center, 0.05f);
                }
            }
            Gizmos.color = Color.blue;
            foreach (var vertex in _hexGrid.vertices)
            {
                Gizmos.DrawSphere(vertex.position, 0.1f);
            }
        }
    }
}