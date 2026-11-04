using System.Collections.Generic;
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
    /// reads every other value as a number too, as Steam's numeric filters read it
    /// (<see cref="ReadNumber"/>, #362), so it drops no lobby for a value that is not a whole
    /// number. A filter whose key is null or empty is skipped and drops no lobby, as the Steam
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
                if (!Satisfies(ReadNumber(text).CompareTo(filter.Value), filter.Comparison))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// A lobby value as Steam's numeric filters read it, by the rule that
        /// <see cref="LobbySearchQuery.AddNumericFilter"/> states and Steam's search on AppID 480
        /// followed for 64 values on 2026-11-04 (#362): one sign, then the hexadecimal digits
        /// after <c>0x</c>, the signed first UTF-8 byte of the character after <c>'</c>, or the
        /// decimal digits, summed in 64 bits that wrap around, of which the low 32 count. A key
        /// the lobby lacks (null) or holds with an empty value reads as 0, as Steam read both
        /// that day (#343, #353).
        /// </summary>
        private static int ReadNumber(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int i = 0;
            long sign = 1;
            if (text[0] == '-')
            {
                sign = -1;
                i++;
            }
            else if (text[0] == '+')
            {
                i++;
            }

            long value = 0;
            if (i + 1 < text.Length && text[i] == '0' && (text[i + 1] == 'x' || text[i + 1] == 'X'))
            {
                for (i += 2; i < text.Length; i++)
                {
                    int digit = HexDigit(text[i]);
                    if (digit < 0)
                    {
                        break;
                    }

                    value = unchecked((value << 4) + digit);
                }
            }
            else if (i < text.Length && text[i] == '\'')
            {
                value = i + 1 < text.Length ? unchecked((sbyte)FirstUtf8Byte(text, i + 1)) : 0;
            }
            else
            {
                for (; i < text.Length; i++)
                {
                    char c = text[i];
                    if (c < '0' || c > '9')
                    {
                        break;
                    }

                    value = unchecked(value * 10 + (c - '0'));
                }
            }

            return unchecked((int)(value * sign));
        }

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }

            if (c >= 'a' && c <= 'f')
            {
                return c - 'a' + 10;
            }

            return c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
        }

        /// <summary>
        /// The first byte of the UTF-8 form of the character at <paramref name="index"/>, an
        /// unpaired surrogate taken as U+FFFD, as UTF-8 encoding replaces it on its way to Steam
        /// or to a lobby file.
        /// </summary>
        private static byte FirstUtf8Byte(string text, int index)
        {
            char c = text[index];
            if (c < 0x80)
            {
                return (byte)c;
            }

            if (c < 0x800)
            {
                return (byte)(0xC0 | (c >> 6));
            }

            if (char.IsSurrogate(c))
            {
                return index + 1 < text.Length && char.IsSurrogatePair(c, text[index + 1])
                    ? (byte)(0xF0 | (char.ConvertToUtf32(c, text[index + 1]) >> 18))
                    : (byte)0xEF;
            }

            return (byte)(0xE0 | (c >> 12));
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
