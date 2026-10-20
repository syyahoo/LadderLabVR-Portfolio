using UnityEngine;

namespace LadderLab.Placement
{
    [DisallowMultipleComponent]
    public sealed class TrailerEndpointProductRemover : MonoBehaviour
    {
        // 도착 상품 제거
        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
                return;

            var body = other.attachedRigidbody;
            if (body == null || body.isKinematic)
                return;

            Destroy(body.gameObject);
        }
    }
}
