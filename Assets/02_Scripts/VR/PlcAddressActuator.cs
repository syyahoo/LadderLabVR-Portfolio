using System.Collections;
using LadderLab.Wiring;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LadderLab.VR
{
    public enum PlcActuatorMode
    {
        Momentary,
        Toggle,
        Pulse
    }

    [DisallowMultipleComponent]
    public sealed class PlcAddressActuator : MonoBehaviour, IPlcAddressBindable
    {
        [SerializeField] private PlcAddressBinding binding;
        [SerializeField] private XRSimpleInteractable interactable;
        [SerializeField] private PlcActuatorMode mode = PlcActuatorMode.Momentary;
        [SerializeField, Min(0.01f)] private float pulseDuration = 0.12f;

        private Coroutine pulseRoutine;
        private bool momentaryActive;

        public PlcActuatorMode Mode => mode;

        public void Configure(PlcAddressBinding targetBinding, PlcActuatorMode targetMode)
        {
            binding = targetBinding;
            mode = targetMode;
        }

        public void ConfigureBinding(PlcAddressBinding targetBinding)
        {
            binding = targetBinding;
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();
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

            ReleaseMomentaryInput();
        }

        private void OnMouseDown()
        {
            Activate();
        }

        private void OnMouseUp()
        {
            if (mode == PlcActuatorMode.Momentary)
                ReleaseMomentaryInput();
        }

        private void HandleSelectEntered(SelectEnterEventArgs eventArgs)
        {
            Activate();
        }

        private void HandleSelectExited(SelectExitEventArgs eventArgs)
        {
            if (mode == PlcActuatorMode.Momentary)
                ReleaseMomentaryInput();
        }

        public void Activate()
        {
            if (!CanWriteBinding())
                return;

            switch (mode)
            {
                case PlcActuatorMode.Momentary:
                    SetBindingValue(true);
                    momentaryActive = true;
                    break;
                case PlcActuatorMode.Toggle:
                    SetBindingValue(!binding.Value);
                    break;
                case PlcActuatorMode.Pulse:
                    if (pulseRoutine != null)
                        StopCoroutine(pulseRoutine);

                    pulseRoutine = StartCoroutine(Pulse());
                    break;
            }
        }

        private IEnumerator Pulse()
        {
            SetBindingValue(true);
            yield return new WaitForSeconds(pulseDuration);
            SetBindingValue(false);
            pulseRoutine = null;
        }

        private void ReleaseMomentaryInput()
        {
            if (!momentaryActive)
                return;

            SetBindingValue(false);
            momentaryActive = false;
        }

        private bool CanWriteBinding()
        {
            return binding != null && binding.HasController && binding.IsAssigned;
        }

        private void SetBindingValue(bool value)
        {
            if (CanWriteBinding())
                binding.SetValue(value);
        }

        private void EnsureReferences()
        {
            if (binding == null)
                binding = GetComponent<PlcAddressBinding>();

            if (interactable == null)
                interactable = GetComponent<XRSimpleInteractable>();
        }
    }
}
