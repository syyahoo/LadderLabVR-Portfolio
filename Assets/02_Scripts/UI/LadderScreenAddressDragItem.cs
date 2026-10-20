using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LadderLab.UI
{
    [DisallowMultipleComponent]
    public sealed class LadderScreenAddressDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private string address;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private bool enableHapticFeedback = true;
        [SerializeField, Range(0f, 1f)] private float hapticAmplitude = 0.28f;
        [SerializeField, Min(0f)] private float hapticDuration = 0.05f;

        private Canvas rootCanvas;
        private GameObject dragVisual;
        private LadderScreenDiagramSlot snappedSlot;

        public string Address => address;
        public char Area => string.IsNullOrEmpty(address) ? '\0' : address[0];

        public void Configure(string targetAddress, Color targetColor)
        {
            address = targetAddress;
            color = targetColor;

            var text = GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = address;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas == null)
                return;

            if (enableHapticFeedback)
                LadderScreenHaptics.PlayRightHandImpulse(hapticAmplitude, hapticDuration);

            dragVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dragVisual.name = $"Address Drag Cube {address}";
            dragVisual.transform.localScale = new Vector3(0.075f, 0.075f, 0.03f);
            var collider = dragVisual.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;

            var renderer = dragVisual.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var material = new Material(shader)
                {
                    color = color
                };
                material.SetColor("_BaseColor", color);
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
                snappedSlot.ApplyAddressDrop(address, Area);

            if (dragVisual != null)
                Destroy(dragVisual);

            dragVisual = null;
            snappedSlot = null;
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
}
