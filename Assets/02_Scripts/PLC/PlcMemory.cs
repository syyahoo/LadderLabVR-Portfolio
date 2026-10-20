using System;
using System.Collections.Generic;

namespace LadderLab.PLC
{
    public sealed class PlcMemory : IPlcBackend
    {
        public const int InputCount = 16;
        public const int OutputCount = 16;
        public const int InternalRelayCount = 32;

        private readonly bool[] inputs = new bool[InputCount];
        private readonly bool[] outputs = new bool[OutputCount];
        private readonly bool[] internalRelays = new bool[InternalRelayCount];

        public bool GetBit(string address)
        {
            return GetBit(PlcAddress.Parse(address));
        }

        public bool GetBit(PlcAddress address)
        {
            return GetStorage(address)[address.Index];
        }

        public void SetBit(string address, bool value)
        {
            SetBit(PlcAddress.Parse(address), value);
        }

        public void SetBit(PlcAddress address, bool value)
        {
            GetStorage(address)[address.Index] = value;
        }

        public void ResetAll()
        {
            Array.Clear(inputs, 0, inputs.Length);
            ResetOutputs();
            Array.Clear(internalRelays, 0, internalRelays.Length);
        }

        public void ResetOutputs()
        {
            Array.Clear(outputs, 0, outputs.Length);
        }

        public IReadOnlyList<bool> Inputs => inputs;
        public IReadOnlyList<bool> Outputs => outputs;
        public IReadOnlyList<bool> InternalRelays => internalRelays;

        private bool[] GetStorage(PlcAddress address)
        {
            var storage = address.Area switch
            {
                PlcMemoryArea.Input => inputs,
                PlcMemoryArea.Output => outputs,
                PlcMemoryArea.InternalRelay => internalRelays,
                _ => throw new ArgumentOutOfRangeException(nameof(address))
            };

            if (address.Index < 0 || address.Index >= storage.Length)
                throw new ArgumentOutOfRangeException(nameof(address), address.ToString(), "PLC address is outside the supported MVP range.");

            return storage;
        }
    }
}
