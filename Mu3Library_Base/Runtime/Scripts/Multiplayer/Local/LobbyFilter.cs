using System.Collections.Generic;
using System.Globalization;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Local
{
    /// <summary>
    /// The search filters as Steam applies them, for the lobbies of a local network. A filter
    /// finds its key in any case, as Steam does, since the local backends keep lobby data in
    /// dictionaries that ignore case, and compares a string value as Steam compares it
    /// (<see cref="CompareText"/>). A string filter drops a lobby that lacks its key under every
    /// comparison, as Steam's search on AppID 480 did on 2026-11-01 (#286). A lobby that holds
    /// the key with an empty value, which the manager never writes since it deletes such a key
    /// (#313) but a caller of a backend itself or a lobby file another process wrote with an
    /// earlier version of the library brings here, is compared as Steam's search compared one on
    /// 2026-11-04 (#343): a string filter takes the empty value as an empty string, so
    /// <c>Equal ""</c> and <c>NotEqual "x"</c> keep the lobby, and a numeric filter reads it as
    /// 0. A numeric filter reads a key the lobby lacks as 0 too, as Steam's numeric filters read
    /// it that day (#353), so <c>Equal 0</c>, <c>EqualOrLessThan 0</c>, and
    /// <c>EqualOrGreaterThan 0</c> keep such a lobby, unlike a string filter. A numeric filter
    /// drops a lobby that holds a value that is not a whole number, which Steam reads otherwise
    /// (#362). A filter whose key is null or empty is skipped and drops no lobby, as the Steam
    /// backend skips it rather than hand Steam such a key (#314).
    /// </summary>
    internal static class LobbyFilter
    {
        public static bool Matches(IReadOnlyDictionary<string, string> data, LobbySearchQuery query)
        {
            for (int i = 0; i < query.StringFilters.Count; i++)
            {
                LobbyStringFilter filter = query.StringFilters[i];
                if (string.IsNullOrEmpty(filter.Key))
                {
                    continue;
                }

                if (!data.TryGetValue(filter.Key, out string value)
                    || !Satisfies(CompareText(value ?? string.Empty, filter.Value ?? string.Empty), filter.Comparison))
                {
                    return false;
                }
            }

            for (int i = 0; i < query.NumericFilters.Count; i++)
            {
                LobbyNumericFilter filter = query.NumericFilters[i];
                if (string.IsNullOrEmpty(filter.Key))
                {
                    continue;
                }

                // A key the lobby lacks leaves the text null, which reads as 0.
                data.TryGetValue(filter.Key, out string text);
                if (!TryReadNumber(text, out int value)
                    || !Satisfies(value.CompareTo(filter.Value), filter.Comparison))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// A lobby value as a numeric filter reads it: a whole number, and a key the lobby lacks
        /// (null) or holds with an empty value as 0, as Steam's numeric filters read both on
        /// AppID 480 on 2026-11-04 (#343, #353).
        /// </summary>
        private static bool TryReadNumber(string text, out int value)
        {
            if (string.IsNullOrEmpty(text))
            {
                value = 0;
                return true;
            }

            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Compares two lobby values as Steam's string filters compare them, which Steam's search
        /// on AppID 480 showed on 2026-11-01: the letters a to z as A to Z, so case makes no
        /// difference and <c>_</c> sorts after both <c>A</c> and <c>a</c>, and every other
        /// character, letters with accents or of other scripts among them, by its code point, as
        /// UTF-8 bytes sort, so <c>é</c> and <c>É</c> differ and a character beyond U+FFFF sorts
        /// after U+FFFF. A string that another one begins with sorts first.
        /// </summary>
        internal static int CompareText(string a, string b)
        {
            int length = a.Length < b.Length ? a.Length : b.Length;
            for (int i = 0; i < length; i++)
            {
                int x = SortKey(a[i]);
                int y = SortKey(b[i]);
                if (x != y)
                {
                    return x < y ? -1 : 1;
                }
            }

            return a.Length.CompareTo(b.Length);
        }

        /// <summary>
        /// Where a UTF-16 unit sorts: a to z as A to Z, and the surrogates, which hold the code
        /// points beyond U+FFFF, after U+E000 to U+FFFF, so units sort as their code points do.
        /// </summary>
        private static int SortKey(char c)
        {
            if (c >= 'a' && c <= 'z')
            {
                return c - ('a' - 'A');
            }

            if (c >= '\uD800' && c <= '\uDFFF')
            {
                return c + 0x2000;
            }

            return c >= '\uE000' ? c - 0x800 : c;
        }

        /// <summary>
        /// Whether the lobby's value, compared with the filter's value, gives <paramref name="compared"/>
        /// the comparison asks for.
        /// </summary>
        private static bool Satisfies(int compared, LobbyComparison comparison)
        {
            switch (comparison)
            {
                case LobbyComparison.EqualOrLessThan: return compared <= 0;
                case LobbyComparison.LessThan: return compared < 0;
                case LobbyComparison.Equal: return compared == 0;
                case LobbyComparison.GreaterThan: return compared > 0;
                case LobbyComparison.EqualOrGreaterThan: return compared >= 0;
                case LobbyComparison.NotEqual: return compared != 0;
                default: return false;
            }
        }
    }
}
