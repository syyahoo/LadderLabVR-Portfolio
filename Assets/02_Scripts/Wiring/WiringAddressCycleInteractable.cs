using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LadderLab.Wiring
{
    [DisallowMultipleComponent]
    public sealed class WiringAddressCycleInteractable : MonoBehaviour
    {
        [SerializeField] private WiringDeviceEndpoint endpoint;
        [SerializeField] private WiringCabinetTerminal terminal;
        [SerializeField] private XRSimpleInteractable interactable;

        public void Configure(WiringDeviceEndpoint targetEndpoint, WiringCabinetTerminal targetTerminal)
        {
            endpoint = targetEndpoint;
            terminal = targetTerminal;
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();
            if (interactable != null)
                interactable.selectEntered.AddListener(HandleSelectEntered);
        }

        private void OnDisable()
        {
            if (interactable != null)
                interactable.selectEntered.RemoveListener(HandleSelectEntered);
        }

        private void OnMouseDown()
        {
            CycleAddress();
        }

        private void HandleSelectEntered(SelectEnterEventArgs eventArgs)
        {
            CycleAddress();
        }

        private void CycleAddress()
        {
            if (terminal != null)
            {
                terminal.CycleAddress();
                return;
            }

            endpoint?.CycleAddress();
        }

        private void EnsureReferences()
        {
            if (endpoint == null)
                endpoint = GetComponent<WiringDeviceEndpoint>();

            if (terminal == null)
                terminal = GetComponent<WiringCabinetTerminal>();

            if (interactable == null)
                interactable = GetComponent<XRSimpleInteractable>();
        }
    }
}
