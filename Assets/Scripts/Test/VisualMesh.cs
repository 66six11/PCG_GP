using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace HexagonalGrids.Test
{
    public class VisualMesh : MonoBehaviour
    {
        public Mesh originalMesh;
        public Mesh finalMesh;

        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;

        public Vertex[] vertices = new Vertex[4];

        private List<SubEdge> subEdges = new List<SubEdge>();

        private Cell cell;


        public void Start()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshFilter.mesh = originalMesh;
            FlipXMesh();
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
            gameObject.transform.position = cell.Center;
            gameObject.transform.rotation = cell.rotation;
            meshFilter.mesh = finalMesh;
        }

        private void OnValidate()
        {
            if (vertices == null || vertices.Length != 4) return;
            subEdges.Clear();

            var quadDown = new SubQuad(vertices[0], vertices[1], vertices[2], vertices[3], subEdges);
            var vertex1 = new Vertex(new Vector3(vertices[0].position.x, vertices[0].position.y + 1, vertices[0].position.z));
            var vertex2 = new Vertex(new Vector3(vertices[1].position.x, vertices[1].position.y + 1, vertices[1].position.z));
            var vertex3 = new Vertex(new Vector3(vertices[2].position.x, vertices[2].position.y + 1, vertices[2].position.z));
            var vertex4 = new Vertex(new Vector3(vertices[3].position.x, vertices[3].position.y + 1, vertices[3].position.z));
            var quadUp = new SubQuad(vertex1, vertex2, vertex3, vertex4, subEdges);
            cell = new Cell(quadUp, quadDown, subEdges);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            if (cell == null) return;
            foreach (var subEdge in subEdges)
            {
                var endpoints = new List<Vertex>(subEdge.endpoints);
                Gizmos.DrawLine(endpoints[0].position, endpoints[1].position);
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