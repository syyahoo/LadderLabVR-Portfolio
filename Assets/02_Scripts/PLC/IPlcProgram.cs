namespace LadderLab.PLC
{
    public interface IPlcProgram
    {
        void Execute(IPlcBackend backend);
    }

    public interface IPlcTimedProgram : IPlcProgram
    {
        void Execute(IPlcBackend backend, float deltaTime);
    }

    public interface IPlcResettableProgram
    {
        void ResetState();
    }
}
