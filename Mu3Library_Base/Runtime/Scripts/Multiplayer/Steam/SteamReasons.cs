using Mu3Library.Multiplayer.Data;
using Steamworks;

namespace Mu3Library.Multiplayer.Steam
{
    /// <summary>
    /// Maps Steam's answers to the library's reasons and types.
    /// </summary>
    internal static class SteamReasons
    {
        public static MultiplayerReason FromInitResult(ESteamAPIInitResult result)
        {
            switch (result)
            {
                case ESteamAPIInitResult.k_ESteamAPIInitResult_OK: return MultiplayerReason.None;
                case ESteamAPIInitResult.k_ESteamAPIInitResult_NoSteamClient:
                case ESteamAPIInitResult.k_ESteamAPIInitResult_FailedGeneric: return MultiplayerReason.SteamNotRunning;
                default: return MultiplayerReason.BackendError;
            }
        }

        /// <summary>
        /// Steam refuses a lobby that is not joinable and an invite-only lobby without an invite
        /// with the same answer; lobby data that says the game runs tells the first apart.
        /// </summary>
        public static MultiplayerReason FromEnterResponse(EChatRoomEnterResponse response, bool isInGame)
        {
            switch (response)
            {
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess: return MultiplayerReason.None;
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist: return MultiplayerReason.LobbyNotFound;
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseFull: return MultiplayerReason.SessionFull;
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseBanned: return MultiplayerReason.Banned;
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseNotAllowed:
                    return isInGame ? MultiplayerReason.GameInProgress : MultiplayerReason.LobbyNotFound;
                default: return MultiplayerReason.BackendError;
            }
        }

        /// <summary>
        /// Steam answers a client that created too many lobbies with <c>k_EResultLimitExceeded</c>,
        /// as its documentation says, and in a measurement (#423) answered an account that had
        /// created 21 lobbies within about 20 seconds with <c>k_EResultTimeout</c>, 10 seconds
        /// after the request. Both get a reason of their own, so a game can tell them from other
        /// failures and create the lobby again later.
        /// </summary>
        public static MultiplayerReason FromCreateResult(EResult result)
        {
            switch (result)
            {
                case EResult.k_EResultOK: return MultiplayerReason.None;
                case EResult.k_EResultNoConnection: return MultiplayerReason.SteamNotRunning;
                case EResult.k_EResultLimitExceeded: return MultiplayerReason.RateLimited;
                case EResult.k_EResultTimeout: return MultiplayerReason.Timeout;
                default: return MultiplayerReason.BackendError;
            }
        }

        public static ELobbyType ToLobbyType(LobbyVisibility visibility)
        {
            switch (visibility)
            {
                case LobbyVisibility.Public: return ELobbyType.k_ELobbyTypePublic;
                case LobbyVisibility.FriendsOnly: return ELobbyType.k_ELobbyTypeFriendsOnly;
                default: return ELobbyType.k_ELobbyTypePrivate;
            }
        }
    }
}
