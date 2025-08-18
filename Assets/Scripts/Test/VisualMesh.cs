using System;
using System.Collections.Generic;
using MarchingCube;
using UnityEngine;
using UnityEngine.XR;
using Utility;

namespace HexagonalGrids.Test
{
    public class VisualMesh : MonoBehaviour
    {
        public ModelLibrary modelLibrary;
        public GameObject modelGo;
        public Mesh originalMesh;
        public Mesh finalMesh;

        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;

        public Vector3[] vertices = new Vector3[4];

        public bool[] IsEnbaled = new bool[8] { false, false, false, false, false, false, false, false };

        private List<SubEdge> subEdges = new List<SubEdge>();

        private Cell cell;


        public void Start()
        {
            meshFilter = modelGo.GetComponent<MeshFilter>();
            meshRenderer = modelGo.GetComponent<MeshRenderer>();
            meshFilter.mesh = originalMesh;
            FlipXMesh();
        }

        [ContextMenu("Generate Mesh")]
        public void GenerateMesh()
        {
            if (modelLibrary == null) return;
            Debug.Log("获取状态" + ModelHelper.Byte2State(cell.GetCellByte()));
            ModelInfo? model = modelLibrary.GetModel(cell.GetCellByte());
            if (model == null)
            {
                Debug.Log("No model found for cell " + cell.GetCellByte());
                return;
            }

            if (model.Value.mesh == null)
            {
                meshFilter.mesh = null;
                return;
            }

            originalMesh = model.Value.mesh;
            var rotation = model.Value.rotation;
            finalMesh = originalMesh.TransformMesh(cell.localV1, cell.localV2, cell.localV3, cell.localV4, 1);
            modelGo.transform.position = cell.Center;
            modelGo.transform.rotation = cell.rotation * rotation;
            meshFilter.mesh = finalMesh;
        }

        [ContextMenu("Flip X Mesh")]
        public void FlipXMesh()
        {
            finalMesh = MeshHelper.FlipXMesh(originalMesh);
            meshFilter.mesh = finalMesh;
        }

        [ContextMenu("变换网格")]
        public void TransformMesh()
        {
            finalMesh = originalMesh.TransformMesh(cell.localV5, cell.localV6, cell.localV7, cell.localV8, 1);
            modelGo.transform.position = cell.Center;
            modelGo.transform.rotation = cell.rotation;
            meshFilter.mesh = finalMesh;
        }

        private void OnValidate()
        {
            if (vertices == null || vertices.Length != 4) return;
            subEdges.Clear();
            Vertex v1 = new Vertex(vertices[0] + transform.position) { IsEnabled = IsEnbaled[4] };
            Vertex v2 = new Vertex(vertices[1] + transform.position) { IsEnabled = IsEnbaled[5] };
            Vertex v3 = new Vertex(vertices[2] + transform.position) { IsEnabled = IsEnbaled[6] };
            Vertex v4 = new Vertex(vertices[3] + transform.position) { IsEnabled = IsEnbaled[7] };

            var quadDown = new SubQuad(v1, v2, v3, v4, subEdges);

            var vertex1 = new Vertex(new Vector3(v1.position.x, v1.position.y + 1, v1.position.z)) { IsEnabled = IsEnbaled[0] };
            var vertex2 = new Vertex(new Vector3(v2.position.x, v2.position.y + 1, v2.position.z)) { IsEnabled = IsEnbaled[1] };
            var vertex3 = new Vertex(new Vector3(v3.position.x, v3.position.y + 1, v3.position.z)) { IsEnabled = IsEnbaled[2] };
            var vertex4 = new Vertex(new Vector3(v4.position.x, v4.position.y + 1, v4.position.z)) { IsEnabled = IsEnbaled[3] };

            var quadUp = new SubQuad(vertex1, vertex2, vertex3, vertex4, subEdges);
            cell = new Cell(quadUp, quadDown, subEdges);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.aquamarine;
            if (cell == null) return;
            foreach (var subEdge in subEdges)
            {
                var endpoints = new List<Vertex>(subEdge.endpoints);
                Gizmos.DrawLine(endpoints[0].position, endpoints[1].position);
            }

            foreach (var vertex in cell.vertices)
            {
                if (vertex.IsEnabled)
                {
                    Gizmos.color = Color.green;
                }
                else
                {
                    Gizmos.color = Color.red;
                }

                Gizmos.DrawSphere(vertex.position, 0.1f);
            }

            // Gizmos.color = Color.blue;
            // Gizmos.DrawSphere(cell.V1.position, 0.1f);
            // Gizmos.DrawSphere(cell.V2.position, 0.1f);
            // //
            // Gizmos.color = Color.green;
            // Gizmos.DrawLine(cell.localV1, cell.localV2);
            // Gizmos.DrawLine(cell.localV2, cell.localV3);
            // Gizmos.DrawLine(cell.localV3, cell.localV4);
            // Gizmos.DrawLine(cell.localV4, cell.localV1);
            //
            // Gizmos.DrawLine(cell.localV5, cell.localV6);
            // Gizmos.DrawLine(cell.localV6, cell.localV7);
            // Gizmos.DrawLine(cell.localV7, cell.localV8);
            // Gizmos.DrawLine(cell.localV8, cell.localV5);
            //
            // Gizmos.DrawLine(cell.localV1, cell.localV5);
            // Gizmos.DrawLine(cell.localV2, cell.localV6);
            // Gizmos.DrawLine(cell.localV3, cell.localV7);
            // Gizmos.DrawLine(cell.localV4, cell.localV8);
            //
        }
    }
}