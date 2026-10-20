using System;
using LadderLab.Devices;
using UnityEngine;

namespace LadderLab.Placement
{
    [DisallowMultipleComponent]
    public sealed class TrailerConveyorEndpointInstaller : MonoBehaviour
    {
        [SerializeField] private PlacementGridManager gridManager;
        [SerializeField] private Vector2Int line02Footprint = new(1, 1);
        [SerializeField, Range(0.5f, 1.2f)] private float footprintVisualFill = 0.96f;
        [SerializeField, Min(0f)] private float beltSpeed = 1.15f;
        [SerializeField] private bool installOnStart = true;

        private const string InstalledEndpointName = "Installed Line_02 Endpoint";
        private const string SpawnPointName = "Trailer Spawn Point";
        private const string RemovePointName = "Trailer Remove Point";
        private const string ConveyorSurfaceName = "Physics Conveyor Surface";
        private const float SpawnInterval = 3f;
        private const int MaxSpawnedBoxCount = 10;
        private static readonly Vector3 SpawnOccupancyHalfExtents = new(0.45f, 0.45f, 0.45f);

        private EquipmentPlacementController placementController;
        private bool configuredThisRun;

        public PlacementGridManager GridManager => gridManager;
        public Vector2Int Line02Footprint => new(Mathf.Max(1, line02Footprint.x), Mathf.Max(1, line02Footprint.y));
        public float FootprintVisualFill => Mathf.Clamp(footprintVisualFill, 0.5f, 1.2f);

        private void Awake()
        {
            if (Application.isPlaying && installOnStart)
                ConfigureOnce();
        }

        private void Start()
        {
            if (Application.isPlaying && installOnStart)
                ConfigureOnce();
        }

        [ContextMenu("Configure Trailer Conveyor Endpoints")]
        public void InstallAll()
        {
            var slots = FindObjectsByType<TrailerConveyorEndpointSlot>(FindObjectsInactive.Exclude);
            var startInstalled = false;
            var endInstalled = false;

            foreach (var slot in slots)
            {
                if (!ShouldInstallSlot(slot, ref startInstalled, ref endInstalled))
                    continue;

                Install(slot);
            }
        }

        private void ConfigureOnce()
        {
            if (configuredThisRun)
                return;

            configuredThisRun = true;
            InstallAll();
        }

        private static bool ShouldInstallSlot(
            TrailerConveyorEndpointSlot slot,
            ref bool startInstalled,
            ref bool endInstalled)
        {
            if (slot == null)
                return false;

            if (slot.EndpointKind == TrailerConveyorEndpointKind.StartPoint)
            {
                if (startInstalled)
                    return false;

                startInstalled = true;
                return true;
            }

            if (endInstalled)
                return false;

            endInstalled = true;
            return true;
        }

        private void Install(TrailerConveyorEndpointSlot slot)
        {
            if (slot == null)
                return;

            var endpoint = ResolveEndpoint(slot);
            if (endpoint == null)
            {
                Debug.LogWarning($"[LadderLab] Trailer endpoint slot '{slot.name}' has no Line_02 child to configure.");
                return;
            }

            FitEndpointToGrid(endpoint);
            EquipmentPlacementController.AlignObjectUpperVisualBoundsToWorldCenter(endpoint, slot.transform.position);

            var localDirection = slot.EndpointKind == TrailerConveyorEndpointKind.StartPoint
                ? Vector3.left
                : Vector3.right;
            var suffix = slot.EndpointKind == TrailerConveyorEndpointKind.StartPoint ? "start" : "end";
            var endpointId = $"trailer.{suffix}.{BuildSlotId(slot)}";
            var displayName = $"Trailer {slot.EndpointKind}";

            EquipmentPlacementController.ConfigureInstalledConveyorEndpoint(
                endpoint,
                endpointId,
                displayName,
                localDirection,
                beltSpeed);

            if (slot.EndpointKind == TrailerConveyorEndpointKind.StartPoint)
                ConfigureStartPoint(endpoint);
            else
                ConfigureEndPoint(endpoint, localDirection);
        }

        private static GameObject ResolveEndpoint(TrailerConveyorEndpointSlot slot)
        {
            var existing = slot.transform.Find(InstalledEndpointName);
            if (existing != null)
                return existing.gameObject;

            for (var index = 0; index < slot.transform.childCount; index++)
            {
                var child = slot.transform.GetChild(index);
                if (child == null || child.name == SpawnPointName || child.name == RemovePointName)
                    continue;

                return child.gameObject;
            }

            return null;
        }

        private static string BuildSlotId(TrailerConveyorEndpointSlot slot)
        {
            var slotName = string.IsNullOrWhiteSpace(slot.name) ? "slot" : slot.name.Trim().Replace(' ', '_').ToLowerInvariant();
            return $"{slotName}.{slot.transform.GetSiblingIndex()}";
        }

        // 엔드포인트 크기를 그리드에 맞춤
        private void FitEndpointToGrid(GameObject endpoint)
        {
            if (gridManager == null)
                gridManager = FindAnyObjectByType<PlacementGridManager>();

            EquipmentPlacementController.FitObjectToGridFootprint(
                endpoint,
                gridManager,
                line02Footprint,
                footprintVisualFill);
        }

        // 시작점 스폰 연결
        private void ConfigureStartPoint(GameObject endpoint)
        {
            var productPrefab = ResolveProductPrefab();
            if (productPrefab == null)
            {
                Debug.LogWarning("[LadderLab] Trailer start endpoint requires product prefab on EquipmentPlacementController.");
                return;
            }

            var spawnPoint = EnsureChildPoint(endpoint.transform, SpawnPointName);
            spawnPoint.localPosition = CalculateConveyorSurfaceLocalPoint(endpoint, Vector3.zero, 0f);
            spawnPoint.localRotation = Quaternion.identity;

            var spawner = endpoint.GetComponent<SandboxBoxSpawner>();
            if (spawner == null)
                spawner = endpoint.AddComponent<SandboxBoxSpawner>();

            spawner.Configure(productPrefab, new[] { spawnPoint }, SpawnInterval, MaxSpawnedBoxCount);
            spawner.ConfigureSpawnedPhysics(true);
            spawner.ConfigureSpawnWhenAreaEmpty(SpawnOccupancyHalfExtents, ~0);
            spawner.ConfigureSpawnedColliderFromRenderers(true);
        }

        // 끝점 제거 연결
        private static void ConfigureEndPoint(GameObject endpoint, Vector3 localDirection)
        {
            var removePoint = EnsureChildPoint(endpoint.transform, RemovePointName);
            removePoint.localPosition = CalculateConveyorSurfaceLocalPoint(endpoint, localDirection, 0.03f);
            removePoint.localRotation = Quaternion.identity;

            var trigger = removePoint.GetComponent<BoxCollider>();
            if (trigger == null)
                trigger = removePoint.gameObject.AddComponent<BoxCollider>();

            trigger.isTrigger = true;
            trigger.size = CalculateRemoveTriggerSize(endpoint);

            var remover = removePoint.GetComponent<TrailerEndpointProductRemover>();
            if (remover == null)
                remover = removePoint.gameObject.AddComponent<TrailerEndpointProductRemover>();
        }

        private GameObject ResolveProductPrefab()
        {
            if (placementController == null)
                placementController = FindAnyObjectByType<EquipmentPlacementController>();

            return placementController != null ? placementController.ProductPrefab : null;
        }

        private static Transform EnsureChildPoint(Transform parent, string pointName)
        {
            var existing = parent.Find(pointName);
            if (existing != null)
                return existing;

            var point = new GameObject(pointName);
            point.transform.SetParent(parent, false);
            return point.transform;
        }

        private static Vector3 CalculateTopLocalPoint(GameObject endpoint, Vector3 localDirection, float heightPadding)
        {
            if (!TryCalculateLocalRendererBounds(endpoint.transform, out var bounds))
                return Vector3.up * heightPadding;

            localDirection.y = 0f;
            if (localDirection != Vector3.zero)
                localDirection.Normalize();

            var offset = Vector3.zero;
            if (localDirection != Vector3.zero)
            {
                var horizontalExtents = new Vector3(bounds.extents.x, 0f, bounds.extents.z);
                var edgeDistance = Mathf.Abs(Vector3.Dot(horizontalExtents, Abs(localDirection)));
                offset = localDirection * Mathf.Max(0.05f, edgeDistance * 0.8f);
            }

            return bounds.center + offset + Vector3.up * (bounds.extents.y + heightPadding);
        }

        private static Vector3 CalculateConveyorSurfaceLocalPoint(GameObject endpoint, Vector3 localDirection, float heightPadding)
        {
            var surface = endpoint != null ? endpoint.transform.Find(ConveyorSurfaceName) : null;
            var collider = surface != null ? surface.GetComponent<BoxCollider>() : null;
            if (collider == null)
                return CalculateTopLocalPoint(endpoint, localDirection, heightPadding);

            localDirection.y = 0f;
            if (localDirection != Vector3.zero)
                localDirection.Normalize();

            var offset = Vector3.zero;
            if (localDirection != Vector3.zero)
            {
                var horizontalExtents = new Vector3(collider.size.x * 0.5f, 0f, collider.size.z * 0.5f);
                var edgeDistance = Mathf.Abs(Vector3.Dot(horizontalExtents, Abs(localDirection)));
                offset = localDirection * Mathf.Max(0.05f, edgeDistance * 0.82f);
            }

            var surfaceBottom = collider.center.y - collider.size.y * 0.5f;
            return new Vector3(collider.center.x, surfaceBottom + heightPadding, collider.center.z) + offset;
        }

        private static Vector3 CalculateRemoveTriggerSize(GameObject endpoint)
        {
            if (!TryCalculateLocalRendererBounds(endpoint.transform, out var bounds))
                return new Vector3(0.3f, 0.35f, 0.3f);

            return new Vector3(
                Mathf.Max(0.2f, bounds.size.x * 0.35f),
                Mathf.Max(0.25f, bounds.size.y + 0.25f),
                Mathf.Max(0.2f, bounds.size.z * 0.7f));
        }

        private static bool TryCalculateLocalRendererBounds(Transform root, out Bounds localBounds)
        {
            localBounds = default;
            if (root == null)
                return false;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var hasBounds = false;
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                    continue;

                var rendererBounds = renderer.localBounds;
                var min = rendererBounds.min;
                var max = rendererBounds.max;
                EncapsulateRendererCorner(root, renderer.transform, new Vector3(min.x, min.y, min.z), ref localBounds, ref hasBounds);
                EncapsulateRendererCorner(root, renderer.transform, new Vector3(min.x, min.y, max.z), ref localBounds, ref hasBounds);
                EncapsulateRendererCorner(root, renderer.transform, new Vector3(min.x, max.y, min.z), ref localBounds, ref hasBounds);
                EncapsulateRendererCorner(root, renderer.transform, new Vector3(min.x, max.y, max.z), ref localBounds, ref hasBounds);
                EncapsulateRendererCorner(root, renderer.transform, new Vector3(max.x, min.y, min.z), ref localBounds, ref hasBounds);
                EncapsulateRendererCorner(root, renderer.transform, new Vector3(max.x, min.y, max.z), ref localBounds, ref hasBounds);
                EncapsulateRendererCorner(root, renderer.transform, new Vector3(max.x, max.y, min.z), ref localBounds, ref hasBounds);
                EncapsulateRendererCorner(root, renderer.transform, new Vector3(max.x, max.y, max.z), ref localBounds, ref hasBounds);
            }

            return hasBounds;
        }

        private static void EncapsulateRendererCorner(
            Transform root,
            Transform rendererTransform,
            Vector3 rendererLocalPoint,
            ref Bounds localBounds,
            ref bool hasBounds)
        {
            var rootLocalPoint = root.InverseTransformPoint(rendererTransform.TransformPoint(rendererLocalPoint));
            if (!hasBounds)
            {
                localBounds = new Bounds(rootLocalPoint, Vector3.zero);
                hasBounds = true;
                return;
            }

            localBounds.Encapsulate(rootLocalPoint);
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private void OnValidate()
        {
            line02Footprint = new Vector2Int(Mathf.Max(1, line02Footprint.x), Mathf.Max(1, line02Footprint.y));
            beltSpeed = Mathf.Max(0f, beltSpeed);
        }
    }

}
