using System;
using LadderLab.PLC;
using LadderLab.VR;
using LadderLab.Wiring;
using UnityEngine;

namespace LadderLab.Devices
{
    [DisallowMultipleComponent]
    public sealed class ConveyorBeltVisual : MonoBehaviour, IPlcAddressBindable
    {
        [SerializeField] private PlcScanController scanController;
        [SerializeField] private PlcAddressBinding addressBinding;
        [SerializeField] private string outputAddress = "Y0";
        [SerializeField] private Renderer beltRenderer;
        [SerializeField] private Transform[] rollers = Array.Empty<Transform>();
        [SerializeField] private Vector2 textureScrollPerSecond = new(0.8f, 0f);
        [SerializeField] private bool hideLegacyRollerObjects = true;

        private Material runtimeMaterial;
        private Vector2 textureOffset;
        private bool ownsRuntimeMaterial;

        public bool IsMoving
        {
            get
            {
                if (addressBinding != null)
                    return addressBinding.Value;

                return scanController != null && scanController.Backend.GetBit(outputAddress);
            }
        }
        public Vector2 TextureOffset => textureOffset;

        public void Configure(
            PlcScanController targetScanController,
            string targetOutputAddress,
            Renderer targetBeltRenderer,
            Transform[] targetRollers)
        {
            scanController = targetScanController;
            outputAddress = targetOutputAddress;
            beltRenderer = targetBeltRenderer;
            rollers = targetRollers ?? Array.Empty<Transform>();
            ValidateOutputAddress();
            HideLegacyRollers();
            RefreshVisual();
        }

        public void ConfigureBinding(PlcAddressBinding targetAddressBinding)
        {
            addressBinding = targetAddressBinding;
            scanController = targetAddressBinding != null ? targetAddressBinding.ScanController : scanController;
            outputAddress = targetAddressBinding != null ? targetAddressBinding.Address : outputAddress;
            ValidateOutputAddress();
            RefreshVisual();
        }

        public void Tick(float deltaTime)
        {
            if (!IsMoving)
            {
                RefreshVisual();
                return;
            }

            textureOffset.x += textureScrollPerSecond.x * deltaTime;
            RefreshVisual();
        }

        private void Awake()
        {
            HideLegacyRollers();
            EnsureRuntimeMaterial();
            RefreshVisual();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (!ownsRuntimeMaterial || runtimeMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(runtimeMaterial);
            else
                DestroyImmediate(runtimeMaterial);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(outputAddress))
                outputAddress = "Y0";

            textureScrollPerSecond.y = 0f;
        }

        private void RefreshVisual()
        {
            EnsureRuntimeMaterial();
            if (runtimeMaterial == null)
                return;

            SetTextureOffset(runtimeMaterial, textureOffset);
        }

        private void EnsureRuntimeMaterial()
        {
            if (runtimeMaterial != null || beltRenderer == null)
                return;

            var source = Application.isPlaying ? beltRenderer.material : beltRenderer.sharedMaterial;
            if (source == null)
                return;

            runtimeMaterial = new Material(source);
            ownsRuntimeMaterial = true;

            if (Application.isPlaying)
                beltRenderer.material = runtimeMaterial;
            else
                beltRenderer.sharedMaterial = runtimeMaterial;
        }

        private void HideLegacyRollers()
        {
            if (!hideLegacyRollerObjects)
                return;

            foreach (var roller in rollers)
            {
                if (roller != null)
                    roller.gameObject.SetActive(false);
            }
        }

        private void ValidateOutputAddress()
        {
            var targetAddress = addressBinding != null ? addressBinding.Address : outputAddress;
            if (string.IsNullOrWhiteSpace(targetAddress))
                return;

            var address = PlcAddress.Parse(targetAddress);
            if (address.Area != PlcMemoryArea.Output)
                throw new ArgumentException($"{nameof(ConveyorBeltVisual)} can only read PLC Y output addresses.", nameof(outputAddress));
        }

        private static void SetTextureOffset(Material material, Vector2 offset)
        {
            if (material.HasProperty("_BaseMap"))
                material.SetTextureOffset("_BaseMap", offset);

            if (material.HasProperty("_MainTex"))
                material.SetTextureOffset("_MainTex", offset);
        }
    }
}
