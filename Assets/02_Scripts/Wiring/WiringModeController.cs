using System.Collections.Generic;
using LadderLab.Devices;
using LadderLab.PLC;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using LadderLab.Placement;
using XrInputDevice = UnityEngine.XR.InputDevice;

namespace LadderLab.Wiring
{
    [DisallowMultipleComponent]
    public sealed class WiringModeController : MonoBehaviour
    {
        [SerializeField] private PlcScanController scanController;
        [SerializeField] private PlacementCursor placementCursor;
        [SerializeField] private Transform fallbackRayOrigin;
        [SerializeField, Min(0.1f)] private float nodeRayDistance = 20f;
        [SerializeField] private LayerMask nodeLayerMask = ~0;
        [SerializeField] private Color previewValidColor = new(0.2f, 1f, 0.42f, 0.95f);
        [SerializeField] private Color previewInvalidColor = new(1f, 0.14f, 0.08f, 0.95f);
        [SerializeField, Min(0.05f)] private float nodeDoubleClickSeconds = 0.35f;

        private readonly List<WiringControllerBox> controllerBoxes = new();
        private readonly List<WiringNode> deviceNodes = new();
        private readonly List<WiringConnection> connections = new();
        private readonly List<XrInputDevice> inputDevices = new();
        private readonly RaycastHit[] nodeRaycastHits = new RaycastHit[64];
        private Material lineMaterial;
        private LineRenderer previewLine;
        private WiringNode dragStartNode;
        private WiringNode currentCandidateNode;
        private bool wiringModeActive;
        private bool previousRightTrigger;
        private WiringNode lastClickedNode;
        private float lastClickTime = -1f;

        public bool WiringModeActive => wiringModeActive;

        private void Awake()
        {
            HideAllWiringNodes();
        }

        public void Configure(PlcScanController targetScanController, PlacementCursor targetPlacementCursor)
        {
            scanController = targetScanController;
            placementCursor = targetPlacementCursor;
        }

        public void Configure(
            PlcScanController targetScanController,
            PlacementCursor targetPlacementCursor,
            Transform targetFallbackRayOrigin)
        {
            scanController = targetScanController;
            placementCursor = targetPlacementCursor;
            fallbackRayOrigin = targetFallbackRayOrigin;
        }

        public void SetWiringModeActive(bool active)
        {
            if (wiringModeActive == active)
                return;

            wiringModeActive = active;
            if (wiringModeActive)
                RebuildWiringTargets();
            else
                CancelDrag();

            SetVisible(wiringModeActive);
        }

        private void Start()
        {
            RebuildWiringTargets();
            SetVisible(false);
        }

        private void OnDisable()
        {
            SetVisible(false);
            CancelDrag();
        }

        private void Update()
        {
            if (!wiringModeActive)
                return;

            var rightTrigger = ReadRightTrigger();
            if (rightTrigger && !previousRightTrigger)
                BeginDrag();

            if (rightTrigger && dragStartNode != null)
                UpdateDragPreview();

            if (!rightTrigger && previousRightTrigger)
                EndDrag();

            previousRightTrigger = rightTrigger;
        }

        private void RebuildWiringTargets()
        {
            ResolveScanController();
            EnsureControllerOne();
            RefreshControllerBoxes();
            RefreshDeviceNodes();
        }

        private void ResolveScanController()
        {
            if (scanController == null)
                scanController = FindPlcRuntimeController();

            if (scanController == null)
                scanController = FindAnyObjectByType<PlcScanController>();
        }

        private static PlcScanController FindPlcRuntimeController()
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            foreach (var candidate in transforms)
            {
                if (candidate != null && candidate.name == "PLC Runtime")
                    return candidate.GetComponent<PlcScanController>();
            }

            return null;
        }

        private static void HideAllWiringNodes()
        {
            var nodes = Object.FindObjectsByType<WiringNode>(FindObjectsInactive.Include);
            foreach (var node in nodes)
                node?.SetVisible(false);

            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            foreach (var candidate in transforms)
            {
                if (candidate != null &&
                    candidate.name == "Nodes" &&
                    IsWiringNodesRoot(candidate) &&
                    candidate.gameObject.activeSelf)
                {
                    candidate.gameObject.SetActive(false);
                }
            }
        }

        private static bool IsWiringNodesRoot(Transform candidate)
        {
            if (candidate == null)
                return false;

            if (candidate.GetComponentInParent<WiringControllerBox>() != null)
                return true;

            var parent = candidate.parent;
            return parent != null && IsControllerOneName(parent.name);
        }

        private void EnsureControllerOne()
        {
            var controllerOne = FindControllerOne();
            if (controllerOne == null)
                return;

            var box = controllerOne.GetComponent<WiringControllerBox>();
            if (box == null)
                box = controllerOne.AddComponent<WiringControllerBox>();

            box.ConfigureAsSourceController();
        }

        private static GameObject FindControllerOne()
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            GameObject fallback = null;

            foreach (var candidate in transforms)
            {
                if (candidate == null || !IsControllerOneName(candidate.name))
                    continue;

                if (candidate.GetComponentInChildren<WiringNode>(true) != null)
                    return candidate.gameObject;

                fallback ??= candidate.gameObject;
            }

            return fallback;
        }

        private static bool IsControllerOneName(string objectName)
        {
            return !string.IsNullOrWhiteSpace(objectName) &&
                   (objectName.StartsWith("Controller_1", System.StringComparison.OrdinalIgnoreCase) ||
                    objectName.StartsWith("Controller 1", System.StringComparison.OrdinalIgnoreCase) ||
                    objectName.StartsWith("Controller1", System.StringComparison.OrdinalIgnoreCase));
        }

        private void RefreshControllerBoxes()
        {
            controllerBoxes.Clear();
            var boxes = Object.FindObjectsByType<WiringControllerBox>(FindObjectsInactive.Include);

            foreach (var box in boxes)
            {
                if (box == null)
                    continue;

                ConfigureKnownRelayController(box);
                box.EnsureNodes();
                controllerBoxes.Add(box);
            }
        }

        private static void ConfigureKnownRelayController(WiringControllerBox box)
        {
            if (box == null || box.SourceController)
                return;

            var objectName = box.gameObject.name;
            if (objectName.Contains("Controller_2") || objectName.Contains("Controller 2") || objectName.Contains("Controller2"))
            {
                box.ConfigureAsRelayController(WiringRelayAddressFilter.OutputOnly);
                return;
            }

            if (IsControllerThreeName(objectName))
                box.ConfigureAsRelayController(WiringRelayAddressFilter.OutputOnly);
        }

        private static bool IsControllerThreeName(string objectName)
        {
            return objectName.Contains("Controller_3") ||
                   objectName.Contains("Controller 3") ||
                   objectName.Contains("Controller3");
        }

        private void RefreshDeviceNodes()
        {
            deviceNodes.Clear();
            var endpoints = Object.FindObjectsByType<WiringDeviceEndpoint>(FindObjectsInactive.Include);

            foreach (var endpoint in endpoints)
            {
                if (endpoint == null)
                    continue;

                var node = ResolveDeviceNode(endpoint);
                if (node == null)
                    continue;

                node.RefreshFromEndpoint();
                deviceNodes.Add(node);
            }
        }

        private WiringNode ResolveDeviceNode(WiringDeviceEndpoint endpoint)
        {
            if (endpoint == null)
                return null;

            DisableLegacyGeneratedNode(endpoint);
            var node = endpoint.GetComponent<WiringNode>();
            if (node == null)
                return null;

            node.ConfigureDevice(endpoint);
            return node;
        }

        // 이전 런타임 생성 노드가 남아 있으면 미리 배치한 노드와 겹치지 않게 숨김
        private static void DisableLegacyGeneratedNode(WiringDeviceEndpoint endpoint)
        {
            var legacyNode = endpoint != null ? endpoint.transform.Find("Wiring Device Node") : null;
            if (legacyNode != null && legacyNode.gameObject.activeSelf)
                legacyNode.gameObject.SetActive(false);
        }

        private void SetVisible(bool visible)
        {
            for (var index = controllerBoxes.Count - 1; index >= 0; index--)
            {
                var box = controllerBoxes[index];
                if (box == null)
                {
                    controllerBoxes.RemoveAt(index);
                    continue;
                }

                box.SetNodesVisible(visible);
            }

            for (var index = deviceNodes.Count - 1; index >= 0; index--)
            {
                var node = deviceNodes[index];
                if (node == null)
                {
                    deviceNodes.RemoveAt(index);
                    continue;
                }

                node.SetVisible(visible);
            }

            for (var index = connections.Count - 1; index >= 0; index--)
            {
                var connection = connections[index];
                if (connection == null)
                {
                    connections.RemoveAt(index);
                    continue;
                }

                connection.SetVisible(visible);
            }

            if (previewLine != null)
                previewLine.enabled = false;
        }

        private void BeginDrag()
        {
            if (!TryRaycastNode(out var node))
                return;

            if (TryConsumeNodeDoubleClick(node))
            {
                RemoveAllConnections(node);
                return;
            }

            dragStartNode = node;
            dragStartNode.SetCandidate(true);
            EnsurePreviewLine();
            previewLine.enabled = true;
            UpdateDragPreview();
        }

        private bool TryConsumeNodeDoubleClick(WiringNode node)
        {
            var now = Time.unscaledTime;
            var doubleClicked = node != null &&
                                node == lastClickedNode &&
                                now - lastClickTime <= nodeDoubleClickSeconds;

            lastClickedNode = node;
            lastClickTime = now;
            return doubleClicked;
        }

        private void UpdateDragPreview()
        {
            if (dragStartNode == null || !TryGetCurrentRay(out var ray))
                return;

            var endPoint = ray.GetPoint(Mathf.Max(8f, nodeRayDistance));
            currentCandidateNode?.SetIdle();
            currentCandidateNode = null;
            if (TryRaycastNode(ray, out var candidate, out var hitPoint))
            {
                endPoint = hitPoint;
                if (candidate != dragStartNode)
                {
                    currentCandidateNode = candidate;
                    currentCandidateNode.SetCandidate(CanConnect(dragStartNode, currentCandidateNode));
                }
            }

            SetPreviewLine(dragStartNode.transform.position, endPoint, currentCandidateNode != null && CanConnect(dragStartNode, currentCandidateNode));
        }

        private void EndDrag()
        {
            if (dragStartNode != null && TryRaycastNode(out var endNode) && endNode != dragStartNode)
                TryCreateConnection(dragStartNode, endNode);

            CancelDrag();
        }

        private void CancelDrag()
        {
            if (dragStartNode != null)
                dragStartNode.SetIdle();
            if (currentCandidateNode != null)
                currentCandidateNode.SetIdle();

            dragStartNode = null;
            currentCandidateNode = null;
            if (previewLine != null)
                previewLine.enabled = false;
            previousRightTrigger = false;
        }

        private bool CanConnect(WiringNode first, WiringNode second)
        {
            return TryResolveProviderAndReceiver(first, second, out var provider, out var receiver) &&
                   receiver.CanReceiveAddress(provider.AssignedAddress);
        }

        private bool TryCreateConnection(WiringNode first, WiringNode second)
        {
            if (!TryResolveProviderAndReceiver(first, second, out var provider, out var receiver) ||
                !receiver.CanReceiveAddress(provider.AssignedAddress))
            {
                return false;
            }

            ApplyAddress(receiver, provider.AssignedAddress);
            RemoveIncomingConnection(receiver);

            var connectionObject = new GameObject($"Wiring {provider.name} -> {receiver.name}");
            connectionObject.transform.SetParent(transform, false);
            var connection = connectionObject.AddComponent<WiringConnection>();
            connection.Configure(provider, receiver, GetLineMaterial());
            connection.SetVisible(wiringModeActive);
            connections.Add(connection);
            RefreshControllerSpeedDial(provider, receiver);
            return true;
        }

        private static void RefreshControllerSpeedDial(WiringNode provider, WiringNode receiver)
        {
            if (provider == null || receiver == null || provider.NodeKind != WiringNodeKind.Relay || receiver.NodeKind != WiringNodeKind.Device)
                return;

            var controllerBox = provider.ControllerBox;
            if (controllerBox == null || !controllerBox.gameObject.name.Contains("Controller_2", System.StringComparison.OrdinalIgnoreCase))
                return;

            var conveyor = receiver.Endpoint != null ? receiver.Endpoint.GetComponentInParent<PhysicsConveyorSurface>() : null;
            if (conveyor == null)
                return;

            var dial = controllerBox.GetComponentInChildren<ConveyorSpeedDial>(true);
            dial?.AddTargetConveyor(conveyor);
        }

        private static bool TryResolveProviderAndReceiver(
            WiringNode first,
            WiringNode second,
            out WiringNode provider,
            out WiringNode receiver)
        {
            if (first != null && first.HasAddress && second != null && second.CanReceiveAddress(first.AssignedAddress))
            {
                provider = first;
                receiver = second;
                return true;
            }

            if (second != null && second.HasAddress && first != null && first.CanReceiveAddress(second.AssignedAddress))
            {
                provider = second;
                receiver = first;
                return true;
            }

            provider = null;
            receiver = null;
            return false;
        }

        private void ApplyAddress(WiringNode receiver, string address)
        {
            if (receiver.NodeKind == WiringNodeKind.Relay)
            {
                receiver.AssignAddress(address);
                return;
            }

            var endpoint = receiver.Endpoint;
            if (endpoint == null)
                return;

            var parsed = PlcAddress.Parse(address);
            if (endpoint.AddressBinding == null || !endpoint.AddressBinding.HasController)
                endpoint.ConfigureBinding(scanController, "", new[] { parsed.Area }, BuildAddressList(parsed.Area));

            endpoint.SetAddress(address);
            receiver.RefreshFromEndpoint();
        }

        private void RemoveIncomingConnection(WiringNode receiver)
        {
            for (var index = connections.Count - 1; index >= 0; index--)
            {
                var connection = connections[index];
                if (connection == null)
                {
                    connections.RemoveAt(index);
                    continue;
                }

                if (connection.ToNode != receiver)
                    continue;

                var connectionObject = connection.gameObject;
                connections.RemoveAt(index);
                Destroy(connectionObject);
            }
        }

        private void RemoveAllConnections(WiringNode node)
        {
            if (node == null)
                return;

            for (var index = connections.Count - 1; index >= 0; index--)
            {
                var connection = connections[index];
                if (connection == null)
                {
                    connections.RemoveAt(index);
                    continue;
                }

                if (connection.FromNode != node && connection.ToNode != node)
                    continue;

                var receiver = connection.ToNode;
                var connectionObject = connection.gameObject;
                connections.RemoveAt(index);
                Destroy(connectionObject);
                ClearNodeAssignment(receiver);
            }

            ClearNodeAssignment(node);
            node.SetIdle();
        }

        private static void ClearNodeAssignment(WiringNode node)
        {
            if (node == null || node.NodeKind == WiringNodeKind.Source)
                return;

            if (node.Endpoint != null)
                node.Endpoint.SetAddress("");

            node.ClearAssignedAddress();
            node.RefreshFromEndpoint();
        }

        private bool TryRaycastNode(out WiringNode node)
        {
            if (!TryGetCurrentRay(out var ray))
            {
                node = null;
                return false;
            }

            return TryRaycastNode(ray, out node, out _);
        }

        private bool TryRaycastNode(Ray ray, out WiringNode node, out Vector3 hitPoint)
        {
            var hitCount = Physics.RaycastNonAlloc(ray, nodeRaycastHits, nodeRayDistance, nodeLayerMask, QueryTriggerInteraction.Collide);
            var closestDistance = float.PositiveInfinity;
            node = null;
            hitPoint = default;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = nodeRaycastHits[index];
                var candidate = hit.collider != null ? hit.collider.GetComponentInParent<WiringNode>() : null;
                if (candidate == null || hit.distance >= closestDistance)
                    continue;

                closestDistance = hit.distance;
                node = candidate;
                hitPoint = hit.point;
            }

            return node != null;
        }

        private bool TryGetCurrentRay(out Ray ray)
        {
            if (placementCursor != null && placementCursor.TryGetCurrentRay(out ray))
                return true;

            if (fallbackRayOrigin != null)
            {
                ray = new Ray(fallbackRayOrigin.position, fallbackRayOrigin.forward);
                return true;
            }

            return TryGetXrHandRay(XRNode.RightHand, out ray);
        }

        private bool ReadRightTrigger()
        {
            var nodeDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (TryReadTrigger(nodeDevice))
                return true;

            inputDevices.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, inputDevices);
            foreach (var device in inputDevices)
            {
                if (TryReadTrigger(device))
                    return true;
            }

            if (Mouse.current != null)
                return Mouse.current.leftButton.isPressed;

            return false;
        }

        private static bool TryReadTrigger(XrInputDevice device)
        {
            if (!device.isValid)
                return false;

            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out var pressed) && pressed)
                return true;

            return device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out var value) && value > 0.55f;
        }

        private bool TryGetXrHandRay(XRNode node, out Ray ray)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (TryReadDeviceRay(device, out ray))
                return true;

            inputDevices.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, inputDevices);
            foreach (var candidate in inputDevices)
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

        private void EnsurePreviewLine()
        {
            if (previewLine != null)
                return;

            var previewObject = new GameObject("Wiring Preview Line");
            previewObject.transform.SetParent(transform, false);
            previewLine = previewObject.AddComponent<LineRenderer>();
            previewLine.useWorldSpace = true;
            previewLine.positionCount = 2;
            previewLine.startWidth = 0.02f;
            previewLine.endWidth = 0.02f;
            previewLine.material = GetLineMaterial();
            previewLine.enabled = false;
            previewLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            previewLine.receiveShadows = false;
        }

        private void SetPreviewLine(Vector3 start, Vector3 end, bool valid)
        {
            EnsurePreviewLine();
            previewLine.SetPosition(0, start);
            previewLine.SetPosition(1, end);
            var color = valid ? previewValidColor : previewInvalidColor;
            previewLine.startColor = color;
            previewLine.endColor = color;
        }

        private Material GetLineMaterial()
        {
            if (lineMaterial != null)
                return lineMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Standard");
            lineMaterial = new Material(shader);
            return lineMaterial;
        }

        private static string[] BuildAddressList(PlcMemoryArea area)
        {
            var count = area == PlcMemoryArea.Input ? PlcMemory.InputCount : PlcMemory.OutputCount;
            var prefix = area == PlcMemoryArea.Input ? "X" : "Y";
            var addresses = new string[count];
            for (var index = 0; index < count; index++)
                addresses[index] = prefix + index;
            return addresses;
        }
    }
}
