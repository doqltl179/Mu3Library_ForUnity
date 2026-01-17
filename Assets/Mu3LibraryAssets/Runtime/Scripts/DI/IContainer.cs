namespace Mu3Library.DI
{
    public interface IContainer
    {
        public T Get<T>() where T : class;

        public void LateUpdate();
        public void Update();
        public void Dispose();
        public void Initialize();

        public void RemoveInterfaceIgnoreType<T>();
        public void RemoveInterfaceIgnoreType(System.Type type);
        public void AddInterfaceIgnoreType<T>();
        public void AddInterfaceIgnoreType(System.Type type);

        public void Register<T>() where T : class, new();
    }
}
