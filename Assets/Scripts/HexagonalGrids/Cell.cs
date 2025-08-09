using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids
{
    public class Cell
    {
        //从上到下顺时针的八个个顶点
        public Vertex V1;
        public Vertex V2;
        public Vertex V3;
        public Vertex V4;
        public Vertex V5;
        public Vertex V6;
        public Vertex V7;
        public Vertex V8;

        public Vertex[] vertices;

        //12个边
        public SubEdge E1;
        public SubEdge E2;
        public SubEdge E3;
        public SubEdge E4;
        public SubEdge E5;
        public SubEdge E6;
        public SubEdge E7;
        public SubEdge E8;
        public SubEdge E9;
        public SubEdge E10;
        public SubEdge E11;
        public SubEdge E12;

        public SubEdge[] edges;

        public SubQuad Q1;
        public SubQuad Q2;


        //中心点
        public Vector3 Center;

        public Cell(SubQuad upQuad, SubQuad downQuad, List<SubEdge> edges)
        {
            Q1 = upQuad;
            Q2 = downQuad;

            V1 = upQuad.a;
            V2 = upQuad.b;
            V3 = upQuad.c;
            V4 = upQuad.d;

            V5 = downQuad.a;
            V6 = downQuad.b;
            V7 = downQuad.c;
            V8 = downQuad.d;


            vertices = new Vertex[] { V1, V2, V3, V4, V5, V6, V7, V8 };

            E1 = upQuad.edges[0];
            E2 = upQuad.edges[1];
            E3 = upQuad.edges[2];
            E4 = upQuad.edges[3];

            E5 = downQuad.edges[0];
            E6 = downQuad.edges[1];
            E7 = downQuad.edges[2];
            E8 = downQuad.edges[3];

            E9 = SubEdge.GenerateSubEdge(V1, V5, edges);
            E10 = SubEdge.GenerateSubEdge(V2, V6, edges);
            E11 = SubEdge.GenerateSubEdge(V3, V7, edges);
            E12 = SubEdge.GenerateSubEdge(V4, V8, edges);

            this.edges = new SubEdge[] { E1, E2, E3, E4, E5, E6, E7, E8, E9, E10, E11, E12 };


            Center = (V1.position + V2.position + V3.position + V4.position + V5.position + V6.position + V7.position + V8.position) / 8;
        }
    }
}