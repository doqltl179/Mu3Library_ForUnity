using System.Globalization;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Save;

namespace Mu3Library.SessionSave
{
    /// <summary>
    /// The section keys and metadata keys that <see cref="SessionSaveManager"/> writes into a slot.
    /// A player's section is saved as <c>player.&lt;PlayerId&gt;.&lt;key&gt;</c>, so give no section
    /// of your own a key that starts with <see cref="PlayerSectionPrefix"/>.
    /// </summary>
    public static class SessionSaveKeys
    {
        /// <summary>The start of every player section key in a slot.</summary>
        public const string PlayerSectionPrefix = "player.";

        /// <summary>
        /// Longest key of an <see cref="IPlayerSaveSection"/>, in characters: what is left of
        /// <see cref="SaveLimits.MaxKeyLength"/> after <see cref="PlayerSectionPrefix"/>, the 20
        /// digits of the largest <see cref="PlayerId"/>, and the dot after them.
        /// </summary>
        public const int MaxPlayerSectionKeyLength = 36;

        /// <summary>Metadata: the <see cref="PlayerId"/> of the host who last saved the slot.</summary>
        public const string HostId = "mu3.session.host";

        /// <summary>Metadata: the name of the host who last saved the slot, as the session's roster showed it.</summary>
        public const string HostName = "mu3.session.hostName";

        /// <summary>Metadata: the session name the host last saved the slot with, if it gave one.</summary>
        public const string SessionName = "mu3.session.name";



        /// <summary>
        /// The key under which the slot holds <paramref name="player"/>'s part of the section
        /// <paramref name="key"/>.
        /// </summary>
        public static string PlayerSectionKey(PlayerId player, string key) =>
            PlayerSectionPrefix + player.Value.ToString(CultureInfo.InvariantCulture) + "." + key;

        /// <summary>
        /// Reads the player and the section key out of a slot's section key that
        /// <see cref="PlayerSectionKey"/> made; false for any other section key.
        /// </summary>
        public static bool TryParsePlayerSectionKey(string sectionKey, out PlayerId player, out string key)
        {
            player = PlayerId.None;
            key = null;
            if (sectionKey == null || !sectionKey.StartsWith(PlayerSectionPrefix, System.StringComparison.Ordinal))
            {
                return false;
            }

            int start = PlayerSectionPrefix.Length;
            int dot = sectionKey.IndexOf('.', start);
            if (dot <= start || dot == sectionKey.Length - 1)
            {
                return false;
            }

            string digits = sectionKey.Substring(start, dot - start);
            foreach (char c in digits)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }

            if (!ulong.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value) || value == 0)
            {
                return false;
            }

            player = new PlayerId(value);
            key = sectionKey.Substring(dot + 1);
            return true;
        }
    }
}
