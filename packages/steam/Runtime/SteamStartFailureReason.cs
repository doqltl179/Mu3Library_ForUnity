namespace Mu3Library.Steam
{
    public enum SteamStartFailureReason
    {
        None = 0,

        /// <summary>
        /// Steam cannot start in this process: the Steam client is not running, Steam found no app
        /// ID, or Steam's native library is missing, lacks a function, or is built for another
        /// architecture; <see cref="SteamStartResult.Message"/> says what Steam or the runtime answered.
        /// </summary>
        SteamNotRunning,
        /// <summary>
        /// The Steam client is older than the Steam native library the game ships, which updating
        /// Steam fixes.
        /// </summary>
        VersionMismatch,
    }
}
