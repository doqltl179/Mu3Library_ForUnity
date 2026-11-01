using System.Collections.Generic;
using System.Globalization;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Local
{
    /// <summary>
    /// The search filters as Steam applies them, for the lobbies of a local network. A filter
    /// finds its key in any case, as Steam does, since the local backends keep lobby data in
    /// dictionaries that ignore case.
    /// </summary>
    internal static class LobbyFilter
    {
        public static bool Matches(IReadOnlyDictionary<string, string> data, LobbySearchQuery query)
        {
            for (int i = 0; i < query.StringFilters.Count; i++)
            {
                LobbyStringFilter filter = query.StringFilters[i];
                data.TryGetValue(filter.Key ?? string.Empty, out string value);
                int compared = string.CompareOrdinal(value ?? string.Empty, filter.Value ?? string.Empty);
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
