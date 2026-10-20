using TMPro;
using LadderLab.PLC;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LadderLab.UI
{
    [DisallowMultipleComponent]
    public sealed class LadderScreenDiagramSlot : MonoBehaviour, IDropHandler, IPointerClickHandler
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text addressLabel;
        [SerializeField] private Image background;
        [SerializeField] private LadderScreenScrollableRungBoard rungBoard;
        [SerializeField] private int rungIndex = -1;
        [SerializeField] private int contactSlotIndex = -1;
        [SerializeField] private int parallelBranchStartSlotIndex = -1;
        [SerializeField] private bool acceptsParallelBranch;

        private bool hasNode;
        private LadderScreenNodeKind nodeKind;
        private string nodeLabel;
        private string assignedAddress;

        public bool HasNode => hasNode;
        public int RungIndex => rungIndex;
        public int ContactSlotIndex => contactSlotIndex;
        public int ParallelBranchStartSlotIndex => parallelBranchStartSlotIndex;
        public bool IsParallelBranchSlot => parallelBranchStartSlotIndex >= 0;
        public bool IsCoilSlot => contactSlotIndex < 0;
        public LadderScreenNodeKind NodeKind => nodeKind;
        public string AssignedAddress => assignedAddress;

        public bool TryGetSnapWorldPosition(out Vector3 worldPosition)
        {
            if (transform is RectTransform rectTransform)
            {
                worldPosition = rectTransform.TransformPoint(rectTransform.rect.center);
                return true;
            }

            worldPosition = transform.position;
            return true;
        }

        public float GetSnapRadius()
        {
            if (transform is not RectTransform rectTransform)
                return 0.14f;

            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            var width = Vector3.Distance(corners[0], corners[3]);
            var height = Vector3.Distance(corners[0], corners[1]);
            return Mathf.Max(0.08f, Mathf.Max(width, height) * 0.72f);
        }

        public void Configure(TMP_Text targetLabel, TMP_Text targetAddressLabel, Image targetBackground)
        {
            label = targetLabel;
            addressLabel = targetAddressLabel;
            background = targetBackground;
            Clear();
        }

        public void Configure(
            TMP_Text targetLabel,
            TMP_Text targetAddressLabel,
            Image targetBackground,
            LadderScreenScrollableRungBoard board,
            int targetRungIndex,
            int targetContactSlotIndex,
            bool canCreateParallelBranch)
        {
            Configure(
                targetLabel,
                targetAddressLabel,
                targetBackground,
                board,
                targetRungIndex,
                targetContactSlotIndex,
                -1,
                canCreateParallelBranch);
        }

        public void Configure(
            TMP_Text targetLabel,
            TMP_Text targetAddressLabel,
            Image targetBackground,
            LadderScreenScrollableRungBoard board,
            int targetRungIndex,
            int targetContactSlotIndex,
            int targetParallelBranchStartSlotIndex,
            bool canCreateParallelBranch)
        {
            rungBoard = board;
            rungIndex = targetRungIndex;
            contactSlotIndex = targetContactSlotIndex;
            parallelBranchStartSlotIndex = targetParallelBranchStartSlotIndex;
            acceptsParallelBranch = canCreateParallelBranch;
            Configure(targetLabel, targetAddressLabel, targetBackground);
        }

        public void OnDrop(PointerEventData eventData)
        {
            var dragItem = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<LadderScreenNodeDragItem>()
                : null;

            if (dragItem != null)
            {
                ApplyNodeDrop(dragItem.NodeKind, dragItem.DisplayLabel);
                return;
            }

            var addressItem = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<LadderScreenAddressDragItem>()
                : null;

            if (addressItem != null)
                ApplyAddressDrop(addressItem.Address, addressItem.Area);
        }

        public bool ApplyNodeDrop(LadderScreenNodeKind kind, string displayLabel)
        {
            if (kind == LadderScreenNodeKind.Clear)
            {
                ClearParallelBranch();
                Clear();
                return true;
            }

            if (kind == LadderScreenNodeKind.ParallelBranch)
            {
                if (!acceptsParallelBranch || rungBoard == null)
                    return false;

                rungBoard.CreateParallelBranch(rungIndex, contactSlotIndex);
                return true;
            }

            Place(kind, displayLabel);
            return true;
        }

        public bool ApplyAddressDrop(string targetAddress, char area)
        {
            if (!hasNode || !CanAssignAddress(area))
                return false;

            AssignAddress(targetAddress);
            return true;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                Clear();
        }

        public void SetScanHighlight(bool isTrue, bool isCurrent)
        {
            if (background == null)
                return;

            if (!isTrue)
            {
                RefreshBackground();
                return;
            }

            background.color = isCurrent
                ? new Color(0.02f, 0.98f, 0.34f, 1f)
                : new Color(0.02f, 0.70f, 0.24f, 0.96f);
        }

        public bool EvaluateContact(IPlcBackend backend)
        {
            if (!hasNode || IsCoilSlot)
                return true;

            if (string.IsNullOrWhiteSpace(assignedAddress) || backend == null)
                return false;

            var addressOn = backend.GetBit(assignedAddress);
            return nodeKind switch
            {
                LadderScreenNodeKind.NormallyOpen => addressOn,
                LadderScreenNodeKind.NormallyClosed => !addressOn,
                _ => false
            };
        }

        private void Place(LadderScreenNodeKind kind, string displayLabel)
        {
            hasNode = true;
            nodeKind = kind;
            nodeLabel = displayLabel;
            assignedAddress = string.Empty;

            if (label != null)
                label.text = addressLabel == null ? $"{displayLabel}\nDROP ADDRESS" : displayLabel;

            if (addressLabel != null)
                addressLabel.text = "DROP ADDRESS";

            RefreshBackground();
        }

        private void AssignAddress(string address)
        {
            assignedAddress = address;

            if (label != null)
                label.text = addressLabel == null ? $"{nodeLabel}\n{assignedAddress}" : nodeLabel;

            if (addressLabel != null)
                addressLabel.text = assignedAddress;
        }

        private bool CanAssignAddress(char area)
        {
            return nodeKind switch
            {
                LadderScreenNodeKind.NormallyOpen => area == 'X' || area == 'M',
                LadderScreenNodeKind.NormallyClosed => area == 'X' || area == 'M',
                LadderScreenNodeKind.Coil => area == 'Y' || area == 'M',
                _ => false
            };
        }

        private void ClearParallelBranch()
        {
            if (rungBoard == null)
                return;

            if (parallelBranchStartSlotIndex >= 0)
            {
                rungBoard.ClearParallelBranch(rungIndex, parallelBranchStartSlotIndex);
                return;
            }

            if (acceptsParallelBranch)
                rungBoard.ClearParallelBranch(rungIndex, contactSlotIndex);
        }

        private void Clear()
        {
            hasNode = false;
            nodeKind = default;
            nodeLabel = string.Empty;
            assignedAddress = string.Empty;

            if (label != null)
                label.text = "";

            if (addressLabel != null)
                addressLabel.text = "";

            RefreshBackground();
        }

        private void RefreshBackground()
        {
            if (background == null)
                return;

            background.color = hasNode
                ? LadderScreenNodeDragItem.ResolveColor(nodeKind, 0.92f)
                : new Color(0.06f, 0.10f, 0.12f, 0.82f);
        }
    }
}
