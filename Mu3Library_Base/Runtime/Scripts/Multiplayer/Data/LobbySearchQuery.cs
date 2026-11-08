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
        /// wrote it empty. A lobby that holds the key with an empty value, which only a caller of
        /// a backend itself writes, compares it as an empty string on every backend, as Steam's
        /// search did on 2026-11-04 (<see cref="Backend.IMultiplayerBackend.SetLobbyData"/>). A
        /// filter whose key is null or empty is skipped on every backend
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
        /// <para>
        /// A UTF-16 surrogate without its pair, in the filter's key or value or in the lobby's
        /// data, counts as U+FFFD on every backend, since UTF-8 carries it to Steam as U+FFFD
        /// (<see cref="Backend.IMultiplayerBackend.SetLobbyData"/>): <c>"a\uD800"</c> equals
        /// <c>"a\uDC00"</c> and <c>"a\uFFFD"</c>, and sorts where <c>"a\uFFFD"</c> sorts. The
        /// manager refuses such text in the lobby data a game sets, but not in a filter, nor in
        /// the game ID or the lobby name it writes itself.
        /// </para>
        /// </remarks>
        public LobbySearchQuery AddStringFilter(string key, string value, LobbyComparison comparison = LobbyComparison.Equal)
        {
            _stringFilters.Add(new LobbyStringFilter(key, value, comparison));
            return this;
        }

        /// <summary>
        /// Keeps the lobbies whose value under <paramref name="key"/>, read as a number, compared
        /// with <paramref name="value"/>, gives <paramref name="comparison"/>. The key
        /// matches in any case, and a filter whose key is null or empty is skipped on every
        /// backend and leaves out no lobby, as for <see cref="AddStringFilter"/>. Unlike a string
        /// filter, a numeric filter reads a key the lobby lacks as 0 on every backend, as Steam's
        /// search read it on AppID 480 on 2026-11-04, and so also a key the host wrote with an
        /// empty value, which the manager deletes: <c>Equal</c>, <c>EqualOrLessThan</c>, and
        /// <c>EqualOrGreaterThan</c> 0 find a lobby that never wrote the key or wrote it empty,
        /// and <c>NotEqual</c>, <c>LessThan</c>, and <c>GreaterThan</c> 0 do not. An
        /// empty value, which only a caller of a backend itself writes, reads as 0 too.
        /// <para>
        /// Every backend reads every value as Steam's search read 64 values on AppID 480 on
        /// 2026-11-04, so a value that is not a whole number leaves no lobby out by itself. From
        /// the first character, without skipping white space, it takes one <c>+</c> or <c>-</c>, then
        /// the hexadecimal digits after <c>0x</c> or <c>0X</c>, the character after a <c>'</c>
        /// as the first byte of its UTF-8 form taken as a signed byte, or else the decimal digits,
        /// each up to the first character that is not one, so a value without any reads as 0. A
        /// number past the range of <see cref="int"/> keeps the low 32 bits of a sum in 64 bits
        /// that wraps around: <c>"12abc"</c> reads as 12, <c>"5.5"</c> as 5, <c>"abc"</c> and
        /// <c>" 7"</c> as 0, <c>"010"</c> as 10, <c>"0x10"</c> as 16, <c>"'A"</c> as 65, and
        /// <c>"2147483648"</c> as -2147483648. A whole number written in decimal within the range
        /// of <see cref="int"/>, with no space before it, reads as written.
        /// </para>
        /// <para>
        /// A UTF-16 surrogate without its pair, in the filter's key or in the lobby's data, counts
        /// as U+FFFD on every backend, since UTF-8 carries it to Steam as U+FFFD, as for
        /// <see cref="AddStringFilter"/>: after a <c>'</c> it reads as the first byte of U+FFFD,
        /// so <c>"'\uD800"</c> reads as -17.
        /// </para>
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
