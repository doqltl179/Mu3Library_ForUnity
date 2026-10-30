using System.Collections.Generic;

namespace Mu3Library.Multiplayer
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

    public readonly struct LobbyStringFilter
    {
        public string Key { get; }
        public string Value { get; }
        public LobbyComparison Comparison { get; }

        public LobbyStringFilter(string key, string value, LobbyComparison comparison)
        {
            Key = key;
            Value = value;
            Comparison = comparison;
        }
    }

    public readonly struct LobbyNumericFilter
    {
        public string Key { get; }
        public int Value { get; }
        public LobbyComparison Comparison { get; }

        public LobbyNumericFilter(string key, int value, LobbyComparison comparison)
        {
            Key = key;
            Value = value;
            Comparison = comparison;
        }
    }

    /// <summary>
    /// A public lobby search. The manager adds the game identifier and protocol version filters
    /// itself, and the backend returns only lobbies with a free slot.
    /// </summary>
    public sealed class LobbySearchQuery
    {
        public const int MaxResultLimit = 50;

        private readonly List<LobbyStringFilter> _stringFilters = new List<LobbyStringFilter>();
        private readonly List<LobbyNumericFilter> _numericFilters = new List<LobbyNumericFilter>();

        public IReadOnlyList<LobbyStringFilter> StringFilters => _stringFilters;
        public IReadOnlyList<LobbyNumericFilter> NumericFilters => _numericFilters;

        public LobbyDistance Distance { get; set; } = LobbyDistance.Default;

        /// <summary>Most lobbies to return, from 1 to <see cref="MaxResultLimit"/>.</summary>
        public int MaxResults { get; set; } = MaxResultLimit;

        public LobbySearchQuery AddStringFilter(string key, string value, LobbyComparison comparison = LobbyComparison.Equal)
        {
            _stringFilters.Add(new LobbyStringFilter(key, value, comparison));
            return this;
        }

        public LobbySearchQuery AddNumericFilter(string key, int value, LobbyComparison comparison = LobbyComparison.Equal)
        {
            _numericFilters.Add(new LobbyNumericFilter(key, value, comparison));
            return this;
        }

        internal LobbySearchQuery Clone()
        {
            LobbySearchQuery clone = new LobbySearchQuery
            {
                Distance = Distance,
                MaxResults = MaxResults,
            };
            clone._stringFilters.AddRange(_stringFilters);
            clone._numericFilters.AddRange(_numericFilters);
            return clone;
        }
    }
}
