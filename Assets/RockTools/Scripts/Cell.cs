using System.Linq;
using UnityEngine;

namespace RockTools
{
    public class Cell
    {
        public Cell()
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            scale = Vector3.one;
        }

        public Vector3 position;

        public Quaternion rotation;

        public Vector3 scale;

        private Vector3[] vertices;

        private int[] triangles;

        private Color[] colors;

        public void Rotate(Vector3 axis, float angle, Space space = Space.Self)
        {
            if (space == Space.Self)
            {
                rotation *= Quaternion.AngleAxis(angle, axis);
            }
            else
            {
                rotation = Quaternion.AngleAxis(angle, axis) * rotation;
            }
        }

        public void WriteMesh(ref MeshBuffer meshBuffer)
        {
            if (scale.sqrMagnitude == 0)
            {
                return;
            }

            var outVertices = vertices.ToArray();

            for (var i = 0; i < outVertices.Length; i++)
            {
                outVertices[i] = Vector3.Scale(outVertices[i], scale);
                outVertices[i] = rotation * outVertices[i];
                outVertices[i] = position + outVertices[i];
            }

            meshBuffer.Append(outVertices, triangles, colors);
        }

        public void SetMesh(Mesh inMesh)
        {
            vertices = inMesh.vertices.ToArray();
            triangles = inMesh.triangles.ToArray();
            colors = inMesh.colors.ToArray();
        }
    }
}