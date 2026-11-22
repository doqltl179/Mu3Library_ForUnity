using System;
using System.Collections.Generic;
using System.IO;
using Mu3Library.Save;
using NUnit.Framework;
using UnityEngine;

namespace Mu3Library.Tests
{
    public class SaveManagerTests
    {
        [Serializable]
        public class PlayerData
        {
            public string Name;
            public int Level;
            public Vector3 Position;
            public Quaternion Rotation;
            public Color Tint;
            public List<int> Items = new();
        }

        [Serializable]
        public struct StageData
        {
            public int Stage;
            public float BestTime;
        }

        private sealed class PlayerSection : SaveSection<PlayerData>
        {
            private readonly int _version;

            public PlayerData Current = new();
            public int RestoredFromVersion = -1;
            public int MigrateCalls;
            public int DefaultCalls;
            public bool ThrowOnRestore;

            public override string Key => "player";
            public override int Version => _version;



            public PlayerSection(int version = 1)
            {
                _version = version;
            }

            protected override PlayerData Capture() => Current;

            protected override void Restore(PlayerData data)
            {
                if (ThrowOnRestore)
                {
                    throw new InvalidOperationException("restore failed");
                }

                Current = data;
            }

            protected override PlayerData Migrate(int savedVersion, PlayerData data)
            {
                MigrateCalls++;
                RestoredFromVersion = savedVersion;
                if (savedVersion < 2)
                {
                    data.Level *= 10;
                }

                return data;
            }

            protected override void RestoreDefault()
            {
                DefaultCalls++;
                Current = new PlayerData { Name = "Default" };
            }
        }

        private MemorySaveStorage _storage;
        private SaveManager _manager;
        private string _directory;



        [SetUp]
        public void SetUp()
        {
            _storage = new MemorySaveStorage();
            _manager = new SaveManager(_storage);
        }

        [TearDown]
        public void TearDown()
        {
            if (_directory != null && Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }

            _directory = null;
        }

        private static PlayerData SamplePlayer() => new()
        {
            Name = "Mu",
            Level = 7,
            Position = new Vector3(1.5f, -2f, 30f),
            Rotation = Quaternion.Euler(10f, 20f, 30f),
            Tint = new Color(0.1f, 0.2f, 0.3f, 0.4f),
            Items = new List<int> { 3, 1, 4 },
        };

        private (PlayerSection Player, SaveSection<StageData> Stage, Func<StageData> Read) RegisterBoth(StageData stage)
        {
            var player = new PlayerSection { Current = SamplePlayer() };
            StageData current = stage;
            SaveSection<StageData> stageSection = SaveSection.Create("stage", () => current, data => current = data);
            Assert.That(_manager.RegisterSection(player).IsSuccess);
            Assert.That(_manager.RegisterSection(stageSection).IsSuccess);
            return (player, stageSection, () => current);
        }

        [Test]
        public void SaveAndLoad_RoundTripsUnityTypes()
        {
            var player = new PlayerSection { Current = SamplePlayer() };
            _manager.RegisterSection(player);

            Assert.That(_manager.Save("slot1").IsSuccess);
            player.Current = new PlayerData();
            SaveResult result = _manager.Load("slot1");

            Assert.That(result.IsSuccess, result.ToString());
            PlayerData expected = SamplePlayer();
            Assert.That(player.Current.Name, Is.EqualTo(expected.Name));
            Assert.That(player.Current.Level, Is.EqualTo(expected.Level));
            Assert.That(player.Current.Position, Is.EqualTo(expected.Position));
            Assert.That(Quaternion.Angle(player.Current.Rotation, expected.Rotation), Is.LessThan(0.01f));
            Assert.That(player.Current.Tint, Is.EqualTo(expected.Tint));
            Assert.That(player.Current.Items, Is.EqualTo(expected.Items));
        }

        [Test]
        public void SaveOneSection_KeepsTheOtherSections()
        {
            (PlayerSection player, _, Func<StageData> readStage) = RegisterBoth(new StageData { Stage = 1, BestTime = 12f });
            Assert.That(_manager.Save("slot").IsSuccess);

            player.Current.Level = 99;
            Assert.That(_manager.Save("slot", "player").IsSuccess);

            Assert.That(_manager.Peek("slot", "stage", out StageData stage, out _).IsSuccess);
            Assert.That(stage.Stage, Is.EqualTo(1));
            Assert.That(_manager.Peek("slot", "player", out PlayerData saved, out _).IsSuccess);
            Assert.That(saved.Level, Is.EqualTo(99));
            Assert.That(readStage().Stage, Is.EqualTo(1));
        }

        [Test]
        public void LoadOneSection_RestoresOnlyThatSection()
        {
            (PlayerSection player, _, Func<StageData> readStage) = RegisterBoth(new StageData { Stage = 3 });
            Assert.That(_manager.Save("slot").IsSuccess);

            player.Current = new PlayerData();
            SaveResult result = _manager.Load("slot", "player");

            Assert.That(result.IsSuccess, result.ToString());
            Assert.That(player.Current.Name, Is.EqualTo("Mu"));
            Assert.That(readStage().Stage, Is.EqualTo(3));
        }

        [Test]
        public void SectionTheSlotDoesNotRegister_StaysInTheSlot()
        {
            RegisterBoth(new StageData { Stage = 5 });
            Assert.That(_manager.Save("slot").IsSuccess);

            var other = new SaveManager(_storage);
            var player = new PlayerSection { Current = SamplePlayer() };
            other.RegisterSection(player);
            player.Current.Level = 2;
            Assert.That(other.Save("slot").IsSuccess);

            Assert.That(_manager.TryGetSlotInfo("slot", out SaveSlotInfo info));
            Assert.That(info.HasSection("stage"));
            Assert.That(_manager.Peek("slot", "stage", out StageData stage, out _).IsSuccess);
            Assert.That(stage.Stage, Is.EqualTo(5));
        }

        [Test]
        public void LoadWholeSlot_SectionItDoesNotHold_RestoresDefault()
        {
            var player = new PlayerSection { Current = SamplePlayer() };
            _manager.RegisterSection(player);
            Assert.That(_manager.Save("slot").IsSuccess);

            StageData current = new StageData { Stage = 9 };
            int defaults = 0;
            _manager.RegisterSection(SaveSection.Create("stage", () => current, d => current = d, restoreDefault: () =>
            {
                defaults++;
                current = default;
            }));

            Assert.That(_manager.Load("slot").IsSuccess);
            Assert.That(defaults, Is.EqualTo(1));
            Assert.That(current.Stage, Is.EqualTo(0));
            Assert.That(player.DefaultCalls, Is.EqualTo(0));
        }

        [Test]
        public void LoadOneSection_SlotDoesNotHoldIt_FailsWithSectionNotFound()
        {
            var player = new PlayerSection();
            _manager.RegisterSection(player);
            _manager.RegisterSection(SaveSection.Create("stage", () => new StageData(), _ => { }));
            Assert.That(_manager.Save("slot", "stage").IsSuccess);

            SaveResult result = _manager.Load("slot", "player");

            Assert.That(result.Reason, Is.EqualTo(SaveFailureReason.SectionNotFound));
            Assert.That(result.SectionKey, Is.EqualTo("player"));
        }

        [Test]
        public void OlderVersion_PassesThroughMigration()
        {
            var v1 = new PlayerSection(version: 1) { Current = SamplePlayer() };
            _manager.RegisterSection(v1);
            Assert.That(_manager.Save("slot").IsSuccess);

            var newer = new SaveManager(_storage);
            var v2 = new PlayerSection(version: 2);
            newer.RegisterSection(v2);
            Assert.That(newer.Load("slot").IsSuccess);

            Assert.That(v2.MigrateCalls, Is.EqualTo(1));
            Assert.That(v2.RestoredFromVersion, Is.EqualTo(1));
            Assert.That(v2.Current.Level, Is.EqualTo(70));
        }

        [Test]
        public void SameVersion_SkipsMigration()
        {
            var player = new PlayerSection(version: 2) { Current = SamplePlayer() };
            _manager.RegisterSection(player);
            Assert.That(_manager.Save("slot").IsSuccess);

            Assert.That(_manager.Load("slot").IsSuccess);

            Assert.That(player.MigrateCalls, Is.EqualTo(0));
            Assert.That(player.Current.Level, Is.EqualTo(7));
        }

        [Test]
        public void NewerVersion_IsRefused_AndRestoresNothing()
        {
            var v3 = new PlayerSection(version: 3) { Current = SamplePlayer() };
            StageData stage = new StageData { Stage = 4 };
            _manager.RegisterSection(v3);
            _manager.RegisterSection(SaveSection.Create("stage", () => stage, d => stage = d));
            Assert.That(_manager.Save("slot").IsSuccess);

            var older = new SaveManager(_storage);
            var v1 = new PlayerSection(version: 1);
            StageData loaded = default;
            older.RegisterSection(SaveSection.Create("stage", () => loaded, d => loaded = d));
            older.RegisterSection(v1);
            SaveResult result = older.Load("slot");

            Assert.That(result.Reason, Is.EqualTo(SaveFailureReason.VersionTooNew));
            Assert.That(result.SectionKey, Is.EqualTo("player"));
            Assert.That(loaded.Stage, Is.EqualTo(0), "A refused load restores no section, the readable ones included.");
            Assert.That(v1.Current.Name, Is.Null);
        }

        [Test]
        public void DamagedSlot_FailsWithCorrupted()
        {
            var player = new PlayerSection { Current = SamplePlayer() };
            _manager.RegisterSection(player);
            Assert.That(_manager.Save("slot").IsSuccess);
            Assert.That(_manager.ExportSlot("slot", out byte[] bytes).IsSuccess);

            bytes[bytes.Length / 2] ^= 0xFF;
            _storage.Write("slot", bytes);

            Assert.That(_manager.Load("slot").Reason, Is.EqualTo(SaveFailureReason.Corrupted));
            Assert.That(_manager.TryGetSlotInfo("slot", out _), Is.False);
            Assert.That(_manager.ImportSlot("copy", bytes).Reason, Is.EqualTo(SaveFailureReason.Corrupted));
        }

        [Test]
        public void SaveOneSection_IntoDamagedSlot_Fails_WhileWholeSaveWritesItAnew()
        {
            var player = new PlayerSection { Current = SamplePlayer() };
            _manager.RegisterSection(player);
            _storage.Write("slot", new byte[] { 1, 2, 3 });

            Assert.That(_manager.Save("slot", "player").Reason, Is.EqualTo(SaveFailureReason.Corrupted));
            Assert.That(_manager.Save("slot").IsSuccess);
            Assert.That(_manager.Load("slot").IsSuccess);
        }

        [Test]
        public void MissingSlot_FailsWithSlotNotFound()
        {
            _manager.RegisterSection(new PlayerSection());

            Assert.That(_manager.Load("nothing").Reason, Is.EqualTo(SaveFailureReason.SlotNotFound));
            Assert.That(_manager.HasSlot("nothing"), Is.False);
        }

        [Test]
        public void SectionThatThrows_ReportsSectionFailed_WithItsKey()
        {
            var player = new PlayerSection { Current = SamplePlayer() };
            _manager.RegisterSection(player);
            Assert.That(_manager.Save("slot").IsSuccess);
            player.ThrowOnRestore = true;

            SaveResult result = _manager.Load("slot");

            Assert.That(result.Reason, Is.EqualTo(SaveFailureReason.SectionFailed));
            Assert.That(result.SectionKey, Is.EqualTo("player"));
            Assert.That(result.Message, Does.Contain("restore failed"));
        }

        [Test]
        public void SectionThatCapturesNull_WritesNothing()
        {
            _manager.RegisterSection(new PlayerSection { Current = SamplePlayer() });
            Assert.That(_manager.Save("slot").IsSuccess);
            _manager.RegisterSection(SaveSection.Create<PlayerData>("broken", () => null, _ => { }));

            SaveResult result = _manager.Save("slot");

            Assert.That(result.Reason, Is.EqualTo(SaveFailureReason.SerializationFailed));
            Assert.That(result.SectionKey, Is.EqualTo("broken"));
            Assert.That(_manager.TryGetSlotInfo("slot", out SaveSlotInfo info));
            Assert.That(info.HasSection("broken"), Is.False);
        }

        [Test]
        public void PrimitiveData_IsRefusedBySerializer()
        {
            _manager.RegisterSection(SaveSection.Create("count", () => 5, _ => { }));

            SaveResult result = _manager.Save("slot");

            Assert.That(result.Reason, Is.EqualTo(SaveFailureReason.SerializationFailed));
            Assert.That(_storage.Exists("slot"), Is.False);
        }

        [Test]
        public void Register_RefusesInvalidAndDuplicateKeys()
        {
            Assert.That(_manager.RegisterSection(new PlayerSection()).IsSuccess);
            Assert.That(_manager.RegisterSection(new PlayerSection()).Reason, Is.EqualTo(SaveFailureReason.SectionAlreadyRegistered));
            Assert.That(_manager.RegisterSection(SaveSection.Create("bad key", () => new StageData(), _ => { })).Reason,
                Is.EqualTo(SaveFailureReason.InvalidKey));
            Assert.That(_manager.RegisterSection(SaveSection.Create(new string('a', SaveLimits.MaxKeyLength + 1), () => new StageData(), _ => { })).Reason,
                Is.EqualTo(SaveFailureReason.InvalidKey));

            Assert.That(_manager.UnregisterSection("player"));
            Assert.That(_manager.IsSectionRegistered("player"), Is.False);
            Assert.That(_manager.RegisterSection(new PlayerSection()).IsSuccess);
        }

        [Test]
        public void InvalidSlotIds_AreRefused()
        {
            _manager.RegisterSection(new PlayerSection { Current = SamplePlayer() });

            Assert.That(_manager.Save("../escape").Reason, Is.EqualTo(SaveFailureReason.InvalidKey));
            Assert.That(_manager.Save("a.b").Reason, Is.EqualTo(SaveFailureReason.InvalidKey));
            Assert.That(_manager.Save(string.Empty).Reason, Is.EqualTo(SaveFailureReason.InvalidKey));
            Assert.That(_manager.Load(null).Reason, Is.EqualTo(SaveFailureReason.InvalidKey));
        }

        [Test]
        public void SlotInfo_ListsSectionsWithVersionsAndTimes()
        {
            DateTime before = DateTime.UtcNow.AddSeconds(-1);
            var player = new PlayerSection(version: 2) { Current = SamplePlayer() };
            _manager.RegisterSection(player);
            _manager.RegisterSection(SaveSection.Create("stage", () => new StageData { Stage = 1 }, _ => { }));
            Assert.That(_manager.Save("b").IsSuccess);
            Assert.That(_manager.Save("a", "stage").IsSuccess);

            var slots = new List<SaveSlotInfo>();
            _manager.GetSlots(slots);

            Assert.That(slots.Count, Is.EqualTo(2));
            Assert.That(_manager.TryGetSlotInfo("b", out SaveSlotInfo info));
            Assert.That(info.Sections.Count, Is.EqualTo(2));
            Assert.That(info.TryGetSection("player", out SaveSectionInfo section));
            Assert.That(section.Version, Is.EqualTo(2));
            Assert.That(section.Size, Is.GreaterThan(0));
            Assert.That(info.CreatedUtc, Is.GreaterThanOrEqualTo(before));
            Assert.That(info.UpdatedUtc, Is.GreaterThanOrEqualTo(info.CreatedUtc));
            Assert.That(info.Size, Is.GreaterThan(section.Size));
        }

        [Test]
        public void DeleteSection_KeepsTheOthers_AndDeleteSlotRemovesIt()
        {
            RegisterBoth(new StageData { Stage = 2 });
            Assert.That(_manager.Save("slot").IsSuccess);
            var deleted = new List<string>();
            _manager.OnSlotDeleted += deleted.Add;

            Assert.That(_manager.DeleteSection("slot", "stage").IsSuccess);
            Assert.That(_manager.TryGetSlotInfo("slot", out SaveSlotInfo info));
            Assert.That(info.HasSection("stage"), Is.False);
            Assert.That(info.HasSection("player"));

            Assert.That(_manager.DeleteSlot("slot").IsSuccess);
            Assert.That(_manager.HasSlot("slot"), Is.False);
            Assert.That(deleted, Is.EqualTo(new[] { "slot" }));
        }

        [Test]
        public void ExportAndImport_CopiesTheSlotUnderAnotherId()
        {
            var player = new PlayerSection { Current = SamplePlayer() };
            _manager.RegisterSection(player);
            Assert.That(_manager.Save("source").IsSuccess);
            Assert.That(_manager.ExportSlot("source", out byte[] bytes).IsSuccess);

            var target = new SaveManager(new MemorySaveStorage());
            var copy = new PlayerSection();
            target.RegisterSection(copy);
            Assert.That(target.ImportSlot("shared", bytes).IsSuccess);
            Assert.That(target.Load("shared").IsSuccess);

            Assert.That(copy.Current.Position, Is.EqualTo(player.Current.Position));
        }

        [Test]
        public void SlotOverTheSizeLimit_IsRefused()
        {
            var player = new PlayerSection { Current = SamplePlayer() };
            player.Current.Items = new List<int>(new int[2000]);
            _manager.RegisterSection(player);
            _manager.MaxSlotSize = 1024;

            Assert.That(_manager.Save("slot").Reason, Is.EqualTo(SaveFailureReason.TooLarge));
            Assert.That(_storage.Exists("slot"), Is.False);
            Assert.That(_manager.ImportSlot("slot", new byte[2048]).Reason, Is.EqualTo(SaveFailureReason.TooLarge));
        }

        [Test]
        public void Events_NameTheSlotAndSection()
        {
            RegisterBoth(new StageData());
            var saved = new List<(string, string)>();
            var loaded = new List<(string, string)>();
            _manager.OnSaved += (slot, key) => saved.Add((slot, key));
            _manager.OnLoaded += (slot, key) => loaded.Add((slot, key));

            _manager.Save("slot");
            _manager.Save("slot", "stage");
            _manager.Load("slot");
            _manager.Load("slot", "player");

            Assert.That(saved, Is.EqualTo(new[] { ("slot", (string)null), ("slot", "stage") }));
            Assert.That(loaded, Is.EqualTo(new[] { ("slot", (string)null), ("slot", "player") }));
        }

        [Test]
        public void FileStorage_WritesReadsListsAndDeletes()
        {
            _directory = Path.Combine(Path.GetTempPath(), "Mu3SaveTests", Guid.NewGuid().ToString("N"));
            var storage = new FileSaveStorage(_directory);
            var manager = new SaveManager(storage);
            var player = new PlayerSection { Current = SamplePlayer() };
            manager.RegisterSection(player);

            Assert.That(manager.Save("slot1").IsSuccess);
            Assert.That(manager.Save("slot2").IsSuccess);
            player.Current.Level = 42;
            Assert.That(manager.Save("slot1").IsSuccess, "A second write replaces the existing file.");

            var ids = new List<string>();
            storage.GetSlotIds(ids);
            Assert.That(ids, Is.EquivalentTo(new[] { "slot1", "slot2" }));
            Assert.That(File.Exists(storage.GetPath("slot1")));

            player.Current = new PlayerData();
            Assert.That(manager.Load("slot1").IsSuccess);
            Assert.That(player.Current.Level, Is.EqualTo(42));

            Assert.That(manager.DeleteSlot("slot1").IsSuccess);
            Assert.That(File.Exists(storage.GetPath("slot1")), Is.False);
            Assert.That(storage.Read("slot1", out _).Reason, Is.EqualTo(SaveFailureReason.SlotNotFound));
        }

        [Test]
        public void FileStorage_WriteThatStoppedPartway_LeavesThePreviousSaveReadable()
        {
            _directory = Path.Combine(Path.GetTempPath(), "Mu3SaveTests", Guid.NewGuid().ToString("N"));
            var storage = new FileSaveStorage(_directory);
            var manager = new SaveManager(storage);
            var player = new PlayerSection { Current = SamplePlayer() };
            manager.RegisterSection(player);
            Assert.That(manager.Save("slot").IsSuccess);

            // A write that stopped before it replaced the slot leaves only its temporary file.
            File.WriteAllBytes(storage.GetPath("slot") + ".tmp", new byte[] { 9, 9, 9 });

            var ids = new List<string>();
            storage.GetSlotIds(ids);
            Assert.That(ids, Is.EqualTo(new[] { "slot" }));
            player.Current = new PlayerData();
            Assert.That(manager.Load("slot").IsSuccess);
            Assert.That(player.Current.Name, Is.EqualTo("Mu"));

            player.Current.Level = 3;
            Assert.That(manager.Save("slot").IsSuccess, "The next write replaces the leftover temporary file.");
            Assert.That(File.Exists(storage.GetPath("slot") + ".tmp"), Is.False);
        }
    }
}
