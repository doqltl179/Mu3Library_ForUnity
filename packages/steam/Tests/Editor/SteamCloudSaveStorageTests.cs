#if MU3LIBRARY_STEAMWORKS_SUPPORT && UNITY_STANDALONE
using System;
using System.Collections.Generic;
using Mu3Library.Save;
using Mu3Library.Steam.Cloud;
using NUnit.Framework;

namespace Mu3Library.Tests
{
    /// <summary>
    /// <see cref="SteamCloudSaveStorage"/> on a stand-in for Steam Remote Storage: the file each
    /// slot takes, what it answers when Steam refuses or does not run, and a save manager on top.
    /// </summary>
    public class SteamCloudSaveStorageTests
    {
        private sealed class FakeRemoteStorage : ISteamRemoteStorage
        {
            public readonly Dictionary<string, byte[]> Files = new(StringComparer.OrdinalIgnoreCase);
            public bool IsRunning = true;
            public bool RefusesWrites;
            public int ShortRead = -1;

            public bool FileExists(string fileName) => Check() && Files.ContainsKey(fileName);
            public int GetFileSize(string fileName) => Check() && Files.TryGetValue(fileName, out byte[] data) ? data.Length : 0;

            public int FileRead(string fileName, byte[] buffer, int size)
            {
                Check();
                byte[] data = Files[fileName];
                int count = ShortRead >= 0 ? ShortRead : Math.Min(size, data.Length);
                Array.Copy(data, buffer, count);
                return count;
            }

            public bool FileWrite(string fileName, byte[] data, int size)
            {
                Check();
                if (RefusesWrites)
                {
                    return false;
                }

                Files[fileName] = data.AsSpan(0, size).ToArray();
                return true;
            }

            public bool FileDelete(string fileName) => Check() && Files.Remove(fileName);
            public int GetFileCount() => Check() ? Files.Count : 0;

            public string GetFileNameAndSize(int index, out int size)
            {
                Check();
                string name = new List<string>(Files.Keys)[index];
                size = Files[name].Length;
                return name;
            }

            public bool IsCloudEnabledForAccount() => Check();
            public bool IsCloudEnabledForApp() => Check();

            private bool Check()
            {
                if (!IsRunning)
                {
                    // What Steamworks.NET throws for a Steam it has not started.
                    throw new InvalidOperationException("Steamworks is not initialized.");
                }

                return true;
            }
        }

        [Test]
        public void SaveManager_OnSteamCloud_KeepsEachSlotInItsOwnFileOfTheFolder()
        {
            var steam = new FakeRemoteStorage();
            steam.Files["other.txt"] = new byte[] { 1 };
            steam.Files["Elsewhere/slot9.sav"] = new byte[] { 2 };
            var storage = new SteamCloudSaveStorage("Game/Saves", steam);
            var saves = new SaveManager(storage);
            int level = 3;
            Assert.That(saves.RegisterSection(SaveSection.Create("progress", () => new Progress { Level = level }, data => level = data.Level)).IsSuccess, Is.True);

            Assert.That(saves.Save("slot1").IsSuccess, Is.True);
            level = 0;
            Assert.That(saves.Load("slot1").IsSuccess, Is.True);

            Assert.That(level, Is.EqualTo(3));
            Assert.That(storage.GetFileName("slot1"), Is.EqualTo("Game/Saves/slot1.sav"));
            Assert.That(steam.Files.ContainsKey("Game/Saves/slot1.sav"), Is.True);
            var ids = new List<string>();
            storage.GetSlotIds(ids);
            Assert.That(ids, Is.EqualTo(new[] { "slot1" }), "Files outside the folder or of another kind are no slots.");
            Assert.That(saves.DeleteSlot("slot1").IsSuccess, Is.True);
            Assert.That(storage.Exists("slot1"), Is.False);
            Assert.That(storage.Delete("slot1").IsSuccess, Is.True, "Deleting a missing slot succeeds.");
        }

        [Test]
        public void Storage_AnswersWhatSteamRefuses_AsAStorageFailure()
        {
            var steam = new FakeRemoteStorage();
            var storage = new SteamCloudSaveStorage(SteamCloudSaveStorage.DefaultFolder, steam);

            Assert.That(storage.Read("missing", out _).Reason, Is.EqualTo(SaveFailureReason.SlotNotFound));
            Assert.That(storage.Write("bad id", new byte[] { 1 }).Reason, Is.EqualTo(SaveFailureReason.InvalidKey));

            steam.RefusesWrites = true;
            Assert.That(storage.Write("slot1", new byte[] { 1 }).Reason, Is.EqualTo(SaveFailureReason.StorageFailed));

            steam.RefusesWrites = false;
            Assert.That(storage.Write("slot1", new byte[] { 1, 2, 3 }).IsSuccess, Is.True);
            steam.ShortRead = 2;
            Assert.That(storage.Read("slot1", out byte[] data).Reason, Is.EqualTo(SaveFailureReason.StorageFailed));
            Assert.That(data, Is.Null);
        }

        [Test]
        public void Storage_WithoutSteam_FailsWithoutThrowing()
        {
            var steam = new FakeRemoteStorage();
            var storage = new SteamCloudSaveStorage(SteamCloudSaveStorage.DefaultFolder, steam);
            Assert.That(storage.Write("slot1", new byte[] { 1 }).IsSuccess, Is.True);
            steam.IsRunning = false;

            Assert.That(storage.IsCloudEnabled, Is.False);
            Assert.That(storage.Exists("slot1"), Is.False);
            Assert.That(storage.Read("slot1", out _).Reason, Is.EqualTo(SaveFailureReason.StorageFailed));
            Assert.That(storage.Write("slot1", new byte[] { 1 }).Reason, Is.EqualTo(SaveFailureReason.StorageFailed));
            Assert.That(storage.Delete("slot1").Reason, Is.EqualTo(SaveFailureReason.StorageFailed));
            var ids = new List<string>();
            storage.GetSlotIds(ids);
            Assert.That(ids, Is.Empty);
        }

        [Test]
        public void Folder_TakesPlainNamesOnly()
        {
            Assert.That(new SteamCloudSaveStorage(string.Empty, new FakeRemoteStorage()).GetFileName("a"), Is.EqualTo("a.sav"));
            Assert.Throws<ArgumentException>(() => new SteamCloudSaveStorage("../up", new FakeRemoteStorage()));
            Assert.Throws<ArgumentException>(() => new SteamCloudSaveStorage("Saves/", new FakeRemoteStorage()));
            Assert.Throws<ArgumentNullException>(() => new SteamCloudSaveStorage(null, new FakeRemoteStorage()));
        }

        [Serializable]
        public class Progress
        {
            public int Level;
        }
    }
}
#endif
