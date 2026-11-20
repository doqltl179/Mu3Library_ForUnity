namespace Mu3Library.DI
{
    /// <summary>
    /// Lifetime of a registered service.
    /// </summary>
    public enum ServiceLifetime
    {
        Singleton,
        Scoped,
        Transient
    }
}
