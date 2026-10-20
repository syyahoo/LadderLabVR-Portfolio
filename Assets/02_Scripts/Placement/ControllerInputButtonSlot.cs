using System;
using UnityEngine;

namespace LadderLab.Placement
{
    [DisallowMultipleComponent]
    public sealed class ControllerInputButtonSlot : MonoBehaviour
    {
        private const int MaxSlotCount = 3;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private int slotIndex = -1;
        [SerializeField] private string acceptedEquipmentId;
        [SerializeField] private GameObject buttonObject;
        [SerializeField] private GameObject assignedButton;
        [SerializeField] private EquipmentPlacementData assignedEquipment;
        [SerializeField] private Vector3 buttonLocalOffset = new(0f, 0.04f, 0f);
        [SerializeField] private Vector3 buttonLocalEulerAngles = Vector3.zero;
        [SerializeField] private Color idleColor = new(0.24f, 0.7f, 1f, 0.35f);
        [SerializeField] private Color hoverColor = new(0.1f, 0.85f, 1f, 0.95f);

        private MaterialPropertyBlock propertyBlock;

        public int SlotIndex => slotIndex;
        public bool IsOccupied => assignedButton != null;
        public Vector3 ButtonWorldPosition => buttonObject != null ? buttonObject.transform.position : transform.TransformPoint(buttonLocalOffset);
        public Quaternion ButtonWorldRotation => buttonObject != null ? buttonObject.transform.rotation : transform.rotation * Quaternion.Euler(buttonLocalEulerAngles);

        public void Configure(int targetSlotIndex)
        {
            Configure(targetSlotIndex, acceptedEquipmentId, buttonObject != null ? buttonObject : gameObject);
        }

        public void Configure(int targetSlotIndex, string targetEquipmentId, GameObject targetButtonObject)
        {
            slotIndex = Mathf.Max(0, targetSlotIndex);
            acceptedEquipmentId = targetEquipmentId;
            buttonObject = buttonObject != null ? buttonObject : targetButtonObject != null ? targetButtonObject : gameObject;
        }

        public GameObject ActivateButton(EquipmentPlacementData equipment)
        {
            if (!CanAcceptButton(equipment, null))
                return null;

            var target = buttonObject != null ? buttonObject : gameObject;
            target.SetActive(true);
            target.transform.SetParent(transform, false);
            target.transform.SetPositionAndRotation(ButtonWorldPosition, ButtonWorldRotation);
            target.name = $"{equipment.DisplayName} ({name})";
            SetButtonVisualState(true, false, true);
            return target;
        }

        public void AssignButton(GameObject button, EquipmentPlacementData equipment)
        {
            assignedButton = button;
            assignedEquipment = equipment;
        }

        public bool CanAcceptButton(EquipmentPlacementData equipment, PlacedEquipmentHandle movingHandle)
        {
            if (equipment == null)
                return false;

            if (slotIndex < 0 || slotIndex >= MaxSlotCount)
                return false;

            if (assignedButton != null && (movingHandle == null || assignedButton != movingHandle.gameObject))
                return false;

            if (!string.IsNullOrWhiteSpace(acceptedEquipmentId) &&
                !string.Equals(acceptedEquipmentId, equipment.EquipmentId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !HasDuplicateButtonTypeOnController(equipment, movingHandle);
        }

        public void ClearAssignedButton(PlacedEquipmentHandle handle)
        {
            if (handle == null || assignedButton == handle.gameObject)
            {
                assignedButton = null;
                assignedEquipment = null;
                SetButtonVisualState(false, false, false);
            }
        }

        public void SetVisible(bool visible)
        {
            SetVisualState(visible, false);
        }

        public void SetVisualState(bool visible, bool highlighted)
        {
            foreach (var slotRenderer in GetComponentsInChildren<Renderer>(true))
            {
                if (IsButtonChild(slotRenderer.transform))
                    continue;

                slotRenderer.enabled = visible;
                if (slotRenderer.gameObject != gameObject)
                    slotRenderer.gameObject.SetActive(visible);

                if (visible)
                    ApplySlotColor(slotRenderer, highlighted ? hoverColor : idleColor);
            }

            foreach (var slotCollider in GetComponentsInChildren<Collider>(true))
            {
                if (IsButtonChild(slotCollider.transform))
                    continue;

                if (slotCollider.gameObject != gameObject)
                    slotCollider.gameObject.SetActive(visible);

                slotCollider.enabled = visible;
            }

            SetButtonVisualState(visible, highlighted, IsOccupied);
        }

        private bool HasDuplicateButtonTypeOnController(EquipmentPlacementData equipment, PlacedEquipmentHandle movingHandle)
        {
            var controllerRoot = ResolveControllerRoot();
            if (controllerRoot == null)
                return false;

            var slots = controllerRoot.GetComponentsInChildren<ControllerInputButtonSlot>(true);
            foreach (var slot in slots)
            {
                if (slot == null || slot == this || slot.assignedEquipment == null)
                    continue;

                if (movingHandle != null && slot.assignedButton == movingHandle.gameObject)
                    continue;

                if (string.Equals(slot.assignedEquipment.EquipmentId, equipment.EquipmentId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private Transform ResolveControllerRoot()
        {
            var controllerHandle = GetComponentInParent<PlacedEquipmentHandle>();
            return controllerHandle != null ? controllerHandle.transform : transform.parent;
        }

        private bool IsButtonChild(Transform target)
        {
            if (buttonObject != null && target.IsChildOf(buttonObject.transform))
                return true;

            return assignedButton != null && target.IsChildOf(assignedButton.transform);
        }

        private void SetButtonVisualState(bool visible, bool highlighted, bool solid)
        {
            var target = buttonObject != null ? buttonObject : assignedButton;
            if (target == null)
                return;

            target.SetActive(visible || solid);
            foreach (var buttonRenderer in target.GetComponentsInChildren<Renderer>(true))
            {
                buttonRenderer.enabled = visible || solid;
                if (solid)
                {
                    buttonRenderer.SetPropertyBlock(null);
                    continue;
                }

                if (visible)
                    ApplySlotColor(buttonRenderer, highlighted ? hoverColor : idleColor);
            }

            foreach (var buttonCollider in target.GetComponentsInChildren<Collider>(true))
                buttonCollider.enabled = solid || visible;
        }

        private void ApplySlotColor(Renderer slotRenderer, Color color)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            slotRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            slotRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
