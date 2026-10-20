using System.Collections.Generic;
using LadderLab.Devices;
using UnityEngine;

namespace LadderLab.Placement
{
    [DisallowMultipleComponent]
    public sealed class ConveyorSpeedDialSlot : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private GameObject assignedDial;
        [SerializeField] private GameObject dialObject;
        [SerializeField] private bool controllerSlot;
        [SerializeField] private Vector3 dialLocalOffset = new(0f, 0.08f, 0f);
        [SerializeField] private Vector3 dialLocalEulerAngles = Vector3.zero;
        [SerializeField] private Color idleColor = new(0.95f, 0.72f, 0.18f, 0.36f);
        [SerializeField] private Color hoverColor = new(1f, 0.74f, 0.08f, 0.95f);

        private readonly List<PhysicsConveyorSurface> targetConveyors = new();
        private MaterialPropertyBlock propertyBlock;
        private float currentSpeedMultiplier = 1f;

        public bool CanAcceptDial => controllerSlot && assignedDial == null;
        public Vector3 DialWorldPosition => dialObject != null ? dialObject.transform.position : transform.TransformPoint(dialLocalOffset);
        public Quaternion DialWorldRotation => dialObject != null ? dialObject.transform.rotation : transform.rotation * Quaternion.Euler(dialLocalEulerAngles);

        public void ConfigureForConveyor(PhysicsConveyorSurface conveyor)
        {
            ConfigureForConveyor(conveyor, Vector3.forward);
        }

        public void ConfigureForConveyor(PhysicsConveyorSurface conveyor, Vector3 conveyorLocalDirection)
        {
            ConfigureDisabledForConveyor();
        }

        public void ConfigureDisabledForConveyor()
        {
            controllerSlot = false;
            ResetTargetConveyorsToDefault();
            targetConveyors.Clear();
            assignedDial = null;
            currentSpeedMultiplier = 1f;
            ResolveDialObject();
            SetVisible(false);
        }

        public void ConfigureForController()
        {
            controllerSlot = true;
            ResetTargetConveyorsToDefault();
            targetConveyors.Clear();
            currentSpeedMultiplier = 1f;
            ResolveDialObject();
            SetVisible(false);
        }

        public void AddTargetConveyor(PhysicsConveyorSurface conveyor)
        {
            if (conveyor == null || targetConveyors.Contains(conveyor))
                return;

            targetConveyors.Add(conveyor);
            conveyor.SetSpeedMultiplier(currentSpeedMultiplier);
        }

        public void AssignDial(GameObject dial)
        {
            assignedDial = dial;
            if (dialObject == null)
                dialObject = dial;
        }

        public GameObject ActivateDial(EquipmentPlacementData equipment)
        {
            if (!CanAcceptDial)
                return null;

            ResolveDialObject();
            var target = dialObject;
            if (target == null)
                return null;

            target.SetActive(true);
            target.transform.SetParent(transform, false);
            target.transform.SetPositionAndRotation(DialWorldPosition, DialWorldRotation);
            target.name = $"{equipment.DisplayName} ({name})";
            SetDialVisualState(true, false, true);
            return target;
        }

        public bool CanAcceptDialFor(PlacedEquipmentHandle handle)
        {
            return controllerSlot && (assignedDial == null || (handle != null && assignedDial == handle.gameObject));
        }

        public void ClearAssignedDial(PlacedEquipmentHandle handle)
        {
            if (handle == null || assignedDial == handle.gameObject)
            {
                assignedDial = null;
                ApplySpeedMultiplier(1f);
                SetDialVisualState(false, false, false);
            }
        }

        public void ApplySpeedMultiplier(float multiplier)
        {
            currentSpeedMultiplier = Mathf.Max(0f, multiplier);
            foreach (var conveyor in targetConveyors)
                conveyor?.SetSpeedMultiplier(currentSpeedMultiplier);
        }

        public void SetVisible(bool visible)
        {
            SetVisualState(visible, false);
        }

        public void SetVisualState(bool visible, bool highlighted)
        {
            foreach (var slotRenderer in GetComponentsInChildren<Renderer>(true))
            {
                if (IsDialChild(slotRenderer.transform))
                    continue;

                slotRenderer.enabled = visible;
                if (slotRenderer.gameObject != gameObject)
                    slotRenderer.gameObject.SetActive(visible);

                if (visible)
                    ApplySlotColor(slotRenderer, highlighted ? hoverColor : idleColor);
            }

            foreach (var slotCollider in GetComponentsInChildren<Collider>(true))
            {
                if (!IsDialChild(slotCollider.transform))
                {
                    if (slotCollider.gameObject != gameObject)
                        slotCollider.gameObject.SetActive(visible);

                    slotCollider.enabled = visible;
                }
            }

            SetDialVisualState(visible, highlighted, assignedDial != null);
        }

        private bool IsDialChild(Transform target)
        {
            if (dialObject != null && target.IsChildOf(dialObject.transform))
                return true;

            return assignedDial != null && target.IsChildOf(assignedDial.transform);
        }

        private void ApplySlotColor(Renderer slotRenderer, Color color)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            slotRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            slotRenderer.SetPropertyBlock(propertyBlock);
        }

        private void ResetTargetConveyorsToDefault()
        {
            foreach (var conveyor in targetConveyors)
                conveyor?.SetSpeedMultiplier(1f);
        }

        private void ResolveDialObject()
        {
            if (dialObject != null)
                return;

            var rotaryKnob = transform.Find("RotaryKnob");
            if (rotaryKnob != null)
            {
                dialObject = rotaryKnob.gameObject;
                return;
            }

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null && renderer.gameObject != gameObject)
                {
                    dialObject = renderer.transform.root == transform ? renderer.gameObject : renderer.transform.parent != null ? renderer.transform.parent.gameObject : renderer.gameObject;
                    return;
                }
            }
        }

        private void SetDialVisualState(bool visible, bool highlighted, bool solid)
        {
            ResolveDialObject();
            if (dialObject == null)
                return;

            dialObject.SetActive(visible || solid);
            foreach (var dialRenderer in dialObject.GetComponentsInChildren<Renderer>(true))
            {
                dialRenderer.enabled = visible || solid;
                if (solid)
                {
                    dialRenderer.SetPropertyBlock(null);
                    continue;
                }

                if (visible)
                    ApplySlotColor(dialRenderer, highlighted ? hoverColor : idleColor);
            }

            foreach (var dialCollider in dialObject.GetComponentsInChildren<Collider>(true))
                dialCollider.enabled = visible || solid;
        }
    }
}
