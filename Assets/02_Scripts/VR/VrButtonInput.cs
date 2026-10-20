using System;
using LadderLab.PLC;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace LadderLab.VR
{
    [DisallowMultipleComponent]
    public sealed class VrButtonInput : MonoBehaviour
    {
        [SerializeField] private PlcScanController scanController;
        [SerializeField] private XRSimpleInteractable interactable;
        [SerializeField] private string inputAddress = "X0";
        [SerializeField] private bool releaseResetsInput = true;
        [SerializeField] private bool enableHapticFeedback = true;
        [SerializeField, Range(0f, 1f)] private float hapticAmplitude = 0.35f;
        [SerializeField, Min(0f)] private float hapticDuration = 0.08f;
        [SerializeField] private bool enableClickSound = true;
        [SerializeField, Range(0f, 1f)] private float clickVolume = 0.22f;
        [SerializeField, Min(20f)] private float clickFrequency = 950f;

        private AudioSource audioSource;
        private AudioClip clickClip;

        public string InputAddress => inputAddress;
        public PlcScanController ScanController => scanController;
        public bool HapticFeedbackEnabled => enableHapticFeedback;
        public float HapticAmplitude => hapticAmplitude;
        public float HapticDuration => hapticDuration;
        public bool ClickSoundEnabled => enableClickSound;
        public bool ClickAudioConfigured => enableClickSound && GetComponent<AudioSource>() != null;

        public void Configure(PlcScanController targetScanController, string targetInputAddress, bool resetOnRelease)
        {
            scanController = targetScanController;
            inputAddress = targetInputAddress;
            releaseResetsInput = resetOnRelease;
            ValidateInputAddress();
        }

        public void SetInputAddress(string targetInputAddress)
        {
            ValidateInputAddress(targetInputAddress);
            if (scanController != null && !string.IsNullOrWhiteSpace(inputAddress))
                scanController.Backend.SetBit(inputAddress, false);

            inputAddress = targetInputAddress;
        }

        public void ConfigureHaptics(bool enabled, float amplitude, float duration)
        {
            enableHapticFeedback = enabled;
            hapticAmplitude = Mathf.Clamp01(amplitude);
            hapticDuration = Mathf.Max(0f, duration);
        }

        public void ConfigureClickSound(bool enabled, float targetVolume, float targetFrequency)
        {
            enableClickSound = enabled;
            clickVolume = Mathf.Clamp01(targetVolume);
            clickFrequency = Mathf.Max(20f, targetFrequency);
            EnsureClickAudio();
        }

        public void Press()
        {
            SetInput(true);
        }

        public void Release()
        {
            if (releaseResetsInput)
                SetInput(false);
        }

        public void SetInput(bool value)
        {
            if (scanController == null)
                throw new InvalidOperationException($"{nameof(VrButtonInput)} requires a PLC scan controller.");

            ValidateInputAddress();
            scanController.Backend.SetBit(inputAddress, value);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(inputAddress))
                inputAddress = "X0";
        }

        private void Awake()
        {
            if (interactable == null)
                interactable = GetComponent<XRSimpleInteractable>();

            EnsureClickAudio();
        }

        private void OnEnable()
        {
            if (interactable == null)
                interactable = GetComponent<XRSimpleInteractable>();

            if (interactable == null)
                return;

            interactable.selectEntered.AddListener(HandleSelectEntered);
            interactable.selectExited.AddListener(HandleSelectExited);
        }

        private void OnDisable()
        {
            if (interactable == null)
                return;

            interactable.selectEntered.RemoveListener(HandleSelectEntered);
            interactable.selectExited.RemoveListener(HandleSelectExited);
        }

        private void HandleSelectEntered(SelectEnterEventArgs eventArgs)
        {
            Press();
            TryPlayHapticFeedback(eventArgs);
            TryPlayClickSound();
        }

        private void HandleSelectExited(SelectExitEventArgs eventArgs)
        {
            Release();
        }

        private void TryPlayHapticFeedback(SelectEnterEventArgs eventArgs)
        {
            if (!enableHapticFeedback || hapticAmplitude <= 0f || hapticDuration <= 0f)
                return;

            if (eventArgs?.interactorObject is XRBaseInputInteractor inputInteractor)
                inputInteractor.SendHapticImpulse(hapticAmplitude, hapticDuration);
        }

        private void TryPlayClickSound()
        {
            if (!enableClickSound || clickVolume <= 0f)
                return;

            EnsureClickAudio();
            if (audioSource != null && clickClip != null)
                audioSource.PlayOneShot(clickClip, clickVolume);
        }

        private void EnsureClickAudio()
        {
            if (!enableClickSound)
                return;

            if (audioSource == null)
            {
                var existingSource = GetComponent<AudioSource>();
                audioSource = existingSource != null ? existingSource : gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 0.2f;
            audioSource.maxDistance = 3f;

            if (clickClip == null)
                clickClip = CreateClickClip($"{name} Click", clickFrequency);
        }

        private void OnDestroy()
        {
            if (clickClip == null)
                return;

            if (Application.isPlaying)
                Destroy(clickClip);
            else
                DestroyImmediate(clickClip);
        }

        private void ValidateInputAddress()
        {
            ValidateInputAddress(inputAddress);
        }

        private static void ValidateInputAddress(string targetInputAddress)
        {
            var address = PlcAddress.Parse(targetInputAddress);
            if (address.Area != PlcMemoryArea.Input)
                throw new ArgumentException($"{nameof(VrButtonInput)} can only write PLC X input addresses.", nameof(inputAddress));
        }

        private static AudioClip CreateClickClip(string clipName, float frequency)
        {
            const int sampleRate = 22050;
            var sampleCount = Mathf.CeilToInt(sampleRate * 0.045f);
            var samples = new float[sampleCount];
            for (var index = 0; index < samples.Length; index++)
            {
                var time = index / (float)sampleRate;
                var decay = 1f - index / (float)samples.Length;
                samples[index] = Mathf.Sin(2f * Mathf.PI * frequency * time) * 0.35f * decay;
            }

            var clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
