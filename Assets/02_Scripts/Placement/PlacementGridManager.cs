using System.Collections.Generic;
using UnityEngine;

namespace LadderLab.Placement
{
    [DisallowMultipleComponent]
    public sealed class PlacementGridManager : MonoBehaviour
    {
        [SerializeField, Min(1)] private int gridWidth = 12;
        [SerializeField, Min(1)] private int gridDepth = 12;
        [SerializeField, Min(0.1f)] private float cellSize = 1f;
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private bool drawOnlyInPlacementMode = true;
        [SerializeField] private bool drawRuntimeGrid = true;
        [SerializeField, Min(0.002f)] private float runtimeLineWidth = 0.015f;
        [SerializeField, Min(0f)] private float runtimeGridHeightOffset = 0.025f;
        [SerializeField] private Color gridColor = new(0.35f, 0.8f, 0.9f, 0.45f);
        [SerializeField] private Color occupiedColor = new(1f, 0.35f, 0.2f, 0.65f);

        private readonly Dictionary<Vector2Int, GameObject> occupiedCells = new();
        private readonly HashSet<Vector2Int> allowedCells = new();
        private bool restrictToAllowedCells;
        private bool placementModeGizmosVisible;
        private Transform runtimeGridRoot;
        private Transform runtimeSurfaceRoot;
        private Material runtimeGridMaterial;
        private int runtimeGridWidth;
        private int runtimeGridDepth;
        private float runtimeGridCellSize;

        public int GridWidth => gridWidth;
        public int GridDepth => gridDepth;
        public float CellSize => cellSize;
        public IReadOnlyDictionary<Vector2Int, GameObject> OccupiedCells => occupiedCells;
        public IReadOnlyCollection<Vector2Int> AllowedCells => allowedCells;

        public void SetPlacementModeGizmosVisible(bool visible)
        {
            placementModeGizmosVisible = visible;
            SetRuntimeGridVisible(visible);
        }

        public void Configure(int width, int depth, float targetCellSize)
        {
            gridWidth = Mathf.Max(1, width);
            gridDepth = Mathf.Max(1, depth);
            cellSize = Mathf.Max(0.1f, targetCellSize);
        }

        public void ConfigureFromWorldBounds(Bounds worldBounds, float targetCellSize, float gridHeight)
        {
            cellSize = Mathf.Max(0.1f, targetCellSize);
            gridWidth = Mathf.Max(1, Mathf.CeilToInt(worldBounds.size.x / cellSize));
            gridDepth = Mathf.Max(1, Mathf.CeilToInt(worldBounds.size.z / cellSize));
            transform.position = new Vector3(worldBounds.center.x, gridHeight, worldBounds.center.z);
        }

        public void ClearAllowedCells()
        {
            allowedCells.Clear();
            restrictToAllowedCells = false;
        }

        public void AddAllowedWorldBounds(Bounds worldBounds)
        {
            restrictToAllowedCells = true;
            var minCell = WorldToGrid(worldBounds.min);
            var maxCell = WorldToGrid(worldBounds.max);

            for (var x = minCell.x; x <= maxCell.x; x++)
            {
                for (var z = minCell.y; z <= maxCell.y; z++)
                {
                    var cell = new Vector2Int(x, z);
                    if (!IsInsideGrid(cell))
                        continue;

                    var center = GridToWorldCenter(cell);
                    if (center.x >= worldBounds.min.x &&
                        center.x <= worldBounds.max.x &&
                        center.z >= worldBounds.min.z &&
                        center.z <= worldBounds.max.z)
                    {
                        allowedCells.Add(cell);
                    }
                }
            }
        }

        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            var minCorner = GetMinCorner();
            var x = Mathf.FloorToInt((worldPosition.x - minCorner.x) / cellSize);
            var y = Mathf.FloorToInt((worldPosition.z - minCorner.z) / cellSize);
            return new Vector2Int(x, y);
        }

        public Vector3 GridToWorldCenter(Vector2Int gridPosition)
        {
            var minCorner = GetMinCorner();
            var worldX = minCorner.x + gridPosition.x * cellSize + cellSize * 0.5f;
            var worldZ = minCorner.z + gridPosition.y * cellSize + cellSize * 0.5f;
            return new Vector3(worldX, transform.position.y, worldZ);
        }

        public bool IsInsideGrid(Vector2Int gridPosition)
        {
            return gridPosition.x >= 0 &&
                   gridPosition.x < gridWidth &&
                   gridPosition.y >= 0 &&
                   gridPosition.y < gridDepth;
        }

        public bool IsAllowed(Vector2Int gridPosition)
        {
            return !restrictToAllowedCells || allowedCells.Contains(gridPosition);
        }

        public bool IsOccupied(Vector2Int gridPosition)
        {
            return occupiedCells.ContainsKey(gridPosition);
        }

        public bool CanPlace(Vector2Int originCell)
        {
            return CanPlace(originCell, Vector2Int.one);
        }

        public bool CanPlace(Vector2Int originCell, Vector2Int footprint)
        {
            foreach (var cell in EnumerateFootprintCells(originCell, footprint))
            {
                if (!IsInsideGrid(cell) || !IsAllowed(cell) || IsOccupied(cell))
                    return false;
            }

            return true;
        }

        public bool CanPlace(Vector2Int originCell, Vector2Int footprint, GameObject ignoredObject)
        {
            foreach (var cell in EnumerateFootprintCells(originCell, footprint))
            {
                if (!IsInsideGrid(cell) || !IsAllowed(cell))
                    return false;

                if (occupiedCells.TryGetValue(cell, out var occupiedObject) &&
                    occupiedObject != null &&
                    occupiedObject != ignoredObject)
                {
                    return false;
                }
            }

            return true;
        }

        public void SetOccupied(Vector2Int originCell, GameObject placedObject)
        {
            SetOccupied(originCell, Vector2Int.one, placedObject);
        }

        public void SetOccupied(Vector2Int originCell, Vector2Int footprint, GameObject placedObject)
        {
            if (placedObject == null || !CanPlace(originCell, footprint))
                return;

            foreach (var cell in EnumerateFootprintCells(originCell, footprint))
                occupiedCells[cell] = placedObject;
        }

        public void ClearOccupiedByObject(GameObject placedObject)
        {
            if (placedObject == null)
                return;

            var cellsToClear = new List<Vector2Int>();
            foreach (var pair in occupiedCells)
            {
                if (pair.Value == placedObject)
                    cellsToClear.Add(pair.Key);
            }

            foreach (var cell in cellsToClear)
                occupiedCells.Remove(cell);
        }

        public void ClearAll()
        {
            occupiedCells.Clear();
        }

        public bool TryGetOccupiedObject(Vector2Int gridPosition, out GameObject occupiedObject)
        {
            return occupiedCells.TryGetValue(gridPosition, out occupiedObject);
        }

        public IEnumerable<Vector2Int> EnumerateFootprintCells(Vector2Int originCell, Vector2Int footprint)
        {
            footprint = NormalizeFootprint(footprint);
            for (var x = 0; x < footprint.x; x++)
            {
                for (var y = 0; y < footprint.y; y++)
                    yield return new Vector2Int(originCell.x + x, originCell.y + y);
            }
        }

        private Vector3 GetMinCorner()
        {
            return transform.position - new Vector3(gridWidth * cellSize * 0.5f, 0f, gridDepth * cellSize * 0.5f);
        }

        private static Vector2Int NormalizeFootprint(Vector2Int footprint)
        {
            return new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
        }

        private void OnValidate()
        {
            gridWidth = Mathf.Max(1, gridWidth);
            gridDepth = Mathf.Max(1, gridDepth);
            cellSize = Mathf.Max(0.1f, cellSize);
            runtimeLineWidth = Mathf.Max(0.002f, runtimeLineWidth);
            runtimeGridHeightOffset = Mathf.Max(0f, runtimeGridHeightOffset);
        }

        private void SetRuntimeGridVisible(bool visible)
        {
            if (!Application.isPlaying || !drawRuntimeGrid)
            {
                if (runtimeGridRoot != null)
                    runtimeGridRoot.gameObject.SetActive(false);
                if (runtimeSurfaceRoot != null)
                    runtimeSurfaceRoot.gameObject.SetActive(false);
                return;
            }

            if (visible)
            {
                EnsureRuntimeGridVisual();
                EnsureRuntimePlacementSurface();
            }

            if (runtimeGridRoot != null && runtimeGridRoot.gameObject.activeSelf != visible)
                runtimeGridRoot.gameObject.SetActive(visible);
            if (runtimeSurfaceRoot != null && runtimeSurfaceRoot.gameObject.activeSelf != visible)
                runtimeSurfaceRoot.gameObject.SetActive(visible);
        }

        private void EnsureRuntimeGridVisual()
        {
            if (runtimeGridRoot != null &&
                runtimeGridWidth == gridWidth &&
                runtimeGridDepth == gridDepth &&
                Mathf.Approximately(runtimeGridCellSize, cellSize))
            {
                return;
            }

            DestroyRuntimeGridVisual();

            var root = new GameObject("Placement Grid Runtime Visual");
            runtimeGridRoot = root.transform;
            runtimeGridRoot.SetParent(transform, false);
            runtimeGridWidth = gridWidth;
            runtimeGridDepth = gridDepth;
            runtimeGridCellSize = cellSize;

            var minCorner = GetMinCorner();
            for (var x = 0; x <= gridWidth; x++)
            {
                var start = minCorner + new Vector3(x * cellSize, runtimeGridHeightOffset, 0f);
                var end = minCorner + new Vector3(x * cellSize, runtimeGridHeightOffset, gridDepth * cellSize);
                CreateRuntimeGridLine($"Grid X {x}", start, end);
            }

            for (var z = 0; z <= gridDepth; z++)
            {
                var start = minCorner + new Vector3(0f, runtimeGridHeightOffset, z * cellSize);
                var end = minCorner + new Vector3(gridWidth * cellSize, runtimeGridHeightOffset, z * cellSize);
                CreateRuntimeGridLine($"Grid Z {z}", start, end);
            }

            root.transform.position = Vector3.zero;
        }

        private void EnsureRuntimePlacementSurface()
        {
            if (runtimeSurfaceRoot != null &&
                runtimeGridWidth == gridWidth &&
                runtimeGridDepth == gridDepth &&
                Mathf.Approximately(runtimeGridCellSize, cellSize))
            {
                return;
            }

            DestroyRuntimePlacementSurface();

            var surface = new GameObject("Placement Grid Raycast Surface");
            runtimeSurfaceRoot = surface.transform;
            runtimeSurfaceRoot.SetParent(transform, false);
            runtimeSurfaceRoot.localPosition = Vector3.zero;
            runtimeSurfaceRoot.localRotation = Quaternion.identity;
            runtimeSurfaceRoot.localScale = Vector3.one;

            var collider = surface.AddComponent<BoxCollider>();
            collider.size = new Vector3(gridWidth * cellSize, 0.04f, gridDepth * cellSize);
            collider.center = Vector3.zero;
            surface.AddComponent<PlacementSurfaceMarker>();
        }

        private void CreateRuntimeGridLine(string lineName, Vector3 worldStart, Vector3 worldEnd)
        {
            var lineObject = new GameObject(lineName);
            lineObject.transform.SetParent(runtimeGridRoot, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, worldStart);
            line.SetPosition(1, worldEnd);
            line.startWidth = runtimeLineWidth;
            line.endWidth = runtimeLineWidth;
            line.material = GetRuntimeGridMaterial();
            line.startColor = gridColor;
            line.endColor = gridColor;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        private Material GetRuntimeGridMaterial()
        {
            if (runtimeGridMaterial != null)
                return runtimeGridMaterial;

            var shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Standard");
            runtimeGridMaterial = new Material(shader)
            {
                color = gridColor
            };

            if (runtimeGridMaterial.HasProperty("_BaseColor"))
                runtimeGridMaterial.SetColor("_BaseColor", gridColor);
            runtimeGridMaterial.SetOverrideTag("RenderType", "Transparent");
            runtimeGridMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return runtimeGridMaterial;
        }

        private void DestroyRuntimeGridVisual()
        {
            if (runtimeGridRoot == null)
                return;

            if (Application.isPlaying)
                Destroy(runtimeGridRoot.gameObject);
            else
                DestroyImmediate(runtimeGridRoot.gameObject);

            runtimeGridRoot = null;
        }

        private void DestroyRuntimePlacementSurface()
        {
            if (runtimeSurfaceRoot == null)
                return;

            if (Application.isPlaying)
                Destroy(runtimeSurfaceRoot.gameObject);
            else
                DestroyImmediate(runtimeSurfaceRoot.gameObject);

            runtimeSurfaceRoot = null;
        }

        private void OnDestroy()
        {
            DestroyRuntimeGridVisual();
            DestroyRuntimePlacementSurface();
            if (runtimeGridMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(runtimeGridMaterial);
            else
                DestroyImmediate(runtimeGridMaterial);
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos)
                return;

            if (Application.isPlaying && drawOnlyInPlacementMode && !placementModeGizmosVisible)
                return;

            var minCorner = GetMinCorner();
            Gizmos.color = gridColor;

            for (var x = 0; x <= gridWidth; x++)
            {
                var start = minCorner + new Vector3(x * cellSize, 0f, 0f);
                var end = minCorner + new Vector3(x * cellSize, 0f, gridDepth * cellSize);
                Gizmos.DrawLine(start, end);
            }

            for (var z = 0; z <= gridDepth; z++)
            {
                var start = minCorner + new Vector3(0f, 0f, z * cellSize);
                var end = minCorner + new Vector3(gridWidth * cellSize, 0f, z * cellSize);
                Gizmos.DrawLine(start, end);
            }

            Gizmos.color = occupiedColor;
            foreach (var cell in occupiedCells.Keys)
                Gizmos.DrawCube(GridToWorldCenter(cell) + Vector3.up * 0.02f, new Vector3(cellSize, 0.04f, cellSize));

            if (restrictToAllowedCells)
            {
                Gizmos.color = new Color(gridColor.r, gridColor.g, gridColor.b, 0.12f);
                foreach (var cell in allowedCells)
                    Gizmos.DrawCube(GridToWorldCenter(cell) + Vector3.up * 0.01f, new Vector3(cellSize, 0.01f, cellSize));
            }
        }
    }
}
