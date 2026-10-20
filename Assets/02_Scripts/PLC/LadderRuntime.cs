using UnityEngine;

namespace LadderLab.PLC
{
    [DisallowMultipleComponent]
    public sealed class LadderRuntime : MonoBehaviour, IPlcTimedProgram, IPlcResettableProgram
    {
        [SerializeField] private LadderProgramData program = LadderProgramData.Empty();

        public LadderProgramData Program => program;

        public void SetProgram(LadderProgramData nextProgram)
        {
            program = nextProgram ?? LadderProgramData.Empty();
        }

        public void ClearProgram()
        {
            SetProgram(LadderProgramData.Empty());
        }

        public void Execute(IPlcBackend backend)
        {
            program?.Execute(backend);
        }

        public void Execute(IPlcBackend backend, float deltaTime)
        {
            program?.Execute(backend, deltaTime);
        }

        public void ResetState()
        {
            program?.ResetRuntimeState();
        }
    }
}
