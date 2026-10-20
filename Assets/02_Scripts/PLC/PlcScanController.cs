using UnityEngine;

namespace LadderLab.PLC
{
    [DisallowMultipleComponent]
    public sealed class PlcScanController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float scanInterval = 0f;
        [SerializeField] private bool runOnFixedUpdate = true;
        [SerializeField] private MonoBehaviour programSource;
        [SerializeField] private bool startRunningOnEnable;

        private readonly PlcMemory internalBackend = new();
        private IPlcProgram program;
        private float elapsedSinceScan;

        public IPlcBackend Backend => internalBackend;
        public PlcMemory Memory => internalBackend;
        public IPlcProgram Program => program;
        public bool IsRunning { get; private set; }
        public bool IsEmergencyInputActive => Backend.GetBit("X3");

        public void SetProgram(IPlcProgram nextProgram)
        {
            program = nextProgram;
            ResetProgramState();
        }

        public void ConfigureScan(float targetScanInterval, bool targetRunOnFixedUpdate, bool targetStartRunningOnEnable)
        {
            scanInterval = Mathf.Max(0f, targetScanInterval);
            runOnFixedUpdate = targetRunOnFixedUpdate;
            startRunningOnEnable = targetStartRunningOnEnable;

            if (isActiveAndEnabled && startRunningOnEnable)
                Run();
        }

        private void Awake()
        {
            if (program == null && programSource != null)
                program = programSource as IPlcProgram;

            if (program == null)
                program = FindProgramOnThisObject();
        }

        private void OnEnable()
        {
            if (startRunningOnEnable)
                Run();
        }

        private IPlcProgram FindProgramOnThisObject()
        {
            var components = GetComponents<MonoBehaviour>();
            foreach (var component in components)
            {
                if (component != this && component is IPlcProgram candidate)
                    return candidate;
            }

            return null;
        }

        public void Run()
        {
            IsRunning = true;
            elapsedSinceScan = 0f;
        }

        public void Stop()
        {
            IsRunning = false;
            ResetProgramState();
            elapsedSinceScan = 0f;
            Backend.ResetOutputs();
        }

        public void ResetAll()
        {
            IsRunning = false;
            ResetProgramState();
            elapsedSinceScan = 0f;
            Backend.ResetAll();
        }

        public void ExecuteScan()
        {
            ExecuteScan(scanInterval > 0f ? scanInterval : Time.fixedDeltaTime);
        }

        public void ExecuteScan(float deltaTime)
        {
            if (!IsRunning)
            {
                ResetProgramState();
                Backend.ResetOutputs();
                return;
            }

            Backend.ResetOutputs();
            if (program is IPlcTimedProgram timedProgram)
                timedProgram.Execute(Backend, Mathf.Max(0f, deltaTime));
            else
                program?.Execute(Backend);
        }

        private void FixedUpdate()
        {
            if (!runOnFixedUpdate)
                return;

            if (scanInterval <= 0f)
            {
                ExecuteScan();
                return;
            }

            elapsedSinceScan += Time.fixedDeltaTime;
            if (elapsedSinceScan < scanInterval)
                return;

            var scanDeltaTime = elapsedSinceScan;
            elapsedSinceScan = 0f;
            ExecuteScan(scanDeltaTime);
        }

        private void ResetProgramState()
        {
            if (program is IPlcResettableProgram resettableProgram)
                resettableProgram.ResetState();
        }
    }
}
