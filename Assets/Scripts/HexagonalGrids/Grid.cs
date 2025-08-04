using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids
{
    public class HexGrid
    {
        public int radius;
        public Vector3 origin;
        public float cellSize;

        public readonly List<HexVertex> vertices = new List<HexVertex>();
        public readonly List<Triangle> triangles = new List<Triangle>();
        public readonly List<Quad> quads = new List<Quad>();
        public readonly List<Edge> edges = new List<Edge>();

        public HexGrid(int radius, float cellSize, Vector3 origin)
        {
            this.radius = radius;
            this.cellSize = cellSize;
            this.origin = origin;

            Init();
        }

        private void Init()
        {
            foreach (var coord in Coord.HexCoord(radius))
            {
                // 通过网格配置计算世界坐标
                vertices.Add(new HexVertex(coord, coord.ToWorldPosition(cellSize, origin)));
            }

            GenerateHexTriangles();
        }

        public List<HexVertex> RingVertices(int radius)
        {
            List<HexVertex> result = new List<HexVertex>();
            var count = radius > 0 ? 6 * radius : 1;
            var index = 0;
            for (int i = 0; i <= radius - 1; i++)
            {
                if (i <= 0)
                {
                    index += 1;
                }
                else
                {
                    index += 6 * i;
                }
            }

            for (int i = 0; i < count; i++)
            {
                result.Add(vertices[index + i]);
            }

            return result;
        }


        public void GenerateHexTriangles()
        {
            // 六边形网格的三角形生成算法

            List<HexVertex> inRingVertices = new List<HexVertex>();
            List<HexVertex> outRingVertices = new List<HexVertex>();

            for (int i = 0; i < radius; i++)
            {
                inRingVertices = RingVertices(i);
                outRingVertices = RingVertices(i + 1);


                triangles.AddRange(RingTriangles(i, inRingVertices, outRingVertices, edges));
            }
        }

        //随机合并两个相邻的三角形
        public void RandomMergeTriangles()
        {
            while (triangles.Count > 0)
            {
                bool hasNeighbor = false;

                foreach (var t in triangles)
                {
                    if (t.FindNeighborTriangles(triangles).Count > 0)
                    {
                        hasNeighbor = true;
                        break;
                    }
                }

                if (!hasNeighbor) return;

                Triangle t1 = triangles[Random.Range(0, triangles.Count)];

                List<Triangle> t2 = t1.FindNeighborTriangles(triangles);
                if (t2.Count == 0) continue;
                Triangle t3 = t2[Random.Range(0, t2.Count)];

                var quad = Quad.MergeTriangles(t1, t3, edges);

                triangles.Remove(t1);
                triangles.Remove(t3);
                quads.Add(quad);
            }
        }

        public static List<Triangle> RingTriangles(int inRadius, List<HexVertex> inRingVertices, List<HexVertex> outRingVertices, List<Edge> outEdges)
        {
            List<Triangle> result = new List<Triangle>();


            //拐角点的索引间隔
            int inCornerVertexIndexInterval = inRadius == 0 ? 0 : inRingVertices.Count / 6;


            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j <= inCornerVertexIndexInterval; j++)
                {
                    int a = i * inCornerVertexIndexInterval + j;
                    int b = a + i;
                    int c = b + 1;
                    if (a > inRingVertices.Count - 1)
                    {
                        a = 0;
                    }

                    if (c > outRingVertices.Count - 1)
                    {
                        c = 0;
                    }

                    result.Add(new Triangle(inRingVertices[a], outRingVertices[b], outRingVertices[c], outEdges));

                    if (inRadius != 0)
                    {
                        if (a != i * inCornerVertexIndexInterval && a % inCornerVertexIndexInterval == 0) continue;
                        b = c;
                        c = a + 1;
                        if (c > inRingVertices.Count - 1)
                        {
                            c = 0;
                        }

                        result.Add(new Triangle(inRingVertices[a], outRingVertices[b], inRingVertices[c], outEdges));
                    }
                }
            }


            return result;
        }

        //细分网格
        public void SubdivideTriangle(Triangle triangle, out List<Quad> quads)
        {
            quads = new List<Quad>();

            HexVertex abCenter;
            HexVertex bcCenter;
            HexVertex caCenter;
            HexVertex triangleCenter;
            
        }
    }
}