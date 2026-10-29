using System.Collections.Generic;
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
            foreach (Edge subEdge in baseLayer.subEdges)
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
        private void CreateLineRenderer(Edge subEdge)
        {
            GameObject lineRenderer = new GameObject("LineRenderer");
            lineRenderer.transform.parent = transform;
            LineRenderer lineRendererComponent = lineRenderer.AddComponent<LineRenderer>();
            lineRendererComponent.sharedMaterial = material;
            lineRendererComponent.startWidth = girdWidth;
            lineRendererComponent.endWidth = girdWidth;
            lineRendererComponent.startColor = girdColor;
            lineRendererComponent.endColor = girdColor;
            lineRendererComponent.positionCount = 2;
            lineRendererComponent.useWorldSpace = true;
            lineRendererComponent.SetPosition(0, subEdge.a.position);
            lineRendererComponent.SetPosition(1, subEdge.b.position);
            lineRenderers.Add(lineRenderer);

        }
    }
}
