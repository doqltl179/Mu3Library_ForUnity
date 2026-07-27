namespace Mu3Library.DI
{
    public interface IDICore
    {
        public bool IsContainerConfigured { get; }
        public bool IsPrepared { get; }

        public int ExecutionOrder { get; }
    }
}