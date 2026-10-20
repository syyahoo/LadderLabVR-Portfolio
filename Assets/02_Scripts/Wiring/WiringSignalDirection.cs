namespace LadderLab.Wiring
{
    public enum WiringSignalDirection
    {
        DeviceWritesPlcInput,
        DeviceReadsPlcOutput
    }

    public enum WiringDeviceKind
    {
        Generic,
        Switch,
        Sensor,
        Motor,
        Lamp,
        Alarm,
        Conveyor
    }
}
