using LadderLab.Devices;
using LadderLab.PLC;
using LadderLab.VR;
using UnityEngine;

namespace LadderLab.UI
{
    [DisallowMultipleComponent]
    public sealed class MainConsoleBinder : MonoBehaviour
    {
        private const string RuntimeName = "PLC Runtime";
        private const string ConsoleName = "Control Room Main Test Console";

        [SerializeField] private PlcScanController scanController;
        [SerializeField] private LadderRuntime fallbackRuntime;
        [SerializeField] private Transform mainConsole;

        private void Start()
        {
            Bind();
        }

        // 기존 오브젝트 연결
        public void Bind()
        {
            ResolveReferences();

            if (scanController == null)
            {
                Debug.LogWarning("[LadderLab] Main console binder requires an existing PlcScanController.");
                return;
            }

            if (mainConsole == null)
            {
                Debug.LogWarning("[LadderLab] Main console binder requires an existing main console object.");
                return;
            }

            ParentConsoleToRuntime();
            BindLadderBoard();
            HardwireInputs();
            HardwireOutputs();
        }

        // 참조 확인
        private void ResolveReferences()
        {
            if (scanController == null)
            {
                var runtime = FindSceneTransform(RuntimeName);
                if (runtime != null)
                    scanController = runtime.GetComponent<PlcScanController>();
            }

            if (scanController == null)
                scanController = GetComponentInChildren<PlcScanController>(true);

            if (fallbackRuntime == null && scanController != null)
                fallbackRuntime = scanController.GetComponent<LadderRuntime>();

            if (mainConsole == null && scanController != null)
                mainConsole = scanController.transform.Find(ConsoleName);

            if (mainConsole == null)
                mainConsole = transform.Find(ConsoleName);
        }

        // PLC 하위로 이동
        private void ParentConsoleToRuntime()
        {
            if (scanController == null || mainConsole == null || mainConsole.parent == scanController.transform)
                return;

            mainConsole.SetParent(scanController.transform, true);
        }

        // Ladder 화면 연결
        private void BindLadderBoard()
        {
            var board = GetComponentInChildren<LadderScreenScrollableRungBoard>(true);
            if (board == null)
                return;

            board.ConfigureRuntime(scanController, fallbackRuntime);
        }

        // X 입력 고정 연결
        private void HardwireInputs()
        {
            for (var index = 0; index < PlcMemory.InputCount; index++)
            {
                var address = $"X{index}";
                var input = FindDescendantByName(mainConsole, $"Input Button {address}");
                if (input == null)
                {
                    Debug.LogWarning($"[LadderLab] Missing main console input button: {address}");
                    continue;
                }

                var binding = input.GetComponent<PlcAddressBinding>();
                var actuator = input.GetComponent<PlcAddressActuator>();
                var visual = input.GetComponent<VrButtonVisualFeedback>();

                if (binding == null || actuator == null || visual == null)
                {
                    Debug.LogWarning($"[LadderLab] Main console input {address} is missing PLC input components.");
                    continue;
                }

                binding.Configure(scanController, address, new[] { PlcMemoryArea.Input }, new[] { address });
                actuator.Configure(binding, PlcActuatorMode.Momentary);
                visual.Configure(scanController, address, input.GetComponent<Renderer>(), null, new Color(0.19f, 0.58f, 0.96f, 1f), new Vector3(0f, -0.014f, 0f));
            }
        }

        // Y 출력 고정 연결
        private void HardwireOutputs()
        {
            for (var index = 0; index < PlcMemory.OutputCount; index++)
            {
                var address = $"Y{index}";
                var output = FindDescendantByName(mainConsole, $"Output Lamp {address}");
                if (output == null)
                {
                    Debug.LogWarning($"[LadderLab] Missing main console output lamp: {address}");
                    continue;
                }

                var lamp = output.GetComponent<LampDevice>();
                if (lamp == null)
                {
                    Debug.LogWarning($"[LadderLab] Main console output {address} is missing LampDevice.");
                    continue;
                }

                lamp.Configure(scanController, address, output.GetComponent<Renderer>(), output.GetComponent<Light>());
            }
        }

        private static Transform FindDescendantByName(Transform root, string objectName)
        {
            if (root == null)
                return null;

            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate != null && candidate.name == objectName)
                    return candidate;
            }

            return null;
        }

        private static Transform FindSceneTransform(string objectName)
        {
            var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
            foreach (var candidate in transforms)
            {
                if (candidate != null && candidate.name == objectName)
                    return candidate;
            }

            return null;
        }
    }
}
