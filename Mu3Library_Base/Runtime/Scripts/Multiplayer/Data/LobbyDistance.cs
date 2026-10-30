namespace Mu3Library.Multiplayer.Data
{
    /// <summary>
    /// How far away a lobby may be. Backends without regions, such as the local backend, ignore it.
    /// </summary>
    public enum LobbyDistance
    {
        /// <summary>The same region only.</summary>
        Close = 0,
        /// <summary>The same or nearby regions.</summary>
        Default = 1,
        /// <summary>Up to about half-way around the globe.</summary>
        Far = 2,
        /// <summary>No distance limit; latency can reach several seconds.</summary>
        Worldwide = 3,
    }
}
