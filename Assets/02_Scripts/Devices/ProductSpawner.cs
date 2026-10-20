using System;
using UnityEngine;

namespace LadderLab.Devices
{
    [DisallowMultipleComponent]
    public sealed class ProductSpawner : MonoBehaviour
    {
        [SerializeField] private Transform product;
        [SerializeField] private SensorDevice[] sensorsToReset = { };
        [SerializeField] private Vector3 localSpawnPosition = new(-0.65f, 0.58f, 0f);
        [SerializeField] private Vector3 localSpawnEulerAngles;
        [SerializeField] private Rigidbody productRigidbody;

        public void Configure(Transform targetProduct, Vector3 targetLocalSpawnPosition)
        {
            product = targetProduct;
            productRigidbody = product != null ? product.GetComponent<Rigidbody>() : null;
            localSpawnPosition = targetLocalSpawnPosition;
            ResetProduct();
        }

        public void ConfigureSensors(SensorDevice[] targetSensorsToReset)
        {
            sensorsToReset = targetSensorsToReset ?? Array.Empty<SensorDevice>();
        }

        public void ResetProduct()
        {
            ResetSensors();

            if (product == null)
                return;

            product.SetParent(transform, false);
            product.localPosition = localSpawnPosition;
            product.localRotation = Quaternion.Euler(localSpawnEulerAngles);

            if (productRigidbody == null)
                productRigidbody = product.GetComponent<Rigidbody>();

            if (productRigidbody == null)
                return;

            productRigidbody.linearVelocity = Vector3.zero;
            productRigidbody.angularVelocity = Vector3.zero;
            productRigidbody.Sleep();
        }

        private void ResetSensors()
        {
            foreach (var sensor in sensorsToReset)
                sensor?.ClearDetection();
        }
    }
}
