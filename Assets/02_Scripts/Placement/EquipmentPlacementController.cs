using System;
using LadderLab.Devices;
using LadderLab.PLC;
using LadderLab.VR;
using LadderLab.Wiring;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LadderLab.Placement
{
    [DisallowMultipleComponent]
    public sealed class EquipmentPlacementController : MonoBehaviour
    {
        private enum AttachmentVisualState
        {
            Off,
            Ghost,
            On
        }

        [SerializeField] private PlacementGridManager gridManager;
        [SerializeField] private PlacementCursor placementCursor;
        [SerializeField] private EquipmentPlacementData selectedEquipment;
        [SerializeField] private EquipmentPlacementData selectedEquipmentOverride;
        [SerializeField] private Transform placedEquipmentParent;
        [SerializeField] private GameObject productPrefab;
        [SerializeField] private bool startInPlacementMode;
        [SerializeField] private bool enablePcDebugInput = true;
        [SerializeField] private bool fitEquipmentToFootprint = true;
        [SerializeField, Range(0.5f, 1.2f)] private float footprintVisualFill = 0.96f;
        [SerializeField] private Color ghostColor = new(0.2f, 0.85f, 1f, 0.38f);
        [SerializeField, Min(0.1f)] private float sensorSlotRayDistance = 20f;

        private const string ProcessingLine06EquipmentId = "processing_line_06";
        private const string ProcessingLine07EquipmentId = "processing_line_07";
        private const string ConveyorLine05EquipmentId = "conveyor_normal_line_05";
        private const string ConveyorLine04EquipmentId = "conveyor_long_line_04";
        private static readonly string[] ControllerInputButtonObjectNames =
        {
            "StartButton",
            "StopButton",
            "EmergencySwitch"
        };
        private static readonly string[] ControllerInputButtonEquipmentIds =
        {
            "input_start_button",
            "input_stop_button",
            "input_emergency_switch"
        };

        private static readonly Color AttachmentGhostColor = new(0.2f, 0.85f, 1f, 0.38f);
        private static MaterialPropertyBlock attachmentPropertyBlock;
        private int rotationIndex;
        private bool placementModeActive;
        private GameObject placementGhost;
        private GameObject ghostSourcePrefab;
        private Material ghostMaterial;
        private readonly RaycastHit[] sensorSlotRaycastHits = new RaycastHit[32];
        private readonly RaycastHit[] placedEquipmentRaycastHits = new RaycastHit[64];
        private PlacedEquipmentHandle selectedPlacedEquipment;
        private PlacedEquipmentHandle lastClickedPlacedEquipment;
        private float lastPlacedEquipmentClickTime = -1f;

        public EquipmentPlacementData SelectedEquipment => selectedEquipment;
        public bool PlacementModeActive => placementModeActive;
        public GameObject ProductPrefab => productPrefab;
        public bool HasSelectedPlacedEquipment => selectedPlacedEquipment != null;
        public bool SelectedEquipmentIsAttachment => IsAttachmentEquipment(selectedEquipment);
        public int RotationIndex => rotationIndex;
        public Vector2Int CurrentFootprint => selectedEquipment != null ? selectedEquipment.GetFootprint(rotationIndex) : Vector2Int.one;

        public void Configure(
            PlacementGridManager targetGridManager,
            PlacementCursor targetPlacementCursor,
            Transform targetPlacedEquipmentParent,
            GameObject targetProductPrefab = null)
        {
            gridManager = targetGridManager;
            placementCursor = targetPlacementCursor;
            placedEquipmentParent = targetPlacedEquipmentParent;
            productPrefab = targetProductPrefab;
            RefreshCursorFootprint();
        }

        public void ConfigureProductPrefab(GameObject targetProductPrefab)
        {
            productPrefab = targetProductPrefab;
        }

        public void SelectEquipment(EquipmentPlacementData equipment)
        {
            OverwriteSelectedEquipment(equipment, true);
        }

        public void OverwriteSelectedEquipment(EquipmentPlacementData equipment)
        {
            OverwriteSelectedEquipment(equipment, true);
        }

        public void OverwriteSelectedEquipment(EquipmentPlacementData equipment, bool activatePlacementMode)
        {
            selectedEquipment = equipment;
            rotationIndex = 0;
            if (activatePlacementMode)
                placementModeActive = selectedEquipment != null;

            RebuildPlacementGhost();
            RefreshCursorFootprint();
        }

        [ContextMenu("Apply Selected Equipment Override")]
        private void ApplySelectedEquipmentOverride()
        {
            OverwriteSelectedEquipment(selectedEquipmentOverride, true);
        }

        public void EnablePlacementMode()
        {
            ClearSelectedEquipment(false);
            placementModeActive = true;
            RefreshCursorFootprint();
        }

        public void SetPlacementModeActive(bool active)
        {
            if (active)
            {
                EnablePlacementMode();
                return;
            }

            CancelPlacement();
        }

        public void CancelPlacement()
        {
            placementModeActive = false;
            rotationIndex = 0;
            selectedPlacedEquipment = null;
            ClearSelectedEquipment(false);
            RefreshCursorFootprint();
        }

        public bool CancelCurrentPlacementSelection()
        {
            if (selectedEquipment == null && selectedPlacedEquipment == null)
                return false;

            selectedPlacedEquipment = null;
            ClearSelectedEquipment(false);
            RefreshCursorFootprint();
            return true;
        }

        public void RotatePlacement()
        {
            if (!placementModeActive || selectedEquipment == null || !selectedEquipment.CanRotate)
                return;

            rotationIndex = (rotationIndex + 1) % 4;
            RefreshCursorFootprint();
        }

        public GameObject PlaceSelectedEquipment()
        {
            if (!placementModeActive || selectedEquipment == null)
                return null;

            if (IsAttachmentEquipment(selectedEquipment))
                return PlaceAttachmentOnTarget();

            if (selectedEquipment.Prefab == null)
                return null;

            if (gridManager == null || placementCursor == null)
                return null;

            if (!placementCursor.HasGridTarget)
                return null;

            if (!placementCursor.HasValidCell &&
                selectedEquipment.PlacementKind != EquipmentPlacementKind.ProcessingEquipment)
            {
                return null;
            }

            var originCell = placementCursor.CurrentGridPosition;
            var footprint = CurrentFootprint;
            if (selectedEquipment.PlacementKind == EquipmentPlacementKind.ProcessingEquipment)
                return PlaceProcessingEquipmentOverlay(originCell, footprint);

            if (!gridManager.CanPlace(originCell, footprint))
                return null;

            var worldPosition = GetFootprintCenterWorldPosition(originCell, footprint);
            var rotation = Quaternion.Euler(0f, rotationIndex * 90f, 0f);
            var placed = Instantiate(selectedEquipment.Prefab, worldPosition, rotation, placedEquipmentParent);
            placed.SetActive(true);
            placed.name = $"{selectedEquipment.DisplayName} ({originCell.x},{originCell.y})";
            FitEquipmentToFootprint(placed, selectedEquipment);
            AlignUpperVisualBoundsToFootprintCenter(placed, worldPosition);
            SnapConveyorToAdjacentConveyors(placed, originCell, footprint, selectedEquipment);

            gridManager.SetOccupied(originCell, footprint, placed);
            ConfigurePlacedEquipment(placed, selectedEquipment);
            RegisterGridPlacedEquipment(placed, selectedEquipment, originCell, footprint);
            UpdatePlacementGhost();
            return placed;
        }

        private GameObject PlaceProcessingEquipmentOverlay(Vector2Int originCell, Vector2Int footprint)
        {
            if (!TryResolveProcessingOverlayTarget(
                    originCell,
                    footprint,
                    selectedEquipment,
                    null,
                    out var targetHandle))
            {
                return null;
            }

            var worldPosition = targetHandle.transform.position;
            var placed = Instantiate(selectedEquipment.Prefab, worldPosition, targetHandle.transform.rotation, placedEquipmentParent);
            placed.SetActive(true);
            placed.name = $"{selectedEquipment.DisplayName} ({originCell.x},{originCell.y})";
            FitEquipmentToFootprint(placed, selectedEquipment);
            ConfigurePlacedEquipment(placed, selectedEquipment);
            RegisterGridPlacedEquipment(placed, selectedEquipment, targetHandle.OriginCell, targetHandle.Footprint);
            UpdatePlacementGhost();
            return placed;
        }

        public bool TrySelectPlacedEquipmentFromCurrentRay()
        {
            if (!TryFindPlacedEquipmentFromCurrentRay(out var closestHandle))
                return false;

            selectedPlacedEquipment = closestHandle;
            OverwriteSelectedEquipment(closestHandle.Equipment, true);
            rotationIndex = NormalizeRotationIndex(closestHandle.transform.eulerAngles.y);
            RefreshCursorFootprint();
            return true;
        }

        private bool TryFindPlacedEquipmentFromCurrentRay(out PlacedEquipmentHandle closestHandle)
        {
            closestHandle = null;
            if (!placementModeActive || placementCursor == null || !placementCursor.TryGetCurrentRay(out var ray))
                return false;

            return TryFindPlacedEquipmentFromRay(ray, out closestHandle);
        }

        private bool TryFindPlacedEquipmentFromRay(Ray ray, out PlacedEquipmentHandle closestHandle)
        {
            closestHandle = null;
            var hitCount = Physics.RaycastNonAlloc(
                ray,
                placedEquipmentRaycastHits,
                sensorSlotRayDistance,
                ~0,
                QueryTriggerInteraction.Collide);
            if (hitCount <= 0)
                return false;

            var closestDistance = float.PositiveInfinity;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = placedEquipmentRaycastHits[index];
                var handle = hit.collider != null ? hit.collider.GetComponentInParent<PlacedEquipmentHandle>() : null;
                if (handle == null || handle.Equipment == null || hit.distance >= closestDistance)
                    continue;

                closestDistance = hit.distance;
                closestHandle = handle;
            }

            return closestHandle != null;
        }

        public bool TryRepositionSelectedPlacedEquipment()
        {
            if (selectedPlacedEquipment == null || selectedPlacedEquipment.Equipment == null)
                return false;

            var equipment = selectedPlacedEquipment.Equipment;
            if (IsAttachmentEquipment(equipment))
                return TryRepositionSelectedAttachment(equipment);

            if (gridManager == null || placementCursor == null)
                return false;

            if (!placementCursor.HasGridTarget)
                return false;

            if (!placementCursor.HasValidCell &&
                equipment.PlacementKind != EquipmentPlacementKind.ProcessingEquipment)
            {
                return false;
            }

            var originCell = placementCursor.CurrentGridPosition;
            var footprint = equipment.GetFootprint(rotationIndex);
            if (equipment.PlacementKind == EquipmentPlacementKind.ProcessingEquipment)
                return TryRepositionSelectedProcessingEquipment(equipment, originCell, footprint);

            if (!gridManager.CanPlace(originCell, footprint, selectedPlacedEquipment.gameObject))
                return false;

            gridManager.ClearOccupiedByObject(selectedPlacedEquipment.gameObject);
            var worldPosition = GetFootprintCenterWorldPosition(originCell, footprint);
            selectedPlacedEquipment.transform.SetPositionAndRotation(worldPosition, Quaternion.Euler(0f, rotationIndex * 90f, 0f));
            AlignUpperVisualBoundsToFootprintCenter(selectedPlacedEquipment.gameObject, worldPosition);
            SnapConveyorToAdjacentConveyors(selectedPlacedEquipment.gameObject, originCell, footprint, equipment);
            gridManager.SetOccupied(originCell, footprint, selectedPlacedEquipment.gameObject);
            selectedPlacedEquipment.UpdateGridPlacement(originCell, footprint);
            selectedPlacedEquipment = null;
            UpdatePlacementGhost();
            return true;
        }

        private bool TryRepositionSelectedProcessingEquipment(
            EquipmentPlacementData equipment,
            Vector2Int originCell,
            Vector2Int footprint)
        {
            if (!TryResolveProcessingOverlayTarget(
                    originCell,
                    footprint,
                    equipment,
                    selectedPlacedEquipment.gameObject,
                    out var targetHandle))
            {
                return false;
            }

            selectedPlacedEquipment.transform.SetPositionAndRotation(targetHandle.transform.position, targetHandle.transform.rotation);
            FitEquipmentToFootprint(selectedPlacedEquipment.gameObject, equipment);
            ConfigurePlacedEquipment(selectedPlacedEquipment.gameObject, equipment);
            selectedPlacedEquipment.UpdateGridPlacement(targetHandle.OriginCell, targetHandle.Footprint);
            selectedPlacedEquipment = null;
            UpdatePlacementGhost();
            return true;
        }

        public bool RemoveSelectedPlacedEquipment()
        {
            if (selectedPlacedEquipment == null)
                return false;

            var target = selectedPlacedEquipment;
            if (gridManager != null)
                gridManager.ClearOccupiedByObject(target.gameObject);

            var destroyObject = target.GridPlaced;
            target.ClearSlotAssignment();
            selectedPlacedEquipment = null;
            if (!destroyObject)
                target.gameObject.SetActive(false);
            else if (Application.isPlaying)
                Destroy(target.gameObject);
            else
                DestroyImmediate(target.gameObject);

            UpdatePlacementGhost();
            return true;
        }

        public bool TryRemovePlacedEquipmentFromCurrentRay()
        {
            if (!TryFindPlacedEquipmentFromCurrentRay(out var handle))
                return false;

            selectedPlacedEquipment = handle;
            return RemoveSelectedPlacedEquipment();
        }

        public bool TryRemovePlacedEquipmentFromRay(Ray ray)
        {
            if (!placementModeActive || !TryFindPlacedEquipmentFromRay(ray, out var handle))
                return false;

            selectedPlacedEquipment = handle;
            return RemoveSelectedPlacedEquipment();
        }

        public bool TryRemovePlacedEquipmentDoubleClickFromCurrentRay(float doubleClickSeconds)
        {
            if (!TryFindPlacedEquipmentFromCurrentRay(out var handle))
                return false;

            var now = Time.unscaledTime;
            var doubleClicked = handle == lastClickedPlacedEquipment &&
                                now - lastPlacedEquipmentClickTime <= doubleClickSeconds;

            lastClickedPlacedEquipment = handle;
            lastPlacedEquipmentClickTime = now;
            if (!doubleClicked)
                return false;

            selectedPlacedEquipment = handle;
            lastClickedPlacedEquipment = null;
            lastPlacedEquipmentClickTime = -1f;
            return RemoveSelectedPlacedEquipment();
        }

        private bool TryResolveProcessingOverlayTarget(
            Vector2Int originCell,
            Vector2Int footprint,
            EquipmentPlacementData equipment,
            GameObject ignoredOverlay,
            out PlacedEquipmentHandle targetHandle)
        {
            targetHandle = null;
            if (gridManager == null ||
                equipment == null ||
                equipment.PlacementKind != EquipmentPlacementKind.ProcessingEquipment ||
                !TryResolveRequiredBaseConveyorId(equipment.EquipmentId, out var requiredConveyorId))
            {
                return false;
            }

            if (!gridManager.IsInsideGrid(originCell) ||
                !gridManager.IsAllowed(originCell) ||
                !gridManager.TryGetOccupiedObject(originCell, out var targetObject) ||
                targetObject == null)
            {
                return false;
            }

            targetHandle = targetObject != null ? targetObject.GetComponent<PlacedEquipmentHandle>() : null;
            if (targetHandle == null ||
                targetHandle.Equipment == null ||
                targetHandle.Equipment.PlacementKind != EquipmentPlacementKind.Conveyor ||
                !string.Equals(targetHandle.Equipment.EquipmentId, requiredConveyorId, StringComparison.OrdinalIgnoreCase))
            {
                targetHandle = null;
                return false;
            }

            return !HasProcessingOverlayAt(targetHandle.OriginCell, targetHandle.Footprint, ignoredOverlay);
        }

        private static bool TryResolveRequiredBaseConveyorId(string processingEquipmentId, out string conveyorEquipmentId)
        {
            if (string.Equals(processingEquipmentId, ProcessingLine06EquipmentId, StringComparison.OrdinalIgnoreCase))
            {
                conveyorEquipmentId = ConveyorLine05EquipmentId;
                return true;
            }

            if (string.Equals(processingEquipmentId, ProcessingLine07EquipmentId, StringComparison.OrdinalIgnoreCase))
            {
                conveyorEquipmentId = ConveyorLine04EquipmentId;
                return true;
            }

            conveyorEquipmentId = "";
            return false;
        }

        private static bool HasProcessingOverlayAt(Vector2Int originCell, Vector2Int footprint, GameObject ignoredOverlay)
        {
            var handles = UnityEngine.Object.FindObjectsByType<PlacedEquipmentHandle>(FindObjectsInactive.Exclude);
            foreach (var handle in handles)
            {
                if (handle == null ||
                    handle.gameObject == ignoredOverlay ||
                    handle.Equipment == null ||
                    handle.Equipment.PlacementKind != EquipmentPlacementKind.ProcessingEquipment)
                {
                    continue;
                }

                if (handle.OriginCell == originCell && handle.Footprint == footprint)
                    return true;
            }

            return false;
        }

        private void ConfigurePlacedEquipment(GameObject placed, EquipmentPlacementData equipment)
        {
            if (placed == null || equipment == null)
                return;

            switch (equipment.PlacementKind)
            {
                case EquipmentPlacementKind.Conveyor:
                    ConfigureConveyor(placed, equipment);
                    break;
                case EquipmentPlacementKind.ProcessingEquipment:
                    ConfigureProcessingEquipment(placed, equipment);
                    break;
                case EquipmentPlacementKind.Sensor:
                    ConfigureEndpoint(placed, equipment);
                    ConfigureSensor(placed);
                    break;
                case EquipmentPlacementKind.Lamp:
                    ConfigureEndpoint(placed, equipment);
                    break;
                case EquipmentPlacementKind.SpawnPoint:
                    ConfigureSpawnPoint(placed);
                    break;
                case EquipmentPlacementKind.Generic:
                    if (IsInputControlEquipment(equipment.EquipmentId))
                    {
                        ConfigurePlacedInputControl(placed, equipment);
                        break;
                    }

                    if (IsSpeedDialEquipment(equipment.EquipmentId))
                    {
                        ConfigurePlacedSpeedDial(placed);
                        break;
                    }

                    ConfigureWiringControllerBox(placed, equipment);
                    if (equipment.CreatesWiringEndpoint)
                        ConfigureEndpoint(placed, equipment);

                    break;
            }
        }

        private static void ConfigureWiringControllerBox(GameObject placed, EquipmentPlacementData equipment)
        {
            if (placed == null ||
                equipment == null ||
                !equipment.EquipmentId.StartsWith("wiring_controller_", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            EnsureControllerBodyCollider(placed);

            var controllerBox = placed.GetComponent<WiringControllerBox>();
            if (controllerBox == null)
                controllerBox = placed.AddComponent<WiringControllerBox>();

            controllerBox.ConfigureAsRelayController(ResolveControllerRelayAddressFilter(equipment.EquipmentId));
            controllerBox.SetNodesVisible(false);

            ConfigureControllerAttachmentTargets(placed);
        }

        // 컨트롤러 본체 콜라이더 구성
        private static void EnsureControllerBodyCollider(GameObject placed)
        {
            if (placed == null || !TryCalculateLocalRendererBounds(placed.transform, out var bounds))
                return;

            var collider = placed.GetComponent<BoxCollider>();
            if (collider == null)
                collider = placed.AddComponent<BoxCollider>();

            collider.isTrigger = false;
            collider.center = bounds.center;
            collider.size = new Vector3(
                Mathf.Max(0.01f, bounds.size.x),
                Mathf.Max(0.01f, bounds.size.y),
                Mathf.Max(0.01f, bounds.size.z));
        }

        private static WiringRelayAddressFilter ResolveControllerRelayAddressFilter(string equipmentId)
        {
            if (equipmentId != null && equipmentId.Contains("controller_2", StringComparison.OrdinalIgnoreCase))
                return WiringRelayAddressFilter.OutputOnly;

            if (equipmentId != null && equipmentId.Contains("controller_3", StringComparison.OrdinalIgnoreCase))
                return WiringRelayAddressFilter.OutputOnly;

            return WiringRelayAddressFilter.Any;
        }

        private static bool IsController3(string equipmentId)
        {
            return equipmentId != null && equipmentId.Contains("controller_3", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInputControlEquipment(string equipmentId)
        {
            return equipmentId != null && equipmentId.StartsWith("input_", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSpeedDialEquipment(string equipmentId)
        {
            return equipmentId != null &&
                   (equipmentId.Contains("speed_dial", StringComparison.OrdinalIgnoreCase) ||
                    equipmentId.Contains("rotary", StringComparison.OrdinalIgnoreCase) ||
                    equipmentId.Contains("knob", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsAttachmentEquipment(EquipmentPlacementData equipment)
        {
            return equipment != null &&
                   (equipment.PlacementKind == EquipmentPlacementKind.Sensor ||
                    IsSpeedDialEquipment(equipment.EquipmentId) ||
                    IsInputControlEquipment(equipment.EquipmentId));
        }

        private static int NormalizeRotationIndex(float yEuler)
        {
            var rounded = Mathf.RoundToInt(yEuler / 90f) % 4;
            return rounded < 0 ? rounded + 4 : rounded;
        }

        // 컨트롤러 부착물 초기 상태 구성
        private static void ConfigureControllerAttachmentTargets(GameObject placed)
        {
            if (placed == null)
                return;

            foreach (var buttonName in ControllerInputButtonObjectNames)
                ConfigureAttachmentTarget(FindChildRecursive(placed.transform, buttonName)?.gameObject);

            ConfigureAttachmentTarget(FindChildRecursive(placed.transform, "RotaryKnob")?.gameObject);
        }

        private static void ConfigureAttachmentTarget(GameObject target)
        {
            if (target == null)
                return;

            SetAttachmentVisualState(target, AttachmentVisualState.Off);
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
                return null;

            foreach (var childTransform in root.GetComponentsInChildren<Transform>(true))
            {
                if (childTransform != root &&
                    string.Equals(childTransform.name, childName, StringComparison.OrdinalIgnoreCase))
                {
                    return childTransform;
                }
            }

            return null;
        }

        private static string[] BuildInputAddressList()
        {
            var addresses = new string[PlcMemory.InputCount];
            for (var index = 0; index < addresses.Length; index++)
                addresses[index] = $"X{index}";

            return addresses;
        }

        // 입력 장치 구성
        private static void ConfigurePlacedInputControl(GameObject placed, EquipmentPlacementData equipment)
        {
            var endpoint = ResolvePlacedEndpoint(placed, WiringSignalDirection.DeviceWritesPlcInput);
            PlcAddressBinding binding = null;
            if (endpoint == null)
            {
                Debug.LogWarning($"[LadderLab] '{placed.name}' needs a pre-placed {nameof(WiringDeviceEndpoint)} for wiring.");
            }
            else
            {
                endpoint.ConfigureMetadata(
                    equipment.EquipmentId,
                    equipment.DisplayName,
                    WiringDeviceKind.Generic,
                    WiringSignalDirection.DeviceWritesPlcInput);
                endpoint.ConfigureBinding(null, "", new[] { PlcMemoryArea.Input }, BuildInputAddressList());
                binding = endpoint.AddressBinding;
            }

            if (binding == null)
            {
                binding = placed.GetComponent<PlcAddressBinding>();
                if (binding == null)
                    binding = placed.AddComponent<PlcAddressBinding>();

                binding.Configure(null, "", new[] { PlcMemoryArea.Input }, BuildInputAddressList());
            }

            if (placed.GetComponent<XRSimpleInteractable>() == null)
                placed.AddComponent<XRSimpleInteractable>();

            var actuator = placed.GetComponent<PlcAddressActuator>();
            if (actuator == null)
                actuator = placed.AddComponent<PlcAddressActuator>();

            actuator.Configure(binding, ResolveInputControlMode(equipment.EquipmentId));

            var visual = placed.GetComponent<VrButtonVisualFeedback>();
            if (visual == null && !equipment.EquipmentId.Contains("dial", StringComparison.OrdinalIgnoreCase))
                visual = placed.AddComponent<VrButtonVisualFeedback>();

            visual?.ConfigureBinding(binding);
        }

        // 속도 노브 구성
        private static void ConfigurePlacedSpeedDial(GameObject placed)
        {
            var dial = placed.GetComponent<ConveyorSpeedDial>();
            if (dial == null)
                dial = placed.AddComponent<ConveyorSpeedDial>();

            if (placed.GetComponent<XRSimpleInteractable>() == null)
                placed.AddComponent<XRSimpleInteractable>();

            dial.Configure();
        }

        private static PlcActuatorMode ResolveInputControlMode(string equipmentId)
        {
            if (equipmentId != null &&
                (equipmentId.Contains("emergency", StringComparison.OrdinalIgnoreCase) ||
                 equipmentId.Contains("dial", StringComparison.OrdinalIgnoreCase)))
            {
                return PlcActuatorMode.Toggle;
            }

            return PlcActuatorMode.Momentary;
        }

        private static void ConfigureConveyor(GameObject placed, EquipmentPlacementData equipment)
        {
            var conveyor = placed.GetComponent<PhysicsConveyorSurface>();
            if (conveyor == null)
                conveyor = placed.AddComponent<PhysicsConveyorSurface>();

            conveyor.Configure(equipment.ConveyorLocalDirection, equipment.ConveyorBeltSpeed);
            ConfigureEndpoint(placed, equipment);
        }

        private static void ConfigureProcessingEquipment(GameObject placed, EquipmentPlacementData equipment)
        {
            var conveyor = placed.GetComponent<PhysicsConveyorSurface>();
            if (conveyor != null)
                conveyor.Configure(equipment.ConveyorLocalDirection, equipment.ConveyorBeltSpeed);

            ConfigureProcessingEquipmentCycle(placed, equipment);
            ConfigureProcessingSensorTarget(placed);
        }

        private static void ConfigureProcessingEquipmentCycle(GameObject placed, EquipmentPlacementData equipment)
        {
            if (placed == null || equipment == null)
                return;

            RemoveGeneratedProcessingEndpoints(placed.transform);

            var cycleDevice = placed.GetComponent<ProcessingEquipmentCycleDevice>();
            if (cycleDevice == null)
                cycleDevice = placed.AddComponent<ProcessingEquipmentCycleDevice>();

            cycleDevice.ConfigureAnimation(
                placed.GetComponentInChildren<Animator>(true),
                placed.GetComponentInChildren<Animation>(true));

            ConfigureProcessingCycleEndpoint(
                placed.transform,
                new[] { "OutputNode", "Node" },
                "Processing Start Endpoint",
                Vector3.left * 0.18f,
                equipment.EquipmentId,
                $"{equipment.DisplayName} Start",
                WiringDeviceKind.Motor,
                WiringSignalDirection.DeviceReadsPlcOutput,
                cycleDevice,
                true);

            ConfigureProcessingCycleEndpoint(
                placed.transform,
                new[] { "InputNode" },
                "Processing Complete Endpoint",
                Vector3.right * 0.18f,
                $"{equipment.EquipmentId}_complete",
                $"{equipment.DisplayName} Complete",
                WiringDeviceKind.Sensor,
                WiringSignalDirection.DeviceWritesPlcInput,
                cycleDevice,
                false);
        }

        private static void ConfigureProcessingCycleEndpoint(
            Transform parent,
            string[] preferredChildNames,
            string fallbackChildName,
            Vector3 localPosition,
            string endpointId,
            string displayName,
            WiringDeviceKind deviceKind,
            WiringSignalDirection signalDirection,
            ProcessingEquipmentCycleDevice cycleDevice,
            bool startEndpoint)
        {
            var endpointObject = ResolveProcessingNodeObject(parent, preferredChildNames, fallbackChildName);
            if (endpointObject == null)
            {
                Debug.LogWarning($"[LadderLab] '{parent?.name}' needs child '{string.Join("' or '", preferredChildNames)}' for processing wiring.");
                return;
            }

            var endpoint = endpointObject.GetComponent<WiringDeviceEndpoint>();
            if (endpoint == null)
            {
                Debug.LogWarning($"[LadderLab] '{endpointObject.name}' needs a pre-placed {nameof(WiringDeviceEndpoint)} for wiring.");
                return;
            }

            endpoint.ConfigureMetadata(endpointId, displayName, deviceKind, signalDirection);

            if (startEndpoint)
            {
                var binding = endpointObject.GetComponent<ProcessingEquipmentStartBinding>();
                if (binding == null)
                    binding = endpointObject.AddComponent<ProcessingEquipmentStartBinding>();

                binding.Configure(cycleDevice);
                return;
            }

            var completeBinding = endpointObject.GetComponent<ProcessingEquipmentCompleteBinding>();
            if (completeBinding == null)
                completeBinding = endpointObject.AddComponent<ProcessingEquipmentCompleteBinding>();

            completeBinding.Configure(cycleDevice);
        }

        private static GameObject ResolveProcessingNodeObject(
            Transform parent,
            string[] preferredChildNames,
            string fallbackChildName)
        {
            if (parent != null && preferredChildNames != null)
            {
                foreach (var childName in preferredChildNames)
                {
                    var child = parent.Find(childName);
                    if (child != null)
                        return child.gameObject;
                }
            }

            var fallback = parent != null ? parent.Find(fallbackChildName) : null;
            return fallback != null ? fallback.gameObject : null;
        }

        private static void RemoveGeneratedProcessingEndpoints(Transform root)
        {
            RemoveChild(root, "Processing Start Endpoint");
            RemoveChild(root, "Processing Complete Endpoint");
        }

        private static void RemoveChild(Transform parent, string childName)
        {
            var child = parent != null ? parent.Find(childName) : null;
            if (child == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(child.gameObject);
            else
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        public static void ConfigureInstalledConveyorEndpoint(
            GameObject placed,
            string endpointId,
            string displayName,
            Vector3 localDirection,
            float beltSpeed)
        {
            if (placed == null)
                return;

            var conveyor = placed.GetComponent<PhysicsConveyorSurface>();
            if (conveyor == null)
                conveyor = placed.AddComponent<PhysicsConveyorSurface>();

            conveyor.Configure(localDirection == Vector3.zero ? Vector3.forward : localDirection, beltSpeed);

            var endpoint = ResolvePlacedEndpoint(placed, WiringSignalDirection.DeviceReadsPlcOutput);
            if (endpoint == null)
            {
                Debug.LogWarning($"[LadderLab] '{placed.name}' needs a pre-placed {nameof(WiringDeviceEndpoint)} for wiring.");
                return;
            }

            endpoint.ConfigureMetadata(
                string.IsNullOrWhiteSpace(endpointId) ? placed.name : endpointId,
                string.IsNullOrWhiteSpace(displayName) ? placed.name : displayName,
                WiringDeviceKind.Conveyor,
                WiringSignalDirection.DeviceReadsPlcOutput);
        }

        // 설치형 장비를 그리드 footprint 크기에 맞춤
        public static void FitObjectToGridFootprint(
            GameObject equipmentObject,
            PlacementGridManager targetGrid,
            Vector2Int footprint,
            float visualFill)
        {
            if (equipmentObject == null || targetGrid == null)
                return;

            var boundsSpace = equipmentObject.transform.parent != null
                ? equipmentObject.transform.parent
                : equipmentObject.transform;
            if (!TryCalculateRendererBoundsInSpace(equipmentObject.transform, boundsSpace, out var localBounds))
                return;

            var size = localBounds.size;
            if (size.x <= 0.001f || size.z <= 0.001f)
                return;

            footprint = new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
            var fill = Mathf.Clamp(visualFill, 0.1f, 1.2f);
            var targetX = footprint.x * targetGrid.CellSize * fill;
            var targetZ = footprint.y * targetGrid.CellSize * fill;
            var localScale = equipmentObject.transform.localScale;
            var uniformScale = CalculateUniformFootprintScale(size, targetX, targetZ);
            localScale.x *= uniformScale;
            localScale.z *= uniformScale;
            equipmentObject.transform.localScale = localScale;
        }

        // 설치형 장비 상단 렌더러 중심을 기준 위치에 맞춤
        public static void AlignObjectUpperVisualBoundsToWorldCenter(GameObject equipmentObject, Vector3 worldCenter)
        {
            AlignUpperVisualBoundsToFootprintCenter(equipmentObject, worldCenter);
        }

        private static bool TryCalculateRendererBoundsInSpace(
            Transform root,
            Transform boundsSpace,
            out Bounds localBounds)
        {
            localBounds = default;
            if (root == null || boundsSpace == null)
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
                EncapsulateRendererCorner(boundsSpace, renderer.transform, new Vector3(min.x, min.y, min.z), ref localBounds, ref hasBounds);
                EncapsulateRendererCorner(boundsSpace, renderer.transform, new Vector3(min.x, min.y, max.z), ref localBounds, ref hasBounds);
                EncapsulateRendererCorner(boundsSpace, renderer.transform, new Vector3(min.x, max.y, min.z), ref localBounds, ref hasBounds);
                EncapsulateRendererCorner(boundsSpace, renderer.transform, new Vector3(min.x, max.y, max.z), ref localBounds, ref hasBounds);
                EncapsulateRendererCorner(boundsSpace, renderer.transform, new Vector3(max.x, min.y, min.z), ref localBounds, ref hasBounds);
                EncapsulateRendererCorner(boundsSpace, renderer.transform, new Vector3(max.x, min.y, max.z), ref localBounds, ref hasBounds);
                EncapsulateRendererCorner(boundsSpace, renderer.transform, new Vector3(max.x, max.y, min.z), ref localBounds, ref hasBounds);
                EncapsulateRendererCorner(boundsSpace, renderer.transform, new Vector3(max.x, max.y, max.z), ref localBounds, ref hasBounds);
            }

            return hasBounds;
        }

        // 기배치 부착물 배치
        private GameObject PlaceAttachmentOnTarget()
        {
            if (!TryResolveAttachmentTarget(selectedEquipment, null, out var target))
                return null;

            SetAttachmentVisualState(target, AttachmentVisualState.On);
            ConfigurePlacedEquipment(target, selectedEquipment);
            RegisterAttachmentPlacedEquipment(target, selectedEquipment, target);
            UpdatePlacementGhost();
            return target;
        }

        private bool TryResolveAttachmentTarget(
            EquipmentPlacementData equipment,
            GameObject ignoredAttachment,
            out GameObject target)
        {
            target = null;
            if (equipment == null ||
                placementCursor == null ||
                !placementCursor.TryGetCurrentRay(out var ray) ||
                !TryGetAttachmentObjectName(equipment, out var attachmentName))
            {
                return false;
            }

            var hitCount = Physics.RaycastNonAlloc(
                ray,
                sensorSlotRaycastHits,
                sensorSlotRayDistance,
                ~0,
                QueryTriggerInteraction.Collide);
            if (hitCount <= 0)
                return false;

            var closestDistance = float.PositiveInfinity;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = sensorSlotRaycastHits[index];
                var handle = hit.collider != null ? hit.collider.GetComponentInParent<PlacedEquipmentHandle>() : null;
                if (handle == null ||
                    !CanHostAttachment(handle, equipment) ||
                    hit.distance >= closestDistance)
                {
                    continue;
                }

                var candidate = FindChildRecursive(handle.transform, attachmentName)?.gameObject;
                if (!CanUseAttachmentTarget(candidate, ignoredAttachment))
                    continue;

                closestDistance = hit.distance;
                target = candidate;
            }

            return target != null;
        }

        private static bool CanUseAttachmentTarget(GameObject candidate, GameObject ignoredAttachment)
        {
            if (candidate == null)
                return false;

            if (candidate == ignoredAttachment)
                return true;

            var handle = candidate.GetComponent<PlacedEquipmentHandle>();
            return handle == null || handle.Equipment == null;
        }

        private static bool CanHostAttachment(PlacedEquipmentHandle host, EquipmentPlacementData equipment)
        {
            if (host == null || host.Equipment == null || equipment == null)
                return false;

            if (equipment.PlacementKind == EquipmentPlacementKind.Sensor)
                return host.Equipment.PlacementKind == EquipmentPlacementKind.ProcessingEquipment;

            if (IsSpeedDialEquipment(equipment.EquipmentId) || IsInputControlEquipment(equipment.EquipmentId))
            {
                return host.Equipment.EquipmentId != null &&
                       host.Equipment.EquipmentId.StartsWith("wiring_controller_", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static bool TryGetAttachmentObjectName(EquipmentPlacementData equipment, out string objectName)
        {
            objectName = "";
            if (equipment == null)
                return false;

            if (equipment.PlacementKind == EquipmentPlacementKind.Sensor)
            {
                objectName = "Sensor";
                return true;
            }

            if (IsSpeedDialEquipment(equipment.EquipmentId))
            {
                objectName = "RotaryKnob";
                return true;
            }

            for (var index = 0; index < ControllerInputButtonEquipmentIds.Length; index++)
            {
                if (string.Equals(equipment.EquipmentId, ControllerInputButtonEquipmentIds[index], StringComparison.OrdinalIgnoreCase))
                {
                    objectName = ControllerInputButtonObjectNames[index];
                    return true;
                }
            }

            if (equipment.EquipmentId != null && equipment.EquipmentId.Contains("start", StringComparison.OrdinalIgnoreCase))
                objectName = "StartButton";
            else if (equipment.EquipmentId != null && equipment.EquipmentId.Contains("stop", StringComparison.OrdinalIgnoreCase))
                objectName = "StopButton";
            else if (equipment.EquipmentId != null && equipment.EquipmentId.Contains("emergency", StringComparison.OrdinalIgnoreCase))
                objectName = "EmergencySwitch";

            return !string.IsNullOrWhiteSpace(objectName);
        }

        private static void SetAttachmentVisualState(GameObject target, AttachmentVisualState state, bool highlighted = false)
        {
            if (target == null)
                return;

            target.SetActive(state != AttachmentVisualState.Off);

            foreach (var collider in target.GetComponentsInChildren<Collider>(true))
                collider.enabled = state == AttachmentVisualState.On;

            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if (state == AttachmentVisualState.Ghost)
                {
                    var propertyBlock = GetAttachmentPropertyBlock();
                    var color = highlighted ? Color.Lerp(AttachmentGhostColor, Color.white, 0.35f) : AttachmentGhostColor;
                    propertyBlock.SetColor("_BaseColor", color);
                    propertyBlock.SetColor("_Color", color);
                    renderer.SetPropertyBlock(propertyBlock);
                }
                else
                {
                    renderer.SetPropertyBlock(null);
                }
            }
        }

        private static MaterialPropertyBlock GetAttachmentPropertyBlock()
        {
            attachmentPropertyBlock ??= new MaterialPropertyBlock();
            attachmentPropertyBlock.Clear();
            return attachmentPropertyBlock;
        }

        // 센서 구성
        private static void ConfigureSensor(GameObject placed)
        {
            var sensor = placed.GetComponent<SensorDevice>();
            if (sensor == null)
                sensor = placed.AddComponent<SensorDevice>();

            ConfigureSensorAreaRelay(placed, sensor);

            var collider = placed.GetComponent<Collider>();
            if (collider != null)
                collider.isTrigger = true;

            _ = sensor;
        }

        private static void ConfigureSensorAreaRelay(GameObject placed, SensorDevice sensor)
        {
            if (placed == null || sensor == null)
                return;

            var area = placed.transform.Find("SensorArea");
            if (area == null)
                return;

            var areaCollider = area.GetComponent<Collider>();
            if (areaCollider == null)
                areaCollider = area.gameObject.AddComponent<BoxCollider>();

            areaCollider.isTrigger = true;

            var relay = area.GetComponent<SensorAreaRelay>();
            if (relay == null)
                relay = area.gameObject.AddComponent<SensorAreaRelay>();

            relay.Configure(sensor);
        }

        private bool TryRepositionSelectedAttachment(EquipmentPlacementData equipment)
        {
            if (selectedPlacedEquipment == null ||
                equipment == null ||
                !TryResolveAttachmentTarget(equipment, selectedPlacedEquipment.gameObject, out var target))
            {
                return false;
            }

            var previous = selectedPlacedEquipment.gameObject;
            if (previous != target)
            {
                selectedPlacedEquipment.ClearSlotAssignment();
                previous.SetActive(false);
                SetAttachmentVisualState(target, AttachmentVisualState.On);
                ConfigurePlacedEquipment(target, equipment);
                RegisterAttachmentPlacedEquipment(target, equipment, target);
            }
            else
            {
                ConfigurePlacedEquipment(previous, equipment);
                selectedPlacedEquipment.UpdateAttachmentTarget(previous);
            }

            selectedPlacedEquipment = null;
            UpdatePlacementGhost();
            return true;
        }

        private static void RegisterGridPlacedEquipment(
            GameObject placed,
            EquipmentPlacementData equipment,
            Vector2Int originCell,
            Vector2Int footprint)
        {
            if (placed == null)
                return;

            var handle = placed.GetComponent<PlacedEquipmentHandle>();
            if (handle == null)
                handle = placed.AddComponent<PlacedEquipmentHandle>();

            handle.ConfigureGrid(equipment, originCell, footprint);
        }

        private static void RegisterAttachmentPlacedEquipment(GameObject placed, EquipmentPlacementData equipment, GameObject target)
        {
            if (placed == null)
                return;

            var handle = placed.GetComponent<PlacedEquipmentHandle>();
            if (handle == null)
                handle = placed.AddComponent<PlacedEquipmentHandle>();

            handle.ConfigureAttachment(equipment, target);
        }

        // 가공 장비 센서 초기 상태 구성
        private static void ConfigureProcessingSensorTarget(GameObject equipmentObject)
        {
            if (equipmentObject == null)
                return;

            var sensor = FindChildRecursive(equipmentObject.transform, "Sensor")?.gameObject;
            if (sensor == null)
                Debug.LogWarning($"[LadderLab] '{equipmentObject.name}' prefab needs a pre-placed Sensor child for sensor placement.");
            else
                SetAttachmentVisualState(sensor, AttachmentVisualState.Off);
        }

        private void ConfigureSpawnPoint(GameObject placed)
        {
            var spawner = placed.GetComponent<SandboxBoxSpawner>();
            if (spawner == null)
                spawner = placed.AddComponent<SandboxBoxSpawner>();

            spawner.Configure(productPrefab, new[] { placed.transform }, 3f, 10);
        }

        private static void ConfigureEndpoint(GameObject placed, EquipmentPlacementData equipment)
        {
            var endpoint = ResolvePlacedEndpoint(placed, equipment);
            if (endpoint == null)
            {
                Debug.LogWarning($"[LadderLab] '{placed.name}' needs a pre-placed {nameof(WiringDeviceEndpoint)} for wiring.");
                return;
            }

            endpoint.ConfigureMetadata(
                equipment.EquipmentId,
                equipment.DisplayName,
                equipment.WiringDeviceKind,
                equipment.SignalDirection);
        }

        private static WiringDeviceEndpoint ResolvePlacedEndpoint(GameObject placed, EquipmentPlacementData equipment)
        {
            if (placed == null)
                return null;

            var signalDirection = equipment != null
                ? equipment.SignalDirection
                : WiringSignalDirection.DeviceReadsPlcOutput;
            return ResolvePlacedEndpoint(placed, signalDirection);
        }

        private static WiringDeviceEndpoint ResolvePlacedEndpoint(GameObject placed, WiringSignalDirection signalDirection)
        {
            if (placed == null)
                return null;

            if (signalDirection == WiringSignalDirection.DeviceWritesPlcInput)
            {
                var inputNode = placed.transform.Find("InputNode");
                var inputEndpoint = inputNode != null ? inputNode.GetComponent<WiringDeviceEndpoint>() : null;
                if (inputEndpoint != null)
                    return inputEndpoint;
            }
            else
            {
                foreach (var childName in new[] { "OutputNode", "Node" })
                {
                    var outputNode = placed.transform.Find(childName);
                    var outputEndpoint = outputNode != null ? outputNode.GetComponent<WiringDeviceEndpoint>() : null;
                    if (outputEndpoint != null)
                        return outputEndpoint;
                }
            }

            return placed.GetComponent<WiringDeviceEndpoint>();
        }

        private void Start()
        {
            ClearSelectedEquipment(false);
            placementModeActive = startInPlacementMode;
            RefreshCursorFootprint();
        }

        private void Update()
        {
            UpdatePlacementGhost();

            if (!enablePcDebugInput)
                return;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                PlaceSelectedEquipment();

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                RotatePlacement();

            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
                CancelPlacement();
        }

        private void RefreshCursorFootprint()
        {
            if (selectedEquipment != null && selectedEquipment.Prefab != ghostSourcePrefab)
                RebuildPlacementGhost();

            if (placementCursor != null)
            {
                placementCursor.SetCursorActive(placementModeActive);
                placementCursor.SetFootprint(CurrentFootprint);
                var showDirection = placementModeActive &&
                                    selectedEquipment != null &&
                                    selectedEquipment.PlacementKind == EquipmentPlacementKind.Conveyor;
                placementCursor.SetDirectionIndicator(
                    showDirection,
                    selectedEquipment != null ? selectedEquipment.ConveyorLocalDirection : Vector3.forward,
                    rotationIndex);
            }

            if (gridManager != null)
                gridManager.SetPlacementModeGizmosVisible(placementModeActive);

            UpdatePlacementGhost();
        }

        private void ClearSelectedEquipment(bool refresh)
        {
            selectedEquipment = null;
            rotationIndex = 0;
            DestroyPlacementGhost();

            if (refresh)
                RefreshCursorFootprint();
        }

        private void RebuildPlacementGhost()
        {
            DestroyPlacementGhost();

            ghostSourcePrefab = selectedEquipment != null ? selectedEquipment.Prefab : null;
            if (ghostSourcePrefab == null)
                return;

            placementGhost = Instantiate(ghostSourcePrefab, transform);
            placementGhost.name = $"{ghostSourcePrefab.name} Placement Ghost";
            FitEquipmentToFootprint(placementGhost, selectedEquipment);
            ConfigureGhostObject(placementGhost);
            placementGhost.SetActive(false);
        }

        private void FitEquipmentToFootprint(GameObject equipmentObject, EquipmentPlacementData equipment)
        {
            if (!fitEquipmentToFootprint || equipmentObject == null || equipment == null || gridManager == null)
                return;

            if (!TryCalculateLocalRendererBounds(equipmentObject.transform, out var localBounds))
                return;

            var size = localBounds.size;
            if (size.x <= 0.001f || size.z <= 0.001f)
                return;

            var baseFootprint = equipment.Footprint;
            var visualScale = equipment.VisualFootprintScale;
            var cellSize = gridManager.CellSize;
            var localScale = equipmentObject.transform.localScale;
            if (equipment.PlacementKind == EquipmentPlacementKind.Conveyor)
            {
                var conveyorTargetX = baseFootprint.x * cellSize * footprintVisualFill * visualScale.x;
                var conveyorTargetZ = baseFootprint.y * cellSize * footprintVisualFill * visualScale.y;
                localScale.x *= conveyorTargetX / size.x;
                localScale.z *= conveyorTargetZ / size.z;
                equipmentObject.transform.localScale = localScale;
                return;
            }

            var targetX = baseFootprint.x * cellSize * footprintVisualFill * visualScale.x;
            var targetZ = baseFootprint.y * cellSize * footprintVisualFill * visualScale.y;
            if (IsWiringControllerEquipment(equipment.EquipmentId))
            {
                var uniformScale = CalculateUniformFootprintScale(size, targetX, targetZ);
                localScale.x *= uniformScale;
                localScale.z *= uniformScale;
                equipmentObject.transform.localScale = localScale;
                return;
            }

            localScale.x *= targetX / size.x;
            localScale.z *= targetZ / size.z;
            equipmentObject.transform.localScale = localScale;
        }

        private static bool IsWiringControllerEquipment(string equipmentId)
        {
            return equipmentId != null &&
                   equipmentId.StartsWith("wiring_controller_", StringComparison.OrdinalIgnoreCase);
        }

        private static float CalculateUniformFootprintScale(Vector3 currentSize, float targetX, float targetZ)
        {
            var xScale = targetX / Mathf.Max(0.001f, currentSize.x);
            var zScale = targetZ / Mathf.Max(0.001f, currentSize.z);
            return Mathf.Min(xScale, zScale);
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

        private void UpdatePlacementGhost()
        {
            UpdateAttachmentTargetVisibility();

            if (placementGhost == null)
                return;

            if (IsAttachmentEquipment(selectedEquipment))
            {
                placementGhost.SetActive(false);
                return;
            }

            if (selectedEquipment != null && selectedEquipment.PlacementKind == EquipmentPlacementKind.ProcessingEquipment)
            {
                UpdateProcessingEquipmentPlacementGhost();
                return;
            }

            var visible = placementModeActive &&
                          selectedEquipment != null &&
                          selectedEquipment.Prefab != null &&
                          placementCursor != null &&
                          placementCursor.HasValidCell;

            if (!visible)
            {
                placementGhost.SetActive(false);
                return;
            }

            var footprint = CurrentFootprint;
            var position = GetFootprintCenterWorldPosition(placementCursor.CurrentGridPosition, footprint);
            var rotation = Quaternion.Euler(0f, rotationIndex * 90f, 0f);
            if (!placementGhost.activeSelf)
                placementGhost.SetActive(true);

            placementGhost.transform.SetPositionAndRotation(position, rotation);
            AlignUpperVisualBoundsToFootprintCenter(placementGhost, position);

        }
        private void UpdateProcessingEquipmentPlacementGhost()
        {
            var footprint = CurrentFootprint;
            PlacedEquipmentHandle targetHandle = null;
            var visible = placementModeActive &&
                          selectedEquipment != null &&
                          selectedEquipment.Prefab != null &&
                          placementCursor != null &&
                          placementCursor.HasGridTarget &&
                          TryResolveProcessingOverlayTarget(
                              placementCursor.CurrentGridPosition,
                              footprint,
                              selectedEquipment,
                              selectedPlacedEquipment != null ? selectedPlacedEquipment.gameObject : null,
                              out targetHandle);

            if (!visible)
            {
                placementGhost.SetActive(false);
                return;
            }

            if (!placementGhost.activeSelf)
                placementGhost.SetActive(true);

            var worldPosition = targetHandle.transform.position;
            placementGhost.transform.SetPositionAndRotation(worldPosition, targetHandle.transform.rotation);
        }

        private void UpdateAttachmentTargetVisibility()
        {
            var showTargets = placementModeActive && IsAttachmentEquipment(selectedEquipment);
            var hoveredTarget = showTargets && TryResolveAttachmentTarget(
                selectedEquipment,
                selectedPlacedEquipment != null ? selectedPlacedEquipment.gameObject : null,
                out var resolvedTarget)
                    ? resolvedTarget
                    : null;

            var handles = UnityEngine.Object.FindObjectsByType<PlacedEquipmentHandle>(FindObjectsInactive.Include);
            foreach (var handle in handles)
            {
                if (handle == null || handle.Equipment == null || !handle.GridPlaced)
                    continue;

                if (!showTargets)
                {
                    HideUnassignedAttachmentTargets(handle);
                    continue;
                }

                UpdateHostAttachmentTarget(handle, selectedEquipment, showTargets, hoveredTarget);
            }
        }

        private static void HideUnassignedAttachmentTargets(PlacedEquipmentHandle host)
        {
            HideUnassignedAttachmentTarget(host, "Sensor");
            HideUnassignedAttachmentTarget(host, "RotaryKnob");

            foreach (var buttonName in ControllerInputButtonObjectNames)
                HideUnassignedAttachmentTarget(host, buttonName);
        }

        private static void HideUnassignedAttachmentTarget(PlacedEquipmentHandle host, string attachmentName)
        {
            var target = FindChildRecursive(host.transform, attachmentName)?.gameObject;
            if (target == null)
                return;

            var targetHandle = target.GetComponent<PlacedEquipmentHandle>();
            SetAttachmentVisualState(
                target,
                targetHandle != null && targetHandle.Equipment != null
                    ? AttachmentVisualState.On
                    : AttachmentVisualState.Off);
        }

        private static void UpdateHostAttachmentTarget(
            PlacedEquipmentHandle host,
            EquipmentPlacementData equipment,
            bool showTargets,
            GameObject hoveredTarget)
        {
            if (host == null || equipment == null || !CanHostAttachment(host, equipment))
                return;

            if (!TryGetAttachmentObjectName(equipment, out var attachmentName))
                return;

            var target = FindChildRecursive(host.transform, attachmentName)?.gameObject;
            if (target == null)
                return;

            var targetHandle = target.GetComponent<PlacedEquipmentHandle>();
            if (targetHandle != null && targetHandle.Equipment != null)
            {
                SetAttachmentVisualState(target, AttachmentVisualState.On);
                return;
            }

            SetAttachmentVisualState(target, showTargets ? AttachmentVisualState.Ghost : AttachmentVisualState.Off, target == hoveredTarget);
        }

        private Vector3 GetFootprintCenterWorldPosition(Vector2Int originCell, Vector2Int footprint)
        {
            if (gridManager == null)
                return Vector3.zero;

            var cellSize = gridManager.CellSize;
            return gridManager.GridToWorldCenter(originCell) +
                   new Vector3((footprint.x - 1) * cellSize * 0.5f, 0f, (footprint.y - 1) * cellSize * 0.5f);
        }

        private static void AlignUpperVisualBoundsToFootprintCenter(GameObject equipmentObject, Vector3 footprintCenter)
        {
            if (!TryCalculateUpperWorldRendererBounds(equipmentObject, out var worldBounds))
                return;

            var offset = footprintCenter - worldBounds.center;
            offset.y = 0f;
            equipmentObject.transform.position += offset;
        }

        private void SnapConveyorToAdjacentConveyors(
            GameObject placed,
            Vector2Int originCell,
            Vector2Int footprint,
            EquipmentPlacementData equipment)
        {
            if (gridManager == null ||
                placed == null ||
                equipment == null ||
                equipment.PlacementKind != EquipmentPlacementKind.Conveyor)
            {
                return;
            }

            foreach (var cell in gridManager.EnumerateFootprintCells(originCell, footprint))
            {
                TrySnapConveyorSide(placed, cell, Vector2Int.left);
                TrySnapConveyorSide(placed, cell, Vector2Int.right);
                TrySnapConveyorSide(placed, cell, Vector2Int.down);
                TrySnapConveyorSide(placed, cell, Vector2Int.up);
            }
        }

        private void TrySnapConveyorSide(GameObject placed, Vector2Int cell, Vector2Int direction)
        {
            var neighborCell = cell + direction;
            if (!gridManager.TryGetOccupiedObject(neighborCell, out var neighbor) ||
                neighbor == null ||
                neighbor == placed ||
                neighbor.GetComponentInChildren<PhysicsConveyorSurface>() == null)
            {
                return;
            }

            if (!TryCalculateUpperWorldRendererBounds(placed, out var placedBounds) ||
                !TryCalculateUpperWorldRendererBounds(neighbor, out var neighborBounds))
            {
                return;
            }

            const float connectionGap = 0.015f;
            var offset = Vector3.zero;
            if (direction == Vector2Int.left)
                offset.x = neighborBounds.max.x + connectionGap - placedBounds.min.x;
            else if (direction == Vector2Int.right)
                offset.x = neighborBounds.min.x - connectionGap - placedBounds.max.x;
            else if (direction == Vector2Int.down)
                offset.z = neighborBounds.max.z + connectionGap - placedBounds.min.z;
            else if (direction == Vector2Int.up)
                offset.z = neighborBounds.min.z - connectionGap - placedBounds.max.z;

            placed.transform.position += offset;
        }

        private static bool TryCalculateUpperWorldRendererBounds(GameObject root, out Bounds worldBounds)
        {
            worldBounds = default;
            if (root == null)
                return false;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var highestRendererTop = float.NegativeInfinity;
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                    highestRendererTop = Mathf.Max(highestRendererTop, renderer.bounds.max.y);
            }

            if (float.IsNegativeInfinity(highestRendererTop))
                return false;

            var hasBounds = false;
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                    continue;

                if (renderer.bounds.max.y < highestRendererTop - 0.35f)
                    continue;

                if (!hasBounds)
                {
                    worldBounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                worldBounds.Encapsulate(renderer.bounds);
            }

            return hasBounds;
        }

        private void ConfigureGhostObject(GameObject ghost)
        {
            foreach (var collider in ghost.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            foreach (var rigidbody in ghost.GetComponentsInChildren<Rigidbody>(true))
            {
                rigidbody.isKinematic = true;
                rigidbody.detectCollisions = false;
            }

            foreach (var behaviour in ghost.GetComponentsInChildren<Behaviour>(true))
                behaviour.enabled = false;

            var material = GetGhostMaterial();
            foreach (var renderer in ghost.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = material;
        }

        private Material GetGhostMaterial()
        {
            if (ghostMaterial != null)
                return ghostMaterial;

            var shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard");
            ghostMaterial = new Material(shader)
            {
                color = ghostColor
            };

            if (ghostMaterial.HasProperty("_BaseColor"))
                ghostMaterial.SetColor("_BaseColor", ghostColor);
            if (ghostMaterial.HasProperty("_Surface"))
                ghostMaterial.SetFloat("_Surface", 1f);
            if (ghostMaterial.HasProperty("_Blend"))
                ghostMaterial.SetFloat("_Blend", 0f);
            ghostMaterial.SetOverrideTag("RenderType", "Transparent");
            ghostMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            ghostMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return ghostMaterial;
        }

        private void DestroyPlacementGhost()
        {
            if (placementGhost == null)
                return;

            if (Application.isPlaying)
                Destroy(placementGhost);
            else
                DestroyImmediate(placementGhost);

            placementGhost = null;
            ghostSourcePrefab = null;
        }

        private void OnDestroy()
        {
            DestroyPlacementGhost();
            if (ghostMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(ghostMaterial);
            else
                DestroyImmediate(ghostMaterial);
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                rotationIndex = Math.Clamp(rotationIndex, 0, 3);
        }
    }

}
