using System;
using System.Collections.Generic;
using Mu3Library.DebugConsole;

namespace Mu3Library.Steam.Stats
{
    /// <summary>
    /// Console commands for Steam achievements, which a game adds to its debug console only when it
    /// calls <see cref="Register"/>: <c>steam.achievements</c> lists every achievement and whether it is
    /// unlocked, <c>steam.unlock &lt;id&gt;</c> unlocks one, and <c>steam.clear &lt;id&gt;</c> locks one
    /// again. The game chooses who may list and unlock; <c>steam.clear</c> is always a developer
    /// command. Both <c>steam.unlock</c> and <c>steam.clear</c> are marked as changing progress
    /// (<see cref="ConsoleCommandOptions.AffectsProgress"/>), so the console's
    /// <see cref="IDebugConsoleManager.HasModifiedProgress"/> tells the game a player used them.
    /// </summary>
    public static class SteamStatsCommands
    {
        public const string ListCommand = "steam.achievements";
        public const string UnlockCommand = "steam.unlock";
        public const string ClearCommand = "steam.clear";

        /// <summary>
        /// Registers the commands with <paramref name="console"/>.
        /// </summary>
        /// <param name="access">
        /// Who may list and unlock: <see cref="ConsoleAccess.Developer"/>, the default, in the editor
        /// and a Development Build only, or <see cref="ConsoleAccess.Player"/> for a game that lets its
        /// players take achievements on purpose.
        /// </param>
        /// <exception cref="ArgumentException">A command of one of these names is already registered.</exception>
        public static void Register(IDebugConsoleManager console, ISteamStatsManager stats, ConsoleAccess access = ConsoleAccess.Developer)
        {
            if (console == null)
            {
                throw new ArgumentNullException(nameof(console));
            }

            if (stats == null)
            {
                throw new ArgumentNullException(nameof(stats));
            }

            foreach (string name in new[] { ListCommand, UnlockCommand, ClearCommand })
            {
                if (console.TryGetCommand(name, out _))
                {
                    throw new ArgumentException($"A command named '{name}' is already registered.", nameof(console));
                }
            }

            console.RegisterCommand(ListCommand, call => List(call, stats),
                new ConsoleCommandOptions("Lists the app's Steam achievements.", access));
            console.RegisterCommand<string>(UnlockCommand, id => Report(console, stats.UnlockAchievement(id), $"Unlocked {id}."),
                new ConsoleCommandOptions("Unlocks a Steam achievement by its API name.", access, affectsProgress: true));
            console.RegisterCommand<string>(ClearCommand, id => Report(console, stats.ClearAchievement(id), $"Locked {id} again."),
                new ConsoleCommandOptions("Locks a Steam achievement again, for testing.", ConsoleAccess.Developer, affectsProgress: true));
        }

        /// <summary>Removes the commands <see cref="Register"/> added. False when none was registered.</summary>
        public static bool Unregister(IDebugConsoleManager console)
        {
            if (console == null)
            {
                throw new ArgumentNullException(nameof(console));
            }

            bool removed = console.UnregisterCommand(ListCommand);
            removed |= console.UnregisterCommand(UnlockCommand);
            removed |= console.UnregisterCommand(ClearCommand);
            return removed;
        }



        private static void List(ConsoleCall call, ISteamStatsManager stats)
        {
            var achievements = new List<SteamAchievement>();
            SteamStatsResult result = stats.GetAchievements(achievements);
            if (!result.IsSuccess)
            {
                call.PrintError(result.ToString());
                return;
            }

            if (achievements.Count == 0)
            {
                call.Print("No achievements.");
                return;
            }

            foreach (SteamAchievement achievement in achievements)
            {
                string name = string.IsNullOrEmpty(achievement.DisplayName) ? string.Empty : $" - {achievement.DisplayName}";
                call.Print($"[{(achievement.IsUnlocked ? "x" : " ")}] {achievement.Id}{name}");
            }
        }

        private static void Report(IDebugConsoleManager console, SteamStatsResult result, string success)
        {
            if (result.IsSuccess)
            {
                console.Print(success);
            }
            else
            {
                console.PrintError(result.ToString());
            }
        }
    }
}
