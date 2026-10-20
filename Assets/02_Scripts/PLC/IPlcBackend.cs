using System.Collections.Generic;

namespace LadderLab.PLC
{
    public interface IPlcBackend
    {
        bool GetBit(string address);
        bool GetBit(PlcAddress address);
        void SetBit(string address, bool value);
        void SetBit(PlcAddress address, bool value);
        void ResetAll();
        void ResetOutputs();

        IReadOnlyList<bool> Inputs { get; }
        IReadOnlyList<bool> Outputs { get; }
        IReadOnlyList<bool> InternalRelays { get; }
    }
}
