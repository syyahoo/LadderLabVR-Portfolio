using System;
using LadderLab.PLC;
using UnityEngine;

namespace LadderLab.VR
{
    [DisallowMultipleComponent]
    public sealed class PlcAddressBinding : MonoBehaviour
    {
        [SerializeField] private PlcScanController scanController;
        [SerializeField] private string address = "";
        [SerializeField] private PlcMemoryArea[] allowedAreas = Array.Empty<PlcMemoryArea>();
        [SerializeField] private string[] assignableAddresses = Array.Empty<string>();

        public event Action AddressChanged;

        public PlcScanController ScanController => scanController;
        public string Address => address;
        public bool HasController => scanController != null;
        public bool IsAssigned => !string.IsNullOrWhiteSpace(address);
        public bool Value => scanController != null && IsAssigned && scanController.Backend.GetBit(address);

        public void Configure(
            PlcScanController targetScanController,
            string targetAddress,
            PlcMemoryArea[] targetAllowedAreas,
            string[] targetAssignableAddresses)
        {
            scanController = targetScanController;
            allowedAreas = targetAllowedAreas ?? Array.Empty<PlcMemoryArea>();
            assignableAddresses = targetAssignableAddresses ?? Array.Empty<string>();
            SetAddress(targetAddress);
        }

        public void SetAddress(string targetAddress)
        {
            targetAddress = targetAddress?.Trim() ?? "";
            ValidateAddress(targetAddress);
            address = targetAddress;
            AddressChanged?.Invoke();
        }

        public void SetValue(bool value)
        {
            if (scanController == null)
                throw new InvalidOperationException($"{nameof(PlcAddressBinding)} requires a PLC scan controller.");

            if (!IsAssigned)
                return;

            ValidateAddress(address);
            scanController.Backend.SetBit(address, value);
        }

        public void CycleAddress()
        {
            if (assignableAddresses == null || assignableAddresses.Length == 0)
                return;

            var currentIndex = Array.IndexOf(assignableAddresses, address);
            SetAddress(assignableAddresses[(currentIndex + 1) % assignableAddresses.Length]);
        }

        private void OnValidate()
        {
            address = address?.Trim() ?? "";
        }

        private void ValidateAddress(string targetAddress)
        {
            if (string.IsNullOrWhiteSpace(targetAddress))
                return;

            var parsedAddress = PlcAddress.Parse(targetAddress);
            if (allowedAreas == null || allowedAreas.Length == 0)
                return;

            foreach (var area in allowedAreas)
            {
                if (parsedAddress.Area == area)
                    return;
            }

            throw new ArgumentException($"{nameof(PlcAddressBinding)} address {targetAddress} is outside the allowed PLC areas.");
        }
    }
}
