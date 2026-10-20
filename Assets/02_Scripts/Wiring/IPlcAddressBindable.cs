using LadderLab.VR;

namespace LadderLab.Wiring
{
    public interface IPlcAddressBindable
    {
        void ConfigureBinding(PlcAddressBinding targetAddressBinding);
    }
}
