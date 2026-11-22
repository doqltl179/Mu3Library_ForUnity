using System.Collections.Generic;

namespace Mu3Library.Steam.Stats
{
    /// <summary>
    /// This player's Steam achievements and stats, as the Steamworks partner site defines them for
    /// the app. It uses the Steam the game started, through <c>SteamManager.TryStart</c>,
    /// <c>SteamBackend.TryCreate</c>, or its own <c>SteamAPI.Init</c>, and starts none itself. Steam
    /// hands this player's stats a moment after it starts, and a call answers
    /// <see cref="SteamStatsFailureReason.Refused"/> until then. Every call runs on the calling thread
    /// and comes back as a <see cref="SteamStatsResult"/>; none throws for Steam's sake.
    /// </summary>
    public interface ISteamStatsManager
    {
        /// <summary>True while Steam runs in this process.</summary>
        public bool IsAvailable { get; }

        /// <summary>
        /// Unlocks the achievement and stores it at once, so Steam shows its notification.
        /// Unlocking one that is unlocked already succeeds.
        /// </summary>
        public SteamStatsResult UnlockAchievement(string id);

        /// <summary>
        /// Locks the achievement again and stores it, for testing during development; players keep
        /// what they unlocked, so a released game does not call it.
        /// </summary>
        public SteamStatsResult ClearAchievement(string id);

        /// <summary>Reads the achievement's state, unlock time, and the names the partner site gives it.</summary>
        public SteamStatsResult GetAchievement(string id, out SteamAchievement achievement);

        /// <summary>
        /// Fills <paramref name="results"/>, which it clears first, with every achievement of the
        /// app, in the partner site's order.
        /// </summary>
        public SteamStatsResult GetAchievements(List<SteamAchievement> results);

        /// <summary>
        /// Shows the player the achievement's progress, <paramref name="current"/> of
        /// <paramref name="max"/>, without storing any; <paramref name="current"/> stays below
        /// <paramref name="max"/>, since reaching it is <see cref="UnlockAchievement"/>. Steam refuses
        /// it for an achievement unlocked already.
        /// </summary>
        public SteamStatsResult IndicateAchievementProgress(string id, uint current, uint max);

        public SteamStatsResult GetStat(string name, out int value);
        public SteamStatsResult GetStat(string name, out float value);

        /// <summary>Sets a stat in memory; <see cref="StoreStats"/> sends it to Steam.</summary>
        public SteamStatsResult SetStat(string name, int value);

        /// <summary>Sets a stat in memory; <see cref="StoreStats"/> sends it to Steam.</summary>
        public SteamStatsResult SetStat(string name, float value);

        /// <summary>
        /// Sends the stats set since the last store to Steam, which may unlock the achievements the
        /// partner site ties to them. Steam limits how often a game stores, so store at points such as
        /// the end of a match rather than every frame.
        /// </summary>
        public SteamStatsResult StoreStats();
    }
}
