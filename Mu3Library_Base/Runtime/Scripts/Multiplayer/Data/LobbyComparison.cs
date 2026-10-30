namespace Mu3Library.Multiplayer.Data
{
    /// <summary>
    /// How a filter compares a lobby's data with the filter's value: <see cref="LessThan"/> keeps
    /// lobbies whose value is less than the filter's. A numeric filter compares the data as a
    /// number; for a string filter use <see cref="Equal"/> or <see cref="NotEqual"/>.
    /// </summary>
    public enum LobbyComparison
    {
        EqualOrLessThan = -2,
        LessThan = -1,
        Equal = 0,
        GreaterThan = 1,
        EqualOrGreaterThan = 2,
        NotEqual = 3,
    }
}
