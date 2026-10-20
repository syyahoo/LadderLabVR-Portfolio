using UnityEngine;
using System.Collections.Generic;
using LadderLab.Devices;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LadderLab.Placement
{
    [DisallowMultipleComponent]
    public sealed class ConveyorSpeedDial : MonoBehaviour
    {
        [SerializeField] private XRSimpleInteractable interactable;
        [SerializeField] private Transform rotatingHandle;
        [SerializeField] private string rotatingHandleName = "Grip.001";
        [SerializeField] private float minLocalYAngle = -135f;
        [SerializeField] private float maxLocalYAngle = 135f;
        [SerializeField, Min(0f)] private float minSpeedMultiplier = 0.5f;
        [SerializeField, Min(0f)] private float maxSpeedMultiplier = 2f;
        [SerializeField] private int speedIndex = 1;
        [SerializeField] private float[] speedMultipliers = { 0.5f, 1f, 1.5f, 2f };

        private Transform activeInteractorTransform;
        private readonly List<PhysicsConveyorSurface> targetConveyors = new();
        private float grabStartInteractorAngle;
        private float grabStartLocalYAngle;
        private float currentMultiplier = 1f;

        public float CurrentMultiplier => currentMultiplier;

        public void Configure()
        {
            EnsureInteractable();
            ResolveRotatingHandle();
            ClampHandleRotationToRange();
            ApplyMultiplierFromRotation();
            ApplyCurrentMultiplier();
        }

        public void AddTargetConveyor(PhysicsConveyorSurface conveyor)
        {
            if (conveyor == null || targetConveyors.Contains(conveyor))
                return;

            targetConveyors.Add(conveyor);
            conveyor.SetSpeedMultiplier(CurrentMultiplier);
        }

        private void Awake()
        {
            EnsureInteractable();
            ResolveRotatingHandle();
            ClampHandleRotationToRange();
            ApplyMultiplierFromRotation();
        }

        private void OnEnable()
        {
            EnsureInteractable();
            if (interactable != null)
            {
                interactable.selectEntered.AddListener(HandleSelectEntered);
                interactable.selectExited.AddListener(HandleSelectExited);
            }
        }

        private void OnDisable()
        {
            if (interactable != null)
            {
                interactable.selectEntered.RemoveListener(HandleSelectEntered);
                interactable.selectExited.RemoveListener(HandleSelectExited);
            }

            activeInteractorTransform = null;
        }

        private void OnMouseDown()
        {
            CycleSpeed();
        }

        private void Update()
        {
            if (activeInteractorTransform != null)
                UpdateGrabRotation();
            else
                ClampHandleRotationToRange();
        }

        private void HandleSelectEntered(SelectEnterEventArgs eventArgs)
        {
            BeginGrab(eventArgs);
        }

        private void HandleSelectExited(SelectExitEventArgs eventArgs)
        {
            activeInteractorTransform = null;
        }

        public void CycleSpeed()
        {
            if (speedMultipliers == null || speedMultipliers.Length == 0)
                return;

            speedIndex = (speedIndex + 1) % speedMultipliers.Length;
            currentMultiplier = Mathf.Max(0f, speedMultipliers[Mathf.Clamp(speedIndex, 0, speedMultipliers.Length - 1)]);
            ApplyRotationFromMultiplier();
            ApplyCurrentMultiplier();
        }

        private void ApplyCurrentMultiplier()
        {
            for (var index = targetConveyors.Count - 1; index >= 0; index--)
            {
                var conveyor = targetConveyors[index];
                if (conveyor == null)
                {
                    targetConveyors.RemoveAt(index);
                    continue;
                }

                conveyor.SetSpeedMultiplier(CurrentMultiplier);
            }
        }

        private void EnsureInteractable()
        {
            if (interactable == null)
                interactable = GetComponent<XRSimpleInteractable>();
        }

        private void ResolveRotatingHandle()
        {
            if (rotatingHandle != null)
                return;

            var handle = transform.Find(rotatingHandleName);
            if (handle != null)
            {
                rotatingHandle = handle;
                return;
            }

            rotatingHandle = transform;
        }

        private void BeginGrab(SelectEnterEventArgs eventArgs)
        {
            ResolveRotatingHandle();
            activeInteractorTransform = eventArgs.interactorObject?.transform;
            if (activeInteractorTransform == null || !TryGetInteractorLocalAngle(activeInteractorTransform, out grabStartInteractorAngle))
                return;

            grabStartLocalYAngle = NormalizeSignedAngle(rotatingHandle.localEulerAngles.y);
        }

        private void UpdateGrabRotation()
        {
            if (activeInteractorTransform == null || rotatingHandle == null)
                return;

            if (!TryGetInteractorLocalAngle(activeInteractorTransform, out var currentInteractorAngle))
                return;

            var deltaAngle = Mathf.DeltaAngle(grabStartInteractorAngle, currentInteractorAngle);
            var nextAngle = Mathf.Clamp(grabStartLocalYAngle + deltaAngle, minLocalYAngle, maxLocalYAngle);
            SetHandleLocalYAngle(nextAngle);
            ApplyMultiplierFromRotation();
            ApplyCurrentMultiplier();
        }

        private bool TryGetInteractorLocalAngle(Transform interactorTransform, out float angle)
        {
            angle = 0f;
            if (interactorTransform == null)
                return false;

            var localPoint = transform.InverseTransformPoint(interactorTransform.position);
            localPoint.y = 0f;
            if (localPoint.sqrMagnitude <= 0.0001f)
                return false;

            angle = Mathf.Atan2(localPoint.x, localPoint.z) * Mathf.Rad2Deg;
            return true;
        }

        private void SetHandleLocalYAngle(float localYAngle)
        {
            var euler = rotatingHandle.localEulerAngles;
            euler.y = localYAngle;
            rotatingHandle.localEulerAngles = euler;
        }

        private void ApplyMultiplierFromRotation()
        {
            ResolveRotatingHandle();
            var angle = Mathf.Clamp(NormalizeSignedAngle(rotatingHandle.localEulerAngles.y), minLocalYAngle, maxLocalYAngle);
            if (angle >= 0f)
            {
                var t = maxLocalYAngle <= 0f ? 0f : Mathf.Clamp01(angle / maxLocalYAngle);
                currentMultiplier = Mathf.Lerp(1f, Mathf.Max(0f, maxSpeedMultiplier), t);
                return;
            }

            var negativeRange = Mathf.Abs(minLocalYAngle);
            var negativeT = negativeRange <= 0f ? 0f : Mathf.Clamp01(Mathf.Abs(angle) / negativeRange);
            currentMultiplier = Mathf.Lerp(1f, Mathf.Max(0f, minSpeedMultiplier), negativeT);
        }

        private void ApplyRotationFromMultiplier()
        {
            ResolveRotatingHandle();
            var multiplier = Mathf.Max(0f, currentMultiplier);
            float angle;
            if (multiplier >= 1f)
            {
                var range = Mathf.Max(0.0001f, maxSpeedMultiplier - 1f);
                angle = Mathf.Lerp(0f, maxLocalYAngle, Mathf.Clamp01((multiplier - 1f) / range));
            }
            else
            {
                var range = Mathf.Max(0.0001f, 1f - minSpeedMultiplier);
                angle = Mathf.Lerp(0f, minLocalYAngle, Mathf.Clamp01((1f - multiplier) / range));
            }

            SetHandleLocalYAngle(angle);
        }

        private void ClampHandleRotationToRange()
        {
            ResolveRotatingHandle();
            if (rotatingHandle == null)
                return;

            var currentAngle = NormalizeSignedAngle(rotatingHandle.localEulerAngles.y);
            var clampedAngle = Mathf.Clamp(currentAngle, minLocalYAngle, maxLocalYAngle);
            if (Mathf.Approximately(currentAngle, clampedAngle))
                return;

            SetHandleLocalYAngle(clampedAngle);
            ApplyMultiplierFromRotation();
            ApplyCurrentMultiplier();
        }

        private static float NormalizeSignedAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f)
                angle -= 360f;
            else if (angle < -180f)
                angle += 360f;

            return angle;
        }
    }
}
