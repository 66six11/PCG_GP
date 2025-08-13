using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using Utility.RefCopy;

namespace HexagonalGrids
{
    public class GridLayer
    {
        public List<SubQuad> subQuads = new List<SubQuad>();
        public List<SubEdge> subEdges = new List<SubEdge>();
        public List<Vertex> subVertices = new List<Vertex>();
        public List<SubEdge> boundaryEdges = new List<SubEdge>();
        public List<Vertex> boundaryVertices = new List<Vertex>();
        public VertexKdTree vertexKDTree;
    }

    public class HexGrid
    {
        public int radius;
        public Vector3 origin;
        public float cellSize;
        public float cellHeight;
        public int layerCount;

        public readonly List<HexVertex> vertices = new List<HexVertex>();
        public List<MidVertex> midVertices = new List<MidVertex>();
        public List<CenterVertex> centerVertices = new List<CenterVertex>();


        public readonly List<Triangle> triangles = new List<Triangle>();
        public readonly List<Quad> quads = new List<Quad>();
        public readonly List<Edge> edges = new List<Edge>();


        // 网格层
        public List<SubQuad> allSubQuads = new List<SubQuad>();
        public List<SubEdge> allSubEdges = new List<SubEdge>();
        public List<Vertex> allSubVertices = new List<Vertex>();

        public GridLayer baseLayer;
        public List<GridLayer> layers = new List<GridLayer>(); // <--- 多层网格 --->


        public List<Cell> cells = new List<Cell>(); // <--- 网格单元 --->

        public VertexKdTree vertexKDTree;


        public Dictionary<Vertex, List<Cell>> vextex2cellsMap = new Dictionary<Vertex, List<Cell>>(); //点到网格单元的映射


        public HexGrid(int radius, float cellSize, Vector3 origin, float cellHeight, int layerCount)
        {
            this.radius = radius;
            this.cellSize = cellSize;
            this.origin = origin;
            this.cellHeight = cellHeight;
            this.layerCount = layerCount;
            baseLayer = new GridLayer();
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

        public void BuildKdTree()
        {
            baseLayer.vertexKDTree = new VertexKdTree(baseLayer.subVertices);
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


                triangles.AddRange(RingTriangles(i, inRingVertices, outRingVertices, edges, midVertices, centerVertices));
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

                var quad = Quad.MergeTriangles(t1, t3, edges, midVertices, centerVertices);

                triangles.Remove(t1);
                triangles.Remove(t3);

                centerVertices.Remove(t1.center);
                centerVertices.Remove(t3.center);

                quads.Add(quad);
            }
        }

        public static List<Triangle> RingTriangles(int inRadius, List<HexVertex> inRingVertices, List<HexVertex> outRingVertices, List<Edge> outEdges, List<MidVertex> midVertices, List<CenterVertex> centerVertices)
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

                    result.Add(new Triangle(inRingVertices[a], outRingVertices[b], outRingVertices[c], outEdges, midVertices, centerVertices));

                    if (inRadius != 0)
                    {
                        if (a != i * inCornerVertexIndexInterval && a % inCornerVertexIndexInterval == 0) continue;
                        b = c;
                        c = a + 1;
                        if (c > inRingVertices.Count - 1)
                        {
                            c = 0;
                        }

                        result.Add(new Triangle(inRingVertices[a], outRingVertices[b], inRingVertices[c], outEdges, midVertices, centerVertices));
                    }
                }
            }


            return result;
        }

        //细分三角形
        private void SubdivideTriangle(Triangle triangle)
        {
            Vertex a = triangle.a;
            Vertex b = triangle.b;
            Vertex c = triangle.c;

            Vertex abMid = triangle.ab.midVertex;
            Vertex bcMid = triangle.bc.midVertex;
            Vertex caMid = triangle.ca.midVertex;
            Vertex triangleCenter = triangle.center;


            baseLayer.subQuads.Add(new SubQuad(caMid, a, abMid, triangleCenter, baseLayer.subEdges));
            baseLayer.subQuads.Add(new SubQuad(abMid, b, bcMid, triangleCenter, baseLayer.subEdges));
            baseLayer.subQuads.Add(new SubQuad(bcMid, c, caMid, triangleCenter, baseLayer.subEdges));
        }

        //细分四边形
        private void SubdivideQuad(Quad quad)
        {
            Vertex a = quad.a;
            Vertex b = quad.b;
            Vertex c = quad.c;
            Vertex d = quad.d;

            Vertex abMid = quad.ab.midVertex;
            Vertex bcMid = quad.bc.midVertex;
            Vertex cdMid = quad.cd.midVertex;
            Vertex daMid = quad.da.midVertex;

            Vertex center = quad.centerVertex;

            baseLayer.subQuads.Add(new SubQuad(daMid, a, abMid, center, baseLayer.subEdges));
            baseLayer.subQuads.Add(new SubQuad(abMid, b, bcMid, center, baseLayer.subEdges));
            baseLayer.subQuads.Add(new SubQuad(bcMid, c, cdMid, center, baseLayer.subEdges));
            baseLayer.subQuads.Add(new SubQuad(cdMid, d, daMid, center, baseLayer.subEdges));
        }

        //细分网格
        public void SubdivideGrid()
        {
            foreach (var triangle in triangles)
            {
                SubdivideTriangle(triangle);
            }

            foreach (var quad in quads)
            {
                SubdivideQuad(quad);
            }

            foreach (var subQuad in baseLayer.subQuads)
            {
                foreach (var subVertex in subQuad.vertices)
                {
                    if (!baseLayer.subVertices.Contains(subVertex))
                    {
                        baseLayer.subVertices.Add(subVertex);
                    }
                }
            }

            foreach (var subEdge in baseLayer.subEdges)
            {
                int count = 0;
                foreach (var subQuad in baseLayer.subQuads)
                {
                    foreach (var edge in subQuad.edges)
                    {
                        if (edge == subEdge)
                        {
                            count++;
                        }
                    }
                }

                if (count == 1)
                {
                    List<Vertex> vertices = new List<Vertex>(subEdge.endpoints);
                    baseLayer.boundaryVertices.AddRange(vertices);
                    baseLayer.boundaryEdges.Add(subEdge);
                }
            }

            layers.Add(baseLayer);
            allSubEdges.AddRange(baseLayer.subEdges);
            allSubVertices.AddRange(baseLayer.subVertices);
            allSubQuads.AddRange(baseLayer.subQuads);
        }

        public void RelaxGrid(int times)
        {
            for (int i = 0; i < times; i++)
            {
                foreach (var subQuad in baseLayer.subQuads)
                {
                    RelaxSubQuad(subQuad);
                }
            }
        }

        //优化网格
        private void RelaxSubQuad(SubQuad subQuad)
        {
            Vector3 center2a = subQuad.a.position - subQuad.center;
            Vector3 center2b = subQuad.b.position - subQuad.center;
            Vector3 center2c = subQuad.c.position - subQuad.center;
            Vector3 center2d = subQuad.d.position - subQuad.center;

            Vector3 aDesired = DesiredPosition(center2a, center2b, center2c, center2d);
            Vector3 bDesired = DesiredPosition(center2b, center2c, center2d, center2a);
            Vector3 cDesired = DesiredPosition(center2c, center2d, center2a, center2b);
            Vector3 dDesired = DesiredPosition(center2d, center2a, center2b, center2c);

            subQuad.a.position += (aDesired - subQuad.a.position) * 0.1f;
            subQuad.b.position += (bDesired - subQuad.b.position) * 0.1f;
            subQuad.c.position += (cDesired - subQuad.c.position) * 0.1f;
            subQuad.d.position += (dDesired - subQuad.d.position) * 0.1f;

            //计算a的期望位置
            Vector3 DesiredPosition(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4)
            {
                Vector3 sum = Vector3.zero;
                //v2旋转270度
                Vector3 v2_ = YRotationVector(v2, 270);
                //v3旋转180度
                Vector3 v3_ = YRotationVector(v3, 180);
                //v4旋转90度
                Vector3 v4_ = YRotationVector(v4, 90);
                //计算四个顶点的加权平均值
                sum += v1;
                sum += v2_;
                sum += v3_;
                sum += v4_;
                sum /= 4;
                //向量长度设置为根号2/2的cellSize
                sum.Normalize();

                sum *= cellSize * 0.707f;


                return subQuad.center + sum;
            }

            //旋转向量
            Vector3 YRotationVector(Vector3 v, float angle)
            {
                //绕Y轴旋转
                Quaternion q = Quaternion.AngleAxis(angle, Vector3.up);
                return q * v;
            }
        }

        //复制出其他层的网格
        private GridLayer CopySubGrid(GridLayer gridLayer)
        {
            GridLayer result = new GridLayer();
            RefCopyHelper<Vertex> copyHelper = new RefCopyHelper<Vertex>(gridLayer.subVertices, out var copyList);
            result.subVertices = copyList;
            result.subQuads = copyHelper.ListCopy(gridLayer.subQuads);
            result.subEdges = copyHelper.ListCopy<SubEdge>(gridLayer.subEdges);
            result.boundaryVertices = copyHelper.ListCopy(gridLayer.boundaryVertices);
            result.boundaryEdges = copyHelper.ListCopy<SubEdge>(gridLayer.boundaryEdges);

            foreach (var subQuad in result.subQuads)
            {
                subQuad.SetEdges(result.subEdges);
            }

            //调整顶点位置
            foreach (var vertex in result.subVertices)
            {
                vertex.position += Vector3.up * cellHeight;
            }

            //复制kd树
            result.vertexKDTree = new VertexKdTree(result.subVertices);
            return result;
        }

        public void BuildLayers()
        {
            for (int i = 0; i < layerCount; i++)
            {
                GridLayer layer = CopySubGrid(layers[i]);
                allSubEdges.AddRange(layer.subEdges);
                allSubVertices.AddRange(layer.subVertices);
                allSubQuads.AddRange(layer.subQuads);
                layers.Add(layer);
            }

            vertexKDTree = new VertexKdTree(allSubVertices);
        }

        public void BuildCells()
        {
            if (layers.Count <= 1) return;
            for (int i = 1; i < layers.Count; i++)
            {
                for (int j = 0; j < baseLayer.subQuads.Count; j++)
                {
                    SubQuad upQuad = layers[i].subQuads[j];
                    SubQuad downQuad = layers[i - 1].subQuads[j];

                    Cell cell = new Cell(upQuad, downQuad, allSubEdges);
                    cells.Add(cell);
                }
            }

            //构建点到cell的映射
            BuildVertex2CellsMap();
            //构建cell的邻居映射
            BuildCellNeighborsMap();
        }

        private void BuildVertex2CellsMap()
        {
            foreach (var vertex in allSubVertices)
            {
                vextex2cellsMap.Add(vertex, new List<Cell>());
                foreach (var cell in cells)
                {
                    if (cell.Contains(vertex))
                    {
                        vextex2cellsMap[vertex].Add(cell);
                    }
                }
            }
        }

        public List<Cell> Vertex2Cells(Vertex vertex)
        {
            return vextex2cellsMap[vertex];
        }

        public void BuildCellNeighborsMap()
        {
            foreach (var cell in cells)
            {
                var cellVertices = new HashSet<Vertex>(cell.vertices);
                foreach (var neighbor in cells)
                {
                    if (neighbor == cell)
                    {
                        continue;
                    }

                    var intersection = new HashSet<Vertex>(cellVertices);
                    intersection.IntersectWith(neighbor.vertices);
                    if (intersection.Count == 4)
                    {
                        cell.neighbours.Add(neighbor);
                        
                    }
                }
                Debug.Log(cell.neighbours.Count);
            }
        }
    }
}