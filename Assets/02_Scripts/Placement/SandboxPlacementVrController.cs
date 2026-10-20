using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR;
using LadderLab.PLC;
using LadderLab.Wiring;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LadderLab.Placement
{
    [DisallowMultipleComponent]
    public sealed class SandboxPlacementVrController : MonoBehaviour
    {
        [SerializeField] private EquipmentPlacementController placementController;
        [SerializeField] private PlacementCursor placementCursor;
        [SerializeField] private WiringModeController wiringModeController;
        [SerializeField] private Transform rightRayProxy;
        [SerializeField] private GameObject paletteRoot;
        [SerializeField, Min(0.1f)] private float rightRayDistance = 14f;
        [SerializeField, Min(1)] private int paletteItemsPerPage = 4;
        [SerializeField, Min(1f)] private float paletteItemSpacing = 90f;
        [SerializeField, Min(0.02f)] private float pageDragThreshold = 0.18f;
        [SerializeField, Min(0.05f)] private float placementDoubleClickSeconds = 0.35f;
        private const float RuntimePaletteItemSpacing = 70f;
        private float EffectivePaletteItemSpacing => Mathf.Min(Mathf.Max(1f, paletteItemSpacing), RuntimePaletteItemSpacing);

        private static readonly PaletteCategory[] PalettePageOrder =
        {
            PaletteCategory.Conveyors,
            PaletteCategory.Controllers,
            PaletteCategory.ProcessingMachines,
            PaletteCategory.InputControls,
            PaletteCategory.OutputControls
        };

        private static readonly string[] PaletteEquipmentOrder =
        {
            "conveyor_normal_line_05",
            "conveyor_long_line_04",
            "conveyor_corner_right_line_03",
            "conveyor_corner_left_line_03",
            "wiring_controller_3",
            "wiring_controller_2",
            "processing_line_06",
            "processing_line_07",
            "input_start_button",
            "input_stop_button",
            "input_emergency_switch",
            "sensor",
            "output_rotary_knob"
        };

        private readonly List<SandboxEquipmentPaletteButton> buttons = new();
        private readonly Dictionary<SandboxEquipmentPaletteButton, int> paletteButtonSlots = new();
        private readonly Dictionary<SandboxEquipmentPaletteButton, PaletteCategory> paletteButtonCategories = new();
        private readonly List<SandboxEquipmentPaletteButton> categoryVisibleButtons = new();
        private readonly List<UnityEngine.XR.InputDevice> inputDevices = new();
        private readonly RaycastHit[] paletteRaycastHits = new RaycastHit[32];
        private SandboxPalettePageButton previousPageButton;
        private SandboxPalettePageButton nextPageButton;
        private TMP_Text pageLabel;
        private TMP_FontAsset koreanPaletteFont;
        private PaletteCategory currentPaletteCategory = PaletteCategory.Conveyors;
        private int currentPalettePage;
        private bool paletteDragActive;
        private bool paletteDragPageMoved;
        private Vector3 paletteDragStartWorld;
        private bool placementModeToggled;
        private bool previousLeftPrimaryButton;
        private bool previousLeftSecondaryButton;
        private bool previousLeftGrip;
        private bool previousRightSecondaryButton;
        private bool previousRightGrip;
        private bool previousRightTrigger;

        public void Configure(
            EquipmentPlacementController targetPlacementController,
            PlacementCursor targetPlacementCursor,
            Transform targetRightRayProxy)
        {
            placementController = targetPlacementController;
            placementCursor = targetPlacementCursor;
            rightRayProxy = targetRightRayProxy;
        }

        private void Start()
        {
            EnsureWiringModeController();
            RebuildPaletteUi();
            EnsurePaletteUiRaycaster();
            SetPaletteVisible(false);
            placementController?.SetPlacementModeActive(false);
            wiringModeController?.SetWiringModeActive(false);
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;

            var leftPrimaryButton = ReadButton(InputDeviceCharacteristics.Left, UnityEngine.XR.CommonUsages.primaryButton);
            var leftSecondaryButton = ReadButton(InputDeviceCharacteristics.Left, UnityEngine.XR.CommonUsages.secondaryButton);
            var leftGrip = ReadGrip(InputDeviceCharacteristics.Left);
            var rightSecondaryButton = ReadButton(InputDeviceCharacteristics.Right, UnityEngine.XR.CommonUsages.secondaryButton);
            var rightGrip = ReadGrip(InputDeviceCharacteristics.Right);
            var rightTrigger = ReadTrigger(InputDeviceCharacteristics.Right);
            if (rightSecondaryButton && !previousRightSecondaryButton)
            {
                var nextWiringMode = wiringModeController != null && !wiringModeController.WiringModeActive;
                if (nextWiringMode)
                {
                    placementModeToggled = false;
                    placementController?.SetPlacementModeActive(false);
                    SetPaletteVisible(false);
                    placementCursor?.SetContinuousRaycastActive(true);
                }

                wiringModeController?.SetWiringModeActive(nextWiringMode);
            }

            if (wiringModeController != null && wiringModeController.WiringModeActive)
            {
                previousLeftPrimaryButton = leftPrimaryButton;
                previousLeftSecondaryButton = leftSecondaryButton;
                previousLeftGrip = leftGrip;
                previousRightSecondaryButton = rightSecondaryButton;
                previousRightGrip = rightGrip;
                previousRightTrigger = rightTrigger;
                return;
            }

            if (leftPrimaryButton && !previousLeftPrimaryButton)
            {
                placementModeToggled = !placementModeToggled;
                placementController?.SetPlacementModeActive(placementModeToggled);
            }

            if (placementModeToggled && leftSecondaryButton && !previousLeftSecondaryButton)
                placementController?.RotatePlacement();

            if (placementModeToggled && leftGrip && !previousLeftGrip)
            {
                if (placementController != null &&
                    !placementController.RemoveSelectedPlacedEquipment() &&
                    TryGetXrHandRay(XRNode.LeftHand, out var leftGripRay))
                {
                    placementController.TryRemovePlacedEquipmentFromRay(leftGripRay);
                }
            }

            SetPaletteVisible(placementModeToggled);
            placementCursor?.SetContinuousRaycastActive(placementModeToggled);

            HandleRightGrip(placementModeToggled, rightGrip);
            if (placementModeToggled && rightTrigger && !previousRightTrigger)
                HandleRightTriggerPressed();

            previousLeftPrimaryButton = leftPrimaryButton;
            previousLeftSecondaryButton = leftSecondaryButton;
            previousLeftGrip = leftGrip;
            previousRightSecondaryButton = rightSecondaryButton;
            previousRightGrip = rightGrip;
            previousRightTrigger = rightTrigger;
        }

        private void HandleRightGrip(bool paletteVisible, bool rightGrip)
        {
            if (rightGrip && !previousRightGrip)
            {
                StartPaletteDrag(paletteVisible);
                return;
            }

            if (rightGrip)
            {
                UpdatePaletteDrag();
                return;
            }

            if (!rightGrip && previousRightGrip)
                EndPaletteDrag(paletteVisible);
        }

        private void HandleRightTriggerPressed()
        {
            if (TrySelectPaletteButton())
                return;

            if (placementController != null &&
                placementController.SelectedEquipmentIsAttachment &&
                placementController.PlaceSelectedEquipment() != null)
            {
                return;
            }

            if (placementController != null &&
                placementController.TryRemovePlacedEquipmentDoubleClickFromCurrentRay(placementDoubleClickSeconds))
            {
                return;
            }

            if (placementController != null &&
                placementController.HasSelectedPlacedEquipment &&
                placementController.TryRepositionSelectedPlacedEquipment())
            {
                return;
            }

            if (placementController != null && placementController.TrySelectPlacedEquipmentFromCurrentRay())
                return;

            placementController?.PlaceSelectedEquipment();
        }

        private void StartPaletteDrag(bool paletteVisible)
        {
            paletteDragActive = false;
            paletteDragPageMoved = false;

            if (!paletteVisible || !TryGetSelectionRay(out var ray) || !TryRaycastPalette(ray, out var hit))
                return;

            paletteDragActive = true;
            paletteDragStartWorld = hit.point;
        }

        private void UpdatePaletteDrag()
        {
            if (!paletteDragActive || paletteDragPageMoved || !TryGetSelectionRay(out var ray) || !TryRaycastPalette(ray, out var hit))
                return;

            var dragOffset = hit.point - paletteDragStartWorld;
            var horizontalOffset = ResolvePaletteHorizontalOffset(dragOffset);
            if (Mathf.Abs(horizontalOffset) < pageDragThreshold)
                return;

            MovePalettePage(horizontalOffset < 0f ? 1 : -1);
            paletteDragPageMoved = true;
        }

        private void EndPaletteDrag(bool paletteVisible)
        {
            if (paletteDragPageMoved)
            {
                paletteDragActive = false;
                paletteDragPageMoved = false;
                return;
            }

            paletteDragActive = false;
        }

        private bool TryRaycastPalette(Ray ray, out RaycastHit closestHit)
        {
            var hitCount = Physics.RaycastNonAlloc(
                ray,
                paletteRaycastHits,
                rightRayDistance,
                ~0,
                QueryTriggerInteraction.Collide);
            if (hitCount <= 0)
            {
                closestHit = default;
                return false;
            }

            Array.Sort(paletteRaycastHits, 0, hitCount, RaycastHitDistanceComparer.Instance);
            for (var index = 0; index < hitCount; index++)
            {
                var hit = paletteRaycastHits[index];
                var collider = hit.collider;
                if (collider == null)
                    continue;

                if (collider.GetComponentInParent<SandboxEquipmentPaletteButton>() != null ||
                    collider.GetComponentInParent<SandboxPalettePageButton>() != null)
                {
                    closestHit = hit;
                    return true;
                }
            }

            closestHit = default;
            return false;
        }

        private float ResolvePaletteHorizontalOffset(Vector3 worldOffset)
        {
            if (paletteRoot != null)
                return Vector3.Dot(worldOffset, paletteRoot.transform.right);

            return worldOffset.x;
        }

        private bool TrySelectPaletteButton()
        {
            if (!TryGetSelectionRay(out var ray))
                return false;

            return TryApplyPaletteRayAction(ray);
        }

        private bool TryGetSelectionRay(out Ray ray)
        {
            if (placementCursor != null && placementCursor.TryGetCurrentRay(out ray))
                return true;

            if (rightRayProxy != null)
            {
                ray = new Ray(rightRayProxy.position, rightRayProxy.forward);
                return true;
            }

            ray = default;
            return false;
        }

        private bool ApplyPaletteButtonSelection(SandboxEquipmentPaletteButton button)
        {
            if (button == null || placementController == null || !button.ApplySelection(placementController))
                return false;

            placementCursor?.SetGhostSize(EstimatePrefabSize(button.Equipment.Prefab));
            return true;
        }

        private bool TryApplyPaletteRayAction(Ray ray)
        {
            var hitCount = Physics.RaycastNonAlloc(
                ray,
                paletteRaycastHits,
                rightRayDistance,
                ~0,
                QueryTriggerInteraction.Collide);
            if (hitCount <= 0)
                return false;

            Array.Sort(paletteRaycastHits, 0, hitCount, RaycastHitDistanceComparer.Instance);
            for (var index = 0; index < hitCount; index++)
            {
                var collider = paletteRaycastHits[index].collider;
                var pageButton = collider != null ? collider.GetComponentInParent<SandboxPalettePageButton>() : null;
                if (pageButton != null)
                {
                    pageButton.ApplyPageMove();
                    return true;
                }

                var button = collider != null ? collider.GetComponentInParent<SandboxEquipmentPaletteButton>() : null;
                if (button != null && button.Equipment != null && placementController != null)
                    return ApplyPaletteButtonSelection(button);
            }

            return false;
        }

        private void RebuildPaletteUi()
        {
            buttons.Clear();
            paletteButtonSlots.Clear();
            paletteButtonCategories.Clear();
            categoryVisibleButtons.Clear();
            previousPageButton = null;
            nextPageButton = null;
            pageLabel = null;

            if (paletteRoot == null)
                return;

            ClearPaletteChildren();
            CreatePaletteBackground();
            pageLabel = CreateRuntimeLabel("Palette Page Label", new Vector2(0f, 145f), new Vector2(360f, 44f), 28f, TextAlignmentOptions.Center);
            CreateRuntimeEquipmentButtons();
            previousPageButton = CreateRuntimePageButton("Palette Page Previous", "<", -1, new Vector2(-120f, -195f));
            nextPageButton = CreateRuntimePageButton("Palette Page Next", ">", 1, new Vector2(120f, -195f));
            ApplyPalettePage();
        }

        private void ClearPaletteChildren()
        {
            for (var index = paletteRoot.transform.childCount - 1; index >= 0; index--)
            {
                var child = paletteRoot.transform.GetChild(index);
                if (child == null)
                    continue;

                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        private void CreatePaletteBackground()
        {
            var background = new GameObject("Palette Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background.transform.SetParent(paletteRoot.transform, false);

            var rectTransform = background.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;

            var image = background.GetComponent<Image>();
            image.color = new Color(0.08f, 0.1f, 0.12f, 0.92f);
            image.raycastTarget = false;
        }

        private void CreateRuntimeEquipmentButtons()
        {
            var equipmentList = LoadPaletteEquipment();
            for (var index = 0; index < equipmentList.Count; index++)
            {
                var equipment = equipmentList[index];
                if (equipment == null)
                    continue;

                var button = CreateRuntimeEquipmentButton(equipment);
                buttons.Add(button);
                paletteButtonSlots[button] = index;
                paletteButtonCategories[button] = ResolvePaletteCategory(button);
            }
        }

        private SandboxEquipmentPaletteButton CreateRuntimeEquipmentButton(EquipmentPlacementData equipment)
        {
            var buttonObject = new GameObject(
                $"Palette Equipment {equipment.DisplayName}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(BoxCollider),
                typeof(SandboxEquipmentPaletteButton));
            buttonObject.transform.SetParent(paletteRoot.transform, false);

            var rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(340f, 58f);
            rectTransform.anchoredPosition = Vector2.zero;

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.12f, 0.16f, 0.18f, 0.96f);
            image.raycastTarget = true;

            var uiButton = buttonObject.GetComponent<Button>();
            uiButton.targetGraphic = image;

            var collider = buttonObject.GetComponent<BoxCollider>();
            collider.size = new Vector3(340f, 58f, 48f);
            collider.center = Vector3.zero;

            var paletteButton = buttonObject.GetComponent<SandboxEquipmentPaletteButton>();
            paletteButton.Configure(equipment, placementController);

            var label = CreateRuntimeLabel("Label", Vector2.zero, new Vector2(320f, 46f), 24f, TextAlignmentOptions.Center);
            label.transform.SetParent(buttonObject.transform, false);
            label.text = equipment.DisplayName;
            ApplyPaletteFont(label);

            return paletteButton;
        }

        private SandboxPalettePageButton CreateRuntimePageButton(string objectName, string label, int pageDirection, Vector2 anchoredPosition)
        {
            var buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(BoxCollider),
                typeof(SandboxPalettePageButton));
            buttonObject.transform.SetParent(paletteRoot.transform, false);

            var rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(96f, 46f);
            rectTransform.anchoredPosition = anchoredPosition;

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.12f, 0.16f, 0.18f, 0.96f);
            image.raycastTarget = true;

            var uiButton = buttonObject.GetComponent<Button>();
            uiButton.targetGraphic = image;

            var collider = buttonObject.GetComponent<BoxCollider>();
            collider.size = new Vector3(96f, 46f, 48f);
            collider.center = Vector3.zero;

            var pageButton = buttonObject.GetComponent<SandboxPalettePageButton>();
            pageButton.Configure(this, pageDirection);

            var text = CreateRuntimeLabel("Label", Vector2.zero, new Vector2(80f, 38f), 28f, TextAlignmentOptions.Center);
            text.transform.SetParent(buttonObject.transform, false);
            text.text = label;

            return pageButton;
        }

        private TMP_Text CreateRuntimeLabel(string objectName, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            var labelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(paletteRoot.transform, false);

            var rectTransform = labelObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = anchoredPosition;

            var text = labelObject.GetComponent<TMP_Text>();
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            ApplyPaletteFont(text);
            return text;
        }

        private List<EquipmentPlacementData> LoadPaletteEquipment()
        {
#if UNITY_EDITOR
            return LoadOrderedPaletteEquipment();
#else
            return new List<EquipmentPlacementData>();
#endif
        }

#if UNITY_EDITOR
        private static List<EquipmentPlacementData> LoadOrderedPaletteEquipment()
        {
            var equipmentById = new Dictionary<string, EquipmentPlacementData>(StringComparer.OrdinalIgnoreCase);
            var guids = AssetDatabase.FindAssets("t:EquipmentPlacementData", new[] { "Assets/04_ScriptableObjects/Placement" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var equipment = AssetDatabase.LoadAssetAtPath<EquipmentPlacementData>(path);
                if (equipment == null || string.IsNullOrWhiteSpace(equipment.EquipmentId))
                    continue;

                equipmentById[equipment.EquipmentId] = equipment;
            }

            var orderedEquipment = new List<EquipmentPlacementData>();
            foreach (var equipmentId in PaletteEquipmentOrder)
            {
                if (equipmentById.TryGetValue(equipmentId, out var equipment))
                    orderedEquipment.Add(equipment);
            }

            return orderedEquipment;
        }
#endif

        public void MovePalettePage(int pageDirection)
        {
            if (pageDirection == 0)
                return;

            var categories = PalettePageOrder;
            var currentIndex = Array.IndexOf(categories, currentPaletteCategory);
            if (currentIndex < 0)
                currentIndex = 0;

            var nextIndex = (currentIndex + Math.Sign(pageDirection) + categories.Length) % categories.Length;
            currentPaletteCategory = categories[nextIndex];
            currentPalettePage = 0;
            ApplyPalettePage();
        }

        private void ApplyPalettePage()
        {
            var itemsPerPage = Mathf.Max(1, paletteItemsPerPage);
            var maxPage = GetMaxPalettePage();
            currentPalettePage = 0;
            var firstVisibleIndex = currentPalettePage * itemsPerPage;
            var lastVisibleIndex = firstVisibleIndex + itemsPerPage;
            CollectCurrentCategoryButtons();

            foreach (var button in buttons)
            {
                if (button != null)
                    button.gameObject.SetActive(false);
            }

            for (var index = 0; index < categoryVisibleButtons.Count; index++)
            {
                var button = categoryVisibleButtons[index];
                if (button == null)
                    continue;

                var visible = index >= firstVisibleIndex && index < lastVisibleIndex;
                button.gameObject.SetActive(visible);

                if (!visible)
                    continue;

                var rectTransform = button.GetComponent<RectTransform>();
                if (rectTransform != null)
                    rectTransform.anchoredPosition = GetPageItemPosition(index - firstVisibleIndex, rectTransform.anchoredPosition);

                ApplyEquipmentButtonText(button.gameObject, button.Equipment.DisplayName);
            }

            SetPageButtonState(previousPageButton, true);
            SetPageButtonState(nextPageButton, true);
            if (pageLabel != null)
            {
                ApplyPaletteFont(pageLabel);
                pageLabel.text = GetCategoryLabel(currentPaletteCategory);
            }
        }

        private void ApplyEquipmentButtonText(GameObject buttonObject, string label)
        {
            if (buttonObject == null)
                return;

            var texts = buttonObject.GetComponentsInChildren<TMP_Text>(true);
            foreach (var text in texts)
            {
                if (text == null)
                    continue;

                ApplyPaletteFont(text);
                text.text = label;
            }
        }

        private void ApplyPaletteFont(TMP_Text text)
        {
            if (text == null)
                return;

            koreanPaletteFont ??= Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSansCJKkr-Regular SDF");
            if (koreanPaletteFont != null)
                text.font = koreanPaletteFont;
        }

        private static void SetPageButtonState(SandboxPalettePageButton pageButton, bool interactable)
        {
            if (pageButton == null)
                return;

            if (!pageButton.gameObject.activeSelf)
                pageButton.gameObject.SetActive(true);

            var uiButton = pageButton.GetComponent<Button>();
            if (uiButton != null)
                uiButton.interactable = interactable;
        }

        private Vector2 GetPageItemPosition(int pageIndex, Vector2 currentPosition)
        {
            if (buttons.Count == 0 || buttons[0] == null)
                return currentPosition;

            var templateRect = buttons[0].GetComponent<RectTransform>();
            return templateRect == null
                ? currentPosition
                : new Vector2(0f, 80f - EffectivePaletteItemSpacing * pageIndex);
        }

        private int GetMaxPalettePage()
        {
            var itemsPerPage = Mathf.Max(1, paletteItemsPerPage);
            var count = CountCurrentCategoryButtons();
            return Mathf.Max(0, (count - 1) / itemsPerPage);
        }

        public void SetPaletteCategory(PaletteCategory category)
        {
            if (currentPaletteCategory == category)
                return;

            currentPaletteCategory = category;
            currentPalettePage = 0;
            ApplyPalettePage();
        }

        private void CollectCurrentCategoryButtons()
        {
            categoryVisibleButtons.Clear();
            foreach (var button in buttons)
            {
                if (button != null && IsCurrentCategoryButton(button))
                    categoryVisibleButtons.Add(button);
            }
        }

        private int CountCurrentCategoryButtons()
        {
            var count = 0;
            foreach (var button in buttons)
            {
                if (button != null && IsCurrentCategoryButton(button))
                    count++;
            }

            return count;
        }

        private bool IsCurrentCategoryButton(SandboxEquipmentPaletteButton button)
        {
            return button != null &&
                   paletteButtonCategories.TryGetValue(button, out var category) &&
                   category == currentPaletteCategory;
        }

        private static PaletteCategory ResolvePaletteCategory(SandboxEquipmentPaletteButton button)
        {
            var equipment = button != null ? button.Equipment : null;
            if (equipment == null)
                return PaletteCategory.OutputControls;

            return equipment.PlacementKind switch
            {
                EquipmentPlacementKind.Conveyor => PaletteCategory.Conveyors,
                EquipmentPlacementKind.Sensor => PaletteCategory.InputControls,
                EquipmentPlacementKind.ProcessingEquipment => PaletteCategory.ProcessingMachines,
                _ => ResolveGenericPaletteCategory(equipment.EquipmentId)
            };
        }

        private static PaletteCategory ResolveGenericPaletteCategory(string equipmentId)
        {
            if (string.IsNullOrWhiteSpace(equipmentId))
                return PaletteCategory.OutputControls;

            if (equipmentId.StartsWith("wiring_controller_", StringComparison.OrdinalIgnoreCase))
                return PaletteCategory.Controllers;

            if (equipmentId.Contains("processing_line", StringComparison.OrdinalIgnoreCase))
                return PaletteCategory.ProcessingMachines;

            if (equipmentId.Contains("rotary", StringComparison.OrdinalIgnoreCase) ||
                equipmentId.Contains("speed_dial", StringComparison.OrdinalIgnoreCase) ||
                equipmentId.Contains("knob", StringComparison.OrdinalIgnoreCase))
            {
                return PaletteCategory.OutputControls;
            }

            if (equipmentId.StartsWith("input_", StringComparison.OrdinalIgnoreCase) ||
                equipmentId.Contains("button", StringComparison.OrdinalIgnoreCase) ||
                equipmentId.Contains("switch", StringComparison.OrdinalIgnoreCase) ||
                equipmentId.Contains("sensor", StringComparison.OrdinalIgnoreCase))
            {
                return PaletteCategory.InputControls;
            }

            return PaletteCategory.OutputControls;
        }

        private static string GetCategoryLabel(PaletteCategory category)
        {
            return category switch
            {
                PaletteCategory.Conveyors => "\uCEE8\uBCA0\uC774\uC5B4\uBCA8\uD2B8",
                PaletteCategory.Controllers => "\uCEE8\uD2B8\uB864\uB7EC",
                PaletteCategory.ProcessingMachines => "\uC81C\uC791\uAE30",
                PaletteCategory.InputControls => "\uC785\uB825\uC81C\uC5B4",
                PaletteCategory.OutputControls => "\uCD9C\uB825\uC81C\uC5B4",
                _ => "\uCD9C\uB825\uC81C\uC5B4"
            };
        }

        private void EnsurePaletteUiRaycaster()
        {
            if (paletteRoot == null)
                return;

            var canvas = paletteRoot.GetComponent<Canvas>();
            if (canvas == null || canvas.GetComponent<TrackedDeviceGraphicRaycaster>() != null)
                return;

            canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        }

        private void SetPaletteVisible(bool visible)
        {
            if (paletteRoot != null && paletteRoot.activeSelf != visible)
                paletteRoot.SetActive(visible);
        }

        private void EnsureWiringModeController()
        {
            if (wiringModeController == null)
                wiringModeController = GetComponent<WiringModeController>();
            if (wiringModeController == null)
                wiringModeController = gameObject.AddComponent<WiringModeController>();

            var scanController = FindAnyObjectByType<PlcScanController>();
            wiringModeController.Configure(scanController, placementCursor, rightRayProxy);
        }

        private bool ReadButton(InputDeviceCharacteristics handedness, InputFeatureUsage<bool> buttonUsage)
        {
            var node = (handedness & InputDeviceCharacteristics.Left) != 0 ? XRNode.LeftHand : XRNode.RightHand;
            var nodeDevice = InputDevices.GetDeviceAtXRNode(node);
            if (nodeDevice.isValid &&
                nodeDevice.TryGetFeatureValue(buttonUsage, out var nodePressed) &&
                nodePressed)
            {
                return true;
            }

            inputDevices.Clear();
            InputDevices.GetDevicesWithCharacteristics(handedness | InputDeviceCharacteristics.Controller, inputDevices);
            foreach (var device in inputDevices)
            {
                if (device.TryGetFeatureValue(buttonUsage, out var pressed) && pressed)
                    return true;
            }

            return false;
        }

        private bool ReadGrip(InputDeviceCharacteristics handedness)
        {
            return ReadButton(handedness, UnityEngine.XR.CommonUsages.gripButton);
        }

        private bool ReadTrigger(InputDeviceCharacteristics handedness)
        {
            var node = (handedness & InputDeviceCharacteristics.Left) != 0 ? XRNode.LeftHand : XRNode.RightHand;
            var nodeDevice = InputDevices.GetDeviceAtXRNode(node);
            if (TryReadTrigger(nodeDevice))
                return true;

            inputDevices.Clear();
            InputDevices.GetDevicesWithCharacteristics(handedness | InputDeviceCharacteristics.Controller, inputDevices);
            foreach (var device in inputDevices)
            {
                if (TryReadTrigger(device))
                    return true;
            }

            return false;
        }

        private static bool TryReadTrigger(UnityEngine.XR.InputDevice device)
        {
            if (!device.isValid)
                return false;

            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out var pressed) && pressed)
                return true;

            return device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out var value) && value > 0.55f;
        }

        private bool TryGetXrHandRay(XRNode node, out Ray ray)
        {
            var nodeDevice = InputDevices.GetDeviceAtXRNode(node);
            if (TryReadDeviceRay(nodeDevice, out ray))
                return true;

            var handedness = node == XRNode.LeftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right;
            inputDevices.Clear();
            InputDevices.GetDevicesWithCharacteristics(handedness | InputDeviceCharacteristics.Controller, inputDevices);
            foreach (var device in inputDevices)
            {
                if (TryReadDeviceRay(device, out ray))
                    return true;
            }

            ray = default;
            return false;
        }

        private static bool TryReadDeviceRay(UnityEngine.XR.InputDevice device, out Ray ray)
        {
            if (device.isValid &&
                device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out var position) &&
                device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out var rotation))
            {
                ray = new Ray(position, rotation * Vector3.forward);
                return true;
            }

            ray = default;
            return false;
        }

        private static Vector3 EstimatePrefabSize(GameObject prefab)
        {
            if (prefab == null)
                return Vector3.one;

            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            var hasBounds = false;
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds ? bounds.size : Vector3.one;
        }

        private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new();

            public int Compare(RaycastHit x, RaycastHit y)
            {
                return x.distance.CompareTo(y.distance);
            }
        }

    }

    public enum PaletteCategory
    {
        Conveyors,
        Controllers,
        ProcessingMachines,
        InputControls,
        OutputControls
    }

    [DisallowMultipleComponent]
    public sealed class SandboxPalettePageButton : MonoBehaviour
    {
        [SerializeField] private SandboxPlacementVrController placementVrController;
        [SerializeField] private int pageDirection = 1;

        public void Configure(SandboxPlacementVrController targetPlacementVrController, int targetPageDirection)
        {
            placementVrController = targetPlacementVrController;
            pageDirection = targetPageDirection < 0 ? -1 : 1;
        }

        // 페이지 이동
        public void ApplyPageMove()
        {
            placementVrController?.MovePalettePage(pageDirection);
        }
    }

}
