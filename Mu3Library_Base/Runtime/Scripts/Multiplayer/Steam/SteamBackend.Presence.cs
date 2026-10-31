using System;
using System.Globalization;
using Mu3Library.Multiplayer.Data;
using Steamworks;

namespace Mu3Library.Multiplayer.Steam
{
    public sealed partial class SteamBackend
    {
        internal const string StatusKey = "status";
        internal const string GroupKey = "steam_player_group";
        internal const string GroupSizeKey = "steam_player_group_size";

        // What this backend set on Steam's rich presence keys; null for a key it did not set.
        private string _presenceStatus = null;
        private string _presenceGroup = null;
        private string _presenceGroupSize = null;



        /// <summary>
        /// Sets Steam's rich presence: the key <c>status</c> to the status, and
        /// <c>steam_player_group</c> to the lobby ID with <c>steam_player_group_size</c> to the
        /// player count, and deletes a key that a call leaves empty. It touches no other key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Steam documents <c>status</c> as the text of the game info that its friends list opens
        /// for a friend; the rich presence line in the friends list comes from
        /// <c>steam_display</c>, a localization token that a game configures for its own app ID,
        /// which this backend does not set. Steam's test app 480 has tokens of its own,
        /// not a game's. A game that wants that line sets <c>steam_display</c> itself with
        /// Steamworks.NET, and clears it itself, since the backend deletes only the keys it set.
        /// The group shows no text: Steam uses it to place the players of one session together.
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

            string groupText = group.IsValid ? group.Value.ToString(CultureInfo.InvariantCulture) : null;
            string sizeText = group.IsValid && groupSize > 0 ? groupSize.ToString(CultureInfo.InvariantCulture) : null;

            // Every key is tried, so one Steam refuses holds no other back.
            bool isSet = SetPresenceKey(StatusKey, status, ref _presenceStatus);
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
                SetPresenceKey(StatusKey, null, ref _presenceStatus);
                SetPresenceKey(GroupKey, null, ref _presenceGroup);
                SetPresenceKey(GroupSizeKey, null, ref _presenceGroupSize);
            }

            _presenceStatus = null;
            _presenceGroup = null;
            _presenceGroupSize = null;
        }
    }
}
