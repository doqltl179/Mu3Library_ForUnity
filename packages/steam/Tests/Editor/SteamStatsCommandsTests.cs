#if MU3LIBRARY_STEAMWORKS_SUPPORT && UNITY_STANDALONE
using System.Collections.Generic;
using Mu3Library.DebugConsole;
using Mu3Library.Steam.Stats;
using NUnit.Framework;
using Steamworks;

namespace Mu3Library.Tests
{
    /// <summary>
    /// The console commands of <see cref="SteamStatsCommands"/> on a stand-in for Steam, and
    /// <see cref="SteamStatsManager"/>'s answer while Steam does not run.
    /// </summary>
    public class SteamStatsCommandsTests
    {
        private sealed class FakeStats : ISteamStatsManager
        {
            public readonly List<string> Unlocked = new();
            public readonly List<string> Cleared = new();
            public SteamStatsResult Answer = SteamStatsResult.Success;

            public bool IsAvailable => true;

            public SteamStatsResult UnlockAchievement(string id)
            {
                Unlocked.Add(id);
                return Answer;
            }

            public SteamStatsResult ClearAchievement(string id)
            {
                Cleared.Add(id);
                return Answer;
            }

            public SteamStatsResult GetAchievement(string id, out SteamAchievement achievement)
            {
                achievement = default;
                return Answer;
            }

            public SteamStatsResult GetAchievements(List<SteamAchievement> results)
            {
                results.Clear();
                results.Add(new SteamAchievement("ACH_WIN", "Winner", "Win once.", false, true, null));
                results.Add(new SteamAchievement("ACH_TRAVEL", "Traveler", "Go far.", false, false, null));
                return Answer;
            }

            public SteamStatsResult IndicateAchievementProgress(string id, uint current, uint max) => Answer;
            public SteamStatsResult GetStat(string name, out int value) { value = 0; return Answer; }
            public SteamStatsResult GetStat(string name, out float value) { value = 0f; return Answer; }
            public SteamStatsResult SetStat(string name, int value) => Answer;
            public SteamStatsResult SetStat(string name, float value) => Answer;
            public SteamStatsResult StoreStats() => Answer;
        }

        private DebugConsoleManager _console;



        [SetUp]
        public void SetUp()
        {
            _console = new DebugConsoleManager(captureUnityLog: false);
        }

        [TearDown]
        public void TearDown()
        {
            _console.Dispose();
        }

        [Test]
        public void Console_WithoutRegister_HasNoSteamCommand()
        {
            var commands = new List<ConsoleCommand>();
            _console.GetCommands(commands, runnableOnly: false);

            Assert.That(commands.Exists(command => command.Name.StartsWith("steam.")), Is.False);
        }

        [Test]
        public void Register_ByDefault_AddsDeveloperCommands_AndMarksUnlockAndClearAsProgress()
        {
            SteamStatsCommands.Register(_console, new FakeStats());

            AssertCommand(SteamStatsCommands.ListCommand, ConsoleAccess.Developer, affectsProgress: false);
            AssertCommand(SteamStatsCommands.UnlockCommand, ConsoleAccess.Developer, affectsProgress: true);
            AssertCommand(SteamStatsCommands.ClearCommand, ConsoleAccess.Developer, affectsProgress: true);
        }

        [Test]
        public void Register_ForPlayers_OpensListAndUnlock_AndKeepsClearForDevelopers()
        {
            SteamStatsCommands.Register(_console, new FakeStats(), ConsoleAccess.Player);

            AssertCommand(SteamStatsCommands.ListCommand, ConsoleAccess.Player, affectsProgress: false);
            AssertCommand(SteamStatsCommands.UnlockCommand, ConsoleAccess.Player, affectsProgress: true);
            AssertCommand(SteamStatsCommands.ClearCommand, ConsoleAccess.Developer, affectsProgress: true);
        }

        [Test]
        public void Commands_ReachTheStats_AndMarkProgressOnUnlock()
        {
            var stats = new FakeStats();
            SteamStatsCommands.Register(_console, stats);

            Assert.That(_console.Execute("steam.achievements").IsSuccess, Is.True);
            Assert.That(_console.HasModifiedProgress, Is.False, "Listing changes nothing.");
            Assert.That(LastLines(2), Is.EqualTo(new[] { "[x] ACH_WIN - Winner", "[ ] ACH_TRAVEL - Traveler" }));

            Assert.That(_console.Execute("steam.unlock ACH_TRAVEL").IsSuccess, Is.True);
            Assert.That(stats.Unlocked, Is.EqualTo(new[] { "ACH_TRAVEL" }));
            Assert.That(_console.HasModifiedProgress, Is.True);

            stats.Answer = SteamStatsResult.Failure(SteamStatsFailureReason.SteamNotRunning);
            Assert.That(_console.Execute("steam.clear ACH_WIN").IsSuccess, Is.True);
            Assert.That(stats.Cleared, Is.EqualTo(new[] { "ACH_WIN" }));
            Assert.That(LastLines(1)[0], Does.Contain("SteamNotRunning"), "A failure is printed with its reason.");
        }

        [Test]
        public void Unregister_RemovesTheCommands()
        {
            SteamStatsCommands.Register(_console, new FakeStats());

            Assert.That(SteamStatsCommands.Unregister(_console), Is.True);
            Assert.That(_console.TryGetCommand(SteamStatsCommands.UnlockCommand, out _), Is.False);
            Assert.That(SteamStatsCommands.Unregister(_console), Is.False);
        }

        [Test]
        public void StatsManager_WithoutSteam_AnswersSteamNotRunning()
        {
            if (CallbackDispatcher.IsInitialized)
            {
                Assert.Ignore("Steam runs in this process, started by another test.");
            }

            var stats = new SteamStatsManager();

            Assert.That(stats.IsAvailable, Is.False);
            Assert.That(stats.UnlockAchievement("ACH_WIN_ONE_GAME").Reason, Is.EqualTo(SteamStatsFailureReason.SteamNotRunning));
            Assert.That(stats.GetStat("NumGames", out int _).Reason, Is.EqualTo(SteamStatsFailureReason.SteamNotRunning));
            Assert.That(stats.StoreStats().Reason, Is.EqualTo(SteamStatsFailureReason.SteamNotRunning));
            Assert.That(stats.UnlockAchievement(string.Empty).Reason, Is.EqualTo(SteamStatsFailureReason.InvalidArgument));
            Assert.That(stats.IndicateAchievementProgress("ACH_TRAVEL_FAR_ACCUM", 5, 5).Reason, Is.EqualTo(SteamStatsFailureReason.InvalidArgument));
        }



        private void AssertCommand(string name, ConsoleAccess access, bool affectsProgress)
        {
            Assert.That(_console.TryGetCommand(name, out ConsoleCommand command), Is.True, name);
            Assert.That(command.Access, Is.EqualTo(access), name);
            Assert.That(command.AffectsProgress, Is.EqualTo(affectsProgress), name);
        }

        private string[] LastLines(int count)
        {
            var lines = new string[count];
            for (int i = 0; i < count; i++)
            {
                lines[i] = _console.Lines[_console.Lines.Count - count + i].Text;
            }

            return lines;
        }
    }
}
#endif
