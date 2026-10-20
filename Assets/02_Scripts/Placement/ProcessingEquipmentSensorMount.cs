using UnityEngine;

namespace LadderLab.Placement
{
    [DisallowMultipleComponent]
    public sealed class ProcessingEquipmentSensorMount : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private GameObject sensorObject;
        [SerializeField] private GameObject assignedSensor;
        [SerializeField] private EquipmentPlacementData assignedEquipment;
        [SerializeField] private Color ghostColor = new(0.1f, 0.85f, 1f, 0.32f);
        [SerializeField] private Color hoverColor = new(0.02f, 0.95f, 1f, 0.92f);

        private MaterialPropertyBlock propertyBlock;

        public bool CanAcceptSensor => assignedSensor == null;

        public void Configure()
        {
            ResolveSensorObject();
            assignedSensor = null;
            assignedEquipment = null;
            SetVisualState(false, false);
        }

        public GameObject ActivateSensor(EquipmentPlacementData equipment)
        {
            if (!CanAcceptSensor)
                return null;

            ResolveSensorObject();
            if (sensorObject == null)
                return null;

            sensorObject.SetActive(true);
            SetSensorVisualState(true, false, true);
            return sensorObject;
        }

        public void AssignSensor(GameObject sensor, EquipmentPlacementData equipment)
        {
            assignedSensor = sensor;
            assignedEquipment = equipment;
            SetSensorVisualState(true, false, true);
        }

        public bool CanAcceptSensorFor(PlacedEquipmentHandle handle)
        {
            return assignedSensor == null || (handle != null && assignedSensor == handle.gameObject);
        }

        public void ClearAssignedSensor(PlacedEquipmentHandle handle)
        {
            if (handle != null && assignedSensor != handle.gameObject)
                return;

            assignedSensor = null;
            assignedEquipment = null;
            SetVisualState(false, false);
        }

        public void SetVisualState(bool visible, bool highlighted)
        {
            SetSensorVisualState(visible, highlighted, assignedSensor != null);
        }

        private void ResolveSensorObject()
        {
            if (sensorObject != null)
                return;

            sensorObject = gameObject;
        }

        private void SetSensorVisualState(bool visible, bool highlighted, bool solid)
        {
            ResolveSensorObject();
            if (sensorObject == null)
                return;

            sensorObject.SetActive(visible || solid);
            foreach (var sensorRenderer in sensorObject.GetComponentsInChildren<Renderer>(true))
            {
                sensorRenderer.enabled = visible || solid;
                if (solid)
                {
                    sensorRenderer.SetPropertyBlock(null);
                    continue;
                }

                if (visible)
                    ApplySensorColor(sensorRenderer, highlighted ? hoverColor : ghostColor);
            }

            foreach (var sensorCollider in sensorObject.GetComponentsInChildren<Collider>(true))
                sensorCollider.enabled = visible || solid;
        }

        private void ApplySensorColor(Renderer sensorRenderer, Color color)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            sensorRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            sensorRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
