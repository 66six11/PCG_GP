using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HexagonalGrids.Render
{
    public class GridRender : MonoBehaviour
    {
        [SerializeField] private Color girdColor = Color.white;
        [SerializeField] private float girdWidth = 0.01f;
        [SerializeField] private GirdGenerator _generator;
        [SerializeField] private Material material;
        private HexGrid hexGrid;
        private GridLayer baseLayer;
        
        private List<GameObject> lineRenderers = new List<GameObject>();

        private void Start()
        {
        }

        [ContextMenu("Init")]
        public void Init()
        {
            hexGrid = _generator._hexGrid;
            baseLayer = hexGrid.baseLayer;
            lineRenderers.Clear();
            Render();
        }

        [ContextMenu("Render")]
        public void Render()
        {
            foreach (SubEdge subEdge in baseLayer.subEdges)
            {
                CreateLineRenderer(subEdge);    
            }
        }
        [ContextMenu("Clear")]
        public void Clear()
        {
            foreach (GameObject lineRenderer in lineRenderers)
            {
                if (Application.isPlaying)
                {
                    Destroy(lineRenderer.gameObject);
                }
                else
                {
                    DestroyImmediate(lineRenderer.gameObject);
                }
            }
            lineRenderers.Clear();
        }
        private void CreateLineRenderer(SubEdge subEdge)
        {
            GameObject lineRenderer = new GameObject("LineRenderer");
            lineRenderer.transform.parent = transform;
            LineRenderer lineRendererComponent = lineRenderer.AddComponent<LineRenderer>();
            lineRendererComponent.sharedMaterial = material;
            lineRendererComponent.startWidth = girdWidth;
            lineRendererComponent.endWidth = girdWidth;
            lineRendererComponent.startColor = girdColor;
            lineRendererComponent.endColor = girdColor;
            lineRendererComponent.positionCount = subEdge.endpoints.Count;
            lineRendererComponent.useWorldSpace = true;
            List<Vertex> vertices = new List<Vertex>(subEdge.endpoints);
            lineRendererComponent.SetPosition(0, vertices[0].position);
            lineRendererComponent.SetPosition(1, vertices[1].position);
            lineRenderers.Add(lineRenderer);
            
        }
    }
}