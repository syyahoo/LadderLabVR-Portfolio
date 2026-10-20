using System;
using System.Collections.Generic;
using LadderLab.PLC;
using LadderLab.VR;
using LadderLab.Wiring;
using UnityEngine;

namespace LadderLab.Devices
{
    [DisallowMultipleComponent]
    public sealed class SensorDevice : MonoBehaviour, IPlcAddressBindable
    {
        [SerializeField] private PlcScanController scanController;
        [SerializeField] private PlcAddressBinding addressBinding;
        [SerializeField] private string inputAddress = "X2";
        [SerializeField] private string requiredTag = "";

        private readonly HashSet<Collider> detectedColliders = new();

        public bool IsDetected
        {
            get
            {
                if (addressBinding != null)
                    return addressBinding.Value;

                return scanController != null && scanController.Backend.GetBit(inputAddress);
            }
        }

        public void Configure(PlcScanController targetScanController, string targetInputAddress, string targetRequiredTag)
        {
            scanController = targetScanController;
            inputAddress = targetInputAddress;
            requiredTag = targetRequiredTag ?? "";
            ValidateInputAddress();
            RefreshInput();
        }

        public void ConfigureBinding(PlcAddressBinding targetAddressBinding)
        {
            addressBinding = targetAddressBinding;
            scanController = targetAddressBinding != null ? targetAddressBinding.ScanController : scanController;
            inputAddress = targetAddressBinding != null ? targetAddressBinding.Address : inputAddress;
            ValidateInputAddress();
            RefreshInput();
        }

        public void SetDetected(bool detected)
        {
            if (addressBinding != null)
            {
                ValidateInputAddress();
                addressBinding.SetValue(detected);
                return;
            }

            if (scanController == null)
                throw new InvalidOperationException($"{nameof(SensorDevice)} requires a PLC scan controller.");

            ValidateInputAddress();
            scanController.Backend.SetBit(inputAddress, detected);
        }

        public void ClearDetection()
        {
            detectedColliders.Clear();
            if (scanController != null)
                SetDetected(false);
        }

        public void NotifyAreaTriggerEnter(Collider other)
        {
            HandleTriggerEnter(other);
        }

        public void NotifyAreaTriggerExit(Collider other)
        {
            HandleTriggerExit(other);
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleTriggerEnter(other);
        }

        private void OnTriggerExit(Collider other)
        {
            HandleTriggerExit(other);
        }

        private void HandleTriggerEnter(Collider other)
        {
            if (!Accepts(other))
                return;

            detectedColliders.Add(other);
            RefreshInput();
        }

        private void HandleTriggerExit(Collider other)
        {
            if (!Accepts(other))
                return;

            detectedColliders.Remove(other);
            RefreshInput();
        }

        private void OnDisable()
        {
            ClearDetection();
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(inputAddress))
                inputAddress = "X2";
        }

        private bool Accepts(Collider other)
        {
            return other != null && (string.IsNullOrWhiteSpace(requiredTag) || other.CompareTag(requiredTag));
        }

        private void RefreshInput()
        {
            if (scanController != null)
                SetDetected(detectedColliders.Count > 0);
        }

        private void ValidateInputAddress()
        {
            var targetAddress = addressBinding != null ? addressBinding.Address : inputAddress;
            if (string.IsNullOrWhiteSpace(targetAddress))
                return;

            var address = PlcAddress.Parse(targetAddress);
            if (address.Area != PlcMemoryArea.Input)
                throw new ArgumentException($"{nameof(SensorDevice)} can only write PLC X input addresses.", nameof(inputAddress));
        }
    }
}
