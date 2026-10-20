using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace LadderLab.VR
{
    [DisallowMultipleComponent]
    public sealed class PcMouseUiInput : MonoBehaviour
    {
        [SerializeField] private bool enableMouseUiClick = true;

        private readonly List<RaycastResult> raycastResults = new();

        public bool EnableMouseUiClick
        {
            get => enableMouseUiClick;
            set => enableMouseUiClick = value;
        }

        private void Update()
        {
            if (!enableMouseUiClick || Mouse.current == null || EventSystem.current == null)
                return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
                ClickUiAt(Mouse.current.position.ReadValue());
        }

        public bool ClickUiAt(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            RefreshWorldCanvasCameras();
            raycastResults.Clear();

            var pointerData = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                position = screenPosition,
                pressPosition = screenPosition,
                clickCount = 1,
                clickTime = Time.unscaledTime
            };

            eventSystem.RaycastAll(pointerData, raycastResults);
            var target = FirstClickable(raycastResults);
            if (target == null)
                return false;

            eventSystem.SetSelectedGameObject(target, pointerData);
            ExecuteEvents.ExecuteHierarchy(target, pointerData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(target, pointerData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(target, pointerData, ExecuteEvents.pointerClickHandler);
            return true;
        }

        private static GameObject FirstClickable(IReadOnlyList<RaycastResult> results)
        {
            for (var index = 0; index < results.Count; index++)
            {
                var gameObject = results[index].gameObject;
                if (gameObject == null)
                    continue;

                var clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(gameObject);
                if (clickHandler != null)
                    return clickHandler;

                var submitHandler = ExecuteEvents.GetEventHandler<ISubmitHandler>(gameObject);
                if (submitHandler != null)
                    return submitHandler;
            }

            return null;
        }

        private static void RefreshWorldCanvasCameras()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            foreach (var canvas in canvases)
            {
                if (canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
                    canvas.worldCamera = mainCamera;
            }
        }
    }
}
