using TMPro;
using UnityEngine;

namespace LadderLab.Wiring
{
    [DisallowMultipleComponent]
    public sealed class WiringCabinetTerminal : MonoBehaviour
    {
        [SerializeField] private WiringCabinetController cabinet;
        [SerializeField] private WiringDeviceEndpoint endpoint;
        [SerializeField] private TMP_Text deviceLabel;
        [SerializeField] private TMP_Text addressLabel;
        [SerializeField] private Renderer statusRenderer;
        [SerializeField] private Color offColor = new(0.12f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color onColor = new(0.1f, 0.85f, 0.32f, 1f);

        private MaterialPropertyBlock propertyBlock;

        public WiringDeviceEndpoint Endpoint => endpoint;

        public void Configure(WiringCabinetController targetCabinet, WiringDeviceEndpoint targetEndpoint)
        {
            cabinet = targetCabinet;
            SetEndpoint(targetEndpoint);
        }

        public void SetEndpoint(WiringDeviceEndpoint targetEndpoint)
        {
            if (endpoint != null)
                endpoint.AddressChanged -= HandleEndpointChanged;

            endpoint = targetEndpoint;

            if (endpoint != null)
                endpoint.AddressChanged += HandleEndpointChanged;

            Refresh();
        }

        public void CycleAddress()
        {
            endpoint?.CycleAddress();
            Refresh();
        }

        public void AssignAddress(string address)
        {
            if (endpoint != null)
                endpoint.SetAddress(address);
            else if (cabinet != null)
                cabinet.TryAssignAddress(name, address);

            Refresh();
        }

        private void OnEnable()
        {
            if (endpoint != null)
                endpoint.AddressChanged += HandleEndpointChanged;

            if (cabinet != null)
                cabinet.EndpointAddressChanged += HandleCabinetEndpointChanged;

            Refresh();
        }

        private void OnDisable()
        {
            if (endpoint != null)
                endpoint.AddressChanged -= HandleEndpointChanged;

            if (cabinet != null)
                cabinet.EndpointAddressChanged -= HandleCabinetEndpointChanged;
        }

        private void Update()
        {
            RefreshStatusOnly();
        }

        private void HandleEndpointChanged(WiringDeviceEndpoint changedEndpoint)
        {
            if (changedEndpoint == endpoint)
                Refresh();
        }

        private void HandleCabinetEndpointChanged(WiringDeviceEndpoint changedEndpoint)
        {
            if (changedEndpoint == endpoint)
                Refresh();
        }

        private void Refresh()
        {
            if (deviceLabel != null)
                deviceLabel.text = endpoint != null ? endpoint.DisplayName : "-";

            if (addressLabel != null)
                addressLabel.text = endpoint != null ? endpoint.Address : "-";

            RefreshStatusOnly();
        }

        private void RefreshStatusOnly()
        {
            if (statusRenderer == null || endpoint == null)
                return;

            propertyBlock ??= new MaterialPropertyBlock();
            statusRenderer.GetPropertyBlock(propertyBlock);
            var color = endpoint.Value ? onColor : offColor;
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            statusRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
