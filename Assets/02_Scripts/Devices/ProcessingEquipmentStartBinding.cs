using LadderLab.VR;
using LadderLab.Wiring;
using UnityEngine;

namespace LadderLab.Devices
{
    [DisallowMultipleComponent]
    public sealed class ProcessingEquipmentStartBinding : MonoBehaviour, IPlcAddressBindable
    {
        [SerializeField] private ProcessingEquipmentCycleDevice cycleDevice;

        public void Configure(ProcessingEquipmentCycleDevice targetCycleDevice)
        {
            cycleDevice = targetCycleDevice;
        }

        public void ConfigureBinding(PlcAddressBinding targetAddressBinding)
        {
            if (cycleDevice == null)
                cycleDevice = GetComponentInParent<ProcessingEquipmentCycleDevice>();

            cycleDevice?.ConfigureStartBinding(targetAddressBinding);
        }
    }
}
