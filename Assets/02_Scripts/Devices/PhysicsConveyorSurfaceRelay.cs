using UnityEngine;

namespace LadderLab.Devices
{
    [DisallowMultipleComponent]
    public sealed class PhysicsConveyorSurfaceRelay : MonoBehaviour
    {
        [SerializeField] private PhysicsConveyorSurface owner;

        public void Configure(PhysicsConveyorSurface targetOwner)
        {
            owner = targetOwner;
        }

        private void OnTriggerEnter(Collider other)
        {
            owner?.NotifyTriggerEnter(other);
        }

        private void OnTriggerStay(Collider other)
        {
            owner?.NotifyTriggerStay(other);
        }

        private void OnTriggerExit(Collider other)
        {
            owner?.NotifyTriggerExit(other);
        }
    }
}
