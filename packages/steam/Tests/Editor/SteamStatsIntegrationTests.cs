#if MU3LIBRARY_STEAMWORKS_SUPPORT && UNITY_STANDALONE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Save;
using Mu3Library.Steam.Cloud;
using Mu3Library.Steam.Multiplayer;
using Mu3Library.Steam.Stats;
using NUnit.Framework;
using Steamworks;

namespace Mu3Library.Tests
{
    /// <summary>
    /// <see cref="SteamStatsManager"/> and <see cref="SteamCloudSaveStorage"/> against the Steam client
    /// running on this machine, on Steam's test app 480 (Spacewar), whose achievements and stats the
    /// tests set and then put back. They need a signed-in Steam client, so they run only when the
    /// environment variable <c>MU3LIBRARY_STEAM_TESTS</c> is <c>1</c> and are ignored otherwise.
    /// </summary>
    public class SteamStatsIntegrationTests
    {
        private const string EnableVariable = "MU3LIBRARY_STEAM_TESTS";
        private const string Achievement = "ACH_WIN_ONE_GAME";
        private const string IntStat = "NumGames";
        private const string FloatStat = "FeetTraveled";

        private SteamBackend _backend;
        private SteamStatsManager _stats;



        [SetUp]
        public void SetUp()
        {
            if (Environment.GetEnvironmentVariable(EnableVariable) != "1")
            {
                Assert.Ignore($"Set {EnableVariable}=1 with a signed-in Steam client running to check Steam stats and Steam Cloud.");
            }

            // The backend starts Steam on app 480, as a game that also plays sessions does.
            MultiplayerResult created = SteamBackend.TryCreate(new SteamBackendOptions { AppId = 480 }, out _backend);
            Assert.That(created.IsSuccess, Is.True, created.ToString());
            _stats = new SteamStatsManager();
            Assert.That(_stats.IsAvailable, Is.True);

            // Steam hands the player's stats a moment after it starts.
            Stopwatch watch = Stopwatch.StartNew();
            while (!_stats.GetAchievement(Achievement, out _).IsSuccess && watch.Elapsed.TotalSeconds < 10)
            {
                SteamAPI.RunCallbacks();
                System.Threading.Thread.Sleep(50);
            }

            Assert.That(_stats.GetAchievement(Achievement, out _).IsSuccess, Is.True, "Steam did not hand this player's stats within 10 seconds.");
        }

        [TearDown]
        public void TearDown()
        {
            _backend?.Dispose();
            _backend = null;
        }

        [Test]
        public void Achievement_UnlocksAndLocksAgain_AndListsWithItsNames()
        {
            Assert.That(_stats.GetAchievement(Achievement, out SteamAchievement before).IsSuccess, Is.True);
            try
            {
                Assert.That(_stats.UnlockAchievement(Achievement).IsSuccess, Is.True);
                Assert.That(_stats.GetAchievement(Achievement, out SteamAchievement unlocked).IsSuccess, Is.True);
                Assert.That(unlocked.IsUnlocked, Is.True);
                Assert.That(unlocked.UnlockTimeUtc, Is.Not.Null);
                Assert.That(unlocked.DisplayName, Is.Not.Empty);

                var all = new List<SteamAchievement>();
                Assert.That(_stats.GetAchievements(all).IsSuccess, Is.True);
                Assert.That(all.Exists(achievement => achievement.Id == Achievement && achievement.IsUnlocked), Is.True);

                Assert.That(_stats.ClearAchievement(Achievement).IsSuccess, Is.True);
                Assert.That(_stats.GetAchievement(Achievement, out SteamAchievement locked).IsSuccess, Is.True);
                Assert.That(locked.IsUnlocked, Is.False);
                Assert.That(locked.UnlockTimeUtc, Is.Null);
                Assert.That(_stats.GetAchievement("NO_SUCH_ACHIEVEMENT", out _).Reason, Is.EqualTo(SteamStatsFailureReason.Refused));
            }
            finally
            {
                if (before.IsUnlocked)
                {
                    _stats.UnlockAchievement(Achievement);
                }
                else
                {
                    _stats.ClearAchievement(Achievement);
                }
            }
        }

        [Test]
        public void Stats_SetAndStore_ReadBackTheNewValues()
        {
            Assert.That(_stats.GetStat(IntStat, out int games).IsSuccess, Is.True);
            Assert.That(_stats.GetStat(FloatStat, out float feet).IsSuccess, Is.True);
            try
            {
                Assert.That(_stats.SetStat(IntStat, games + 1).IsSuccess, Is.True);
                Assert.That(_stats.SetStat(FloatStat, feet + 2.5f).IsSuccess, Is.True);
                Assert.That(_stats.StoreStats().IsSuccess, Is.True);

                Assert.That(_stats.GetStat(IntStat, out int gamesAfter).IsSuccess, Is.True);
                Assert.That(_stats.GetStat(FloatStat, out float feetAfter).IsSuccess, Is.True);
                Assert.That(gamesAfter, Is.EqualTo(games + 1));
                Assert.That(feetAfter, Is.EqualTo(feet + 2.5f).Within(0.001f));
                Assert.That(_stats.GetStat(IntStat, out float _).Reason, Is.EqualTo(SteamStatsFailureReason.Refused), "The stat is an integer.");
            }
            finally
            {
                _stats.SetStat(IntStat, games);
                _stats.SetStat(FloatStat, feet);
                _stats.StoreStats();
            }
        }

        [Test]
        public void SaveManager_OnSteamCloud_SavesLoadsListsAndDeletesASlot()
        {
            var storage = new SteamCloudSaveStorage("Mu3LibraryTests");
            var saves = new SaveManager(storage);
            int level = 7;
            Assert.That(saves.RegisterSection(SaveSection.Create("progress", () => new SteamCloudSaveStorageTests.Progress { Level = level }, data => level = data.Level)).IsSuccess, Is.True);
            const string slot = "mu3-cloud-test";
            try
            {
                Assert.That(saves.Save(slot).IsSuccess, Is.True);
                level = 0;
                Assert.That(saves.Load(slot).IsSuccess, Is.True);
                Assert.That(level, Is.EqualTo(7));

                var slots = new List<SaveSlotInfo>();
                saves.GetSlots(slots);
                Assert.That(slots.Exists(info => string.Equals(info.SlotId, slot, StringComparison.OrdinalIgnoreCase)), Is.True);
            }
            finally
            {
                Assert.That(saves.DeleteSlot(slot).IsSuccess, Is.True);
            }

            Assert.That(saves.HasSlot(slot), Is.False);
        }
    }
}
#endif
