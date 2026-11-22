namespace Mu3Library.Steam.Stats
{
    public enum SteamStatsFailureReason
    {
        None = 0,

        /// <summary>
        /// Steam does not run in this process: the game has not started Steamworks.NET, shut it
        /// down, or its native library is missing or outdated.
        /// </summary>
        SteamNotRunning,
        /// <summary>A name is null or empty, or a progress is out of range.</summary>
        InvalidArgument,
        /// <summary>
        /// Steam answered no: the name is not one of the app's achievements or stats of that type,
        /// or Steam has not received this player's stats yet; <see cref="SteamStatsResult.Message"/> says which call.
        /// </summary>
        Refused,
    }
}
