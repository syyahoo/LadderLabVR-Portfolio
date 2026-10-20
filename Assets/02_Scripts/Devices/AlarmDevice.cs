using System;
using LadderLab.PLC;
using LadderLab.VR;
using LadderLab.Wiring;
using UnityEngine;

namespace LadderLab.Devices
{
    [DisallowMultipleComponent]
    public sealed class AlarmDevice : MonoBehaviour, IPlcAddressBindable
    {
        [SerializeField] private PlcScanController scanController;
        [SerializeField] private PlcAddressBinding addressBinding;
        [SerializeField] private string outputAddress = "Y2";
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Light targetLight;
        [SerializeField] private Color offColor = new(0.16f, 0.04f, 0.04f, 1f);
        [SerializeField] private Color onColor = new(1f, 0.05f, 0.02f, 1f);
        [SerializeField, Min(0f)] private float onIntensity = 4f;

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
            Renderer targetAlarmRenderer,
            Light targetAlarmLight)
        {
            scanController = targetScanController;
            outputAddress = targetOutputAddress;
            targetRenderer = targetAlarmRenderer;
            targetLight = targetAlarmLight;
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
            SetRendererColor(powered ? onColor : offColor);

            if (targetLight == null)
                return;

            targetLight.enabled = powered;
            targetLight.color = onColor;
            targetLight.intensity = powered ? onIntensity : 0f;
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
                outputAddress = "Y2";
        }

        private void ValidateOutputAddress()
        {
            var targetAddress = addressBinding != null ? addressBinding.Address : outputAddress;
            if (string.IsNullOrWhiteSpace(targetAddress))
                return;

            var address = PlcAddress.Parse(targetAddress);
            if (address.Area != PlcMemoryArea.Output)
                throw new ArgumentException($"{nameof(AlarmDevice)} can only read PLC Y output addresses.", nameof(outputAddress));
        }

        private void SetRendererColor(Color color)
        {
            if (targetRenderer == null)
                return;

            propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private void OnDestroy()
        {
            if (targetRenderer != null)
                targetRenderer.SetPropertyBlock(null);
        }
    }
}
