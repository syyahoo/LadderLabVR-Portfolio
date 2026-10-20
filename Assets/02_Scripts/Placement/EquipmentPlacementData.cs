using LadderLab.Wiring;
using UnityEngine;

namespace LadderLab.Placement
{
    [CreateAssetMenu(fileName = "EquipmentPlacementData", menuName = "LadderLab/Placement/Equipment")]
    public sealed class EquipmentPlacementData : ScriptableObject
    {
        [SerializeField] private string equipmentId = "equipment";
        [SerializeField] private string displayName = "Equipment";
        [SerializeField] private EquipmentPlacementKind placementKind = EquipmentPlacementKind.Generic;
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(1)] private int footprintWidth = 1;
        [SerializeField, Min(1)] private int footprintDepth = 1;
        [SerializeField] private Vector2 visualFootprintScale = Vector2.one;
        [SerializeField] private bool canRotate = true;
        [SerializeField] private Vector3 conveyorLocalDirection = Vector3.forward;
        [SerializeField, Min(0f)] private float conveyorBeltSpeed = 1.15f;

        public string EquipmentId => equipmentId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? equipmentId : displayName;
        public EquipmentPlacementKind PlacementKind => placementKind;
        public GameObject Prefab => prefab;
        public Vector2Int Footprint => new(Mathf.Max(1, footprintWidth), Mathf.Max(1, footprintDepth));
        public Vector2 VisualFootprintScale => new(
            visualFootprintScale.x > 0f ? visualFootprintScale.x : 1f,
            visualFootprintScale.y > 0f ? visualFootprintScale.y : 1f);
        public bool CanRotate => canRotate;
        public Vector3 ConveyorLocalDirection => conveyorLocalDirection == Vector3.zero ? Vector3.forward : conveyorLocalDirection;
        public float ConveyorBeltSpeed => Mathf.Max(0f, conveyorBeltSpeed);
        public bool CreatesWiringEndpoint => placementKind is EquipmentPlacementKind.Conveyor or EquipmentPlacementKind.Sensor or EquipmentPlacementKind.Lamp;
        public WiringDeviceKind WiringDeviceKind => ResolveWiringDeviceKind(placementKind);
        public WiringSignalDirection SignalDirection => ResolveSignalDirection(placementKind);

        public void Configure(
            string targetEquipmentId,
            string targetDisplayName,
            GameObject targetPrefab,
            Vector2Int targetFootprint,
            bool targetCanRotate)
        {
            Configure(
                targetEquipmentId,
                targetDisplayName,
                EquipmentPlacementKind.Generic,
                targetPrefab,
                targetFootprint,
                targetCanRotate,
                Vector3.forward,
                1.15f);
        }

        public void Configure(
            string targetEquipmentId,
            string targetDisplayName,
            EquipmentPlacementKind targetPlacementKind,
            GameObject targetPrefab,
            Vector2Int targetFootprint,
            bool targetCanRotate,
            Vector3 targetConveyorLocalDirection,
            float targetConveyorBeltSpeed)
        {
            equipmentId = targetEquipmentId?.Trim() ?? "";
            displayName = targetDisplayName?.Trim() ?? "";
            placementKind = targetPlacementKind;
            prefab = targetPrefab;
            footprintWidth = Mathf.Max(1, targetFootprint.x);
            footprintDepth = Mathf.Max(1, targetFootprint.y);
            visualFootprintScale = Vector2.one;
            canRotate = targetCanRotate;
            conveyorLocalDirection = targetConveyorLocalDirection == Vector3.zero ? Vector3.forward : targetConveyorLocalDirection;
            conveyorBeltSpeed = Mathf.Max(0f, targetConveyorBeltSpeed);
        }

        public Vector2Int GetFootprint(int rotationIndex)
        {
            var footprint = Footprint;
            return Mathf.Abs(rotationIndex) % 2 == 0
                ? footprint
                : new Vector2Int(footprint.y, footprint.x);
        }

        private void OnValidate()
        {
            footprintWidth = Mathf.Max(1, footprintWidth);
            footprintDepth = Mathf.Max(1, footprintDepth);
            if (visualFootprintScale.x <= 0f)
                visualFootprintScale.x = 1f;
            if (visualFootprintScale.y <= 0f)
                visualFootprintScale.y = 1f;
            equipmentId = equipmentId?.Trim() ?? "";
            displayName = displayName?.Trim() ?? "";
            if (conveyorLocalDirection == Vector3.zero)
                conveyorLocalDirection = Vector3.forward;

            conveyorBeltSpeed = Mathf.Max(0f, conveyorBeltSpeed);
        }

        private static WiringDeviceKind ResolveWiringDeviceKind(EquipmentPlacementKind targetPlacementKind)
        {
            return targetPlacementKind switch
            {
                EquipmentPlacementKind.Conveyor => WiringDeviceKind.Conveyor,
                EquipmentPlacementKind.Sensor => WiringDeviceKind.Sensor,
                EquipmentPlacementKind.Lamp => WiringDeviceKind.Lamp,
                _ => WiringDeviceKind.Generic
            };
        }

        private static WiringSignalDirection ResolveSignalDirection(EquipmentPlacementKind targetPlacementKind)
        {
            return targetPlacementKind == EquipmentPlacementKind.Sensor
                ? WiringSignalDirection.DeviceWritesPlcInput
                : WiringSignalDirection.DeviceReadsPlcOutput;
        }
    }
}
