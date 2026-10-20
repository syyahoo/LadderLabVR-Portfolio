using UnityEngine;

namespace LadderLab.Wiring
{
    [DisallowMultipleComponent]
    public sealed class WiringConnection : MonoBehaviour
    {
        [SerializeField] private WiringNode fromNode;
        [SerializeField] private WiringNode toNode;
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private Color lineColor = new(0.1f, 0.85f, 1f, 0.95f);

        public WiringNode FromNode => fromNode;
        public WiringNode ToNode => toNode;

        public void Configure(WiringNode source, WiringNode target, Material lineMaterial)
        {
            fromNode = source;
            toNode = target;
            EnsureLineRenderer(lineMaterial);
            Refresh();
        }

        public void SetVisible(bool visible)
        {
            if (lineRenderer != null && lineRenderer.enabled != visible)
                lineRenderer.enabled = visible;
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (lineRenderer == null || fromNode == null || toNode == null)
                return;

            lineRenderer.SetPosition(0, fromNode.transform.position);
            lineRenderer.SetPosition(1, toNode.transform.position);
        }

        private void EnsureLineRenderer(Material lineMaterial)
        {
            if (lineRenderer == null)
                lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
                lineRenderer = gameObject.AddComponent<LineRenderer>();

            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
            lineRenderer.startWidth = 0.025f;
            lineRenderer.endWidth = 0.025f;
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            if (lineMaterial != null)
                lineRenderer.material = lineMaterial;
        }
    }
}
