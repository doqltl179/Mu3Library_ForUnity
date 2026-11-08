namespace Mu3Library.Multiplayer.Local
{
    /// <summary>
    /// Text as UTF-8 carries it: to Steam, through Steamworks.NET, and to the other processes of
    /// a machine-wide network, through the lobby files and frames. UTF-8 cannot carry a UTF-16
    /// surrogate without its pair, so <see cref="System.Text.Encoding.UTF8"/> writes U+FFFD in its
    /// place. The manager refuses such text in the lobby data a game sets (#194), though not in
    /// the game ID or the lobby name it writes itself or in search filters, and a caller of a
    /// backend itself can hand it over. So the local backends take lobby data keys and values,
    /// and the keys and string values of search filters, as UTF-8 carries them (#372): every
    /// backend then reads the same text, in this process and in the owner's own on a
    /// machine-wide network. They take the player name they are created with the same way, as
    /// Steam's names arrive through UTF-8 (#384).
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
