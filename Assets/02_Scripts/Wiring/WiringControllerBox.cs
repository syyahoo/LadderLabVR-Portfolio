using System.Collections.Generic;
using LadderLab.PLC;
using UnityEngine;

namespace LadderLab.Wiring
{
    [DisallowMultipleComponent]
    public sealed class WiringControllerBox : MonoBehaviour
    {
        [SerializeField] private bool sourceController;
        [SerializeField] private WiringRelayAddressFilter relayAddressFilter = WiringRelayAddressFilter.Any;

        private readonly List<WiringNode> nodes = new();

        public IReadOnlyList<WiringNode> Nodes => nodes;
        public bool SourceController => sourceController;
        public WiringRelayAddressFilter RelayAddressFilter => relayAddressFilter;

        public void ConfigureAsSourceController()
        {
            sourceController = true;
            relayAddressFilter = WiringRelayAddressFilter.Any;
            EnsureNodes();
        }

        public void ConfigureAsRelayController()
        {
            ConfigureAsRelayController(WiringRelayAddressFilter.Any);
        }

        public void ConfigureAsRelayController(WiringRelayAddressFilter targetRelayAddressFilter)
        {
            sourceController = false;
            relayAddressFilter = targetRelayAddressFilter;
            EnsureNodes();
        }

        public bool CanRelayReceiveAddress(string address)
        {
            if (sourceController || string.IsNullOrWhiteSpace(address))
                return false;

            if (relayAddressFilter == WiringRelayAddressFilter.Any)
                return true;

            var parsed = PlcAddress.Parse(address);
            return relayAddressFilter switch
            {
                WiringRelayAddressFilter.InputOnly => parsed.Area == PlcMemoryArea.Input,
                WiringRelayAddressFilter.OutputOnly => parsed.Area == PlcMemoryArea.Output,
                _ => true
            };
        }

        public void EnsureNodes()
        {
            nodes.Clear();
            var existingNodes = GetComponentsInChildren<WiringNode>(true);
            foreach (var node in existingNodes)
            {
                if (node == null || node.Endpoint != null)
                    continue;

                if (sourceController)
                {
                    if (!TryResolveSourceAddress(node.name, out var sourceAddress))
                        continue;

                    node.ConfigureSource(this, sourceAddress);
                }
                else
                {
                    node.ConfigureRelay(this, true);
                }

                nodes.Add(node);
            }
        }

        public void SetNodesVisible(bool visible)
        {
            EnsureNodes();
            if (sourceController)
                TrySetSourceNodesRootVisible(visible);

            foreach (var node in nodes)
            {
                if (node != null)
                    node.SetVisible(visible);
            }
        }

        // 컨트롤러1은 Nodes 그룹 전체를 토글해서 라벨/캔버스까지 함께 숨김
        private bool TrySetSourceNodesRootVisible(bool visible)
        {
            var nodesRoot = transform.Find("Nodes");
            if (nodesRoot == null)
                return false;

            if (nodesRoot.gameObject.activeSelf != visible)
                nodesRoot.gameObject.SetActive(visible);

            return true;
        }

        private static bool TryResolveSourceAddress(string objectName, out string address)
        {
            if (TryResolveNamedNodeAddress(objectName, "INPUTNODE_", 'X', PlcMemory.InputCount, out address))
                return true;

            if (TryResolveNamedNodeAddress(objectName, "OUTPUTNODE_", 'Y', PlcMemory.OutputCount, out address))
                return true;

            if (TryResolveSourceAddress(objectName, 'X', PlcMemory.InputCount, out address))
                return true;

            return TryResolveSourceAddress(objectName, 'Y', PlcMemory.OutputCount, out address);
        }

        // 컨트롤러1 프리셋 노드 이름을 PLC 주소로 변환
        private static bool TryResolveNamedNodeAddress(
            string objectName,
            string namePrefix,
            char addressPrefix,
            int maxCount,
            out string address)
        {
            address = "";
            if (string.IsNullOrWhiteSpace(objectName))
                return false;

            var normalized = objectName.Trim().ToUpperInvariant();
            if (!normalized.StartsWith(namePrefix, System.StringComparison.Ordinal))
                return false;

            var numberText = normalized.Substring(namePrefix.Length);
            if (!int.TryParse(numberText, out var oneBasedIndex))
                return false;

            var index = oneBasedIndex > 0 ? oneBasedIndex - 1 : oneBasedIndex;
            if (index < 0 || index >= maxCount)
                return false;

            address = $"{addressPrefix}{index}";
            return true;
        }

        private static bool TryResolveSourceAddress(string objectName, char prefix, int maxCount, out string address)
        {
            address = "";
            if (string.IsNullOrWhiteSpace(objectName))
                return false;

            var normalized = objectName.ToUpperInvariant();
            var prefixIndex = normalized.IndexOf(prefix);
            if (prefixIndex < 0)
                return false;

            var digitStart = prefixIndex + 1;
            while (digitStart < normalized.Length && !char.IsDigit(normalized[digitStart]))
                digitStart++;

            if (digitStart >= normalized.Length)
                return false;

            var digitEnd = digitStart;
            while (digitEnd < normalized.Length && char.IsDigit(normalized[digitEnd]))
                digitEnd++;

            if (!int.TryParse(normalized.Substring(digitStart, digitEnd - digitStart), out var index) ||
                index < 0 ||
                index >= maxCount)
            {
                return false;
            }

            address = $"{prefix}{index}";
            return true;
        }
    }

    public enum WiringRelayAddressFilter
    {
        Any,
        InputOnly,
        OutputOnly
    }
}
