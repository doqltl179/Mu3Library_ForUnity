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

        // The token set as steam_display while a status is set; null leaves that key to the game.
        private string _presenceDisplayToken = null;

        // What this backend set on Steam's rich presence keys; null for a key it did not set.
        private string _presenceStatus = null;
        private string _presenceDisplay = null;
        private string _presenceGroup = null;
        private string _presenceGroupSize = null;



        private void InitializePresence(SteamBackendOptions options)
        {
            _presenceDisplayToken = string.IsNullOrEmpty(options.PresenceDisplayToken) ? null : options.PresenceDisplayToken;
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
        /// The backend sets no <c>connect</c> key: joining from the friends list goes through the
        /// Steam lobby, which raises <see cref="IMultiplayerManagerEventBus.OnJoinRequested"/>.
        /// Disposing the backend deletes the keys it set.
        /// </para>
        /// </remarks>
        public bool SetPresence(string status, LobbyId group, int groupSize)
        {
            if (!IsAvailable)
            {
                return false;
            }

            if (string.IsNullOrEmpty(status))
            {
                status = null;
            }

            string display = status != null ? _presenceDisplayToken : null;
            string groupText = group.IsValid ? group.Value.ToString(CultureInfo.InvariantCulture) : null;
            string sizeText = group.IsValid && groupSize > 0 ? groupSize.ToString(CultureInfo.InvariantCulture) : null;

            // Every key is tried, so one Steam refuses holds no other back. The status goes in
            // before the token that shows it and comes out after it, so the line is never empty.
            bool isSet;
            if (display != null)
            {
                isSet = SetPresenceKey(StatusKey, status, ref _presenceStatus);
                isSet &= SetPresenceKey(DisplayKey, display, ref _presenceDisplay);
            }
            else
            {
                isSet = SetPresenceKey(DisplayKey, null, ref _presenceDisplay);
                isSet &= SetPresenceKey(StatusKey, status, ref _presenceStatus);
            }

            isSet &= SetPresenceKey(GroupKey, groupText, ref _presenceGroup);
            isSet &= SetPresenceKey(GroupSizeKey, sizeText, ref _presenceGroupSize);
            return isSet;
        }

        private static bool SetPresenceKey(string key, string value, ref string current)
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
            if (!SteamFriends.SetRichPresence(key, value ?? string.Empty))
            {
                return false;
            }

            current = value;
            return true;
        }

        private void ShutdownPresence(bool steamRuns)
        {
            if (steamRuns)
            {
                SetPresenceKey(DisplayKey, null, ref _presenceDisplay);
                SetPresenceKey(StatusKey, null, ref _presenceStatus);
                SetPresenceKey(GroupKey, null, ref _presenceGroup);
                SetPresenceKey(GroupSizeKey, null, ref _presenceGroupSize);
            }

            _presenceStatus = null;
            _presenceDisplay = null;
            _presenceGroup = null;
            _presenceGroupSize = null;
        }
    }
}
