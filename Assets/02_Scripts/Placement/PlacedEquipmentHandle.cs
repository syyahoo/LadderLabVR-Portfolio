using UnityEngine;

namespace LadderLab.Placement
{
    [DisallowMultipleComponent]
    public sealed class PlacedEquipmentHandle : MonoBehaviour
    {
        [SerializeField] private EquipmentPlacementData equipment;
        [SerializeField] private Vector2Int originCell;
        [SerializeField] private Vector2Int footprint = Vector2Int.one;
        [SerializeField] private bool gridPlaced;
        [SerializeField] private GameObject attachmentTarget;

        public EquipmentPlacementData Equipment => equipment;
        public Vector2Int OriginCell => originCell;
        public Vector2Int Footprint => new(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
        public bool GridPlaced => gridPlaced;
        public GameObject AttachmentTarget => attachmentTarget;

        public void ConfigureGrid(EquipmentPlacementData targetEquipment, Vector2Int targetOriginCell, Vector2Int targetFootprint)
        {
            equipment = targetEquipment;
            originCell = targetOriginCell;
            footprint = new Vector2Int(Mathf.Max(1, targetFootprint.x), Mathf.Max(1, targetFootprint.y));
            gridPlaced = true;
            attachmentTarget = null;
        }

        public void ConfigureAttachment(EquipmentPlacementData targetEquipment, GameObject targetAttachment)
        {
            equipment = targetEquipment;
            attachmentTarget = targetAttachment;
            gridPlaced = false;
            originCell = default;
            footprint = Vector2Int.one;
        }

        public void UpdateGridPlacement(Vector2Int targetOriginCell, Vector2Int targetFootprint)
        {
            originCell = targetOriginCell;
            footprint = new Vector2Int(Mathf.Max(1, targetFootprint.x), Mathf.Max(1, targetFootprint.y));
            gridPlaced = true;
        }

        public void UpdateAttachmentTarget(GameObject targetAttachment)
        {
            attachmentTarget = targetAttachment;
            gridPlaced = false;
        }

        public void ClearSlotAssignment()
        {
            if (!gridPlaced)
                equipment = null;

            attachmentTarget = null;
        }
    }
}
