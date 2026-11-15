namespace Mu3Library.DI
{
    /// <summary>
    /// Applies DI member injection to an object created outside the container.
    /// </summary>
    public interface IObjectInjector
    {
        void Inject(object instance);
    }
}
