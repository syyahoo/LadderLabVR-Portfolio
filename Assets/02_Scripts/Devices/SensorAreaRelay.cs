using UnityEngine;

namespace LadderLab.Devices
{
    [DisallowMultipleComponent]
    public sealed class SensorAreaRelay : MonoBehaviour
    {
        [SerializeField] private SensorDevice sensor;

        public void Configure(SensorDevice targetSensor)
        {
            sensor = targetSensor;
        }

        private void Awake()
        {
            ResolveSensor();
        }

        private void OnTriggerEnter(Collider other)
        {
            ResolveSensor();
            sensor?.NotifyAreaTriggerEnter(other);
        }

        private void OnTriggerExit(Collider other)
        {
            ResolveSensor();
            sensor?.NotifyAreaTriggerExit(other);
        }

        private void ResolveSensor()
        {
            if (sensor == null)
                sensor = GetComponentInParent<SensorDevice>();
        }
    }
}
