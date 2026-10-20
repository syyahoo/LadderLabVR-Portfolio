using UnityEngine;
using UnityEngine.UI;

namespace LadderLab.Placement
{
    [DisallowMultipleComponent]
    public sealed class SandboxEquipmentPaletteButton : MonoBehaviour
    {
        [SerializeField] private EquipmentPlacementData equipment;
        [SerializeField] private EquipmentPlacementController placementController;
        [SerializeField] private Button uiButton;

        public EquipmentPlacementData Equipment => equipment;

        public void Configure(EquipmentPlacementData targetEquipment)
        {
            equipment = targetEquipment;
        }

        public void Configure(EquipmentPlacementData targetEquipment, EquipmentPlacementController targetPlacementController)
        {
            equipment = targetEquipment;
            placementController = targetPlacementController;
        }

        public void SetPlacementController(EquipmentPlacementController targetPlacementController)
        {
            placementController = targetPlacementController;
        }

        public bool ApplySelection()
        {
            return ApplySelection(placementController);
        }

        public bool ApplySelection(EquipmentPlacementController targetPlacementController)
        {
            if (equipment == null || targetPlacementController == null)
                return false;

            targetPlacementController.OverwriteSelectedEquipment(equipment, true);
            return true;
        }

        private void Awake()
        {
            EnsureButtonReference();
        }

        private void OnEnable()
        {
            EnsureButtonReference();
            if (uiButton == null)
                return;

            uiButton.onClick.RemoveListener(HandleClicked);
            uiButton.onClick.AddListener(HandleClicked);
        }

        private void OnDisable()
        {
            if (uiButton != null)
                uiButton.onClick.RemoveListener(HandleClicked);
        }

        private void EnsureButtonReference()
        {
            if (uiButton == null)
                uiButton = GetComponent<Button>();
        }

        private void HandleClicked()
        {
            ApplySelection();
        }
    }
}
