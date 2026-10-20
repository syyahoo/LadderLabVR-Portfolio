using LadderLab.PLC;
using UnityEngine;

namespace LadderLab.Wiring
{
    [DisallowMultipleComponent]
    public sealed class WiringNode : MonoBehaviour
    {
        [SerializeField] private WiringNodeKind nodeKind;
        [SerializeField] private WiringControllerBox controllerBox;
        [SerializeField] private WiringDeviceEndpoint endpoint;
        [SerializeField] private string assignedAddress = "";
        [SerializeField] private Renderer statusRenderer;
        [SerializeField] private Color validColor = new(0.2f, 1f, 0.42f, 1f);
        [SerializeField] private Color invalidColor = new(1f, 0.14f, 0.08f, 1f);

        private MaterialPropertyBlock propertyBlock;
        private MaterialPropertyBlock idlePropertyBlock;
        private Renderer[] cachedRenderers;
        private Collider[] cachedColliders;
        private bool idlePropertyCaptured;

        public WiringNodeKind NodeKind => nodeKind;
        public WiringControllerBox ControllerBox => controllerBox;
        public WiringDeviceEndpoint Endpoint => endpoint;
        public string AssignedAddress => assignedAddress;
        public bool HasAddress => !string.IsNullOrWhiteSpace(assignedAddress);

        public void ConfigureSource(WiringControllerBox owner, string address)
        {
            nodeKind = WiringNodeKind.Source;
            controllerBox = owner;
            endpoint = null;
            assignedAddress = address?.Trim().ToUpperInvariant() ?? "";
            EnsureRenderer();
            SetIdle();
        }

        public void ConfigureRelay(WiringControllerBox owner, bool preserveAddress = false)
        {
            nodeKind = WiringNodeKind.Relay;
            controllerBox = owner;
            endpoint = null;
            if (!preserveAddress)
                assignedAddress = "";
            EnsureRenderer();
            SetIdle();
        }

        public void ConfigureDevice(WiringDeviceEndpoint targetEndpoint)
        {
            nodeKind = WiringNodeKind.Device;
            controllerBox = null;
            endpoint = targetEndpoint;
            assignedAddress = "";
            EnsureRenderer();
            RefreshFromEndpoint();
        }

        public void AssignAddress(string address)
        {
            assignedAddress = address?.Trim().ToUpperInvariant() ?? "";
            SetIdle();
        }

        // 와이어링 연결 해제 시 동적 주소만 초기화
        public void ClearAssignedAddress()
        {
            if (nodeKind == WiringNodeKind.Source)
                return;

            assignedAddress = "";
            SetIdle();
        }

        public void RefreshFromEndpoint()
        {
            if (nodeKind == WiringNodeKind.Device && endpoint != null)
                assignedAddress = endpoint.Address?.Trim().ToUpperInvariant() ?? "";

            SetIdle();
        }

        public bool CanReceiveAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return false;

            if (nodeKind == WiringNodeKind.Source)
                return false;

            if (nodeKind == WiringNodeKind.Relay)
                return controllerBox == null || controllerBox.CanRelayReceiveAddress(address);

            if (endpoint == null)
                return false;

            var parsed = PlcAddress.Parse(address);
            var expectedArea = endpoint.SignalDirection == WiringSignalDirection.DeviceWritesPlcInput
                ? PlcMemoryArea.Input
                : PlcMemoryArea.Output;
            return parsed.Area == expectedArea;
        }

        public void SetIdle()
        {
            RestoreIdleColor();
        }

        public void SetCandidate(bool valid)
        {
            SetColor(valid ? validColor : invalidColor);
        }

        // 와이어링 모드 표시 전환
        public void SetVisible(bool visible)
        {
            EnsureVisualTargets();

            foreach (var nodeRenderer in cachedRenderers)
            {
                if (nodeRenderer != null && nodeRenderer.enabled != visible)
                    nodeRenderer.enabled = visible;
            }

            foreach (var nodeCollider in cachedColliders)
            {
                if (nodeCollider != null && nodeCollider.enabled != visible)
                    nodeCollider.enabled = visible;
            }
        }

        private void Awake()
        {
            EnsureRenderer();
            CaptureIdleColor();
            EnsureVisualTargets();
            SetVisible(false);
        }

        private void EnsureRenderer()
        {
            if (statusRenderer == null)
                statusRenderer = GetComponentInChildren<Renderer>(true);

            CaptureIdleColor();
        }

        private void EnsureVisualTargets()
        {
            cachedRenderers ??= GetComponentsInChildren<Renderer>(true);
            cachedColliders ??= GetComponentsInChildren<Collider>(true);
        }

        private void SetColor(Color color)
        {
            EnsureRenderer();
            if (statusRenderer == null)
                return;

            propertyBlock ??= new MaterialPropertyBlock();
            statusRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", color);
            propertyBlock.SetColor("_BaseColor", color);
            statusRenderer.SetPropertyBlock(propertyBlock);
        }

        private void CaptureIdleColor()
        {
            if (idlePropertyCaptured || statusRenderer == null)
                return;

            idlePropertyBlock ??= new MaterialPropertyBlock();
            statusRenderer.GetPropertyBlock(idlePropertyBlock);
            idlePropertyCaptured = true;
        }

        private void RestoreIdleColor()
        {
            EnsureRenderer();
            if (statusRenderer == null)
                return;

            CaptureIdleColor();
            statusRenderer.SetPropertyBlock(idlePropertyBlock);
        }
    }
}
