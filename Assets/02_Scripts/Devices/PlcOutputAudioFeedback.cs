using System;
using LadderLab.PLC;
using LadderLab.VR;
using LadderLab.Wiring;
using UnityEngine;

namespace LadderLab.Devices
{
    [DisallowMultipleComponent]
    public sealed class PlcOutputAudioFeedback : MonoBehaviour, IPlcAddressBindable
    {
        [SerializeField] private PlcScanController scanController;
        [SerializeField] private PlcAddressBinding addressBinding;
        [SerializeField] private string outputAddress = "Y0";
        [SerializeField, Min(20f)] private float toneFrequency = 180f;
        [SerializeField, Range(0f, 1f)] private float volume = 0.18f;
        [SerializeField] private bool loopWhilePowered = true;

        private AudioSource audioSource;
        private AudioClip toneClip;

        public string OutputAddress => outputAddress;
        public bool IsPowered
        {
            get
            {
                if (addressBinding != null)
                    return addressBinding.Value;

                return scanController != null && scanController.Backend.GetBit(outputAddress);
            }
        }
        public bool IsAudioConfigured => audioSource != null && toneClip != null;

        public void Configure(
            PlcScanController targetScanController,
            string targetOutputAddress,
            float targetToneFrequency,
            float targetVolume,
            bool targetLoopWhilePowered)
        {
            scanController = targetScanController;
            outputAddress = targetOutputAddress;
            toneFrequency = Mathf.Max(20f, targetToneFrequency);
            volume = Mathf.Clamp01(targetVolume);
            loopWhilePowered = targetLoopWhilePowered;
            ValidateOutputAddress();
            EnsureAudio();
            Refresh();
        }

        public void ConfigureBinding(PlcAddressBinding targetAddressBinding)
        {
            addressBinding = targetAddressBinding;
            scanController = targetAddressBinding != null ? targetAddressBinding.ScanController : scanController;
            outputAddress = targetAddressBinding != null ? targetAddressBinding.Address : outputAddress;
            ValidateOutputAddress();
            EnsureAudio();
            Refresh();
        }

        public void Refresh()
        {
            EnsureAudio();
            if (audioSource == null)
                return;

            audioSource.volume = volume;
            audioSource.loop = loopWhilePowered;
            var powered = IsPowered;

            if (powered && !audioSource.isPlaying)
                audioSource.Play();
            else if (!powered && audioSource.isPlaying)
                audioSource.Stop();
        }

        private void Awake()
        {
            EnsureAudio();
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            if (toneClip == null)
                return;

            if (Application.isPlaying)
                Destroy(toneClip);
            else
                DestroyImmediate(toneClip);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(outputAddress))
                outputAddress = "Y0";
        }

        private void EnsureAudio()
        {
            if (audioSource == null)
            {
                var existingSource = GetComponent<AudioSource>();
                audioSource = existingSource != null ? existingSource : gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 0.35f;
            audioSource.maxDistance = 5f;

            if (toneClip == null)
            {
                toneClip = CreateToneClip($"{name} Tone", toneFrequency, 0.25f);
                audioSource.clip = toneClip;
            }
        }

        private void ValidateOutputAddress()
        {
            var targetAddress = addressBinding != null ? addressBinding.Address : outputAddress;
            if (string.IsNullOrWhiteSpace(targetAddress))
                return;

            var address = PlcAddress.Parse(targetAddress);
            if (address.Area != PlcMemoryArea.Output)
                throw new ArgumentException($"{nameof(PlcOutputAudioFeedback)} can only read PLC Y output addresses.", nameof(outputAddress));
        }

        private static AudioClip CreateToneClip(string clipName, float frequency, float duration)
        {
            const int sampleRate = 22050;
            var sampleCount = Mathf.Max(1, Mathf.CeilToInt(sampleRate * duration));
            var samples = new float[sampleCount];
            for (var index = 0; index < samples.Length; index++)
            {
                var time = index / (float)sampleRate;
                var fadeIn = Mathf.Clamp01(index / 64f);
                var fadeOut = Mathf.Clamp01((samples.Length - index) / 64f);
                samples[index] = Mathf.Sin(2f * Mathf.PI * frequency * time) * 0.25f * fadeIn * fadeOut;
            }

            var clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
