using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.VirtualTexturing;

namespace HexagonalGrids
{
    public class GirdGenerator : MonoBehaviour
    {
        [SerializeField] private int radius;
        [SerializeField] private float cellSize;
        [SerializeField] private int relaxTimes;
        [SerializeField] private bool debug;
        public HexGrid _hexGrid;

        private void Awake()
        {
            Generate();
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            GenerateHexGrid();
            RandomMergeTriangles();
            SubdivideGrid();
            Relax();
            BuildKdTree();
        }

        [ContextMenu("生成六边形网格")]
        public void GenerateHexGrid()
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

        [ContextMenu("松弛网格")]
        public void Relax()
        {
            if (_hexGrid == null) return;

            _hexGrid.RelaxGrid(relaxTimes);
        }

        [ContextMenu("构建KD树")]
        public void BuildKdTree()
        {
            if (_hexGrid == null) return;
            if (_hexGrid.subVertices.Count <= 0) return;
            _hexGrid.BuildKdTree();
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

            if (_hexGrid.subQuads.Count <= 0)
            {
                for (int i = 0; i < _hexGrid.edges.Count; i++)
                {
                    Gizmos.color = Color.yellow;
                    var edge = _hexGrid.edges[i];
                    var endpointsAsList = new List<HexVertex>(edge.endpoints);

                    Gizmos.DrawLine(endpointsAsList[0].position, endpointsAsList[1].position);
                }
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
                    Gizmos.color = Color.cornsilk;
                    Gizmos.DrawSphere(subQuad.center, 0.05f);

                    // for (int i = 0; i < subQuad.vertices.Length; i++)
                    // {
                    //     Vector3 p = Vector3.Lerp(subQuad.vertices[i].position, subQuad.center, 0.2f);
                    //     p = new Vector3(p.x, p.y + 0.2f, p.z);
                    //     Handles.Label(p, $"{i}");
                    // }
                }

                foreach (var subQuad in _hexGrid.subEdges)
                {
                    Gizmos.color = Color.chocolate;
                    var endpointsAsList = new List<Vertex>(subQuad.endpoints);
                    Gizmos.DrawLine(endpointsAsList[0].position, endpointsAsList[1].position);
                }
            }

            if (_hexGrid.boundaryEdges.Count > 0)
            {
                Gizmos.color = Color.red;
                foreach (var edge in _hexGrid.boundaryEdges)
                {
                    var endpointsAsList = new List<Vertex>(edge.endpoints);
                    Gizmos.DrawLine(endpointsAsList[0].position, endpointsAsList[1].position);
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