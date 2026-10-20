using System;
using UnityEngine;

namespace LadderLab.PLC
{
    [Serializable]
    public sealed class LadderElementData
    {
        [SerializeField] private LadderElementType elementType;
        [SerializeField] private string address;

        public LadderElementData(LadderElementType targetElementType, string targetAddress)
        {
            elementType = targetElementType;
            address = targetAddress;
        }

        public LadderElementType ElementType => elementType;
        public string Address => address;

        public static LadderElementData NormallyOpen(string targetAddress)
        {
            return new LadderElementData(LadderElementType.NormallyOpenContact, targetAddress);
        }

        public static LadderElementData NormallyClosed(string targetAddress)
        {
            return new LadderElementData(LadderElementType.NormallyClosedContact, targetAddress);
        }

        public static LadderElementData Coil(string targetAddress)
        {
            return new LadderElementData(LadderElementType.Coil, targetAddress);
        }

        public bool EvaluateContact(IPlcBackend backend)
        {
            return elementType switch
            {
                LadderElementType.NormallyOpenContact => backend.GetBit(address),
                LadderElementType.NormallyClosedContact => !backend.GetBit(address),
                _ => throw new InvalidOperationException($"{elementType} is not a ladder contact.")
            };
        }

        public void WriteCoil(IPlcBackend backend, bool value)
        {
            if (elementType != LadderElementType.Coil)
                throw new InvalidOperationException($"{elementType} is not a ladder coil.");

            var parsedAddress = PlcAddress.Parse(address);
            if (parsedAddress.Area == PlcMemoryArea.Input)
                throw new InvalidOperationException("Ladder coils cannot write PLC X input addresses.");

            backend.SetBit(parsedAddress, value);
        }
    }
}
