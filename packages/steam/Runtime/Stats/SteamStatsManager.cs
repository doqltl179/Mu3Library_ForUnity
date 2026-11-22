using System;
using System.Collections.Generic;
using Steamworks;

namespace Mu3Library.Steam.Stats
{
    /// <summary>
    /// <see cref="ISteamStatsManager"/> on Steamworks.NET's <c>SteamUserStats</c>.
    /// </summary>
    /// <remarks>
    /// The assembly compiles only with Steamworks.NET 2025.164.1 or later, and only in the editor
    /// and in 64-bit standalone players, as <c>Mu3Library.Steam.Multiplayer</c> does; game code that
    /// uses it needs the same gate (the package README's Platforms And The Gate). A Steam call that
    /// throws because Steam cannot run in this process, such as before the game started Steam or
    /// with a native library that lacks a function, answers
    /// <see cref="SteamStatsFailureReason.SteamNotRunning"/>.
    /// </remarks>
    public sealed class SteamStatsManager : ISteamStatsManager
    {
        public bool IsAvailable
        {
            get
            {
                try
                {
                    return CallbackDispatcher.IsInitialized;
                }
                catch (Exception exception) when (SteamCalls.IsSteamUnavailable(exception))
                {
                    return false;
                }
            }
        }



        public SteamStatsResult UnlockAchievement(string id)
        {
            return Run(id, () =>
            {
                if (!SteamUserStats.SetAchievement(id))
                {
                    return Refused($"Steam did not unlock '{id}'.");
                }

                return SteamUserStats.StoreStats() ? SteamStatsResult.Success : Refused($"Steam unlocked '{id}' but did not store it.");
            });
        }

        public SteamStatsResult ClearAchievement(string id)
        {
            return Run(id, () =>
            {
                if (!SteamUserStats.ClearAchievement(id))
                {
                    return Refused($"Steam did not lock '{id}'.");
                }

                return SteamUserStats.StoreStats() ? SteamStatsResult.Success : Refused($"Steam locked '{id}' but did not store it.");
            });
        }

        public SteamStatsResult GetAchievement(string id, out SteamAchievement achievement)
        {
            SteamAchievement read = default;
            SteamStatsResult result = Run(id, () => ReadAchievement(id, out read));
            achievement = read;
            return result;
        }

        public SteamStatsResult GetAchievements(List<SteamAchievement> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            return Run(() =>
            {
                uint count = SteamUserStats.GetNumAchievements();
                for (uint i = 0; i < count; i++)
                {
                    string id = SteamUserStats.GetAchievementName(i);
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    SteamStatsResult read = ReadAchievement(id, out SteamAchievement achievement);
                    if (!read.IsSuccess)
                    {
                        return read;
                    }

                    results.Add(achievement);
                }

                return SteamStatsResult.Success;
            });
        }

        public SteamStatsResult IndicateAchievementProgress(string id, uint current, uint max)
        {
            if (max == 0 || current >= max)
            {
                return SteamStatsResult.Failure(SteamStatsFailureReason.InvalidArgument, "The progress must stay below its maximum; unlock the achievement instead.");
            }

            return Run(id, () => SteamUserStats.IndicateAchievementProgress(id, current, max)
                ? SteamStatsResult.Success
                : Refused($"Steam did not show the progress of '{id}', which may be unlocked already."));
        }

        public SteamStatsResult GetStat(string name, out int value)
        {
            int read = 0;
            SteamStatsResult result = Run(name, () => SteamUserStats.GetStat(name, out read)
                ? SteamStatsResult.Success
                : Refused($"Steam has no integer stat '{name}' for this player."));
            value = read;
            return result;
        }

        public SteamStatsResult GetStat(string name, out float value)
        {
            float read = 0f;
            SteamStatsResult result = Run(name, () => SteamUserStats.GetStat(name, out read)
                ? SteamStatsResult.Success
                : Refused($"Steam has no float stat '{name}' for this player."));
            value = read;
            return result;
        }

        public SteamStatsResult SetStat(string name, int value)
        {
            return Run(name, () => SteamUserStats.SetStat(name, value)
                ? SteamStatsResult.Success
                : Refused($"Steam did not set the integer stat '{name}'."));
        }

        public SteamStatsResult SetStat(string name, float value)
        {
            return Run(name, () => SteamUserStats.SetStat(name, value)
                ? SteamStatsResult.Success
                : Refused($"Steam did not set the float stat '{name}'."));
        }

        public SteamStatsResult StoreStats()
        {
            return Run(() => SteamUserStats.StoreStats() ? SteamStatsResult.Success : Refused("Steam did not store the stats."));
        }



        private static SteamStatsResult ReadAchievement(string id, out SteamAchievement achievement)
        {
            achievement = default;
            if (!SteamUserStats.GetAchievementAndUnlockTime(id, out bool unlocked, out uint unlockTime))
            {
                return Refused($"Steam has no achievement '{id}' for this player.");
            }

            DateTime? unlockedAt = unlocked && unlockTime > 0
                ? DateTimeOffset.FromUnixTimeSeconds(unlockTime).UtcDateTime
                : (DateTime?)null;
            achievement = new SteamAchievement(
                id,
                SteamUserStats.GetAchievementDisplayAttribute(id, "name"),
                SteamUserStats.GetAchievementDisplayAttribute(id, "desc"),
                SteamUserStats.GetAchievementDisplayAttribute(id, "hidden") == "1",
                unlocked,
                unlockedAt);
            return SteamStatsResult.Success;
        }

        private static SteamStatsResult Refused(string message) =>
            SteamStatsResult.Failure(SteamStatsFailureReason.Refused, message);

        private static SteamStatsResult Run(string name, Func<SteamStatsResult> call)
        {
            if (string.IsNullOrEmpty(name))
            {
                return SteamStatsResult.Failure(SteamStatsFailureReason.InvalidArgument, "The name is empty.");
            }

            return Run(call);
        }

        private static SteamStatsResult Run(Func<SteamStatsResult> call)
        {
            try
            {
                return call();
            }
            catch (Exception exception) when (SteamCalls.IsSteamUnavailable(exception))
            {
                return SteamStatsResult.Failure(SteamStatsFailureReason.SteamNotRunning, exception.Message);
            }
        }
    }
}
