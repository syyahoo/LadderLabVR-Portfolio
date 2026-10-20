using System;
using System.Collections.Generic;
using UnityEngine;

namespace LadderLab.PLC
{
    [Serializable]
    public sealed class LadderProgramData
    {
        [SerializeField] private List<LadderRungData> rungs = new();

        public LadderProgramData(IEnumerable<LadderRungData> targetRungs)
        {
            rungs = new List<LadderRungData>(targetRungs ?? Array.Empty<LadderRungData>());
        }

        public IReadOnlyList<LadderRungData> Rungs => rungs;

        public static LadderProgramData Empty()
        {
            return new LadderProgramData(Array.Empty<LadderRungData>());
        }

        public static LadderProgramData CreateMissionOneExample()
        {
            return new LadderProgramData(new[]
            {
                new LadderRungData(
                    new[]
                    {
                        LadderElementData.NormallyOpen("X0"),
                        LadderElementData.NormallyClosed("X1")
                    },
                    LadderElementData.NormallyOpen("M0"),
                    LadderElementData.Coil("M0")),
                new LadderRungData(
                    new[]
                    {
                        LadderElementData.NormallyOpen("M0")
                    },
                    LadderElementData.Coil("Y0")),
                new LadderRungData(
                    new[]
                    {
                        LadderElementData.NormallyOpen("X2")
                    },
                    LadderElementData.Coil("Y1"))
            });
        }

        public void Execute(IPlcBackend backend)
        {
            foreach (var rung in rungs)
            {
                if (rung == null)
                    throw new InvalidOperationException("Ladder program contains an empty rung.");

                rung.Execute(backend);
            }
        }

        public void Execute(IPlcBackend backend, float deltaTime)
        {
            foreach (var rung in rungs)
            {
                if (rung == null)
                    throw new InvalidOperationException("Ladder program contains an empty rung.");

                rung.Execute(backend, deltaTime);
            }
        }

        public void ResetRuntimeState()
        {
            foreach (var rung in rungs)
                rung?.ResetRuntimeState();
        }
    }
}
