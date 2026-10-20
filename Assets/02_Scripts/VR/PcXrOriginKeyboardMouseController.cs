using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LadderLab.VR
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XROrigin))]
    public sealed class PcXrOriginKeyboardMouseController : MonoBehaviour
    {
        [SerializeField] private bool enableKeyboardMouseControls = true;
        [SerializeField] private float moveSpeed = 2.2f;
        [SerializeField] private float sprintMultiplier = 2.2f;
        [SerializeField] private float verticalSpeed = 1.4f;
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private bool requireRightMouseButtonForLook = true;

        private XROrigin xrOrigin;
        private Transform cameraTransform;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private float yaw;
        private float pitch;

        public bool EnableKeyboardMouseControls
        {
            get => enableKeyboardMouseControls;
            set => enableKeyboardMouseControls = value;
        }

        private void Awake()
        {
            xrOrigin = GetComponent<XROrigin>();
            cameraTransform = xrOrigin.Camera != null ? xrOrigin.Camera.transform : Camera.main?.transform;
            CaptureInitialPose();
            SyncAnglesFromTransforms();
        }

        private void OnEnable()
        {
            if (xrOrigin == null)
                xrOrigin = GetComponent<XROrigin>();

            if (cameraTransform == null)
                cameraTransform = xrOrigin.Camera != null ? xrOrigin.Camera.transform : Camera.main?.transform;

            SyncAnglesFromTransforms();
        }

        private void Update()
        {
            if (!enableKeyboardMouseControls)
                return;

            UpdateLook();
            UpdateMove();
            UpdateReset();
        }

        public void CaptureInitialPose()
        {
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }

        public void ResetPose()
        {
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            SyncAnglesFromTransforms();
        }

        private void UpdateLook()
        {
            var mouse = Mouse.current;
            if (mouse == null || cameraTransform == null)
                return;

            if (requireRightMouseButtonForLook && !mouse.rightButton.isPressed)
                return;

            var delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude <= 0f)
                return;

            yaw += delta.x * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, -85f, 85f);

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void UpdateMove()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || cameraTransform == null)
                return;

            var input = Vector3.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                input.z += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                input.z -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                input.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                input.x -= 1f;

            var vertical = 0f;
            if (keyboard.eKey.isPressed)
                vertical += 1f;
            if (keyboard.qKey.isPressed)
                vertical -= 1f;

            if (input.sqrMagnitude <= 0f && Mathf.Approximately(vertical, 0f))
                return;

            var forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude <= 0f)
                forward = transform.forward;

            var right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            var horizontal = (right * input.x + forward * input.z);
            if (horizontal.sqrMagnitude > 1f)
                horizontal.Normalize();

            var speed = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed
                ? moveSpeed * sprintMultiplier
                : moveSpeed;
            var delta = horizontal * speed + Vector3.up * (vertical * verticalSpeed);
            transform.position += delta * Time.deltaTime;
        }

        private void UpdateReset()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.homeKey.wasPressedThisFrame)
                ResetPose();
        }

        private void SyncAnglesFromTransforms()
        {
            yaw = transform.eulerAngles.y;
            pitch = cameraTransform != null ? NormalizeAngle(cameraTransform.localEulerAngles.x) : 0f;
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
