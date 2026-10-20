using System;
using LadderLab.PLC;
using LadderLab.VR;
using LadderLab.Wiring;
using UnityEngine;

namespace LadderLab.Devices
{
    [DisallowMultipleComponent]
    public sealed class LampDevice : MonoBehaviour, IPlcAddressBindable
    {
        [SerializeField] private PlcScanController scanController;
        [SerializeField] private PlcAddressBinding addressBinding;
        [SerializeField] private string outputAddress = "Y1";
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Light targetLight;
        [SerializeField] private Color offColor = new(0.16f, 0.16f, 0.16f, 1f);
        [SerializeField] private Color onColor = new(1f, 0.84f, 0.18f, 1f);
        [SerializeField, Min(0f)] private float onIntensity = 3f;
        [SerializeField, Min(0f)] private float emissionMultiplier = 1.8f;
        [SerializeField, Min(0f)] private float pulseAmplitude = 0.25f;
        [SerializeField, Min(0f)] private float pulseSpeed = 5f;

        private MaterialPropertyBlock propertyBlock;

        public bool IsPowered
        {
            get
            {
                if (addressBinding != null)
                    return addressBinding.Value;

                return scanController != null && scanController.Backend.GetBit(outputAddress);
            }
        }

        public void Configure(
            PlcScanController targetScanController,
            string targetOutputAddress,
            Renderer targetLampRenderer,
            Light targetLampLight)
        {
            scanController = targetScanController;
            outputAddress = targetOutputAddress;
            targetRenderer = targetLampRenderer;
            targetLight = targetLampLight;
            ValidateOutputAddress();
            Refresh();
        }

        public void ConfigureBinding(PlcAddressBinding targetAddressBinding)
        {
            addressBinding = targetAddressBinding;
            scanController = targetAddressBinding != null ? targetAddressBinding.ScanController : scanController;
            outputAddress = targetAddressBinding != null ? targetAddressBinding.Address : outputAddress;
            ValidateOutputAddress();
            Refresh();
        }

        public void Refresh()
        {
            var powered = IsPowered;
            SetRendererColor(powered ? onColor : offColor, powered ? onColor * emissionMultiplier : Color.black);

            if (targetLight == null)
                return;

            targetLight.enabled = powered;
            targetLight.color = onColor;
            targetLight.intensity = powered ? BuildPoweredIntensity() : 0f;
        }

        private void Awake()
        {
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(outputAddress))
                outputAddress = "Y1";
        }

        private void ValidateOutputAddress()
        {
            var targetAddress = addressBinding != null ? addressBinding.Address : outputAddress;
            if (string.IsNullOrWhiteSpace(targetAddress))
                return;

            var address = PlcAddress.Parse(targetAddress);
            if (address.Area != PlcMemoryArea.Output)
                throw new ArgumentException($"{nameof(LampDevice)} can only read PLC Y output addresses.", nameof(outputAddress));
        }

        private float BuildPoweredIntensity()
        {
            if (pulseAmplitude <= 0f || pulseSpeed <= 0f)
                return onIntensity;

            var pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
            return Mathf.Max(0f, onIntensity * pulse);
        }

        private void SetRendererColor(Color color, Color emissionColor)
        {
            if (targetRenderer == null)
                return;

            propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            propertyBlock.SetColor("_EmissionColor", emissionColor);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private void OnDestroy()
        {
            if (targetRenderer != null)
                targetRenderer.SetPropertyBlock(null);
        }
    }
}
