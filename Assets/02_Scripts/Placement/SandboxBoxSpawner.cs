using System.Collections.Generic;
using UnityEngine;

namespace LadderLab.Placement
{
    [DisallowMultipleComponent]
    public sealed class SandboxBoxSpawner : MonoBehaviour
    {
        private const float EmptyAreaCheckInterval = 0.25f;
        private const float EmptyAreaSpawnCooldown = 0.35f;

        [SerializeField] private GameObject itemPrefab;
        [SerializeField] private Transform[] spawnPoints = { };
        [SerializeField, Min(0.1f)] private float spawnInterval = 3f;
        [SerializeField, Min(1)] private int maxSpawnedItemCount = 10;
        [SerializeField] private bool spawnOnEnable = true;
        [SerializeField] private bool spawnWhenAreaEmpty;
        [SerializeField] private bool ensureSpawnedPhysics = true;
        [SerializeField] private bool rebuildSpawnedColliderFromRenderers;
        [SerializeField] private bool alignSpawnedBottomToSpawnPoint = true;
        [SerializeField] private bool alignSpawnedCenterToSpawnPoint = true;
        [SerializeField] private Vector3 occupancyCheckHalfExtents = new(0.35f, 0.35f, 0.35f);
        [SerializeField] private LayerMask occupancyLayerMask = ~0;
        [SerializeField, Min(0.01f)] private float spawnedMass = 1f;
        [SerializeField, Min(0f)] private float spawnedLinearDamping = 0.05f;
        [SerializeField, Min(0f)] private float spawnedAngularDamping = 0.05f;
        [SerializeField] private Vector3 fallbackBoxColliderSize = Vector3.one * 0.35f;

        private readonly List<GameObject> spawnedItems = new();
        private float spawnTimer;
        private float emptyAreaCheckTimer;

        public int SpawnedItemCount
        {
            get
            {
                RemoveDestroyedItems();
                return spawnedItems.Count;
            }
        }

        public void Configure(GameObject targetItemPrefab, Transform[] targetSpawnPoints, float targetSpawnInterval, int targetMaxCount)
        {
            itemPrefab = targetItemPrefab;
            spawnPoints = targetSpawnPoints ?? new Transform[] { };
            spawnInterval = Mathf.Max(0.1f, targetSpawnInterval);
            maxSpawnedItemCount = Mathf.Max(1, targetMaxCount);
            spawnTimer = spawnInterval;
            emptyAreaCheckTimer = 0f;
        }

        public void ConfigureSpawnWhenAreaEmpty(Vector3 targetOccupancyCheckHalfExtents, LayerMask targetOccupancyLayerMask)
        {
            spawnWhenAreaEmpty = true;
            occupancyCheckHalfExtents = Abs(targetOccupancyCheckHalfExtents);
            occupancyLayerMask = targetOccupancyLayerMask;
            spawnTimer = 0f;
            emptyAreaCheckTimer = 0f;
        }

        public void ConfigureSpawnedColliderFromRenderers(bool enabled)
        {
            rebuildSpawnedColliderFromRenderers = enabled;
        }

        public void ConfigureSpawnedPhysics(bool enabled)
        {
            ensureSpawnedPhysics = enabled;
        }

        public GameObject TrySpawnItem()
        {
            RemoveDestroyedItems();

            if (spawnedItems.Count >= maxSpawnedItemCount || itemPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
                return null;

            var spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (spawnPoint == null)
                return null;

            if (spawnWhenAreaEmpty && IsSpawnAreaOccupied(spawnPoint))
                return null;

            var item = Instantiate(itemPrefab, spawnPoint.position, spawnPoint.rotation);
            if (ensureSpawnedPhysics)
                EnsurePhysics(item);

            if (alignSpawnedBottomToSpawnPoint || alignSpawnedCenterToSpawnPoint)
                AlignToSpawnPoint(item, spawnPoint.position);

            spawnedItems.Add(item);
            return item;
        }

        public void ResetSpawner()
        {
            for (var index = spawnedItems.Count - 1; index >= 0; index--)
            {
                var item = spawnedItems[index];
                if (item == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(item);
                else
                    DestroyImmediate(item);
            }

            spawnedItems.Clear();
            spawnTimer = 0f;
            emptyAreaCheckTimer = 0f;
        }

        private void OnEnable()
        {
            if (spawnOnEnable)
                spawnTimer = spawnInterval;

            emptyAreaCheckTimer = 0f;
        }

        private void Update()
        {
            if (spawnWhenAreaEmpty)
            {
                UpdateEmptyAreaSpawn();
                return;
            }

            spawnTimer += Time.deltaTime;
            if (spawnTimer < spawnInterval)
                return;

            spawnTimer = 0f;
            TrySpawnItem();
        }

        // 빈 공간 기준 스폰 처리
        private void UpdateEmptyAreaSpawn()
        {
            if (spawnTimer > 0f)
                spawnTimer = Mathf.Max(0f, spawnTimer - Time.deltaTime);

            emptyAreaCheckTimer -= Time.deltaTime;
            if (emptyAreaCheckTimer > 0f || spawnTimer > 0f)
                return;

            emptyAreaCheckTimer = EmptyAreaCheckInterval;

            if (TrySpawnItem() != null)
                spawnTimer = EmptyAreaSpawnCooldown;
        }

        private void RemoveDestroyedItems()
        {
            for (var index = spawnedItems.Count - 1; index >= 0; index--)
            {
                if (spawnedItems[index] == null)
                    spawnedItems.RemoveAt(index);
            }
        }

        private void OnValidate()
        {
            spawnInterval = Mathf.Max(0.1f, spawnInterval);
            maxSpawnedItemCount = Mathf.Max(1, maxSpawnedItemCount);
            spawnedMass = Mathf.Max(0.01f, spawnedMass);
            spawnedLinearDamping = Mathf.Max(0f, spawnedLinearDamping);
            spawnedAngularDamping = Mathf.Max(0f, spawnedAngularDamping);
            occupancyCheckHalfExtents = Abs(occupancyCheckHalfExtents);
            fallbackBoxColliderSize = new Vector3(
                Mathf.Max(0.01f, fallbackBoxColliderSize.x),
                Mathf.Max(0.01f, fallbackBoxColliderSize.y),
                Mathf.Max(0.01f, fallbackBoxColliderSize.z));
        }

        private void EnsurePhysics(GameObject item)
        {
            if (item == null)
                return;

            var body = item.GetComponent<Rigidbody>();
            if (body == null)
                body = item.AddComponent<Rigidbody>();

            body.mass = spawnedMass;
            body.linearDamping = spawnedLinearDamping;
            body.angularDamping = spawnedAngularDamping;
            body.isKinematic = false;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (rebuildSpawnedColliderFromRenderers && TryRebuildRootBoxColliderFromRenderers(item, out _))
            {
                body.ResetCenterOfMass();
                body.ResetInertiaTensor();
                return;
            }

            if (HasNonTriggerCollider(item))
                return;

            var box = item.AddComponent<BoxCollider>();
            box.isTrigger = false;
            box.size = fallbackBoxColliderSize;
        }

        private static bool HasNonTriggerCollider(GameObject item)
        {
            if (item == null)
                return false;

            var colliders = item.GetComponentsInChildren<Collider>(true);
            foreach (var collider in colliders)
            {
                if (collider != null && !collider.isTrigger)
                    return true;
            }

            return false;
        }

        private static bool TryRebuildRootBoxColliderFromRenderers(GameObject item, out BoxCollider rootCollider)
        {
            rootCollider = null;
            if (item == null || !TryCalculateLocalRendererBounds(item.transform, out var bounds))
                return false;

            var colliders = item.GetComponentsInChildren<Collider>(true);
            foreach (var collider in colliders)
            {
                if (collider == null || collider.isTrigger)
                    continue;

                collider.enabled = false;
            }

            rootCollider = item.GetComponent<BoxCollider>();
            if (rootCollider == null)
                rootCollider = item.AddComponent<BoxCollider>();

            rootCollider.enabled = true;
            rootCollider.isTrigger = false;
            rootCollider.center = bounds.center;
            rootCollider.size = new Vector3(
                Mathf.Max(0.01f, bounds.size.x),
                Mathf.Max(0.01f, bounds.size.y),
                Mathf.Max(0.01f, bounds.size.z));
            return true;
        }

        private void AlignToSpawnPoint(GameObject item, Vector3 spawnPosition)
        {
            if (item == null || !TryCalculateWorldColliderBounds(item, out var bounds))
                return;

            var offset = Vector3.zero;
            if (alignSpawnedCenterToSpawnPoint)
            {
                offset.x = spawnPosition.x - bounds.center.x;
                offset.z = spawnPosition.z - bounds.center.z;
            }

            if (alignSpawnedBottomToSpawnPoint)
                offset.y = spawnPosition.y - bounds.min.y;

            item.transform.position += offset;
        }

        private static bool TryCalculateWorldColliderBounds(GameObject item, out Bounds bounds)
        {
            bounds = default;
            if (item == null)
                return false;

            var colliders = item.GetComponentsInChildren<Collider>(true);
            var hasBounds = false;
            foreach (var collider in colliders)
            {
                if (collider == null || collider.isTrigger)
                    continue;

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(collider.bounds);
            }

            return hasBounds;
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

        private bool IsSpawnAreaOccupied(Transform spawnPoint)
        {
            var hits = Physics.OverlapBox(
                spawnPoint.position,
                occupancyCheckHalfExtents,
                spawnPoint.rotation,
                occupancyLayerMask,
                QueryTriggerInteraction.Ignore);

            foreach (var hit in hits)
            {
                if (hit == null || hit.attachedRigidbody == null || hit.attachedRigidbody.isKinematic)
                    continue;

                return true;
            }

            return false;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }
}
