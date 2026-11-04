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
        /// differ only by case. A lobby that lacks the key is left out whatever the comparison on
        /// every backend, as Steam's search on AppID 480 left it out on 2026-11-01, and so is a
        /// lobby whose host wrote the key with an empty value, since the manager deletes such a
        /// key (<see cref="IMultiplayerManager.SetLobbyData"/>). So neither
        /// <c>NotEqual "ranked"</c> nor <c>Equal ""</c> finds a lobby that never wrote the key or
        /// wrote it empty. A filter whose key is null or empty is skipped on every backend
        /// and leaves out no lobby, as the Steam backend skips it rather than hand Steam such a
        /// key, so a filter on a key the game has not chosen yet finds every lobby that the rest of
        /// the search finds.
        /// </summary>
        /// <remarks>
        /// Every backend compares the values as Steam does, which Steam's search on AppID 480
        /// showed on 2026-11-01 for all six comparisons: the letters a to z count as A to Z, so
        /// <c>"desert"</c> equals <c>"DESERT"</c> and <c>"B"</c> is greater than <c>"a"</c>, and a
        /// character that sorts between the upper-case and the lower-case letters sorts after
        /// both, so <c>"_"</c> is greater than <c>"a"</c> as well as <c>"A"</c>. Every other
        /// character, letters with accents or of other scripts among them, compares by its code
        /// point, as UTF-8 text sorts, without regard to case only for a to z: <c>"é"</c> is
        /// greater than <c>"É"</c>, and a character beyond U+FFFF, such as an emoji, is greater
        /// than one up to U+FFFF. A value that another value begins with is less than it.
        /// </remarks>
        public LobbySearchQuery AddStringFilter(string key, string value, LobbyComparison comparison = LobbyComparison.Equal)
        {
            _stringFilters.Add(new LobbyStringFilter(key, value, comparison));
            return this;
        }

        /// <summary>
        /// Keeps the lobbies whose value under <paramref name="key"/> reads as a whole number that,
        /// compared with <paramref name="value"/>, gives <paramref name="comparison"/>. The key
        /// matches in any case, and a filter whose key is null or empty is skipped on every
        /// backend and leaves out no lobby, as for <see cref="AddStringFilter"/>.
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
