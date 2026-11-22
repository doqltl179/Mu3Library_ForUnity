using System;
using System.Collections.Generic;
using System.Linq;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using Mu3Library.Save;
using Mu3Library.SessionSave;
using NUnit.Framework;

namespace Mu3Library.Tests
{
    /// <summary>
    /// <see cref="SessionSaveManager"/>, played out by players on one <see cref="LocalNetwork"/> in
    /// this process, each with a save manager over memory.
    /// </summary>
    public class SessionSaveManagerTests
    {
        private const string GameId = "tests.sessionsave";
        private const ushort ShareMessage = 900;
        private const string Inventory = "inventory";

        [Serializable]
        public class InventoryData
        {
            public int Coins;
            public List<string> Items = new();
        }

        [Serializable]
        public class WorldData
        {
            public int Day;
        }

        private sealed class Player
        {
            public LocalBackend Backend;
            public MultiplayerManager Multiplayer;
            public SaveManager Saves;
            public SessionSaveManager Session;

            // What this player's game holds while it hosts.
            public WorldData World = new();
            public readonly Dictionary<PlayerId, InventoryData> Inventories = new();
            public readonly List<PlayerId> Defaults = new();

            public Action<SaveShareOffer> Answer;
            public readonly List<SaveShareOffer> Offers = new();
            public readonly List<SaveShareTransfer> Progress = new();
            public readonly List<SaveShareTransfer> Completed = new();
            public readonly List<(SaveShareTransfer Transfer, SaveShareFailureReason Reason)> Failed = new();

            public PlayerId Id => Backend.LocalPlayerId;
        }

        /// <summary>Hands out the slot's bytes with one byte changed, as a damaged transfer would carry them.</summary>
        private sealed class DamagingSaveManager : ISaveManager
        {
            private readonly ISaveManager _inner;

            public DamagingSaveManager(ISaveManager inner) => _inner = inner;

            public ISaveStorage Storage => _inner.Storage;
            public int MaxSlotSize { get => _inner.MaxSlotSize; set => _inner.MaxSlotSize = value; }
            public SaveResult RegisterSection(ISaveSection section) => _inner.RegisterSection(section);
            public bool UnregisterSection(string key) => _inner.UnregisterSection(key);
            public bool IsSectionRegistered(string key) => _inner.IsSectionRegistered(key);
            public SaveResult Save(string slotId) => _inner.Save(slotId);
            public SaveResult Save(string slotId, string sectionKey) => _inner.Save(slotId, sectionKey);
            public SaveResult Load(string slotId) => _inner.Load(slotId);
            public SaveResult Load(string slotId, string sectionKey) => _inner.Load(slotId, sectionKey);
            public SaveResult Peek<TData>(string slotId, string sectionKey, out TData data, out int savedVersion) =>
                _inner.Peek(slotId, sectionKey, out data, out savedVersion);
            public SaveResult SetMetadata(string slotId, string key, string value) => _inner.SetMetadata(slotId, key, value);
            public bool HasSlot(string slotId) => _inner.HasSlot(slotId);
            public bool TryGetSlotInfo(string slotId, out SaveSlotInfo info) => _inner.TryGetSlotInfo(slotId, out info);
            public void GetSlots(List<SaveSlotInfo> results) => _inner.GetSlots(results);
            public SaveResult DeleteSlot(string slotId) => _inner.DeleteSlot(slotId);
            public SaveResult DeleteSection(string slotId, string sectionKey) => _inner.DeleteSection(slotId, sectionKey);
            public SaveResult ImportSlot(string slotId, byte[] data) => _inner.ImportSlot(slotId, data);

            public SaveResult ExportSlot(string slotId, out byte[] data)
            {
                SaveResult result = _inner.ExportSlot(slotId, out data);
                if (result.IsSuccess)
                {
                    data = (byte[])data.Clone();
                    data[data.Length / 2] ^= 0x5A;
                }

                return result;
            }
        }

        private LocalNetwork _network;
        private double _now;
        private readonly List<Player> _players = new();



        [SetUp]
        public void SetUp()
        {
            _network = new LocalNetwork();
            _now = 0d;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Player player in _players)
            {
                player.Session.Dispose();
                player.Multiplayer.Dispose();
                player.Backend.Dispose();
            }

            _players.Clear();
        }

        #region Save And Load
        [Test]
        public void Save_KeepsEachPlayersSectionUnderTheirId_AndLoadGivesItBackToThem()
        {
            Player host = CreatePlayer("Host", 1);
            Player guest = CreatePlayer("Guest", 2);
            Host(host);
            Join(guest, host);

            host.World.Day = 3;
            host.Inventories[host.Id] = new InventoryData { Coins = 5 };
            host.Inventories[guest.Id] = new InventoryData { Coins = 7 };
            Assert.That(host.Session.Save("coop", "Forest").IsSuccess, Is.True);

            Assert.That(host.Session.TryGetSessionInfo("coop", out SessionSaveInfo info), Is.True);
            Assert.That(info.HostId, Is.EqualTo(host.Id));
            Assert.That(info.HostName, Is.EqualTo("Host"));
            Assert.That(info.SessionName, Is.EqualTo("Forest"));
            Assert.That(info.Players, Is.EquivalentTo(new[] { host.Id, guest.Id }));
            Assert.That(info.Slot.HasSection("player.1.inventory") && info.Slot.HasSection("player.2.inventory"), Is.True);

            host.World = new WorldData();
            host.Inventories.Clear();
            Assert.That(host.Session.Load("coop").IsSuccess, Is.True);

            Assert.That(host.World.Day, Is.EqualTo(3));
            Assert.That(host.Inventories[host.Id].Coins, Is.EqualTo(5));
            Assert.That(host.Inventories[guest.Id].Coins, Is.EqualTo(7));
            Assert.That(host.Defaults, Is.Empty);
        }

        [Test]
        public void Load_StartsANewPlayerFromDefaults_AndKeepsThePlayerWhoDidNotCome()
        {
            Player host = CreatePlayer("Host", 1);
            Player first = CreatePlayer("First", 2);
            Player second = CreatePlayer("Second", 3);
            Host(host);
            Join(first, host);
            host.Inventories[host.Id] = new InventoryData { Coins = 5 };
            host.Inventories[first.Id] = new InventoryData { Coins = 7 };
            Assert.That(host.Session.Save("coop").IsSuccess, Is.True);

            first.Multiplayer.Leave();
            Pump();
            Join(second, host);
            host.Inventories.Clear();
            Assert.That(host.Session.Load("coop").IsSuccess, Is.True);

            Assert.That(host.Inventories[host.Id].Coins, Is.EqualTo(5));
            Assert.That(host.Defaults, Is.EqualTo(new[] { second.Id }));
            Assert.That(host.Inventories[second.Id].Coins, Is.EqualTo(10), "The default the game gives.");
            Assert.That(host.Inventories.ContainsKey(first.Id), Is.False);

            Assert.That(host.Session.Save("coop").IsSuccess, Is.True);
            Assert.That(host.Session.TryGetSessionInfo("coop", out SessionSaveInfo info), Is.True);
            Assert.That(info.Players, Is.EquivalentTo(new[] { host.Id, first.Id, second.Id }));
            Assert.That(host.Saves.Peek("coop", SessionSaveKeys.PlayerSectionKey(first.Id, Inventory), out InventoryData kept, out _).IsSuccess, Is.True);
            Assert.That(kept.Coins, Is.EqualTo(7));
        }

        [Test]
        public void LoadPlayer_GivesAPlayerWhoJoinsLaterTheirSections_OrTheDefaults()
        {
            Player host = CreatePlayer("Host", 1);
            Player returning = CreatePlayer("Returning", 2);
            Player newcomer = CreatePlayer("Newcomer", 3);
            Host(host);
            Join(returning, host);
            host.Inventories[returning.Id] = new InventoryData { Coins = 7 };
            Assert.That(host.Session.Save("coop").IsSuccess, Is.True);
            returning.Multiplayer.Leave();
            Pump();
            host.Inventories.Clear();

            Join(returning, host);
            Join(newcomer, host);
            Assert.That(host.Session.LoadPlayer("coop", returning.Id).IsSuccess, Is.True);
            Assert.That(host.Session.LoadPlayer("coop", newcomer.Id).IsSuccess, Is.True);

            Assert.That(host.Inventories[returning.Id].Coins, Is.EqualTo(7));
            Assert.That(host.Defaults, Is.EqualTo(new[] { newcomer.Id }));
            Assert.That(host.Inventories.ContainsKey(host.Id), Is.False, "Only the named player loads.");
            Assert.That(host.Session.LoadPlayer("coop", new PlayerId(99)).Reason, Is.EqualTo(SessionSaveFailureReason.PlayerNotInSession));
        }

        [Test]
        public void SaveLoadAndShare_AreForTheHostOnly()
        {
            Player host = CreatePlayer("Host", 1);
            Player guest = CreatePlayer("Guest", 2);
            Player alone = CreatePlayer("Alone", 3);
            Host(host);
            Join(guest, host);

            Assert.That(guest.Session.Save("coop").Reason, Is.EqualTo(SessionSaveFailureReason.NotHost));
            Assert.That(guest.Session.Load("coop").Reason, Is.EqualTo(SessionSaveFailureReason.NotHost));
            Assert.That(guest.Session.LoadPlayer("coop", guest.Id).Reason, Is.EqualTo(SessionSaveFailureReason.NotHost));
            Assert.That(guest.Session.Share("coop", host.Id, out _).Reason, Is.EqualTo(SessionSaveFailureReason.NotHost));
            Assert.That(alone.Session.Save("coop").Reason, Is.EqualTo(SessionSaveFailureReason.NotHost));
            Assert.That(guest.Saves.HasSlot("coop") || alone.Saves.HasSlot("coop"), Is.False);
        }
        #endregion

        #region Share
        [Test]
        public void Share_SendsTheSaveInPacedChunks_AndTheReceiverStoresItWithItsOrigin()
        {
            Player host = CreatePlayer("Host", 1);
            Player guest = CreatePlayer("Guest", 2);
            Host(host);
            Join(guest, host);
            host.Inventories[host.Id] = new InventoryData { Coins = 5, Items = Enumerable.Range(0, 1000).Select(i => "item-" + i).ToList() };
            Assert.That(host.Session.Save("coop", "Forest").IsSuccess, Is.True);
            Assert.That(host.Saves.ExportSlot("coop", out byte[] original).IsSuccess, Is.True);
            Assert.That(original.Length, Is.GreaterThan(8 * 1024));

            host.Session.ShareChunkSize = 1024;
            host.Session.ShareBytesPerSecond = 4096;
            guest.Answer = offer => Assert.That(guest.Session.AcceptShare(offer.ShareId, "from-host").IsSuccess, Is.True);
            Assert.That(host.Session.Share("coop", guest.Id, out uint shareId).IsSuccess, Is.True);

            Pump(4, 0.25);
            Assert.That(guest.Offers.Count, Is.EqualTo(1));
            SaveShareOffer received = guest.Offers[0];
            Assert.That(received.ShareId, Is.EqualTo(shareId));
            Assert.That(received.From, Is.EqualTo(host.Id));
            Assert.That(received.FromName, Is.EqualTo("Host"));
            Assert.That(received.Size, Is.EqualTo(original.Length));
            Assert.That(received.SourceSlotId, Is.EqualTo("coop"));
            Assert.That(received.SessionName, Is.EqualTo("Forest"));
            int afterOneSecond = guest.Progress.Count > 0 ? guest.Progress[guest.Progress.Count - 1].BytesTransferred : 0;
            Assert.That(afterOneSecond, Is.GreaterThan(0).And.LessThanOrEqualTo(4096 + 2 * 1024), "The host keeps to its pace.");

            PumpUntil(() => guest.Completed.Count > 0 && host.Completed.Count > 0, 0.25);

            Assert.That(guest.Progress.Count, Is.GreaterThanOrEqualTo(original.Length / 1024));
            int previous = 0;
            foreach (SaveShareTransfer step in guest.Progress)
            {
                Assert.That(step.BytesTransferred - previous, Is.InRange(1, 1024));
                previous = step.BytesTransferred;
            }

            Assert.That(guest.Completed[0].SlotId, Is.EqualTo("from-host"));
            Assert.That(host.Completed[0].ShareId, Is.EqualTo(shareId));
            Assert.That(host.Completed[0].BytesTransferred, Is.EqualTo(original.Length));
            Assert.That(guest.Failed, Is.Empty);
            Assert.That(host.Failed, Is.Empty);
            Assert.That(guest.Saves.ExportSlot("from-host", out byte[] stored).IsSuccess, Is.True);
            Assert.That(stored, Is.EqualTo(original));
            Assert.That(guest.Session.TryGetSessionInfo("from-host", out SessionSaveInfo info), Is.True);
            Assert.That(info.HostId, Is.EqualTo(host.Id), "The save names the host it came from.");
            Assert.That(info.SessionName, Is.EqualTo("Forest"));
        }

        [Test]
        public void Share_LargerThanTheReceiverTakes_IsRefusedAsTooLarge()
        {
            Player host = CreatePlayer("Host", 1);
            Player guest = CreatePlayer("Guest", 2);
            Host(host);
            Join(guest, host);
            Assert.That(host.Session.Save("coop").IsSuccess, Is.True);
            guest.Saves.MaxSlotSize = 16;
            guest.Answer = offer => guest.Session.AcceptShare(offer.ShareId, "from-host");

            Assert.That(host.Session.Share("coop", guest.Id, out uint shareId).IsSuccess, Is.True);
            Pump();

            Assert.That(host.Failed.Select(failure => (failure.Transfer.ShareId, failure.Reason)),
                Is.EqualTo(new[] { (shareId, SaveShareFailureReason.TooLarge) }));
            Assert.That(guest.Offers, Is.Empty, "The receiver refuses it before its game hears of it.");
            Assert.That(guest.Saves.HasSlot("from-host"), Is.False);
        }

        [Test]
        public void Share_OfDamagedBytes_IsRefused_AndStoresNothing()
        {
            Player host = CreatePlayer("Host", 1, saves => new DamagingSaveManager(saves));
            Player guest = CreatePlayer("Guest", 2);
            Host(host);
            Join(guest, host);
            Assert.That(host.Session.Save("coop").IsSuccess, Is.True);
            guest.Answer = offer => guest.Session.AcceptShare(offer.ShareId, "from-host");

            Assert.That(host.Session.Share("coop", guest.Id, out _).IsSuccess, Is.True);
            PumpUntil(() => guest.Failed.Count > 0 && host.Failed.Count > 0);

            Assert.That(guest.Failed[0].Reason, Is.EqualTo(SaveShareFailureReason.Corrupted));
            Assert.That(host.Failed[0].Reason, Is.EqualTo(SaveShareFailureReason.Corrupted));
            Assert.That(guest.Completed, Is.Empty);
            Assert.That(guest.Saves.HasSlot("from-host"), Is.False);
        }

        [Test]
        public void Share_TheReceiverDeclines_EndsOnBothSides()
        {
            Player host = CreatePlayer("Host", 1);
            Player guest = CreatePlayer("Guest", 2);
            Host(host);
            Join(guest, host);
            Assert.That(host.Session.Save("coop").IsSuccess, Is.True);
            guest.Answer = offer => Assert.That(guest.Session.DeclineShare(offer.ShareId).IsSuccess, Is.True);

            Assert.That(host.Session.Share("coop", guest.Id, out _).IsSuccess, Is.True);
            Pump();

            Assert.That(guest.Failed.Select(failure => failure.Reason), Is.EqualTo(new[] { SaveShareFailureReason.Declined }));
            Assert.That(host.Failed.Select(failure => failure.Reason), Is.EqualTo(new[] { SaveShareFailureReason.Declined }));
            Assert.That(guest.Saves.HasSlot("from-host"), Is.False);
        }

        [Test]
        public void ReceivedSave_LetsTheReceiverHostTheSession_WithEachPlayersSections()
        {
            Player host = CreatePlayer("Host", 1);
            Player guest = CreatePlayer("Guest", 2);
            Host(host);
            Join(guest, host);
            host.World.Day = 4;
            host.Inventories[host.Id] = new InventoryData { Coins = 5 };
            host.Inventories[guest.Id] = new InventoryData { Coins = 7 };
            Assert.That(host.Session.Save("coop", "Forest").IsSuccess, Is.True);
            guest.Answer = offer => guest.Session.AcceptShare(offer.ShareId, "from-host");
            Assert.That(host.Session.Share("coop", guest.Id, out _).IsSuccess, Is.True);
            PumpUntil(() => guest.Completed.Count > 0);

            guest.Multiplayer.Leave();
            host.Multiplayer.Leave();
            Pump();
            Host(guest);
            Join(host, guest);
            Assert.That(guest.Session.Load("from-host").IsSuccess, Is.True);

            Assert.That(guest.World.Day, Is.EqualTo(4));
            Assert.That(guest.Inventories[host.Id].Coins, Is.EqualTo(5), "The first host's data follows them.");
            Assert.That(guest.Inventories[guest.Id].Coins, Is.EqualTo(7));
            Assert.That(guest.Defaults, Is.Empty);

            Assert.That(guest.Session.Save("from-host").IsSuccess, Is.True);
            Assert.That(guest.Session.TryGetSessionInfo("from-host", out SessionSaveInfo info), Is.True);
            Assert.That(info.HostId, Is.EqualTo(guest.Id));
            Assert.That(info.SessionName, Is.EqualTo("Forest"), "A save without a name keeps the one the slot holds.");
        }
        #endregion

        #region Helpers
        private Player CreatePlayer(string name, ulong id, Func<ISaveManager, ISaveManager> wrap = null)
        {
            var backend = new LocalBackend(_network, name, new PlayerId(id));
            var multiplayer = new MultiplayerManager();
            Assert.That(multiplayer.Initialize(backend, new MultiplayerConfig(GameId, 1) { Clock = () => _now }).IsSuccess, Is.True);

            var saves = new SaveManager(new MemorySaveStorage());
            var player = new Player { Backend = backend, Multiplayer = multiplayer, Saves = saves };
            Assert.That(saves.RegisterSection(SaveSection.Create("world", () => player.World, data => player.World = data)).IsSuccess, Is.True);

            player.Session = new SessionSaveManager(multiplayer, wrap != null ? wrap(saves) : saves, ShareMessage) { Clock = () => _now };
            Assert.That(player.Session.RegisterPlayerSection(PlayerSaveSection.Create<InventoryData>(
                Inventory,
                owner => player.Inventories.TryGetValue(owner, out InventoryData data) ? data : new InventoryData(),
                (owner, data) => player.Inventories[owner] = data,
                restoreDefault: owner =>
                {
                    player.Defaults.Add(owner);
                    player.Inventories[owner] = new InventoryData { Coins = 10 };
                })).IsSuccess, Is.True);

            player.Session.OnShareOffered += offer =>
            {
                player.Offers.Add(offer);
                player.Answer?.Invoke(offer);
            };
            player.Session.OnShareProgress += transfer => player.Progress.Add(transfer);
            player.Session.OnShareCompleted += transfer => player.Completed.Add(transfer);
            player.Session.OnShareFailed += (transfer, reason) => player.Failed.Add((transfer, reason));

            _players.Add(player);
            return player;
        }

        private void Host(Player player)
        {
            MultiplayerResult? result = null;
            var options = new LobbyCreateOptions { Visibility = LobbyVisibility.Public, Name = "Room", Rules = SessionRules.Default };
            player.Multiplayer.CreateLobby(options, answer => result = answer);
            Pump();
            Assert.That(result.HasValue && result.Value.IsSuccess, Is.True, "CreateLobby did not succeed.");
        }

        private void Join(Player player, Player host)
        {
            MultiplayerResult? result = null;
            player.Multiplayer.JoinLobby(host.Multiplayer.CurrentLobbyId, answer => result = answer);
            Pump();
            Assert.That(result.HasValue && result.Value.IsSuccess, Is.True, "JoinLobby did not succeed.");
        }

        private void Pump(int frames = 8, double seconds = 0.02)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                _now += seconds;
                foreach (Player player in _players)
                {
                    player.Multiplayer.Update();
                    player.Session.Update();
                }
            }
        }

        private void PumpUntil(Func<bool> done, double seconds = 0.1, int maxFrames = 2000)
        {
            for (int frame = 0; frame < maxFrames && !done(); frame++)
            {
                Pump(1, seconds);
            }

            Assert.That(done(), Is.True, "The share did not end.");
        }
        #endregion
    }
}
