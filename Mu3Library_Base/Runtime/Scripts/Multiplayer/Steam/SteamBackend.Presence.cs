using System;
using System.Globalization;
using System.Text;
using Mu3Library.Multiplayer.Data;
using Steamworks;

namespace Mu3Library.Multiplayer.Steam
{
    public sealed partial class SteamBackend
    {
        internal const string StatusKey = "status";
        internal const string DisplayKey = "steam_display";
        internal const string GroupKey = "steam_player_group";
        internal const string GroupSizeKey = "steam_player_group_size";

        /// <summary>
        /// The rich presence keys a backend set and the order it writes them in, through
        /// <c>SteamFriends.SetRichPresence</c> in the backend and through a stand-in that can
        /// refuse a key in the tests.
        /// </summary>
        internal sealed class PresenceKeys
        {
            // Writes one key, an empty value deleting it; false when Steam refused it.
            private readonly Func<string, string, bool> _write;

            // The token set as steam_display while a status is set; null leaves that key to the game.
            private readonly string _displayToken;

            // What this backend set on Steam's rich presence keys; null for a key it did not set.
            private string _status = null;
            private string _display = null;
            private string _group = null;
            private string _groupSize = null;



            public PresenceKeys(string displayToken, Func<string, string, bool> write)
            {
                _displayToken = string.IsNullOrEmpty(displayToken) ? null : displayToken;
                _write = write;
            }

            /// <summary>Writes the keys that changed, as <see cref="SteamBackend.SetPresence"/> describes; false when Steam refused one.</summary>
            public bool Set(string status, LobbyId group, int groupSize)
            {
                if (string.IsNullOrEmpty(status))
                {
                    status = null;
                }

                string display = status != null ? _displayToken : null;
                string groupText = group.IsValid ? group.Value.ToString(CultureInfo.InvariantCulture) : null;
                string sizeText = group.IsValid && groupSize > 0 ? groupSize.ToString(CultureInfo.InvariantCulture) : null;

                // The status goes in before the token that shows it and comes out only after it,
                // so the friends list never shows the line empty: when Steam refuses the first of
                // the two, the second waits. The group keys are tried either way, so a key Steam
                // refuses holds no other back.
                bool isSet = display != null
                    ? Write(StatusKey, status, ref _status) && Write(DisplayKey, display, ref _display)
                    : Write(DisplayKey, null, ref _display) && Write(StatusKey, status, ref _status);

                isSet &= Write(GroupKey, groupText, ref _group);
                isSet &= Write(GroupSizeKey, sizeText, ref _groupSize);
                return isSet;
            }

            /// <summary>
            /// Deletes every key this backend set, in the order <see cref="Set"/> keeps, when
            /// <paramref name="deleteOnSteam"/>; then forgets them either way.
            /// </summary>
            public void Clear(bool deleteOnSteam)
            {
                if (deleteOnSteam)
                {
                    Set(null, LobbyId.None, 0);
                }

                _status = null;
                _display = null;
                _group = null;
                _groupSize = null;
            }

            private bool Write(string key, string value, ref string current)
            {
                if (string.IsNullOrEmpty(value))
                {
                    value = null;
                }

                if (string.Equals(value, current, StringComparison.Ordinal))
                {
                    return true;
                }

                // An empty value deletes the key.
                if (!_write(key, value ?? string.Empty))
                {
                    return false;
                }

                current = value;
                return true;
            }
        }

        private PresenceKeys _presence = null;



        private void InitializePresence(SteamBackendOptions options)
        {
            _presence = new PresenceKeys(options.PresenceDisplayToken, SteamFriends.SetRichPresence);
        }

        internal static bool IsValidDisplayToken(string token)
        {
            return string.IsNullOrEmpty(token)
                || (token.Length > 1
                    && token[0] == '#'
                    && token.IndexOf('\0') < 0
                    && Encoding.UTF8.GetByteCount(token) <= MultiplayerLimits.MaxPresenceStatusSize);
        }

        /// <summary>
        /// Sets Steam's rich presence: the key <c>status</c> to the status, with
        /// <see cref="SteamBackendOptions.PresenceDisplayToken"/> as <c>steam_display</c> while a
        /// status is set, and <c>steam_player_group</c> to the lobby ID with
        /// <c>steam_player_group_size</c> to the player count. It deletes a key that a call leaves
        /// empty, and touches no key it did not set.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Steam's friends list shows a friend's rich presence line only through
        /// <c>steam_display</c>, a localization token that a game defines for its own app ID;
        /// Steam documents <c>status</c> alone as the text of the game info that its friends list
        /// opens for a friend. Without <see cref="SteamBackendOptions.PresenceDisplayToken"/> the
        /// backend leaves <c>steam_display</c> to the game, which then sets and clears it itself
        /// with Steamworks.NET. Steam's test app 480 has tokens of its own, not a game's. The
        /// group shows no text: Steam uses it to place the players of one session together.
        /// </para>
        /// <para>
        /// So that the line never shows an empty status, the backend sets the token only once
        /// Steam took the status, and deletes the status only once Steam deleted the token: when
        /// Steam refuses the first, the call answers false and leaves the second as it was. It
        /// tries the group keys either way.
        /// </para>
        /// <para>
        /// The backend sets no <c>connect</c> key: joining from the friends list goes through the
        /// Steam lobby, which raises <see cref="IMultiplayerManagerEventBus.OnJoinRequested"/>.
        /// Disposing the backend deletes the keys it set, in the same order.
        /// </para>
        /// </remarks>
        public bool SetPresence(string status, LobbyId group, int groupSize)
        {
            return IsAvailable && _presence.Set(status, group, groupSize);
        }

        private void ShutdownPresence(bool steamRuns)
        {
            _presence?.Clear(deleteOnSteam: steamRuns);
        }
    }
}
