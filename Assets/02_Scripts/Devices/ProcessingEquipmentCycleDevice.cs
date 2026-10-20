using System;
using System.Collections;
using LadderLab.PLC;
using LadderLab.VR;
using LadderLab.Wiring;
using UnityEngine;

namespace LadderLab.Devices
{
    [DisallowMultipleComponent]
    public sealed class ProcessingEquipmentCycleDevice : MonoBehaviour
    {
        [SerializeField] private PlcAddressBinding startBinding;
        [SerializeField] private PlcAddressBinding completeBinding;
        [SerializeField] private Animator animator;
        [SerializeField] private Animation legacyAnimation;
        [SerializeField] private string animatorStateName = "";
        [SerializeField] private string legacyClipName = "";
        [SerializeField, Min(0.1f)] private float cycleDurationSeconds = 10f;

        private Coroutine cycleRoutine;
        private bool previousStartSignal;

        public void ConfigureStartBinding(PlcAddressBinding targetBinding)
        {
            startBinding = targetBinding;
            ValidateOutputBinding(startBinding);
        }

        public void ConfigureCompleteBinding(PlcAddressBinding targetBinding)
        {
            completeBinding = targetBinding;
            ValidateInputBinding(completeBinding);
            SetCompleteSignal(false);
        }

        public void ConfigureAnimation(Animator targetAnimator, Animation targetLegacyAnimation)
        {
            animator = targetAnimator != null ? targetAnimator : animator;
            legacyAnimation = targetLegacyAnimation != null ? targetLegacyAnimation : legacyAnimation;
        }

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);

            if (legacyAnimation == null)
                legacyAnimation = GetComponentInChildren<Animation>(true);

            StopAnimation();
        }

        private void Update()
        {
            var startSignal = startBinding != null && startBinding.Value;
            if (!startSignal)
            {
                previousStartSignal = false;
                if (cycleRoutine == null)
                    SetCompleteSignal(false);
                return;
            }

            if (!previousStartSignal && cycleRoutine == null)
                cycleRoutine = StartCoroutine(RunCycle());

            previousStartSignal = true;
        }

        private void OnDisable()
        {
            if (cycleRoutine != null)
            {
                StopCoroutine(cycleRoutine);
                cycleRoutine = null;
            }

            previousStartSignal = false;
            StopAnimation();
            SetCompleteSignal(false);
        }

        // 가공 사이클 실행
        private IEnumerator RunCycle()
        {
            SetCompleteSignal(false);
            PlayAnimation();
            yield return new WaitForSeconds(cycleDurationSeconds);
            StopAnimation();
            SetCompleteSignal(true);
            cycleRoutine = null;
        }

        private void PlayAnimation()
        {
            if (animator != null)
            {
                animator.speed = 1f;
                if (!string.IsNullOrWhiteSpace(animatorStateName))
                    animator.Play(animatorStateName, 0, 0f);
            }

            if (legacyAnimation == null)
                return;

            var clipName = ResolveLegacyClipName();
            if (string.IsNullOrWhiteSpace(clipName))
                return;

            var state = legacyAnimation[clipName];
            if (state != null && state.length > 0f)
                state.speed = state.length / cycleDurationSeconds;

            legacyAnimation.Play(clipName);
        }

        private void StopAnimation()
        {
            if (animator != null)
                animator.speed = 0f;

            if (legacyAnimation != null)
                legacyAnimation.Stop();
        }

        private string ResolveLegacyClipName()
        {
            if (!string.IsNullOrWhiteSpace(legacyClipName))
                return legacyClipName;

            if (legacyAnimation.clip != null)
                return legacyAnimation.clip.name;

            foreach (AnimationState state in legacyAnimation)
                return state.name;

            return "";
        }

        private void SetCompleteSignal(bool value)
        {
            if (completeBinding == null || !completeBinding.IsAssigned || !completeBinding.HasController)
                return;

            ValidateInputBinding(completeBinding);
            completeBinding.SetValue(value);
        }

        private static void ValidateOutputBinding(PlcAddressBinding binding)
        {
            ValidateBindingArea(binding, PlcMemoryArea.Output, "start");
        }

        private static void ValidateInputBinding(PlcAddressBinding binding)
        {
            ValidateBindingArea(binding, PlcMemoryArea.Input, "complete");
        }

        private static void ValidateBindingArea(PlcAddressBinding binding, PlcMemoryArea expectedArea, string bindingName)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.Address))
                return;

            var parsed = PlcAddress.Parse(binding.Address);
            if (parsed.Area != expectedArea)
                throw new ArgumentException($"{nameof(ProcessingEquipmentCycleDevice)} {bindingName} binding expects {expectedArea} address.");
        }
    }

#if false
    [DisallowMultipleComponent]
    public sealed class ProcessingEquipmentStartBinding : MonoBehaviour, IPlcAddressBindable
    {
        [SerializeField] private ProcessingEquipmentCycleDevice cycleDevice;

        public void Configure(ProcessingEquipmentCycleDevice targetCycleDevice)
        {
            cycleDevice = targetCycleDevice;
        }

        public void ConfigureBinding(PlcAddressBinding targetAddressBinding)
        {
            if (cycleDevice == null)
                cycleDevice = GetComponentInParent<ProcessingEquipmentCycleDevice>();

            cycleDevice?.ConfigureStartBinding(targetAddressBinding);
        }
    }

    [DisallowMultipleComponent]
    public sealed class ProcessingEquipmentCompleteBinding : MonoBehaviour, IPlcAddressBindable
    {
        [SerializeField] private ProcessingEquipmentCycleDevice cycleDevice;

        public void Configure(ProcessingEquipmentCycleDevice targetCycleDevice)
        {
            cycleDevice = targetCycleDevice;
        }

        public void ConfigureBinding(PlcAddressBinding targetAddressBinding)
        {
            if (cycleDevice == null)
                cycleDevice = GetComponentInParent<ProcessingEquipmentCycleDevice>();

            cycleDevice?.ConfigureCompleteBinding(targetAddressBinding);
        }
    }
#endif
}
