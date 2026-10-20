using System;
using LadderLab.PLC;
using LadderLab.Wiring;
using UnityEngine;

namespace LadderLab.VR
{
    [DisallowMultipleComponent]
    public sealed class VrButtonVisualFeedback : MonoBehaviour, IPlcAddressBindable
    {
        [SerializeField] private PlcScanController scanController;
        [SerializeField] private string inputAddress = "X0";
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Light glowLight;
        [SerializeField] private Vector3 pressedLocalOffset = new(0f, -0.035f, 0f);
        [SerializeField] private float followSpeed = 18f;
        [SerializeField] private float glowIntensity = 2.4f;
        [SerializeField] private Color activeColor = new(0.25f, 1f, 0.35f, 1f);

        private Vector3 releasedLocalPosition;
        private MaterialPropertyBlock propertyBlock;
        private Color releasedColor = Color.white;
        private bool initialized;
        private PlcAddressBinding addressBinding;

        public string InputAddress => inputAddress;
        public bool IsOn { get; private set; }

        public void Configure(
            PlcScanController targetScanController,
            string targetInputAddress,
            Renderer renderer,
            Light lightSource,
            Color onColor,
            Vector3 localPressOffset)
        {
            scanController = targetScanController;
            inputAddress = targetInputAddress;
            targetRenderer = renderer;
            glowLight = lightSource;
            activeColor = onColor;
            pressedLocalOffset = localPressOffset;
            ValidateInputAddress();
            InitializeIfNeeded();
            RefreshVisual(true);
        }

        public void SetInputAddress(string targetInputAddress)
        {
            ValidateInputAddress(targetInputAddress);
            inputAddress = targetInputAddress;
            RefreshVisual(true);
        }

        public void ConfigureBinding(PlcAddressBinding targetAddressBinding)
        {
            addressBinding = targetAddressBinding;
            scanController = targetAddressBinding != null ? targetAddressBinding.ScanController : scanController;
            inputAddress = targetAddressBinding != null ? targetAddressBinding.Address : inputAddress;
            ValidateInputAddress(inputAddress);
            RefreshVisual(true);
        }

        public void RefreshVisual(bool instant = false)
        {
            InitializeIfNeeded();

            var inputOn = addressBinding != null
                ? addressBinding.Value
                : scanController != null && !string.IsNullOrWhiteSpace(inputAddress) && scanController.Backend.GetBit(inputAddress);
            IsOn = inputOn;

            var targetPosition = releasedLocalPosition + (inputOn ? pressedLocalOffset : Vector3.zero);
            transform.localPosition = instant
                ? targetPosition
                : Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * followSpeed);

            if (targetRenderer != null)
            {
                var visibleColor = inputOn ? activeColor : releasedColor;
                SetRendererColor(visibleColor, inputOn ? activeColor * 1.6f : Color.black);
            }

            if (glowLight != null)
            {
                glowLight.color = activeColor;
                glowLight.intensity = inputOn ? glowIntensity : 0f;
                glowLight.enabled = inputOn;
            }
        }

        private void OnValidate()
        {
            inputAddress = inputAddress?.Trim() ?? "";
        }

        private void Awake()
        {
            InitializeIfNeeded();
            RefreshVisual(true);
        }

        private void Update()
        {
            RefreshVisual();
        }

        private void InitializeIfNeeded()
        {
            if (initialized)
                return;

            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();

            releasedLocalPosition = transform.localPosition;

            if (targetRenderer != null)
            {
                propertyBlock = new MaterialPropertyBlock();
                releasedColor = GetMaterialColor(targetRenderer.sharedMaterial);
            }

            if (glowLight != null)
            {
                glowLight.intensity = 0f;
                glowLight.enabled = false;
            }

            initialized = true;
        }

        private void ValidateInputAddress()
        {
            ValidateInputAddress(inputAddress);
        }

        private static void ValidateInputAddress(string targetInputAddress)
        {
            if (string.IsNullOrWhiteSpace(targetInputAddress))
                return;

            var address = PlcAddress.Parse(targetInputAddress);
            if (address.Area != PlcMemoryArea.Input)
                throw new ArgumentException($"{nameof(VrButtonVisualFeedback)} can only read PLC X input addresses.", nameof(inputAddress));
        }

        private static Color GetMaterialColor(Material material)
        {
            if (material == null)
                return Color.white;

            if (material.HasProperty("_BaseColor"))
                return material.GetColor("_BaseColor");

            return material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
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
