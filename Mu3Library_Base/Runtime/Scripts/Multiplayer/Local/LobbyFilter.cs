using System.Collections.Generic;
using System.Globalization;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Local
{
    /// <summary>
    /// The search filters as Steam applies them, for the lobbies of a local network. A filter
    /// finds its key in any case, as Steam does, since the local backends keep lobby data in
    /// dictionaries that ignore case, and compares a string value as Steam compares it
    /// (<see cref="CompareText"/>).
    /// </summary>
    internal static class LobbyFilter
    {
        public static bool Matches(IReadOnlyDictionary<string, string> data, LobbySearchQuery query)
        {
            for (int i = 0; i < query.StringFilters.Count; i++)
            {
                LobbyStringFilter filter = query.StringFilters[i];
                data.TryGetValue(filter.Key ?? string.Empty, out string value);
                int compared = CompareText(value ?? string.Empty, filter.Value ?? string.Empty);
                if (!Satisfies(compared, filter.Comparison))
                {
                    return false;
                }
            }

            for (int i = 0; i < query.NumericFilters.Count; i++)
            {
                LobbyNumericFilter filter = query.NumericFilters[i];
                if (!data.TryGetValue(filter.Key ?? string.Empty, out string text)
                    || !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                    || !Satisfies(value.CompareTo(filter.Value), filter.Comparison))
                {
                    return false;
                }
            }

            return true;
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
