using UnityEngine;

namespace LadderLab.Placement
{
    public enum TrailerConveyorEndpointKind
    {
        StartPoint,
        EndPoint
    }

    [DisallowMultipleComponent]
    public sealed class TrailerConveyorEndpointSlot : MonoBehaviour
    {
        [SerializeField] private TrailerConveyorEndpointKind endpointKind = TrailerConveyorEndpointKind.EndPoint;

        public TrailerConveyorEndpointKind EndpointKind => endpointKind;

        // 입력값 정리
        private void OnValidate()
        {
            SnapToGrid();
        }

        // 슬롯 위치 표시
        private void OnDrawGizmos()
        {
            DrawSlotGizmo(false);
        }

        // 선택 슬롯 강조 표시
        private void OnDrawGizmosSelected()
        {
            DrawSlotGizmo(true);
        }

        private void DrawSlotGizmo(bool selected)
        {
            var color = endpointKind == TrailerConveyorEndpointKind.StartPoint
                ? new Color(0.2f, 0.9f, 0.35f, selected ? 0.95f : 0.55f)
                : new Color(1f, 0.25f, 0.18f, selected ? 0.95f : 0.55f);

            var previousColor = Gizmos.color;
            var previousMatrix = Gizmos.matrix;

            Gizmos.color = color;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, CalculateGridGizmoSize(out var cellSize));
            Gizmos.matrix = previousMatrix;

            DrawDirectionArrow(color, cellSize);

#if UNITY_EDITOR
            var label = endpointKind == TrailerConveyorEndpointKind.StartPoint ? "Start +X" : "End -X";
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.Label(transform.position + Vector3.up * Mathf.Max(0.35f, cellSize * 0.35f), label);
#endif

            Gizmos.color = previousColor;
        }

        private Vector3 CalculateGridGizmoSize(out float cellSize)
        {
            var installer = ResolveInstaller();
            var grid = ResolveGrid(installer);
            var footprint = installer != null ? installer.Line02Footprint : new Vector2Int(1, 1);
            var fill = installer != null ? installer.FootprintVisualFill : 0.96f;

            cellSize = grid != null ? grid.CellSize : 1f;
            return new Vector3(
                footprint.x * cellSize * fill,
                Mathf.Max(0.08f, cellSize * 0.08f),
                footprint.y * cellSize * fill);
        }

        private TrailerConveyorEndpointInstaller ResolveInstaller()
        {
            var installer = GetComponentInParent<TrailerConveyorEndpointInstaller>();
            return installer != null ? installer : FindAnyObjectByType<TrailerConveyorEndpointInstaller>();
        }

        private PlacementGridManager ResolveGrid(TrailerConveyorEndpointInstaller installer)
        {
            return installer != null && installer.GridManager != null
                ? installer.GridManager
                : FindAnyObjectByType<PlacementGridManager>();
        }

        [ContextMenu("Snap To Grid")]
        private void SnapToGrid()
        {
            if (Application.isPlaying)
                return;

            var grid = ResolveGrid(ResolveInstaller());
            if (grid == null)
                return;

            var cell = grid.WorldToGrid(transform.position);
            if (!grid.IsInsideGrid(cell))
                return;

            var snapped = grid.GridToWorldCenter(cell);
            snapped.y = transform.position.y;

            if ((transform.position - snapped).sqrMagnitude > 0.000001f)
                transform.position = snapped;
        }

        private void DrawDirectionArrow(Color color, float cellSize)
        {
            var localDirection = endpointKind == TrailerConveyorEndpointKind.StartPoint ? Vector3.right : Vector3.left;
            var direction = transform.TransformDirection(localDirection).normalized;
            var start = transform.position;
            var end = start + direction * Mathf.Max(0.25f, cellSize * 0.65f);
            var side = Vector3.Cross(Vector3.up, direction).normalized;

            if (side == Vector3.zero)
                side = transform.forward;

            Gizmos.color = color;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawSphere(end, Mathf.Max(0.035f, cellSize * 0.055f));
            Gizmos.DrawLine(end, end - direction * cellSize * 0.18f + side * cellSize * 0.08f);
            Gizmos.DrawLine(end, end - direction * cellSize * 0.18f - side * cellSize * 0.08f);
        }
    }
}
