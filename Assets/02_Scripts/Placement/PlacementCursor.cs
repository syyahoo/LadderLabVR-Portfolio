using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using XrInputDevice = UnityEngine.XR.InputDevice;

namespace LadderLab.Placement
{
    [DisallowMultipleComponent]
    public sealed class PlacementCursor : MonoBehaviour
    {
        [SerializeField] private PlacementGridManager gridManager;
        [SerializeField] private Transform rayOrigin;
        [SerializeField] private XRRayInteractor xrRayInteractor;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform cursorVisual;
        [SerializeField] private bool preferRightHandXrRay = true;
        [SerializeField, Min(0.1f)] private float rayDistance = 30f;
        [SerializeField] private LayerMask groundLayerMask = ~0;
        [SerializeField] private LayerMask blockingLayerMask = ~0;
        [SerializeField] private Vector2Int footprint = Vector2Int.one;
        [SerializeField, Min(0.01f)] private float ghostHeight = 1f;
        [SerializeField, Min(0f)] private float ghostPadding = 0.03f;
        [SerializeField] private Color validCursorColor = new(0.2f, 0.95f, 1f, 0.72f);
        [SerializeField] private Color blockedCursorColor = new(1f, 0.18f, 0.08f, 0.72f);
        [SerializeField] private Color directionArrowColor = new(1f, 0.86f, 0.12f, 0.92f);

        private Transform directionArrowRoot;
        private Transform directionArrowShaft;
        private Transform directionArrowHeadLeft;
        private Transform directionArrowHeadRight;
        private readonly System.Collections.Generic.List<XrInputDevice> xrInputDevices = new();
        private bool showDirectionArrow;
        private Vector3 directionArrowLocalDirection = Vector3.forward;
        private int directionArrowRotationIndex;
        private Material directionArrowMaterial;
        private MaterialPropertyBlock cursorPropertyBlock;
        private Renderer[] cursorVisualRenderers;
        private readonly RaycastHit[] groundRaycastHits = new RaycastHit[128];
        private bool cursorActive = true;
        private bool continuousRaycastActive;
        private bool cachedRayInteractorState;
        private bool originalRayInteractorGameObjectActive;
        private bool originalRayInteractorEnabled;
        private XRInteractorLineVisual[] continuousLineVisuals = System.Array.Empty<XRInteractorLineVisual>();
        private bool[] originalLineVisualGameObjectStates = System.Array.Empty<bool>();
        private bool[] originalLineVisualEnabledStates = System.Array.Empty<bool>();
        private LineRenderer[] continuousLineRenderers = System.Array.Empty<LineRenderer>();
        private bool[] originalLineRendererEnabledStates = System.Array.Empty<bool>();

        public bool HasGridTarget { get; private set; }
        public bool HasValidCell { get; private set; }
        public Vector2Int CurrentGridPosition { get; private set; }
        public Vector3 CurrentWorldPosition => gridManager != null ? gridManager.GridToWorldCenter(CurrentGridPosition) : Vector3.zero;

        public bool TryGetCurrentRay(out Ray ray)
        {
            return TryBuildRay(out ray);
        }

        public void SetContinuousRaycastActive(bool active)
        {
            continuousRaycastActive = active;
            var interactor = ResolveRayInteractor(false);
            if (interactor == null)
                return;

            if (active)
            {
                CacheRayInteractorState(interactor);
                interactor.gameObject.SetActive(true);
                interactor.enabled = true;
                interactor.enableUIInteraction = true;
                SetRayLineVisualVisible(false);
                return;
            }

            RestoreRayInteractorState(interactor);
        }

        public void Configure(
            PlacementGridManager targetGridManager,
            Transform targetRayOrigin,
            Camera targetPlayerCamera,
            Transform targetCursorVisual)
        {
            gridManager = targetGridManager;
            rayOrigin = targetRayOrigin;
            playerCamera = targetPlayerCamera;
            cursorVisual = targetCursorVisual;
        }

        public void SetFootprint(Vector2Int targetFootprint)
        {
            footprint = new Vector2Int(Mathf.Max(1, targetFootprint.x), Mathf.Max(1, targetFootprint.y));
            RefreshCursorVisual();
        }

        public void SetGhostSize(Vector3 worldSize)
        {
            ghostHeight = Mathf.Max(0.01f, worldSize.y);
        }

        public void ConfigureMasks(LayerMask targetGroundLayerMask, LayerMask targetBlockingLayerMask)
        {
            groundLayerMask = targetGroundLayerMask;
            blockingLayerMask = targetBlockingLayerMask;
        }

        public void SetCursorActive(bool active)
        {
            cursorActive = active;
            if (!cursorActive)
                HideCursor();
        }

        public void SetDirectionIndicator(bool visible, Vector3 localDirection, int rotationIndex)
        {
            showDirectionArrow = visible && localDirection != Vector3.zero;
            directionArrowLocalDirection = localDirection == Vector3.zero ? Vector3.forward : localDirection.normalized;
            directionArrowRotationIndex = rotationIndex;
            RefreshDirectionArrowVisual();
        }

        public bool RefreshFromRay(Ray ray)
        {
            HasGridTarget = false;
            HasValidCell = false;

            if (!cursorActive)
            {
                HideCursor();
                return false;
            }

            if (gridManager == null)
            {
                HideCursor();
                return false;
            }

            if (!TryResolveCursorTarget(ray, out var gridPosition, out var groundHitCollider))
            {
                HideCursor();
                return false;
            }

            HasGridTarget = true;
            var canPlace = gridManager.CanPlace(gridPosition, footprint);
            var blocked = canPlace && HasBlockingCollision(gridPosition, groundHitCollider);

            CurrentGridPosition = gridPosition;
            HasValidCell = canPlace && !blocked;
            RefreshCursorVisual();
            return HasValidCell;
        }

        private void Update()
        {
            if (!cursorActive)
            {
                HideCursor();
                return;
            }

            if (!TryBuildRay(out var ray))
            {
                HideCursor();
                return;
            }

            RefreshFromRay(ray);
        }

        private void OnDisable()
        {
            if (continuousRaycastActive)
                SetContinuousRaycastActive(false);
        }

        private bool TryBuildRay(out Ray ray)
        {
            if (TryGetRayInteractorRay(out ray))
                return true;

            if (preferRightHandXrRay && TryGetXrHandRay(XRNode.RightHand, out ray))
                return true;

            if (rayOrigin != null && rayOrigin.position.sqrMagnitude > 0.0001f)
            {
                ray = new Ray(rayOrigin.position, rayOrigin.forward);
                return true;
            }

            var fallbackCamera = playerCamera != null ? playerCamera : Camera.main;
            if (fallbackCamera == null && Camera.allCamerasCount > 0)
            {
                var cameras = new Camera[Camera.allCamerasCount];
                Camera.GetAllCameras(cameras);
                fallbackCamera = cameras[0];
            }

            if (fallbackCamera != null && Mouse.current != null)
            {
                ray = fallbackCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                return true;
            }

            if (playerCamera != null)
            {
                ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
                return true;
            }

            if (fallbackCamera != null)
            {
                ray = new Ray(fallbackCamera.transform.position, fallbackCamera.transform.forward);
                return true;
            }

            ray = default;
            return false;
        }

        private bool TryGetRayInteractorRay(out Ray ray)
        {
            var interactor = ResolveRayInteractor(!continuousRaycastActive);
            if (interactor == null)
            {
                ray = default;
                return false;
            }

            var origin = interactor.rayOriginTransform != null ? interactor.rayOriginTransform : interactor.transform;
            ray = new Ray(origin.position, origin.forward);
            return true;
        }

        private XRRayInteractor ResolveRayInteractor(bool requireActive)
        {
            if (xrRayInteractor != null &&
                (!preferRightHandXrRay || IsRightHandRayInteractor(xrRayInteractor)) &&
                (!requireActive || xrRayInteractor.isActiveAndEnabled))
            {
                return xrRayInteractor;
            }

            var interactors = UnityEngine.Object.FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include);
            XRRayInteractor rightFallback = null;
            XRRayInteractor activeFallback = null;
            XRRayInteractor inactiveFallback = null;
            foreach (var interactor in interactors)
            {
                if (interactor == null)
                    continue;

                if (IsRightHandRayInteractor(interactor))
                {
                    rightFallback ??= interactor;
                    if (!requireActive || interactor.isActiveAndEnabled)
                    {
                        xrRayInteractor = interactor;
                        return xrRayInteractor;
                    }
                }

                if (interactor.isActiveAndEnabled)
                    activeFallback ??= interactor;
                else
                    inactiveFallback ??= interactor;
            }

            xrRayInteractor = !requireActive && rightFallback != null
                ? rightFallback
                : activeFallback != null
                    ? activeFallback
                    : inactiveFallback;
            return xrRayInteractor != null && (!requireActive || xrRayInteractor.isActiveAndEnabled) ? xrRayInteractor : null;
        }

        private void CacheRayInteractorState(XRRayInteractor interactor)
        {
            if (cachedRayInteractorState)
                return;

            cachedRayInteractorState = true;
            originalRayInteractorGameObjectActive = interactor.gameObject.activeSelf;
            originalRayInteractorEnabled = interactor.enabled;
            continuousLineVisuals = interactor.GetComponentsInChildren<XRInteractorLineVisual>(true);
            originalLineVisualGameObjectStates = new bool[continuousLineVisuals.Length];
            originalLineVisualEnabledStates = new bool[continuousLineVisuals.Length];
            for (var index = 0; index < continuousLineVisuals.Length; index++)
            {
                var lineVisual = continuousLineVisuals[index];
                originalLineVisualGameObjectStates[index] = lineVisual != null && lineVisual.gameObject.activeSelf;
                originalLineVisualEnabledStates[index] = lineVisual != null && lineVisual.enabled;
            }

            continuousLineRenderers = interactor.GetComponentsInChildren<LineRenderer>(true);
            originalLineRendererEnabledStates = new bool[continuousLineRenderers.Length];
            for (var index = 0; index < continuousLineRenderers.Length; index++)
            {
                var lineRenderer = continuousLineRenderers[index];
                originalLineRendererEnabledStates[index] = lineRenderer != null && lineRenderer.enabled;
            }
        }

        private void SetRayLineVisualVisible(bool visible)
        {
            for (var index = 0; index < continuousLineVisuals.Length; index++)
            {
                var lineVisual = continuousLineVisuals[index];
                if (lineVisual == null)
                    continue;

                lineVisual.enabled = visible;
            }

            foreach (var lineRenderer in continuousLineRenderers)
            {
                if (lineRenderer != null)
                    lineRenderer.enabled = visible;
            }
        }

        private void RestoreRayInteractorState(XRRayInteractor interactor)
        {
            if (!cachedRayInteractorState)
                return;

            ApplyOriginalRayVisualState();
            interactor.enabled = originalRayInteractorEnabled;
            interactor.gameObject.SetActive(originalRayInteractorGameObjectActive);
            cachedRayInteractorState = false;
        }

        private void ApplyOriginalRayVisualState()
        {
            for (var index = 0; index < continuousLineVisuals.Length; index++)
            {
                var lineVisual = continuousLineVisuals[index];
                if (lineVisual == null)
                    continue;

                if (index < originalLineVisualEnabledStates.Length)
                    lineVisual.enabled = originalLineVisualEnabledStates[index];
                if (index < originalLineVisualGameObjectStates.Length)
                    lineVisual.gameObject.SetActive(originalLineVisualGameObjectStates[index]);
            }

            for (var index = 0; index < continuousLineRenderers.Length; index++)
            {
                var lineRenderer = continuousLineRenderers[index];
                if (lineRenderer != null && index < originalLineRendererEnabledStates.Length)
                    lineRenderer.enabled = originalLineRendererEnabledStates[index];
            }
        }

        private static bool IsRightHandRayInteractor(Component interactor)
        {
            for (var current = interactor.transform; current != null; current = current.parent)
            {
                if (current.name.IndexOf("Right", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private bool TryResolveCursorTarget(Ray ray, out Vector2Int gridPosition, out Collider groundHitCollider)
        {
            var hitCount = Physics.RaycastNonAlloc(
                ray,
                groundRaycastHits,
                rayDistance,
                groundLayerMask,
                QueryTriggerInteraction.Ignore);

            var closestSurfaceDistance = float.PositiveInfinity;
            Collider closestSurfaceCollider = null;
            Vector3 closestSurfacePoint = default;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = groundRaycastHits[index];
                var collider = hit.collider;
                if (collider == null || collider.GetComponentInParent<PlacementSurfaceMarker>() == null)
                    continue;

                if (hit.distance >= closestSurfaceDistance)
                    continue;

                closestSurfaceDistance = hit.distance;
                closestSurfaceCollider = collider;
                closestSurfacePoint = hit.point;
            }

            if (closestSurfaceCollider == null)
            {
                gridPosition = default;
                groundHitCollider = null;
                return false;
            }

            gridPosition = gridManager.WorldToGrid(closestSurfacePoint);
            groundHitCollider = closestSurfaceCollider;
            return gridManager.IsInsideGrid(gridPosition);
        }

        private bool TryGetXrHandRay(XRNode node, out Ray ray)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (TryReadDeviceRay(device, out ray))
                return true;

            var handedness = node == XRNode.LeftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right;
            xrInputDevices.Clear();
            InputDevices.GetDevicesWithCharacteristics(handedness | InputDeviceCharacteristics.Controller, xrInputDevices);
            foreach (var candidate in xrInputDevices)
            {
                if (TryReadDeviceRay(candidate, out ray))
                    return true;
            }

            ray = default;
            return false;
        }

        private static bool TryReadDeviceRay(XrInputDevice device, out Ray ray)
        {
            if (device.isValid &&
                device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out var position) &&
                device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out var rotation))
            {
                ray = new Ray(position, rotation * Vector3.forward);
                return true;
            }

            ray = default;
            return false;
        }

        private void RefreshCursorVisual()
        {
            if (cursorVisual == null || gridManager == null)
                return;

            cursorVisual.gameObject.SetActive(true);
            var cellCenter = gridManager.GridToWorldCenter(CurrentGridPosition);
            var size = gridManager.CellSize;
            cursorVisual.position = cellCenter + new Vector3((footprint.x - 1) * size * 0.5f, 0.03f, (footprint.y - 1) * size * 0.5f);
            cursorVisual.localScale = new Vector3(footprint.x * size, 0.02f, footprint.y * size);
            ApplyCursorColor(HasValidCell ? validCursorColor : blockedCursorColor);
            RefreshDirectionArrowVisual();
        }

        private void ApplyCursorColor(Color color)
        {
            if (cursorVisual == null)
                return;

            cursorVisualRenderers ??= cursorVisual.GetComponentsInChildren<Renderer>(true);
            cursorPropertyBlock ??= new MaterialPropertyBlock();
            foreach (var renderer in cursorVisualRenderers)
            {
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(cursorPropertyBlock);
                cursorPropertyBlock.SetColor("_Color", color);
                cursorPropertyBlock.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(cursorPropertyBlock);
            }
        }

        private void RefreshDirectionArrowVisual()
        {
            if (!showDirectionArrow || !HasValidCell || cursorVisual == null || gridManager == null)
            {
                SetDirectionArrowVisible(false);
                return;
            }

            EnsureDirectionArrowVisual();

            var cellSize = gridManager.CellSize;
            var worldDirection = Quaternion.Euler(0f, directionArrowRotationIndex * 90f, 0f) * directionArrowLocalDirection;
            worldDirection.y = 0f;
            if (worldDirection == Vector3.zero)
            {
                SetDirectionArrowVisible(false);
                return;
            }

            worldDirection.Normalize();
            var visualCenter = cursorVisual.position + Vector3.up * 0.07f;
            var projectedLength = Mathf.Abs(worldDirection.x) * footprint.x * cellSize +
                                  Mathf.Abs(worldDirection.z) * footprint.y * cellSize;
            var arrowLength = Mathf.Max(cellSize * 0.45f, projectedLength * 0.7f);
            var shaftLength = arrowLength * 0.58f;
            var headLength = arrowLength * 0.24f;
            var width = Mathf.Max(0.035f, cellSize * 0.08f);
            var rotation = Quaternion.LookRotation(worldDirection, Vector3.up);
            var tip = visualCenter + worldDirection * (arrowLength * 0.38f);

            directionArrowRoot.SetPositionAndRotation(visualCenter, Quaternion.identity);
            directionArrowShaft.SetPositionAndRotation(visualCenter - worldDirection * (arrowLength * 0.04f), rotation);
            directionArrowShaft.localScale = new Vector3(width, 0.035f, shaftLength);

            SetArrowHeadPart(directionArrowHeadLeft, tip, worldDirection, -38f, headLength, width);
            SetArrowHeadPart(directionArrowHeadRight, tip, worldDirection, 38f, headLength, width);
            SetDirectionArrowVisible(true);
        }

        private void EnsureDirectionArrowVisual()
        {
            if (directionArrowRoot != null)
                return;

            var root = new GameObject("Placement Direction Arrow");
            directionArrowRoot = root.transform;
            directionArrowRoot.SetParent(cursorVisual != null ? cursorVisual.parent : transform, false);
            directionArrowShaft = CreateArrowPart("Arrow Shaft");
            directionArrowHeadLeft = CreateArrowPart("Arrow Head Left");
            directionArrowHeadRight = CreateArrowPart("Arrow Head Right");
            SetDirectionArrowVisible(false);
        }

        private Transform CreateArrowPart(string partName)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.transform.SetParent(directionArrowRoot, false);
            var collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                    Destroy(collider);
                else
                    DestroyImmediate(collider);
            }

            var renderer = part.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = GetDirectionArrowMaterial();

            return part.transform;
        }

        private Material GetDirectionArrowMaterial()
        {
            if (directionArrowMaterial != null)
                return directionArrowMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            directionArrowMaterial = new Material(shader);

            directionArrowMaterial.color = directionArrowColor;
            return directionArrowMaterial;
        }

        private static void SetArrowHeadPart(Transform part, Vector3 tip, Vector3 direction, float angle, float length, float width)
        {
            if (part == null)
                return;

            var headDirection = Quaternion.Euler(0f, angle, 0f) * -direction;
            part.SetPositionAndRotation(
                tip + headDirection * (length * 0.5f),
                Quaternion.LookRotation(-headDirection, Vector3.up));
            part.localScale = new Vector3(width, 0.035f, length);
        }

        private void SetDirectionArrowVisible(bool visible)
        {
            if (directionArrowRoot != null && directionArrowRoot.gameObject.activeSelf != visible)
                directionArrowRoot.gameObject.SetActive(visible);
        }

        private bool HasBlockingCollision(Vector2Int gridPosition, Collider groundHitCollider)
        {
            if (gridManager == null)
                return true;

            var size = gridManager.CellSize;
            var center = gridManager.GridToWorldCenter(gridPosition) +
                         new Vector3((footprint.x - 1) * size * 0.5f, ghostHeight * 0.5f, (footprint.y - 1) * size * 0.5f);
            var halfExtents = new Vector3(
                Mathf.Max(0.01f, footprint.x * size * 0.5f - ghostPadding),
                Mathf.Max(0.01f, ghostHeight * 0.5f - ghostPadding),
                Mathf.Max(0.01f, footprint.y * size * 0.5f - ghostPadding));
            var colliders = Physics.OverlapBox(center, halfExtents, Quaternion.identity, blockingLayerMask, QueryTriggerInteraction.Ignore);
            foreach (var collider in colliders)
            {
                if (collider == null ||
                    collider == groundHitCollider ||
                    collider.GetComponentInParent<PlacementSurfaceMarker>() != null)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void HideCursor()
        {
            if (cursorVisual != null)
                cursorVisual.gameObject.SetActive(false);

            SetDirectionArrowVisible(false);
        }
    }
}
