using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;

namespace LadderLab.UI
{
    public enum LadderScreenNodeKind
    {
        NormallyOpen,
        NormallyClosed,
        Coil,
        Clear,
        ParallelBranch
    }

    [DisallowMultipleComponent]
    public sealed class LadderScreenNodeDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private LadderScreenNodeKind nodeKind;
        [SerializeField] private string displayLabel;
        [SerializeField] private bool enableHapticFeedback = true;
        [SerializeField, Range(0f, 1f)] private float hapticAmplitude = 0.32f;
        [SerializeField, Min(0f)] private float hapticDuration = 0.06f;

        private Canvas rootCanvas;
        private GameObject dragVisual;
        private LadderScreenDiagramSlot snappedSlot;

        public LadderScreenNodeKind NodeKind => nodeKind;
        public string DisplayLabel => displayLabel;

        public void Configure(LadderScreenNodeKind kind, string label)
        {
            nodeKind = kind;
            displayLabel = label;

            var text = GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = label;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas == null)
                return;

            if (enableHapticFeedback)
                LadderScreenHaptics.PlayRightHandImpulse(hapticAmplitude, hapticDuration);

            dragVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dragVisual.name = $"Node Drag Cube {displayLabel}";
            dragVisual.transform.localScale = new Vector3(0.10f, 0.10f, 0.035f);
            var collider = dragVisual.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;

            var renderer = dragVisual.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var material = new Material(shader)
                {
                    color = ResolveColor(nodeKind, 0.95f)
                };
                material.SetColor("_BaseColor", ResolveColor(nodeKind, 0.95f));
                renderer.sharedMaterial = material;
            }

            MoveDragVisual(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            MoveDragVisual(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (snappedSlot != null && !LadderScreenSnap.TryGetRaycastSlot(eventData, out _))
                snappedSlot.ApplyNodeDrop(nodeKind, displayLabel);

            if (dragVisual != null)
                Destroy(dragVisual);

            dragVisual = null;
            snappedSlot = null;
        }

        public static Color ResolveColor(LadderScreenNodeKind kind, float alpha = 1f)
        {
            var color = kind switch
            {
                LadderScreenNodeKind.NormallyOpen => new Color(0.10f, 0.43f, 0.78f, alpha),
                LadderScreenNodeKind.NormallyClosed => new Color(0.72f, 0.24f, 0.18f, alpha),
                LadderScreenNodeKind.Coil => new Color(0.91f, 0.58f, 0.16f, alpha),
                LadderScreenNodeKind.Clear => new Color(0.42f, 0.44f, 0.48f, alpha),
                LadderScreenNodeKind.ParallelBranch => new Color(0.18f, 0.55f, 0.48f, alpha),
                _ => new Color(0.35f, 0.38f, 0.42f, alpha)
            };
            color.a = alpha;
            return color;
        }

        private void MoveDragVisual(PointerEventData eventData)
        {
            if (dragVisual == null || rootCanvas == null)
                return;

            if (!LadderScreenSnap.TryGetPointerWorldPosition(eventData, rootCanvas, out var worldPosition))
                return;

            if (LadderScreenSnap.TryFindNearestSlot(rootCanvas, worldPosition, out var slot, out var snapPosition))
            {
                snappedSlot = slot;
                worldPosition = snapPosition;
            }
            else
            {
                snappedSlot = null;
            }

            dragVisual.transform.position = worldPosition;
            var cameraTransform = eventData.pressEventCamera != null
                ? eventData.pressEventCamera.transform
                : rootCanvas.worldCamera != null
                    ? rootCanvas.worldCamera.transform
                    : null;
            if (cameraTransform != null)
                dragVisual.transform.rotation = cameraTransform.rotation;
        }
    }

    internal static class LadderScreenHaptics
    {
        public static void PlayRightHandImpulse(float amplitude, float duration)
        {
            amplitude = Mathf.Clamp01(amplitude);
            duration = Mathf.Max(0f, duration);
            if (amplitude <= 0f || duration <= 0f)
                return;

            var device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (!device.isValid)
                return;

            if (device.TryGetHapticCapabilities(out var capabilities) &&
                !capabilities.supportsImpulse)
            {
                return;
            }

            device.SendHapticImpulse(0u, amplitude, duration);
        }
    }

    internal static class LadderScreenSnap
    {
        public static bool TryGetRaycastSlot(PointerEventData eventData, out LadderScreenDiagramSlot slot)
        {
            slot = eventData?.pointerCurrentRaycast.gameObject != null
                ? eventData.pointerCurrentRaycast.gameObject.GetComponentInParent<LadderScreenDiagramSlot>()
                : null;
            return slot != null;
        }

        public static bool TryGetPointerWorldPosition(PointerEventData eventData, Canvas rootCanvas, out Vector3 worldPosition)
        {
            worldPosition = eventData.pointerCurrentRaycast.worldPosition;
            if (worldPosition != Vector3.zero)
                return true;

            var camera = eventData.pressEventCamera != null ? eventData.pressEventCamera : rootCanvas.worldCamera;
            if (camera == null)
                return false;

            worldPosition = camera.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, 1.2f));
            return true;
        }

        public static bool TryFindNearestSlot(
            Canvas rootCanvas,
            Vector3 worldPosition,
            out LadderScreenDiagramSlot nearestSlot,
            out Vector3 snapPosition)
        {
            nearestSlot = null;
            snapPosition = default;
            if (rootCanvas == null)
                return false;

            var nearestDistance = float.PositiveInfinity;
            var slots = rootCanvas.GetComponentsInChildren<LadderScreenDiagramSlot>(true);
            foreach (var slot in slots)
            {
                if (slot == null || !slot.gameObject.activeInHierarchy || !slot.TryGetSnapWorldPosition(out var candidatePosition))
                    continue;

                var distance = Vector3.Distance(worldPosition, candidatePosition);
                if (distance > slot.GetSnapRadius() || distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                nearestSlot = slot;
                snapPosition = candidatePosition;
            }

            return nearestSlot != null;
        }
    }
}
