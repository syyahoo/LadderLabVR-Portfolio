using System;
using LadderLab.PLC;
using UnityEngine;

namespace LadderLab.Wiring
{
    [Serializable]
    public sealed class WiringEndpointAssignment
    {
        [SerializeField] private string endpointId;
        [SerializeField] private string displayName;
        [SerializeField] private WiringDeviceKind deviceKind = WiringDeviceKind.Generic;
        [SerializeField] private WiringSignalDirection signalDirection;
        [SerializeField] private string initialAddress;
        [SerializeField] private PlcMemoryArea[] allowedAreas = Array.Empty<PlcMemoryArea>();
        [SerializeField] private string[] assignableAddresses = Array.Empty<string>();

        public string EndpointId => endpointId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? endpointId : displayName;
        public WiringDeviceKind DeviceKind => deviceKind;
        public WiringSignalDirection SignalDirection => signalDirection;
        public string InitialAddress => initialAddress;
        public PlcMemoryArea[] AllowedAreas => allowedAreas;
        public string[] AssignableAddresses => assignableAddresses;

        public bool Matches(string targetEndpointId)
        {
            return !string.IsNullOrWhiteSpace(endpointId) &&
                   string.Equals(endpointId, targetEndpointId, StringComparison.Ordinal);
        }
    }
}
