using System;
using LadderLab.PLC;
using LadderLab.VR;
using LadderLab.Wiring;
using UnityEngine;

namespace LadderLab.Devices
{
    [DisallowMultipleComponent]
    public sealed class ConveyorDevice : MonoBehaviour, IPlcAddressBindable
    {
        [SerializeField] private PlcScanController scanController;
        [SerializeField] private PlcAddressBinding addressBinding;
        [SerializeField] private string outputAddress = "Y0";
        [SerializeField] private Transform movingTarget;
        [SerializeField] private Vector3 localMoveDirection = Vector3.forward;
        [SerializeField, Min(0f)] private float moveSpeed = 0.35f;

        public bool IsPowered
        {
            get
            {
                if (addressBinding != null)
                    return addressBinding.Value;

                return scanController != null && scanController.Backend.GetBit(outputAddress);
            }
        }

        public void Configure(
            PlcScanController targetScanController,
            string targetOutputAddress,
            Transform targetToMove,
            Vector3 targetLocalMoveDirection,
            float targetMoveSpeed)
        {
            scanController = targetScanController;
            outputAddress = targetOutputAddress;
            movingTarget = targetToMove;
            localMoveDirection = targetLocalMoveDirection;
            moveSpeed = Mathf.Max(0f, targetMoveSpeed);
            ValidateOutputAddress();
        }

        public void ConfigureBinding(PlcAddressBinding targetAddressBinding)
        {
            addressBinding = targetAddressBinding;
            scanController = targetAddressBinding != null ? targetAddressBinding.ScanController : scanController;
            outputAddress = targetAddressBinding != null ? targetAddressBinding.Address : outputAddress;
            ValidateOutputAddress();
        }

        public void Tick(float deltaTime)
        {
            if (!IsPowered || movingTarget == null || moveSpeed <= 0f)
                return;

            var direction = transform.TransformDirection(localMoveDirection.normalized);
            movingTarget.position += direction * (moveSpeed * deltaTime);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(outputAddress))
                outputAddress = "Y0";
        }

        private void ValidateOutputAddress()
        {
            var targetAddress = addressBinding != null ? addressBinding.Address : outputAddress;
            if (string.IsNullOrWhiteSpace(targetAddress))
                return;

            var address = PlcAddress.Parse(targetAddress);
            if (address.Area != PlcMemoryArea.Output)
                throw new ArgumentException($"{nameof(ConveyorDevice)} can only read PLC Y output addresses.", nameof(outputAddress));
        }
    }
}
