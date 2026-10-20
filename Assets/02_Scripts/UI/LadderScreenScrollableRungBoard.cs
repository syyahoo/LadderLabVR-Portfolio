using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using LadderLab.PLC;
using UnityEngine;
using UnityEngine.UI;

namespace LadderLab.UI
{
    [DisallowMultipleComponent]
    public sealed class LadderScreenScrollableRungBoard : MonoBehaviour, IPlcProgram, IPlcResettableProgram
    {
        private const string BoardName = "Scrollable Rung Board";
        private const string RungContentPath = BoardName + "/Rung Scroll View/Viewport/Content";
        private const string AddRungButtonPath = BoardName + "/Add Rung Button";
        private const string UploadButtonPath = BoardName + "/Upload Program Memory Button";
        private const string ResetButtonPath = BoardName + "/Reset Program Memory Button";
        private const string PlayButtonPath = BoardName + "/Play Scan Button";
        private const int DefaultRungCount = 5;
        private const int MaxRungCount = 16;
        private const int ContactSlotsPerRung = 8;
        private const float RungHeight = 116f;

        [SerializeField] private int rungCount = DefaultRungCount;
        [SerializeField] private Color boardColor = new Color(0.024f, 0.047f, 0.052f, 1f);
        [SerializeField] private float scanStepSeconds = 0.16f;
        [SerializeField] private PlcScanController scanController;
        [SerializeField] private LadderRuntime fallbackRuntime;
        private Coroutine scanRoutine;
        private Button playButton;
        private TMP_Text playButtonLabel;
        private bool programUploaded;
        private bool realtimeFeedbackActive;
        private List<UploadedRung> uploadedRungs = new();

        private void OnEnable()
        {
            BindStaticControls();
            RefreshPlayButtonState();
        }

        private void Update()
        {
            if (!realtimeFeedbackActive)
                return;

            ShowEnergizedScanState();
        }

        private void OnValidate()
        {
            rungCount = Mathf.Clamp(rungCount, 1, MaxRungCount);
        }

        private RectTransform FindRungContent()
        {
            return transform.Find(RungContentPath) as RectTransform;
        }

        private Transform FindRung(int rungIndex)
        {
            return FindRungContent()?.Find(GetRungName(rungIndex));
        }

        private static string GetRungName(int rungIndex)
        {
            return $"Rung {rungIndex + 1:00}";
        }

        // PLC 런타임 연결
        public void ConfigureRuntime(PlcScanController targetScanController, LadderRuntime targetFallbackRuntime)
        {
            scanController = targetScanController;
            fallbackRuntime = targetFallbackRuntime;
        }

        private PlcScanController ResolveScanController()
        {
            if (scanController != null)
                return scanController;

            scanController = GetComponentInParent<PlcScanController>();
            if (scanController != null)
                return scanController;

            var parent = transform.parent;
            while (parent != null)
            {
                scanController = parent.GetComponentInChildren<PlcScanController>(true);
                if (scanController != null)
                    return scanController;

                parent = parent.parent;
            }

            scanController = Object.FindAnyObjectByType<PlcScanController>();
            return scanController;
        }

        private LadderRuntime ResolveFallbackRuntime()
        {
            if (fallbackRuntime != null)
                return fallbackRuntime;

            var controller = ResolveScanController();
            if (controller != null)
                fallbackRuntime = controller.GetComponent<LadderRuntime>();

            if (fallbackRuntime == null)
                fallbackRuntime = Object.FindAnyObjectByType<LadderRuntime>();

            return fallbackRuntime;
        }

        public void AddRung()
        {
            if (this == null)
                return;

            if (rungCount >= MaxRungCount)
                return;

            var content = FindRungContent();
            if (content == null)
                return;

            var nextRung = content.Find(GetRungName(rungCount));
            if (nextRung == null)
            {
                CreateRung(content, rungCount);
                nextRung = content.Find(GetRungName(rungCount));
            }

            if (nextRung == null)
                return;

            nextRung.gameObject.SetActive(true);
            rungCount = Mathf.Clamp(rungCount + 1, 1, MaxRungCount);
            content.sizeDelta = new Vector2(0f, Mathf.Max(rungCount * RungHeight, RungHeight));
            Canvas.ForceUpdateCanvases();
        }

        public void CreateParallelBranch(int targetRungIndex, int targetContactSlotIndex)
        {
            if (this == null)
                return;

            if (targetRungIndex < 0 || targetContactSlotIndex < 0 || targetContactSlotIndex >= ContactSlotsPerRung)
                return;

            var rung = FindRung(targetRungIndex);
            if (rung == null)
                return;

            var currentSpan = GetActiveParallelBranchSpan(rung, targetRungIndex, targetContactSlotIndex);
            var targetSpan = Mathf.Min(currentSpan + 1, ContactSlotsPerRung - targetContactSlotIndex);
            EnsureParallelBranchGroup(rung, this, targetRungIndex, targetContactSlotIndex, targetSpan);
            SetParallelBranchSpanActive(rung, targetRungIndex, targetContactSlotIndex, targetSpan);
        }

        public bool ClearParallelBranch(int targetRungIndex, int targetContactSlotIndex)
        {
            if (this == null)
                return false;

            if (targetRungIndex < 0 || targetContactSlotIndex < 0 || targetContactSlotIndex >= ContactSlotsPerRung)
                return false;

            var rung = FindRung(targetRungIndex);
            return rung != null && SetParallelBranchSpanActive(rung, targetRungIndex, targetContactSlotIndex, 0) > 0;
        }

        public void PlayScanVisualization()
        {
            if (this == null)
                return;

            if (!programUploaded)
            {
                realtimeFeedbackActive = false;
                RefreshPlayButtonState();
                return;
            }

            var controller = ResolveScanController();
            if (controller != null)
            {
                if (realtimeFeedbackActive)
                {
                    realtimeFeedbackActive = false;
                    controller.Stop();
                    ClearScanHighlights(GetScanOrderedSlots());
                    RefreshPlayButtonState();
                    return;
                }

                controller.SetProgram(this);
                controller.Run();
                realtimeFeedbackActive = true;
                RefreshPlayButtonState();
                return;
            }

            if (!Application.isPlaying)
            {
                ShowEnergizedScanState();
                return;
            }

            if (scanRoutine != null)
                StopCoroutine(scanRoutine);

            scanRoutine = StartCoroutine(ScanVisualizationRoutine());
        }

        private static int GetActiveParallelBranchSpan(Transform rung, int targetRungIndex, int targetContactSlotIndex)
        {
            var groupPrefix = GetParallelGroupPrefix(targetRungIndex, targetContactSlotIndex);
            var activeSpan = 0;

            for (var span = 1; span <= ContactSlotsPerRung - targetContactSlotIndex; span++)
            {
                var slot = rung.Find($"{groupPrefix} Slot {span:00}");
                if (slot != null && slot.gameObject.activeSelf)
                    activeSpan = span;
            }

            return activeSpan;
        }

        private static int SetParallelBranchSpanActive(Transform rung, int targetRungIndex, int targetContactSlotIndex, int activeSpan)
        {
            var groupPrefix = GetParallelGroupPrefix(targetRungIndex, targetContactSlotIndex);
            var changedCount = 0;
            var maxSpan = ContactSlotsPerRung - targetContactSlotIndex;
            activeSpan = Mathf.Clamp(activeSpan, 0, maxSpan);

            for (var index = 0; index < rung.childCount; index++)
            {
                var child = rung.GetChild(index);
                if (!child.name.StartsWith(groupPrefix, System.StringComparison.Ordinal))
                    continue;

                var active = activeSpan > 0 && ShouldActivateParallelBranchChild(child.name, groupPrefix, activeSpan);
                child.gameObject.SetActive(active);
                changedCount++;
            }

            return changedCount;
        }

        private static bool ShouldActivateParallelBranchChild(string childName, string groupPrefix, int activeSpan)
        {
            if (childName == $"{groupPrefix} Left")
                return true;

            if (childName.StartsWith($"{groupPrefix} Slot ", System.StringComparison.Ordinal))
                return TryReadTrailingIndex(childName, out var slotIndex) && slotIndex <= activeSpan;

            if (childName.StartsWith($"{groupPrefix} Wire ", System.StringComparison.Ordinal) ||
                childName.StartsWith($"{groupPrefix} Right ", System.StringComparison.Ordinal))
                return TryReadTrailingIndex(childName, out var spanIndex) && spanIndex == activeSpan;

            return activeSpan == 1 && (childName == $"{groupPrefix} Wire" || childName == $"{groupPrefix} Right");
        }

        private static bool TryReadTrailingIndex(string value, out int index)
        {
            index = 0;
            var separator = value.LastIndexOf(' ');
            return separator >= 0 && separator < value.Length - 1 && int.TryParse(value[(separator + 1)..], out index);
        }

        private static string GetParallelGroupPrefix(int targetRungIndex, int targetContactSlotIndex)
        {
            return $"R{targetRungIndex + 1:00} Parallel S{targetContactSlotIndex + 1:00}";
        }

        [ContextMenu("Build Static Rung Board")]
        public void BuildStaticBoard()
        {
            if (this == null || !isActiveAndEnabled || transform is not RectTransform)
                return;

            RemoveExistingBoard();

            var board = CreatePanel(transform, BoardName, Vector2.zero, Vector2.one, boardColor);
            board.SetAsLastSibling();

            var addButton = CreatePanel(board, "Add Rung Button", new Vector2(0.895f, 0.925f), new Vector2(0.985f, 0.985f), new Color(0.17f, 0.50f, 0.44f, 1f));
            var button = addButton.gameObject.AddComponent<Button>();
            button.onClick.AddListener(AddRung);
            CreateText(addButton, "Label", "RUNG+", Vector2.zero, Vector2.one, 18f, TextAlignmentOptions.Center, Color.white);

            var uploadButton = CreatePanel(board, "Upload Program Memory Button", new Vector2(0.255f, 0.925f), new Vector2(0.365f, 0.985f), new Color(0.12f, 0.42f, 0.72f, 1f));
            var upload = uploadButton.gameObject.AddComponent<Button>();
            upload.onClick.AddListener(UploadProgramMemory);
            CreateText(uploadButton, "Label", "UPLOAD", Vector2.zero, Vector2.one, 16f, TextAlignmentOptions.Center, Color.white);

            var resetButton = CreatePanel(board, "Reset Program Memory Button", new Vector2(0.375f, 0.925f), new Vector2(0.495f, 0.985f), new Color(0.56f, 0.24f, 0.20f, 1f));
            var reset = resetButton.gameObject.AddComponent<Button>();
            reset.onClick.AddListener(ResetProgramMemory);
            CreateText(resetButton, "Label", "MEM RESET", Vector2.zero, Vector2.one, 15f, TextAlignmentOptions.Center, Color.white);

            var playButton = CreatePanel(board, "Play Scan Button", new Vector2(0.505f, 0.925f), new Vector2(0.595f, 0.985f), new Color(0.10f, 0.58f, 0.30f, 1f));
            var play = playButton.gameObject.AddComponent<Button>();
            play.onClick.AddListener(PlayScanVisualization);
            CreateText(playButton, "Label", "PLAY", Vector2.zero, Vector2.one, 18f, TextAlignmentOptions.Center, Color.white);

            var scrollRoot = CreatePanel(board, "Rung Scroll View", new Vector2(0.018f, 0.035f), new Vector2(0.985f, 0.908f), new Color(0.014f, 0.025f, 0.029f, 1f));
            var viewport = CreatePanel(scrollRoot, "Viewport", new Vector2(0f, 0f), new Vector2(0.975f, 1f), new Color(0f, 0f, 0f, 0.01f));
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = CreatePanel(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f), Color.clear);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, Mathf.Max(rungCount * RungHeight, RungHeight));
            content.anchoredPosition = Vector2.zero;

            var scrollbar = CreateScrollbar(scrollRoot);

            var scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.vertical = true;
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 42f;

            for (var rung = 0; rung < MaxRungCount; rung++)
            {
                var created = CreateRung(content, rung);
                created.gameObject.SetActive(rung < rungCount);
            }

            BindStaticControls();
        }

        private void BindStaticControls()
        {
            BindButton(AddRungButtonPath, AddRung);
            BindButton(UploadButtonPath, UploadProgramMemory);
            BindButton(ResetButtonPath, ResetProgramMemory);
            BindButton(PlayButtonPath, PlayScanVisualization);
            playButton = transform.Find(PlayButtonPath)?.GetComponent<Button>();
            playButtonLabel = playButton != null
                ? playButton.GetComponentInChildren<TMP_Text>(true)
                : null;
            RefreshPlayButtonState();
        }

        private void BindButton(string relativePath, UnityEngine.Events.UnityAction action)
        {
            var button = transform.Find(relativePath)?.GetComponent<Button>();
            if (button == null)
                return;

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private IEnumerator ScanVisualizationRoutine()
        {
            var slots = GetScanOrderedSlots();
            var backend = ResolveBackend();
            var energizedSlots = programUploaded
                ? GetUploadedEnergizedSlots(backend)
                : GetEnergizedSlots(slots, backend);
            ClearScanHighlights(slots);

            foreach (var slot in slots)
            {
                if (!slot.HasNode)
                    continue;

                var isEnergized = energizedSlots.Contains(slot);
                if (isEnergized)
                    slot.SetScanHighlight(true, true);

                yield return new WaitForSeconds(scanStepSeconds);

                if (isEnergized)
                    slot.SetScanHighlight(true, false);
            }

            scanRoutine = null;
        }

        public void UploadProgramMemory()
        {
            uploadedRungs = BuildUploadedProgramFromBoard();
            programUploaded = uploadedRungs.Count > 0;
            realtimeFeedbackActive = programUploaded;
            var controller = ResolveScanController();
            controller?.SetProgram(this);
            ClearScanHighlights(GetScanOrderedSlots());
            RefreshPlayButtonState();

            Debug.Log($"[LadderLab] Program memory uploaded from ladder board. Rungs={uploadedRungs.Count}");
        }

        public void ResetProgramMemory()
        {
            var runtime = ResolveFallbackRuntime();
            var controller = ResolveScanController();
            runtime?.ClearProgram();
            if (runtime != null)
                controller?.SetProgram(runtime);

            controller?.ResetAll();
            uploadedRungs.Clear();
            programUploaded = false;
            realtimeFeedbackActive = false;
            ClearScanHighlights(GetScanOrderedSlots());
            RefreshPlayButtonState();
            Debug.Log("[LadderLab] Program memory reset from ladder board.");
        }

        public void Execute(IPlcBackend backend)
        {
            if (!programUploaded || backend == null)
                return;

            ExecuteUploadedProgram(backend, null);
        }

        public void ResetState()
        {
            ClearScanHighlights(GetScanOrderedSlots());
        }

        private void RefreshPlayButtonState()
        {
            if (playButton != null)
                playButton.interactable = programUploaded;

            if (playButtonLabel != null)
                playButtonLabel.text = realtimeFeedbackActive ? "STOP" : "PLAY";
        }

        private List<UploadedRung> BuildUploadedProgramFromBoard()
        {
            var rungs = new List<UploadedRung>();
            var slots = GetComponentsInChildren<LadderScreenDiagramSlot>(true)
                .Where(slot => slot.gameObject.activeInHierarchy)
                .ToList();

            foreach (var rungGroup in slots.Where(slot => slot.RungIndex >= 0).GroupBy(slot => slot.RungIndex).OrderBy(group => group.Key))
            {
                var coilSlot = rungGroup
                    .Where(slot => slot.IsCoilSlot && slot.HasNode)
                    .OrderBy(slot => slot.transform.GetSiblingIndex())
                    .FirstOrDefault();
                if (coilSlot == null || coilSlot.NodeKind != LadderScreenNodeKind.Coil || string.IsNullOrWhiteSpace(coilSlot.AssignedAddress))
                    continue;

                var uploadedRung = new UploadedRung(coilSlot.AssignedAddress, coilSlot);
                var invalidRung = false;
                foreach (var contactSlot in rungGroup
                             .Where(slot => !slot.IsCoilSlot && !slot.IsParallelBranchSlot && slot.HasNode)
                             .OrderBy(slot => slot.ContactSlotIndex))
                {
                    if (!TryCreateUploadedContact(contactSlot, out var contact))
                    {
                        invalidRung = true;
                        break;
                    }

                    uploadedRung.MainContacts[contactSlot.ContactSlotIndex] = contact;
                }

                foreach (var branchGroup in rungGroup
                             .Where(slot => slot.IsParallelBranchSlot && slot.HasNode)
                             .GroupBy(slot => slot.ParallelBranchStartSlotIndex))
                {
                    var contacts = new List<UploadedContact>();
                    foreach (var branchSlot in branchGroup.OrderBy(slot => slot.ContactSlotIndex))
                    {
                        if (!TryCreateUploadedContact(branchSlot, out var contact))
                        {
                            invalidRung = true;
                            break;
                        }

                        contacts.Add(contact);
                    }

                    if (invalidRung)
                        break;

                    if (contacts.Count > 0)
                        uploadedRung.ParallelBranches[branchGroup.Key] = contacts;
                }

                if (!invalidRung)
                    rungs.Add(uploadedRung);
            }

            return rungs;
        }

        private static bool TryCreateUploadedContact(LadderScreenDiagramSlot slot, out UploadedContact contact)
        {
            contact = null;
            if (slot == null || string.IsNullOrWhiteSpace(slot.AssignedAddress))
                return false;

            if (slot.NodeKind != LadderScreenNodeKind.NormallyOpen &&
                slot.NodeKind != LadderScreenNodeKind.NormallyClosed)
                return false;

            contact = new UploadedContact(slot, slot.NodeKind, slot.AssignedAddress);
            return true;
        }

        private void ShowEnergizedScanState()
        {
            var slots = GetScanOrderedSlots();
            var energizedSlots = programUploaded
                ? GetUploadedEnergizedSlots(ResolveBackend())
                : GetEnergizedSlots(slots, ResolveBackend());
            ClearScanHighlights(slots);

            foreach (var slot in slots)
            {
                if (energizedSlots.Contains(slot))
                    slot.SetScanHighlight(true, false);
            }
        }

        private static HashSet<LadderScreenDiagramSlot> GetEnergizedSlots(
            IReadOnlyCollection<LadderScreenDiagramSlot> slots,
            IPlcBackend backend)
        {
            var energizedSlots = new HashSet<LadderScreenDiagramSlot>();

            foreach (var rung in slots.Where(slot => slot.RungIndex >= 0).GroupBy(slot => slot.RungIndex))
                EvaluateRung(rung, backend, energizedSlots);

            return energizedSlots;
        }

        private static void EvaluateRung(
            IEnumerable<LadderScreenDiagramSlot> rungSlots,
            IPlcBackend backend,
            ISet<LadderScreenDiagramSlot> energizedSlots)
        {
            var slots = rungSlots.ToList();
            var mainContacts = slots
                .Where(slot => !slot.IsCoilSlot && !slot.IsParallelBranchSlot)
                .GroupBy(slot => slot.ContactSlotIndex)
                .ToDictionary(group => group.Key, group => group.First());
            var branchContacts = slots
                .Where(slot => slot.IsParallelBranchSlot)
                .GroupBy(slot => slot.ParallelBranchStartSlotIndex)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(slot => slot.ContactSlotIndex).ToList());

            var power = true;
            for (var slotIndex = 0; slotIndex < ContactSlotsPerRung; slotIndex++)
            {
                mainContacts.TryGetValue(slotIndex, out var mainSlot);
                var mainPath = EvaluateContactSlot(mainSlot, power, backend, energizedSlots);
                var branchPath = false;

                if (branchContacts.TryGetValue(slotIndex, out var branches))
                    branchPath = EvaluateBranchPath(branches, power, backend, energizedSlots);

                power = mainPath || branchPath;
            }

            foreach (var coil in slots.Where(slot => slot.IsCoilSlot && slot.HasNode))
            {
                if (power)
                    energizedSlots.Add(coil);
            }
        }

        private static bool EvaluateBranchPath(
            IReadOnlyCollection<LadderScreenDiagramSlot> branchSlots,
            bool incomingPower,
            IPlcBackend backend,
            ISet<LadderScreenDiagramSlot> energizedSlots)
        {
            if (!incomingPower || !branchSlots.Any(slot => slot.HasNode))
                return false;

            var power = true;
            foreach (var slot in branchSlots.OrderBy(slot => slot.ContactSlotIndex))
                power = EvaluateContactSlot(slot, power, backend, energizedSlots);

            return power;
        }

        private static bool EvaluateContactSlot(
            LadderScreenDiagramSlot slot,
            bool incomingPower,
            IPlcBackend backend,
            ISet<LadderScreenDiagramSlot> energizedSlots)
        {
            if (!incomingPower)
                return false;

            if (slot == null)
                return true;

            var conducts = slot.EvaluateContact(backend);
            if (conducts && slot.HasNode)
                energizedSlots.Add(slot);

            return conducts;
        }

        private HashSet<LadderScreenDiagramSlot> GetUploadedEnergizedSlots(IPlcBackend backend)
        {
            var energizedSlots = new HashSet<LadderScreenDiagramSlot>();
            if (!programUploaded || backend == null)
                return energizedSlots;

            ExecuteUploadedProgram(backend, energizedSlots);
            return energizedSlots;
        }

        private void ExecuteUploadedProgram(IPlcBackend backend, ISet<LadderScreenDiagramSlot> energizedSlots)
        {
            foreach (var rung in uploadedRungs)
            {
                var energized = EvaluateUploadedRung(rung, backend, energizedSlots);
                backend.SetBit(rung.CoilAddress, energized);
            }
        }

        private static bool EvaluateUploadedRung(
            UploadedRung rung,
            IPlcBackend backend,
            ISet<LadderScreenDiagramSlot> energizedSlots)
        {
            var power = true;
            for (var slotIndex = 0; slotIndex < ContactSlotsPerRung; slotIndex++)
            {
                rung.MainContacts.TryGetValue(slotIndex, out var mainContact);
                var mainPath = EvaluateUploadedContact(mainContact, power, backend, energizedSlots);
                var branchPath = false;

                if (rung.ParallelBranches.TryGetValue(slotIndex, out var branchContacts))
                    branchPath = EvaluateUploadedBranch(branchContacts, power, backend, energizedSlots);

                power = mainPath || branchPath;
            }

            if (power)
                energizedSlots?.Add(rung.CoilSlot);

            return power;
        }

        private static bool EvaluateUploadedBranch(
            IEnumerable<UploadedContact> contacts,
            bool incomingPower,
            IPlcBackend backend,
            ISet<LadderScreenDiagramSlot> energizedSlots)
        {
            if (!incomingPower)
                return false;

            var power = true;
            foreach (var contact in contacts)
                power = EvaluateUploadedContact(contact, power, backend, energizedSlots);

            return power;
        }

        private static bool EvaluateUploadedContact(
            UploadedContact contact,
            bool incomingPower,
            IPlcBackend backend,
            ISet<LadderScreenDiagramSlot> energizedSlots)
        {
            if (!incomingPower)
                return false;

            if (contact == null)
                return true;

            var addressOn = backend.GetBit(contact.Address);
            var conducts = contact.Kind == LadderScreenNodeKind.NormallyClosed ? !addressOn : addressOn;
            if (conducts)
                energizedSlots?.Add(contact.Slot);

            return conducts;
        }

        private IPlcBackend ResolveBackend()
        {
            var controller = ResolveScanController();
            return controller != null ? controller.Backend : null;
        }

        private List<LadderScreenDiagramSlot> GetScanOrderedSlots()
        {
            return GetComponentsInChildren<LadderScreenDiagramSlot>(true)
                .OrderBy(slot => slot.RungIndex)
                .ThenBy(GetScanOrder)
                .ToList();
        }

        private static float GetScanOrder(LadderScreenDiagramSlot slot)
        {
            if (slot.ContactSlotIndex < 0)
                return 100f;

            if (slot.IsParallelBranchSlot)
                return slot.ParallelBranchStartSlotIndex + 0.35f + (slot.ContactSlotIndex - slot.ParallelBranchStartSlotIndex) * 0.02f;

            return slot.ContactSlotIndex;
        }

        private static void ClearScanHighlights(IEnumerable<LadderScreenDiagramSlot> slots)
        {
            foreach (var slot in slots)
                slot.SetScanHighlight(false, false);
        }

        private void RemoveExistingBoard()
        {
            while (true)
            {
                var existing = transform.Find(BoardName);
                if (existing == null)
                    return;

                if (Application.isPlaying)
                    Destroy(existing.gameObject);
                else
                    DestroyImmediate(existing.gameObject);
            }
        }

        private static RectTransform CreateRung(Transform parent, int rungIndex)
        {
            var rung = CreateTopAnchoredPanel(parent, $"Rung {rungIndex + 1:00}", rungIndex * RungHeight, RungHeight - 8f, new Color(0.030f, 0.060f, 0.066f, 0.96f));
            CreateText(rung, "Rung Index", $"{rungIndex + 1:00}", new Vector2(0.008f, 0.38f), new Vector2(0.042f, 0.74f), 14f, TextAlignmentOptions.Center, new Color(0.48f, 0.66f, 0.64f, 1f));

            CreateVerticalLine(rung, "Left Bus", new Vector2(0.055f, 0.18f), new Vector2(0.055f, 0.82f), 5f);
            CreateVerticalLine(rung, "Right Bus", new Vector2(0.952f, 0.18f), new Vector2(0.952f, 0.82f), 5f);
            CreateHorizontalLine(rung, "Main Wire", new Vector2(0.055f, 0.63f), new Vector2(0.952f, 0.63f), 4f);

            var board = rung.GetComponentInParent<LadderScreenScrollableRungBoard>();
            const float slotHalfWidth = 0.034f;

            for (var index = 0; index < ContactSlotsPerRung; index++)
            {
                var x = GetContactSlotX(index);
                CreateDiagramSlot(
                    rung,
                    $"R{rungIndex + 1:00} Main {index + 1:00}",
                    new Vector2(x - slotHalfWidth, 0.47f),
                    new Vector2(x + slotHalfWidth, 0.79f),
                    board,
                    rungIndex,
                    index,
                    -1,
                    true);
                CreateStaticParallelBranchGroup(rung, board, rungIndex, index);
            }

            CreateDiagramSlot(rung, $"R{rungIndex + 1:00} Coil", new Vector2(0.865f, 0.47f), new Vector2(0.935f, 0.79f), board, rungIndex, -1, -1, false);
            return rung;
        }

        private static void CreateStaticParallelBranchGroup(Transform rung, LadderScreenScrollableRungBoard board, int rungIndex, int contactSlotIndex)
        {
            EnsureParallelBranchGroup(rung, board, rungIndex, contactSlotIndex, ContactSlotsPerRung - contactSlotIndex);
        }

        private static void EnsureParallelBranchGroup(Transform rung, LadderScreenScrollableRungBoard board, int rungIndex, int contactSlotIndex, int requiredSpan)
        {
            var groupPrefix = GetParallelGroupPrefix(rungIndex, contactSlotIndex);
            var maxSpan = ContactSlotsPerRung - contactSlotIndex;
            requiredSpan = Mathf.Clamp(requiredSpan, 1, maxSpan);

            if (rung.Find($"{groupPrefix} Slot {requiredSpan:00}") != null &&
                (rung.Find($"{groupPrefix} Right {requiredSpan:00}") != null || requiredSpan == 1 && rung.Find($"{groupPrefix} Right") != null))
                return;

            const float slotHalfWidth = 0.034f;
            const float leftBusX = 0.055f;
            const float branchY = 0.27f;
            const float mainY = 0.63f;
            var slotX = GetContactSlotX(contactSlotIndex);
            var startX = contactSlotIndex == 0
                ? leftBusX
                : slotX - slotHalfWidth;

            var left = EnsureVerticalLine(rung, $"{groupPrefix} Left", new Vector2(startX, branchY), new Vector2(startX, mainY), 3f);
            left.gameObject.SetActive(false);

            for (var span = 1; span <= requiredSpan; span++)
            {
                var branchSlotIndex = contactSlotIndex + span - 1;
                var branchSlotX = GetContactSlotX(branchSlotIndex);
                var endX = branchSlotX + slotHalfWidth;

                var right = EnsureVerticalLine(rung, $"{groupPrefix} Right {span:00}", new Vector2(endX, branchY), new Vector2(endX, mainY), 3f);
                var wire = EnsureHorizontalLine(rung, $"{groupPrefix} Wire {span:00}", new Vector2(startX, branchY), new Vector2(endX, branchY), 3f);
                var slot = EnsureDiagramSlot(
                    rung,
                    $"{groupPrefix} Slot {span:00}",
                    new Vector2(branchSlotX - slotHalfWidth, 0.11f),
                    new Vector2(branchSlotX + slotHalfWidth, 0.43f),
                    board,
                    rungIndex,
                    branchSlotIndex,
                    contactSlotIndex,
                    false);

                right.gameObject.SetActive(false);
                wire.gameObject.SetActive(false);
                slot.gameObject.SetActive(false);
            }

            var legacyRight = rung.Find($"{groupPrefix} Right");
            var legacyWire = rung.Find($"{groupPrefix} Wire");
            if (legacyRight != null)
                legacyRight.gameObject.SetActive(false);
            if (legacyWire != null)
                legacyWire.gameObject.SetActive(false);
        }

        private static float GetContactSlotX(int slotIndex)
        {
            const float startX = 0.135f;
            const float endX = 0.775f;
            return startX + (endX - startX) / (ContactSlotsPerRung - 1) * slotIndex;
        }

        private static RectTransform CreateDiagramSlot(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            LadderScreenScrollableRungBoard board,
            int rungIndex,
            int contactSlotIndex,
            int parallelBranchStartSlotIndex,
            bool acceptsParallelBranch)
        {
            var slot = CreatePanel(parent, name, anchorMin, anchorMax, new Color(0.045f, 0.085f, 0.095f, 0.88f));
            var background = slot.GetComponent<Image>();
            var label = CreateText(slot, "Label", "", new Vector2(0f, 0.40f), Vector2.one, 17f, TextAlignmentOptions.Center, Color.white);
            var addressLabel = CreateText(slot, "Address", "", Vector2.zero, new Vector2(1f, 0.45f), 11f, TextAlignmentOptions.Center, new Color(0.88f, 0.98f, 0.92f, 1f));
            slot.gameObject.AddComponent<LadderScreenDiagramSlot>().Configure(label, addressLabel, background, board, rungIndex, contactSlotIndex, parallelBranchStartSlotIndex, acceptsParallelBranch);
            return slot;
        }

        private static RectTransform EnsureDiagramSlot(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            LadderScreenScrollableRungBoard board,
            int rungIndex,
            int contactSlotIndex,
            int parallelBranchStartSlotIndex,
            bool acceptsParallelBranch)
        {
            var existing = parent.Find(name);
            if (existing == null)
                return CreateDiagramSlot(parent, name, anchorMin, anchorMax, board, rungIndex, contactSlotIndex, parallelBranchStartSlotIndex, acceptsParallelBranch);

            var rect = existing as RectTransform;
            if (rect == null)
                return CreateDiagramSlot(parent, name, anchorMin, anchorMax, board, rungIndex, contactSlotIndex, parallelBranchStartSlotIndex, acceptsParallelBranch);

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var slot = rect.GetComponent<LadderScreenDiagramSlot>();
            if (slot == null)
            {
                var background = rect.GetComponent<Image>();
                var label = rect.Find("Label")?.GetComponent<TMP_Text>() ?? CreateText(rect, "Label", "", new Vector2(0f, 0.40f), Vector2.one, 17f, TextAlignmentOptions.Center, Color.white);
                var addressLabel = rect.Find("Address")?.GetComponent<TMP_Text>() ?? CreateText(rect, "Address", "", Vector2.zero, new Vector2(1f, 0.45f), 11f, TextAlignmentOptions.Center, new Color(0.88f, 0.98f, 0.92f, 1f));
                slot = rect.gameObject.AddComponent<LadderScreenDiagramSlot>();
                slot.Configure(label, addressLabel, background, board, rungIndex, contactSlotIndex, parallelBranchStartSlotIndex, acceptsParallelBranch);
            }

            return rect;
        }

        private static Scrollbar CreateScrollbar(Transform parent)
        {
            var root = CreatePanel(parent, "Scrollbar", new Vector2(0.982f, 0.02f), new Vector2(0.998f, 0.98f), new Color(0.035f, 0.055f, 0.060f, 1f));
            var slidingArea = CreatePanel(root, "Sliding Area", Vector2.zero, Vector2.one, Color.clear);
            var handle = CreatePanel(slidingArea, "Handle", Vector2.zero, Vector2.one, new Color(0.34f, 0.62f, 0.58f, 1f));

            var scrollbar = root.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.handleRect = handle;
            return scrollbar;
        }

        private static RectTransform CreateTopAnchoredPanel(Transform parent, string name, float top, float height, Color color)
        {
            var panel = CreatePanel(parent, name, new Vector2(0f, 1f), new Vector2(1f, 1f), color);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = new Vector2(0f, -top);
            panel.sizeDelta = new Vector2(0f, height);
            return panel;
        }

        private static RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);

            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = panel.GetComponent<Image>();
            image.color = color;

            return rect;
        }

        private static TMP_Text CreateText(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var tmp = textObject.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = Mathf.Max(8f, fontSize * 0.58f);
            tmp.fontSizeMax = fontSize;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.raycastTarget = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            return tmp;
        }

        private static RectTransform CreateHorizontalLine(Transform parent, string name, Vector2 startAnchor, Vector2 endAnchor, float thickness)
        {
            var line = CreatePanel(parent, name, new Vector2(startAnchor.x, startAnchor.y), new Vector2(endAnchor.x, endAnchor.y), new Color(0.52f, 0.76f, 0.72f, 1f));
            line.offsetMin = new Vector2(0f, -thickness * 0.5f);
            line.offsetMax = new Vector2(0f, thickness * 0.5f);
            return line;
        }

        private static RectTransform CreateVerticalLine(Transform parent, string name, Vector2 startAnchor, Vector2 endAnchor, float thickness)
        {
            var line = CreatePanel(parent, name, new Vector2(startAnchor.x, startAnchor.y), new Vector2(endAnchor.x, endAnchor.y), new Color(0.52f, 0.76f, 0.72f, 1f));
            line.offsetMin = new Vector2(-thickness * 0.5f, 0f);
            line.offsetMax = new Vector2(thickness * 0.5f, 0f);
            return line;
        }

        private static RectTransform EnsureHorizontalLine(Transform parent, string name, Vector2 startAnchor, Vector2 endAnchor, float thickness)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing == null)
                return CreateHorizontalLine(parent, name, startAnchor, endAnchor, thickness);

            existing.anchorMin = new Vector2(startAnchor.x, startAnchor.y);
            existing.anchorMax = new Vector2(endAnchor.x, endAnchor.y);
            existing.offsetMin = new Vector2(0f, -thickness * 0.5f);
            existing.offsetMax = new Vector2(0f, thickness * 0.5f);
            return existing;
        }

        private static RectTransform EnsureVerticalLine(Transform parent, string name, Vector2 startAnchor, Vector2 endAnchor, float thickness)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing == null)
                return CreateVerticalLine(parent, name, startAnchor, endAnchor, thickness);

            existing.anchorMin = new Vector2(startAnchor.x, startAnchor.y);
            existing.anchorMax = new Vector2(endAnchor.x, endAnchor.y);
            existing.offsetMin = new Vector2(-thickness * 0.5f, 0f);
            existing.offsetMax = new Vector2(thickness * 0.5f, 0f);
            return existing;
        }

        private sealed class UploadedRung
        {
            public UploadedRung(string coilAddress, LadderScreenDiagramSlot coilSlot)
            {
                CoilAddress = coilAddress;
                CoilSlot = coilSlot;
            }

            public string CoilAddress { get; }
            public LadderScreenDiagramSlot CoilSlot { get; }
            public Dictionary<int, UploadedContact> MainContacts { get; } = new();
            public Dictionary<int, List<UploadedContact>> ParallelBranches { get; } = new();
        }

        private sealed class UploadedContact
        {
            public UploadedContact(LadderScreenDiagramSlot slot, LadderScreenNodeKind kind, string address)
            {
                Slot = slot;
                Kind = kind;
                Address = address;
            }

            public LadderScreenDiagramSlot Slot { get; }
            public LadderScreenNodeKind Kind { get; }
            public string Address { get; }
        }
    }
}
