using LadderLab.PLC;
using UnityEngine;

namespace LadderLab.VR
{
    [DisallowMultipleComponent]
    public sealed class PcDebugControls : MonoBehaviour
    {
        [SerializeField] private PlcScanController scanController;

        private bool lastStartHeld;
        private bool lastStopHeld;
        private bool lastEmergencyHeld;

        public void Configure(PlcScanController targetScanController)
        {
            scanController = targetScanController;
        }

        public void PressStart(bool pressed)
        {
            SetInput("X0", pressed);
            lastStartHeld = pressed;
        }

        public void PressStop(bool pressed)
        {
            SetInput("X1", pressed);
            lastStopHeld = pressed;
        }

        public void PressEmergency(bool pressed)
        {
            SetInput("X3", pressed);
            lastEmergencyHeld = pressed;
        }

        public void RunPlc()
        {
            scanController?.Run();
        }

        public void StopPlc()
        {
            scanController?.Stop();
        }

        public void ResetAll()
        {
            scanController?.ResetAll();

            lastStartHeld = false;
            lastStopHeld = false;
            lastEmergencyHeld = false;
        }

        public void NextMission()
        {
        }

        private void OnDisable()
        {
            if (scanController == null)
                return;

            if (lastStartHeld)
                SetInput("X0", false);
            if (lastStopHeld)
                SetInput("X1", false);
            if (lastEmergencyHeld)
                SetInput("X3", false);

            lastStartHeld = false;
            lastStopHeld = false;
            lastEmergencyHeld = false;
        }

        private void SetInput(string address, bool value)
        {
            if (scanController == null)
                return;

            scanController.Backend.SetBit(address, value);
        }
    }
}
