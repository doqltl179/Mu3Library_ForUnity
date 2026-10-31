using System;
using System.Collections.Generic;
using System.Globalization;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Steam
{
    /// <summary>
    /// Reads the lobby Steam names when it launches the game from an accepted invite or a join
    /// from the friends list.
    /// </summary>
    internal static class SteamLaunchArguments
    {
        public const string ConnectLobby = "+connect_lobby";

        public static bool TryGetConnectLobby(IReadOnlyList<string> arguments, out LobbyId lobby)
        {
            lobby = LobbyId.None;
            if (arguments == null)
            {
                return false;
            }

            for (int i = 0; i + 1 < arguments.Count; i++)
            {
                if (string.Equals(arguments[i], ConnectLobby, StringComparison.Ordinal)
                    && ulong.TryParse(arguments[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out ulong value)
                    && value != 0)
                {
                    lobby = new LobbyId(value);
                    return true;
                }
            }

            return false;
        }
    }
}
