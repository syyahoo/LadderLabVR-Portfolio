using System;
using System.Collections.Generic;
using System.Linq;
using LadderLab.PLC;
using UnityEngine;

namespace LadderLab.Wiring
{
    [DisallowMultipleComponent]
    public sealed class WiringCabinetController : MonoBehaviour
    {
        [SerializeField] private PlcScanController scanController;
        [SerializeField] private WiringCabinetProfile profile;
        [SerializeField] private Transform endpointSearchRoot;
        [SerializeField] private bool autoFindEndpointsInSearchRoot = true;
        [SerializeField] private WiringDeviceEndpoint[] endpoints = Array.Empty<WiringDeviceEndpoint>();

        private readonly Dictionary<string, WiringDeviceEndpoint> endpointById = new(StringComparer.Ordinal);

        public event Action<WiringDeviceEndpoint> EndpointAddressChanged;

        public IReadOnlyCollection<WiringDeviceEndpoint> Endpoints => endpointById.Values;

        public void Configure(
            PlcScanController targetScanController,
            WiringCabinetProfile targetProfile,
            IEnumerable<WiringDeviceEndpoint> targetEndpoints)
        {
            scanController = targetScanController;
            profile = targetProfile;
            endpoints = targetEndpoints?.Where(endpoint => endpoint != null).ToArray() ?? Array.Empty<WiringDeviceEndpoint>();
            Rebuild();
        }

        public void Rebuild()
        {
            endpointById.Clear();
            var resolvedEndpoints = ResolveEndpoints();
            foreach (var endpoint in resolvedEndpoints)
                RegisterEndpoint(endpoint);

            ApplyProfile();
        }

        public bool TryAssignAddress(string endpointId, string address)
        {
            if (!endpointById.TryGetValue(endpointId, out var endpoint))
                return false;

            endpoint.SetAddress(address);
            return true;
        }

        public bool TryGetEndpoint(string endpointId, out WiringDeviceEndpoint endpoint)
        {
            return endpointById.TryGetValue(endpointId, out endpoint);
        }

        private void Awake()
        {
            Rebuild();
        }

        private void OnEnable()
        {
            foreach (var endpoint in endpointById.Values)
                endpoint.AddressChanged += HandleEndpointAddressChanged;
        }

        private void OnDisable()
        {
            foreach (var endpoint in endpointById.Values)
                endpoint.AddressChanged -= HandleEndpointAddressChanged;
        }

        private IEnumerable<WiringDeviceEndpoint> ResolveEndpoints()
        {
            if (!autoFindEndpointsInSearchRoot)
                return endpoints.Where(endpoint => endpoint != null);

            var searchRoot = endpointSearchRoot != null ? endpointSearchRoot : transform;
            var found = searchRoot.GetComponentsInChildren<WiringDeviceEndpoint>(true);
            return endpoints
                .Concat(found)
                .Where(endpoint => endpoint != null)
                .Distinct();
        }

        private void RegisterEndpoint(WiringDeviceEndpoint endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint.EndpointId))
                throw new InvalidOperationException($"{nameof(WiringDeviceEndpoint)} on '{endpoint.name}' needs a non-empty endpoint id.");

            if (endpointById.ContainsKey(endpoint.EndpointId))
                throw new InvalidOperationException($"Duplicate wiring endpoint id: {endpoint.EndpointId}");

            endpointById.Add(endpoint.EndpointId, endpoint);
        }

        private void ApplyProfile()
        {
            if (scanController == null)
                throw new InvalidOperationException($"{nameof(WiringCabinetController)} requires a PLC scan controller.");

            if (profile != null && profile.ContainsDuplicateEndpointIds())
                throw new InvalidOperationException($"{nameof(WiringCabinetProfile)} contains duplicate endpoint ids.");

            foreach (var endpoint in endpointById.Values)
            {
                WiringEndpointAssignment assignment = null;
                profile?.TryGetAssignment(endpoint.EndpointId, out assignment);
                endpoint.Apply(scanController, assignment);
            }
        }

        private void HandleEndpointAddressChanged(WiringDeviceEndpoint endpoint)
        {
            EndpointAddressChanged?.Invoke(endpoint);
        }
    }
}
