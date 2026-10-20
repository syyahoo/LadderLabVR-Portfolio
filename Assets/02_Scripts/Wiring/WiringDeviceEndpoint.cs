using System;
using LadderLab.PLC;
using LadderLab.VR;
using UnityEngine;

namespace LadderLab.Wiring
{
    [DisallowMultipleComponent]
    public sealed class WiringDeviceEndpoint : MonoBehaviour
    {
        [SerializeField] private string endpointId;
        [SerializeField] private string displayName;
        [SerializeField] private WiringDeviceKind deviceKind = WiringDeviceKind.Generic;
        [SerializeField] private WiringSignalDirection signalDirection;
        [SerializeField] private PlcAddressBinding addressBinding;
        [SerializeField] private string fallbackInitialAddress;
        [SerializeField] private PlcMemoryArea[] fallbackAllowedAreas = Array.Empty<PlcMemoryArea>();
        [SerializeField] private string[] fallbackAssignableAddresses = Array.Empty<string>();
        [SerializeField] private Transform bindableSearchRoot;
        [SerializeField] private bool applyBindingToChildBindables = true;

        public event Action<WiringDeviceEndpoint> AddressChanged;

        public string EndpointId => endpointId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? endpointId : displayName;
        public WiringDeviceKind DeviceKind => deviceKind;
        public WiringSignalDirection SignalDirection => signalDirection;
        public PlcAddressBinding AddressBinding => addressBinding;
        public string Address => addressBinding != null ? addressBinding.Address : fallbackInitialAddress;
        public bool IsConfigured => addressBinding != null && addressBinding.HasController && !string.IsNullOrWhiteSpace(addressBinding.Address);
        public bool Value => addressBinding != null && addressBinding.Value;

        public void ConfigureMetadata(
            string targetEndpointId,
            string targetDisplayName,
            WiringDeviceKind targetDeviceKind,
            WiringSignalDirection targetSignalDirection)
        {
            endpointId = string.IsNullOrWhiteSpace(targetEndpointId) ? name : targetEndpointId.Trim();
            displayName = targetDisplayName?.Trim() ?? "";
            deviceKind = targetDeviceKind;
            signalDirection = targetSignalDirection;
        }

        public void Apply(PlcScanController scanController, WiringEndpointAssignment assignment)
        {
            if (assignment != null)
            {
                deviceKind = assignment.DeviceKind;
                signalDirection = assignment.SignalDirection;
                displayName = assignment.DisplayName;
                ConfigureBinding(scanController, assignment.InitialAddress, assignment.AllowedAreas, assignment.AssignableAddresses);
                return;
            }

            ConfigureBinding(scanController, fallbackInitialAddress, fallbackAllowedAreas, fallbackAssignableAddresses);
        }

        public void ConfigureBinding(
            PlcScanController scanController,
            string initialAddress,
            PlcMemoryArea[] allowedAreas,
            string[] assignableAddresses)
        {
            EnsureBinding();
            addressBinding.Configure(scanController, initialAddress, allowedAreas, assignableAddresses);
            ValidateDirection(addressBinding.Address);
            ApplyBindingToBindables();
            AddressChanged?.Invoke(this);
        }

        public void SetAddress(string address)
        {
            EnsureBinding();
            ValidateDirection(address);
            addressBinding.SetAddress(address);
            AddressChanged?.Invoke(this);
        }

        public void CycleAddress()
        {
            EnsureBinding();
            addressBinding.CycleAddress();
            ValidateDirection(addressBinding.Address);
            AddressChanged?.Invoke(this);
        }

        public void WriteInputValue(bool value)
        {
            if (signalDirection != WiringSignalDirection.DeviceWritesPlcInput)
                throw new InvalidOperationException($"{nameof(WiringDeviceEndpoint)} '{EndpointId}' is not configured as a PLC input endpoint.");

            EnsureBinding();
            addressBinding.SetValue(value);
        }

        public bool ReadOutputValue()
        {
            if (signalDirection != WiringSignalDirection.DeviceReadsPlcOutput)
                throw new InvalidOperationException($"{nameof(WiringDeviceEndpoint)} '{EndpointId}' is not configured as a PLC output endpoint.");

            return Value;
        }

        private void Awake()
        {
            EnsureBinding();
        }

        private void OnEnable()
        {
            EnsureBinding();
            if (addressBinding != null)
                addressBinding.AddressChanged += HandleBindingAddressChanged;
        }

        private void OnDisable()
        {
            if (addressBinding != null)
                addressBinding.AddressChanged -= HandleBindingAddressChanged;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(endpointId))
                endpointId = name;
        }

        private void EnsureBinding()
        {
            if (addressBinding != null)
                return;

            addressBinding = GetComponent<PlcAddressBinding>();
            if (addressBinding == null)
                addressBinding = gameObject.AddComponent<PlcAddressBinding>();
        }

        private void ApplyBindingToBindables()
        {
            if (!applyBindingToChildBindables || addressBinding == null)
                return;

            var root = bindableSearchRoot != null ? bindableSearchRoot : transform;
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null || behaviour == this)
                    continue;

                if (behaviour is IPlcAddressBindable bindable)
                    bindable.ConfigureBinding(addressBinding);
            }
        }

        private void HandleBindingAddressChanged()
        {
            ValidateDirection(addressBinding.Address);
            AddressChanged?.Invoke(this);
        }

        private void ValidateDirection(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return;

            var parsed = PlcAddress.Parse(address);
            var expectedArea = signalDirection == WiringSignalDirection.DeviceWritesPlcInput
                ? PlcMemoryArea.Input
                : PlcMemoryArea.Output;

            if (parsed.Area != expectedArea)
                throw new ArgumentException($"Endpoint '{EndpointId}' expects {expectedArea} address, but received {address}.");
        }
    }
}
