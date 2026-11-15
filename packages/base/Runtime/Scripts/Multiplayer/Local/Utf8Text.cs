namespace Mu3Library.Multiplayer.Local
{
    /// <summary>
    /// Text as UTF-8 carries it: to Steam, through Steamworks.NET, and to the other processes of
    /// a machine-wide network, through the lobby files and frames. UTF-8 cannot carry a UTF-16
    /// surrogate without its pair, so <see cref="System.Text.Encoding.UTF8"/> writes U+FFFD in its
    /// place. The manager refuses such text in the lobby data a game sets (#194), the game ID,
    /// the lobby name, and the search filters it hands a backend (#387), but a caller of a
    /// backend itself can hand it over. So the local backends take lobby data keys and values,
    /// and the keys and string values of search filters, as UTF-8 carries them (#372): every
    /// backend then reads the same text, in this process and in the owner's own on a
    /// machine-wide network. They take the player name they are created with the same way, as
    /// Steam's names arrive through UTF-8 (#384), and the host's manager takes the name any
    /// backend reports for its roster the same way, as its packets carry it to the clients (#398).
    /// <para>
    /// Steamworks.NET also hands Steam each such text as a C string, which ends at its first NUL,
    /// and the manager refuses a NUL in the same texts. So the local backends take the lobby
    /// texts up to their first NUL too (<see cref="AsLobbyText"/>, #387), and the player name
    /// only as UTF-8 carries it (<see cref="AsCarried"/>).
    /// </para>
    /// </summary>
    internal static class Utf8Text
    {
        private const char Replacement = '\uFFFD';

        /// <summary>
        /// <paramref name="text"/> with each surrogate without its pair replaced by U+FFFD, as
        /// UTF-8 encoding replaces it, while a pair stays as it is: the same instance, without
        /// allocating, when it holds no such surrogate, and null for null.
        /// </summary>
        public static string AsCarried(string text)
        {
            if (text == null)
            {
                return null;
            }

            int index = IndexOfUnpairedSurrogate(text, 0);
            if (index < 0)
            {
                return text;
            }

            char[] carried = text.ToCharArray();
            for (; index >= 0; index = IndexOfUnpairedSurrogate(text, index + 1))
            {
                carried[index] = Replacement;
            }

            return new string(carried);
        }

        /// <summary>
        /// Lobby text as Steamworks.NET hands it to Steam: up to its first NUL, where the C string
        /// it passes ends, as Steam took lobby data keys and values, the keys it read them by, and
        /// the keys and values of search filters on AppID 480 on 2026-11-08 (#387), and with each
        /// surrogate without its pair as U+FFFD (<see cref="AsCarried"/>). The same instance,
        /// without allocating, when it holds neither, and null for null. Text with nothing before
        /// its first NUL comes out empty.
        /// </summary>
        public static string AsLobbyText(string text)
        {
            if (text == null)
            {
                return null;
            }

            int end = text.IndexOf('\0');
            return AsCarried(end < 0 ? text : text.Substring(0, end));
        }

        private static int IndexOfUnpairedSurrogate(string text, int start)
        {
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))
                    {
                        return i;
                    }

                    i++;
                }
                else if (char.IsLowSurrogate(c))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
