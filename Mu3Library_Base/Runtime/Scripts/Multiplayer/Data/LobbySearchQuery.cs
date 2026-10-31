using System.Collections.Generic;

namespace Mu3Library.Multiplayer.Data
{
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

        /// <summary>
        /// Keeps the lobbies whose value under <paramref name="key"/>, compared with
        /// <paramref name="value"/>, gives <paramref name="comparison"/>. The key matches in any
        /// case on every backend, as Steam matches it, since lobby data never holds two keys that
        /// differ only by case.
        /// </summary>
        public LobbySearchQuery AddStringFilter(string key, string value, LobbyComparison comparison = LobbyComparison.Equal)
        {
            _stringFilters.Add(new LobbyStringFilter(key, value, comparison));
            return this;
        }

        /// <summary>
        /// Keeps the lobbies whose value under <paramref name="key"/> reads as a whole number that,
        /// compared with <paramref name="value"/>, gives <paramref name="comparison"/>. The key
        /// matches in any case, as for <see cref="AddStringFilter"/>.
        /// </summary>
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
