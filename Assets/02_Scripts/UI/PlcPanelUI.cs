using System.Text;
using LadderLab.Devices;
using LadderLab.PLC;
using TMPro;
using UnityEngine;

namespace LadderLab.UI
{
    [DisallowMultipleComponent]
    public sealed class PlcPanelUI : MonoBehaviour
    {
        [SerializeField] private PlcScanController scanController;
        [SerializeField] private LadderRuntime ladderRuntime;
        [SerializeField] private TMP_Text runStateText;
        [SerializeField] private TMP_Text inputStateText;
        [SerializeField] private TMP_Text outputStateText;
        [SerializeField] private TMP_Text relayStateText;
        [SerializeField] private TMP_Text applyStateText;
        [SerializeField] private TMP_Text ladderDiagramText;
        [SerializeField] private TMP_Text wiringGuideText;
        [SerializeField] private ProductSpawner productSpawner;
        [SerializeField] private TMP_Dropdown[] contactTypeDropdowns = { };
        [SerializeField] private TMP_Dropdown[] contactAddressDropdowns = { };
        [SerializeField] private TMP_Dropdown[] coilAddressDropdowns = { };
        [SerializeField] private TMP_Text[] contactTypeGuiLabels = { };
        [SerializeField] private TMP_Text[] contactAddressGuiLabels = { };
        [SerializeField] private TMP_Text[] coilAddressGuiLabels = { };
        [SerializeField] private TMP_Text[] ladderDropZoneLabels = { };

        private void Awake()
        {
            ResolveReferences();
            EnsureControllerProgram();
            RefreshAll();
        }

        private void Update()
        {
            RefreshAll();
        }

        public void Run()
        {
            ResolveReferences();
            EnsureControllerProgram();
            if (scanController == null)
            {
                SetApplyState("PLC controller missing");
                return;
            }

            scanController.Run();
            SetApplyState("PLC RUN");
            RefreshAll();
        }

        public void Stop()
        {
            ResolveReferences();
            scanController?.Stop();
            SetApplyState("PLC STOP");
            RefreshAll();
        }

        public void ResetAll()
        {
            ResolveReferences();
            scanController?.ResetAll();
            productSpawner?.ResetProduct();
            SetApplyState("PLC RESET");
            RefreshAll();
        }

        public void NextMission()
        {
            SetApplyState("Mission system removed");
            RefreshAll();
        }

        public void RespawnProduct()
        {
            ResolveReferences();
            productSpawner?.ResetProduct();
            SetApplyState("Product respawned");
            RefreshAll();
        }

        private void ResolveReferences()
        {
            if (scanController == null)
                scanController = FindAnyObjectByType<PlcScanController>();

            if (ladderRuntime == null)
                ladderRuntime = FindAnyObjectByType<LadderRuntime>();

            if (productSpawner == null)
                productSpawner = FindAnyObjectByType<ProductSpawner>();
        }

        private void EnsureControllerProgram()
        {
            if (scanController == null || ladderRuntime == null)
                return;

            if (scanController.Program == null)
                scanController.SetProgram(ladderRuntime);
        }

        private void RefreshAll()
        {
            RefreshStateText();
            RefreshLadderText();
            RefreshGuiLabels();
        }

        private void RefreshStateText()
        {
            if (scanController == null)
            {
                SetText(runStateText, "PLC: missing");
                return;
            }

            var backend = scanController.Backend;
            SetText(runStateText, scanController.IsRunning ? "PLC: RUN" : "PLC: STOP");
            SetText(inputStateText, BuildBits("X", PlcMemory.InputCount, backend));
            SetText(outputStateText, BuildBits("Y", PlcMemory.OutputCount, backend));
            SetText(relayStateText, BuildBits("M", PlcMemory.InternalRelayCount, backend));
        }

        private void RefreshLadderText()
        {
            SetText(ladderDiagramText, "R1: X0 NO + X1 NC + M0 seal -> M0\nR2: M0 NO -> Y0\nR3: X2 NO -> Y1");
            SetText(wiringGuideText, "Inputs: X0 Start, X1 Stop, X2 Sensor, X3 Emergency\nOutputs: Y0 Conveyor, Y1 Lamp, Y2 Alarm");
        }

        private void RefreshGuiLabels()
        {
            RefreshDropdownLabels(contactTypeGuiLabels, contactTypeDropdowns);
            RefreshDropdownLabels(contactAddressGuiLabels, contactAddressDropdowns);
            RefreshDropdownLabels(coilAddressGuiLabels, coilAddressDropdowns);

            if (ladderDropZoneLabels == null)
                return;

            for (var index = 0; index < ladderDropZoneLabels.Length; index++)
                SetText(ladderDropZoneLabels[index], index < 3 ? $"R{index + 1}" : "");
        }

        private static void RefreshDropdownLabels(TMP_Text[] labels, TMP_Dropdown[] dropdowns)
        {
            if (labels == null || dropdowns == null)
                return;

            var count = Mathf.Min(labels.Length, dropdowns.Length);
            for (var index = 0; index < count; index++)
            {
                if (dropdowns[index] == null)
                    continue;

                if (dropdowns[index].captionText != null)
                    SetText(labels[index], dropdowns[index].captionText.text);
                else if (dropdowns[index].options.Count > 0)
                    SetText(labels[index], dropdowns[index].options[Mathf.Clamp(dropdowns[index].value, 0, dropdowns[index].options.Count - 1)].text);
            }
        }

        private static string BuildBits(string prefix, int count, IPlcBackend backend)
        {
            var builder = new StringBuilder();
            for (var index = 0; index < count; index++)
            {
                if (index > 0)
                    builder.Append(index % 8 == 0 ? '\n' : ' ');

                var address = $"{prefix}{index}";
                builder.Append(address);
                builder.Append(':');
                builder.Append(backend.GetBit(address) ? '1' : '0');
            }

            return builder.ToString();
        }

        private void SetApplyState(string message)
        {
            SetText(applyStateText, message);
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
                text.text = value;
        }
    }
}
