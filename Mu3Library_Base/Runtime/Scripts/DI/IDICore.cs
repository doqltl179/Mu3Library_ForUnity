namespace Mu3Library.DI
{
    public interface IDICore
    {
        public bool IsContainerConfigured { get; }
        public bool IsInitialized { get; }

        public int ExecutionOrder { get; }
    }
}