using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using Mu3Library.Multiplayer.Protocol;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Lobby data and participant data during a session (FR-9, FR-11, NFR-5).
    /// </summary>
    public partial class MultiplayerSessionTests
    {
        #region Lobby Data
        [Test]
        public void LobbyData_FromTheHost_ReachesEveryPlayerAndLobbySearch()
        {
            // FR-11
            LobbyCreateOptions options = PublicOptions(new SessionRules(4));
            options.Data["map"] = "forest";
            Node host = CreateHost(CreateNode("Host"), options);
            Node a = JoinNew("A", host);
            Node b = JoinNew("B", host);
            Assert.That(a.Manager.LobbyData["map"], Is.EqualTo("forest"));

            Assert.That(host.Manager.SetLobbyData("map", "desert").IsSuccess, Is.True);
            Assert.That(host.Manager.SetLobbyData("mode", "coop").IsSuccess, Is.True);
            Assert.That(host.Manager.SetLobbyData("mode", "coop").IsSuccess, Is.True, "setting the same value again");
            Assert.That(host.LobbyDataChanges.Count, Is.EqualTo(2), "the host's own events arrive before the calls return");
            Pump();

            foreach (Node node in new[] { host, a, b })
            {
                Assert.That(node.LobbyDataChanges, Is.EqualTo(new[] { ("map", "desert"), ("mode", "coop") }), node.Backend.PlayerName);
                Assert.That(node.Manager.LobbyData, Is.EquivalentTo(new Dictionary<string, string> { ["map"] = "desert", ["mode"] = "coop" }));
            }

            LobbyInfo found = Search(CreateNode("Seeker")).Lobbies.Single();
            Assert.That(found.Data["map"], Is.EqualTo("desert"));
            Assert.That(found.Data["mode"], Is.EqualTo("coop"));
        }

        [Test]
        public void LobbyData_FromAClient_IsRefusedWithNotHost()
        {
            // FR-11
            Node host = CreateHost("Host");
            Node a = JoinNew("A", host);

            Assert.That(a.Manager.SetLobbyData("map", "desert").Reason, Is.EqualTo(MultiplayerReason.NotHost));
            Pump();

            Assert.That(host.LobbyDataChanges.Concat(a.LobbyDataChanges), Is.Empty);
            Assert.That(host.Manager.LobbyData.ContainsKey("map") || a.Manager.LobbyData.ContainsKey("map"), Is.False);
            Assert.That(host.Backend.GetLobbyData(host.Manager.CurrentLobbyId, "map"), Is.Null);
            Assert.That(CreateNode("Lonely").Manager.SetLobbyData("map", "desert").Reason, Is.EqualTo(MultiplayerReason.InvalidState));
        }

        [Test]
        public void LobbyData_RefusesReservedEmptyAndLongKeys_NullValues_AndTooMuchData()
        {
            // FR-11: the library's own keys stay out of the game's reach.
            Node host = CreateHost("Host");
            LobbyId lobby = host.Manager.CurrentLobbyId;

            // Steam does not tell keys apart by case, so the reserved prefix counts in any case.
            foreach (string key in new[] { LobbyDataKeys.State, "MU3.STATE", "Mu3.custom", string.Empty, null, new string('k', MultiplayerLimits.MaxDataKeyLength + 1) })
            {
                Assert.That(host.Manager.SetLobbyData(key, "spoofed").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument), key);
            }

            // Text that would not reach the other players unchanged.
            foreach (string text in UncleanTexts)
            {
                Assert.That(host.Manager.SetLobbyData(text, "value").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument), Escape(text));
                Assert.That(host.Manager.SetLobbyData("map", text).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument), Escape(text));
            }

            Assert.That(host.Manager.SetLobbyData("map", null).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(host.Backend.GetLobbyData(lobby, LobbyDataKeys.State), Is.EqualTo(LobbyDataKeys.StateLobby));

            // The limit counts UTF-8 bytes: three for each of these characters.
            Assert.That(host.Manager.SetLobbyData("k", new string('가', MultiplayerLimits.MaxLobbyDataSize / 3 + 1)).Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));

            string longest = new string('k', MultiplayerLimits.MaxDataKeyLength);
            Assert.That(host.Manager.SetLobbyData(longest, new string('v', MultiplayerLimits.MaxLobbyDataSize - longest.Length)).IsSuccess, Is.True);
            Assert.That(host.Manager.SetLobbyData("x", "1").Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));

            // A new value takes the place of the old one in the count.
            Assert.That(host.Manager.SetLobbyData(longest, "short").IsSuccess, Is.True);
            Assert.That(host.Manager.SetLobbyData("x", "1").IsSuccess, Is.True);
            Assert.That(host.Manager.SetLobbyData("emoji \U0001F3AE", "\U0001F3AE").IsSuccess, Is.True, "a surrogate pair is fine");
            Assert.That(host.LobbyDataChanges.Select(change => change.Key), Is.EqualTo(new[] { longest, longest, "x", "emoji \U0001F3AE" }));
        }

        [Test]
        public void CreateLobby_RefusesALongKey_AndTooMuchData()
        {
            // FR-11: the data a lobby starts with follows the same rules.
            Node host = CreateNode("Host");
            LobbyCreateOptions longKey = PublicOptions(SessionRules.Default);
            longKey.Data[new string('k', MultiplayerLimits.MaxDataKeyLength + 1)] = "value";
            LobbyCreateOptions large = PublicOptions(SessionRules.Default);
            large.Data["a"] = new string('v', MultiplayerLimits.MaxLobbyDataSize / 2);
            large.Data["b"] = new string('v', MultiplayerLimits.MaxLobbyDataSize / 2);
            LobbyCreateOptions reserved = PublicOptions(SessionRules.Default);
            reserved.Data["MU3.game"] = "spoofed";
            LobbyCreateOptions unclean = PublicOptions(SessionRules.Default);
            unclean.Data["map"] = "forest\0desert";

            Assert.That(CreateLobby(host, longKey).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(CreateLobby(host, reserved).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(CreateLobby(host, unclean).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(CreateLobby(host, large).Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));
            Assert.That(_network.Lobbies, Is.Empty);
        }

        [Test]
        public void LobbyData_AKeyThatDiffersOnlyByCase_IsRefused_SoEveryCopyKeepsTheSameKeys()
        {
            // FR-11, FR-38: Steam keeps one value for both keys, so every backend refuses the second one.
            LobbyCreateOptions options = PublicOptions(new SessionRules(4));
            options.Data["Map"] = "desert";
            Node host = CreateHost(CreateNode("Host"), options);
            Node a = JoinNew("A", host);
            LobbyId lobby = host.Manager.CurrentLobbyId;

            LogAssert.Expect(LogType.Warning, new Regex("'map' differs from the key 'Map' only by case"));
            Assert.That(host.Manager.SetLobbyData("map", "forest").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            LogAssert.Expect(LogType.Warning, new Regex("'MAP' differs from the key 'Map' only by case"));
            Assert.That(host.Manager.SetLobbyData("MAP", new string('v', MultiplayerLimits.MaxLobbyDataSize)).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument),
                "the key is refused before its size counts");
            Assert.That(host.Manager.SetLobbyData("Map", "forest").IsSuccess, Is.True, "the key as it is spelled");
            Pump();

            foreach (Node node in new[] { host, a })
            {
                string name = node.Backend.PlayerName;
                Assert.That(node.Manager.LobbyData, Is.EquivalentTo(new Dictionary<string, string> { ["Map"] = "forest" }), name);
                Assert.That(node.Manager.LobbyData["map"], Is.EqualTo("forest"), $"{name} reads a key in any case, as Steam does");
                Assert.That(node.LobbyDataChanges, Is.EqualTo(new[] { ("Map", "forest") }), name);
            }

            Assert.That(host.Backend.GetLobbyData(lobby, "map"), Is.EqualTo("forest"), "the lobby reads a key in any case, as a Steam lobby does");
            Assert.That(a.Backend.GetLobbyData(lobby, "MAP"), Is.EqualTo("forest"), "so does a member's");
            LobbyInfo found = Search(CreateNode("Seeker")).Lobbies.Single();
            Assert.That(found.Data, Is.EquivalentTo(new Dictionary<string, string> { ["Map"] = "forest" }));
            Assert.That(found.Data["MAP"], Is.EqualTo("forest"), "a search result reads a key in any case");
        }

        [Test]
        public void CreateLobby_KeysThatDifferOnlyByCase_AreRefused()
        {
            // FR-11: the data a lobby starts with follows the same rule.
            Node host = CreateNode("Host");
            LobbyCreateOptions options = PublicOptions(SessionRules.Default);
            options.Data["Map"] = "desert";
            options.Data["map"] = "forest";

            LogAssert.Expect(LogType.Warning, new Regex("differs from the key '(Map|map)' only by case"));
            Assert.That(CreateLobby(host, options).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(_network.Lobbies, Is.Empty);
        }

        [Test]
        public void LobbyData_DeletedByTheHost_LeavesEveryCopyAndLobbySearch_AndFreesItsRoom()
        {
            // FR-11: a long session that keeps changing its keys deletes the old ones.
            LobbyCreateOptions options = PublicOptions(new SessionRules(4));
            options.Data["map"] = "forest";
            options.Data["mode"] = "coop";
            Node host = CreateHost(CreateNode("Host"), options);
            Node a = JoinNew("A", host);
            Node b = JoinNew("B", host);
            LobbyId lobby = host.Manager.CurrentLobbyId;
            Node seeker = CreateNode("Seeker");
            Assert.That(Search(seeker, new LobbySearchQuery().AddStringFilter("map", "forest")).Lobbies.Count, Is.EqualTo(1));

            Assert.That(host.Manager.DeleteLobbyData("map").IsSuccess, Is.True);
            Assert.That(host.LobbyDataChanges, Is.EqualTo(new[] { ("map", (string)null) }), "the host's own event arrives before the call returns");
            Assert.That(host.Manager.DeleteLobbyData("map").IsSuccess, Is.True, "deleting a key that is gone changes nothing");
            Assert.That(host.Manager.DeleteLobbyData("never").IsSuccess, Is.True);
            Pump();

            foreach (Node node in new[] { host, a, b })
            {
                string name = node.Backend.PlayerName;
                Assert.That(node.LobbyDataChanges, Is.EqualTo(new[] { ("map", (string)null) }), name);
                Assert.That(node.Manager.LobbyData, Is.EquivalentTo(new Dictionary<string, string> { ["mode"] = "coop" }), name);
            }

            Assert.That(host.Backend.GetLobbyData(lobby, "map"), Is.Null);
            Assert.That(Search(seeker).Lobbies.Single().Data, Is.EquivalentTo(new Dictionary<string, string> { ["mode"] = "coop" }));
            Assert.That(Search(seeker, new LobbySearchQuery().AddStringFilter("map", "forest")).Lobbies, Is.Empty, "search no longer finds the lobby by the key");

            // The key and value it held leave the count.
            string fill = new string('v', MultiplayerLimits.MaxLobbyDataSize - "fill".Length - "mode".Length - "coop".Length);
            Assert.That(host.Manager.SetLobbyData("fill", fill).IsSuccess, Is.True);
            Assert.That(host.Manager.SetLobbyData("x", "1").Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));
            Assert.That(host.Manager.DeleteLobbyData("mode").IsSuccess, Is.True);
            Assert.That(host.Manager.SetLobbyData("x", "1").IsSuccess, Is.True);
            Pump();

            // A player who joins later never hears of the deleted keys.
            Node late = JoinNew("Late", host);
            Assert.That(late.Manager.LobbyData.Keys, Is.EquivalentTo(new[] { "fill", "x" }));
            Assert.That(a.Manager.LobbyData.Keys, Is.EquivalentTo(new[] { "fill", "x" }));
        }

        [Test]
        public void LobbyData_WrittenEmpty_IsDeleted_FromEveryCopyAndLobbySearch()
        {
            // FR-11, FR-38, #313 in this process: a Steam lobby keeps a key it holds when the key
            // is set empty but writes none it lacks, so an empty value deletes the key, and a lobby
            // created with one starts without it; neither takes room toward the limit. The test of
            // the machine-wide network and Steam's integration test check the same.
            LobbyCreateOptions options = PublicOptions(new SessionRules(4));
            options.Data["map"] = "desert";
            options.Data["e1"] = string.Empty;
            Node host = CreateHost(CreateNode("Host"), options);
            Node a = JoinNew("A", host);
            LobbyId lobby = host.Manager.CurrentLobbyId;
            Node seeker = CreateNode("Seeker");
            Assert.That(a.Manager.LobbyData, Is.EquivalentTo(new Dictionary<string, string> { ["map"] = "desert" }), "the lobby starts without the key written empty");

            Assert.That(host.Manager.SetLobbyData("mode", "coop").IsSuccess, Is.True);
            Assert.That(Search(seeker, new LobbySearchQuery().AddStringFilter("mode", "coop")).Lobbies.Count, Is.EqualTo(1));
            Assert.That(host.Manager.SetLobbyData("MODE", string.Empty).IsSuccess, Is.True, "the key is found in any case, as a deletion finds it");
            Assert.That(host.Manager.SetLobbyData("never", string.Empty).IsSuccess, Is.True, "a key the lobby data does not hold changes nothing");
            Assert.That(a.Manager.SetLobbyData("map", string.Empty).Reason, Is.EqualTo(MultiplayerReason.NotHost));
            Pump();

            foreach (Node node in new[] { host, a })
            {
                string name = node.Backend.PlayerName;
                Assert.That(node.Manager.LobbyData, Is.EquivalentTo(new Dictionary<string, string> { ["map"] = "desert" }), name);
                Assert.That(node.LobbyDataChanges, Is.EqualTo(new[] { ("mode", "coop"), ("mode", (string)null) }), name);
                Assert.That(node.Backend.GetLobbyData(lobby, "mode"), Is.Null, name);
                Assert.That(node.Backend.GetLobbyData(lobby, "e1"), Is.Null, name);
            }

            Assert.That(_network.FindLobby(lobby).Data.Keys.Where(key => !LobbyDataKeys.IsReserved(key)), Is.EquivalentTo(new[] { "map" }));
            Assert.That(Search(seeker).Lobbies.Single().Data, Is.EquivalentTo(new Dictionary<string, string> { ["map"] = "desert" }));
            Assert.That(Search(seeker, new LobbySearchQuery().AddStringFilter("mode", "coop")).Lobbies, Is.Empty);

            // The key written empty takes no room; a deleted one frees its own.
            string fill = new string('v', MultiplayerLimits.MaxLobbyDataSize - "map".Length - "desert".Length - "fill".Length);
            Assert.That(host.Manager.SetLobbyData("fill", fill).IsSuccess, Is.True);
            Assert.That(host.Manager.SetLobbyData("x", "1").Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));
            Assert.That(host.Manager.SetLobbyData("x", string.Empty).IsSuccess, Is.True);
            Assert.That(host.Manager.SetLobbyData("fill", string.Empty).IsSuccess, Is.True);
            Assert.That(host.Manager.SetLobbyData("x", "1").IsSuccess, Is.True);

            // So does a lobby's data at creation, whose keys still may not differ only by case.
            Node other = CreateNode("Other");
            LobbyCreateOptions clash = PublicOptions(SessionRules.Default);
            clash.Data["Map"] = "desert";
            clash.Data["map"] = string.Empty;
            LogAssert.Expect(LogType.Warning, new Regex("differs from the key '(Map|map)' only by case"));
            Assert.That(CreateLobby(other, clash).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            LobbyCreateOptions full = PublicOptions(SessionRules.Default);
            full.Data["fill"] = new string('v', MultiplayerLimits.MaxLobbyDataSize - "fill".Length);
            full.Data["empty"] = string.Empty;
            Assert.That(CreateLobby(other, full).IsSuccess, Is.True);
            Assert.That(other.Manager.LobbyData.Keys, Is.EquivalentTo(new[] { "fill" }));
        }

        [Test]
        public void Backend_WritingAnEmptyValue_KeepsOnlyAKeyTheLobbyHolds_AndSearchFiltersItAsSteamDoes()
        {
            // #343 in this process: the backend itself, which the manager never hands an empty
            // value (#313), takes one as a Steam lobby took it on AppID 480 on 2026-11-04.
            LocalBackend owner = CreateRawBackend("Owner");
            LocalBackend seeker = CreateRawBackend("Seeker");
            Dictionary<string, string> data = new Dictionary<string, string>
            {
                ["map"] = "desert",
                ["e1"] = string.Empty,
                ["n1"] = null,
                [EmptyValueKey] = "v",
            };
            LobbyId lobby = LobbyId.None;
            owner.CreateLobby(LobbyVisibility.Public, 4, data, (reason, id) => lobby = id);
            Pump();
            Assert.That(lobby.IsValid, Is.True);
            Assert.That(_network.FindLobby(lobby).Data.Keys, Is.EquivalentTo(new[] { "map", EmptyValueKey }), "an empty value at creation writes no key");

            // An empty value writes no key the lobby lacks, answering true, and leaves one it holds empty.
            Assert.That(owner.SetLobbyData(lobby, "never", string.Empty), Is.True);
            Assert.That(owner.SetLobbyData(lobby, "never", null), Is.True);
            Assert.That(owner.SetLobbyData(lobby, EmptyValueKey.ToUpperInvariant(), string.Empty), Is.True);
            Assert.That(_network.FindLobby(lobby).Data, Is.EquivalentTo(new Dictionary<string, string> { ["map"] = "desert", [EmptyValueKey] = string.Empty }));
            Assert.That(owner.GetLobbyData(lobby, EmptyValueKey), Is.Null, "the Steam backend reads an empty value as none");
            Assert.That(owner.DeleteLobbyData(lobby, "never"), Is.False, "Steam refuses to delete a key the lobby lacks");

            // Search returns the key with the empty value, and filters compare it as Steam's did.
            IReadOnlyList<BackendLobby> all = null;
            seeker.SearchLobbies(new LobbySearchQuery(), (reason, found) => all = found);
            Pump();
            Assert.That(all.Single().Data, Is.EquivalentTo(new Dictionary<string, string> { ["map"] = "desert", [EmptyValueKey] = string.Empty }));

            List<string> mismatches = new List<string>();
            foreach ((string description, Func<LobbySearchQuery, LobbySearchQuery> addTo, bool isKept) in EmptyValueFilterChecks())
            {
                IReadOnlyList<BackendLobby> found = null;
                seeker.SearchLobbies(addTo(new LobbySearchQuery()), (reason, lobbies) => found = lobbies);
                Pump();
                if ((found.Count == 1) != isKept)
                {
                    mismatches.Add($"{description} should {(isKept ? "keep" : "drop")} the lobby");
                }
            }

            Assert.That(mismatches, Is.Empty);

            Assert.That(owner.DeleteLobbyData(lobby, EmptyValueKey), Is.True);
            Assert.That(_network.FindLobby(lobby).Data.Keys, Is.EquivalentTo(new[] { "map" }));
        }

        [Test]
        public void LobbyData_DeletionFromAClient_IsRefusedWithNotHost_AndTheLibrarysKeysStay()
        {
            // FR-11: deleting follows the rules of setting.
            LobbyCreateOptions options = PublicOptions(new SessionRules(4));
            options.Data["Map"] = "desert";
            Node host = CreateHost(CreateNode("Host"), options);
            Node a = JoinNew("A", host);
            LobbyId lobby = host.Manager.CurrentLobbyId;

            Assert.That(a.Manager.DeleteLobbyData("Map").Reason, Is.EqualTo(MultiplayerReason.NotHost));
            foreach (string key in new[] { LobbyDataKeys.State, LobbyDataKeys.GameId, "MU3.HOST", string.Empty, null, new string('k', MultiplayerLimits.MaxDataKeyLength + 1) })
            {
                Assert.That(host.Manager.DeleteLobbyData(key).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument), key);
            }

            Assert.That(CreateNode("Lonely").Manager.DeleteLobbyData("Map").Reason, Is.EqualTo(MultiplayerReason.InvalidState));
            Pump();

            Assert.That(host.LobbyDataChanges.Concat(a.LobbyDataChanges), Is.Empty);
            Assert.That(a.Manager.LobbyData["Map"], Is.EqualTo("desert"));
            Assert.That(host.Backend.GetLobbyData(lobby, "Map"), Is.EqualTo("desert"));
            Assert.That(host.Backend.GetLobbyData(lobby, LobbyDataKeys.State), Is.EqualTo(LobbyDataKeys.StateLobby));
            Assert.That(host.Backend.GetLobbyData(lobby, LobbyDataKeys.GameId), Is.EqualTo(GameId));

            // The key is found in any case, as reading finds it, and every player hears it as it is
            // spelled; deleting first is how a key changes its case.
            Assert.That(host.Manager.DeleteLobbyData("MAP").IsSuccess, Is.True);
            Assert.That(host.Manager.SetLobbyData("map", "forest").IsSuccess, Is.True);
            Pump();
            foreach (Node node in new[] { host, a })
            {
                Assert.That(node.LobbyDataChanges, Is.EqualTo(new[] { ("Map", (string)null), ("map", "forest") }), node.Backend.PlayerName);
                Assert.That(node.Manager.LobbyData.Keys, Is.EquivalentTo(new[] { "map" }), node.Backend.PlayerName);
            }

            Assert.That(host.Backend.GetLobbyData(lobby, "map"), Is.EqualTo("forest"));
        }

        [Test]
        public void LobbySearch_FindsTheKeyOfAFilterInAnyCase_AsSteamDoes()
        {
            // FR-6, FR-38: the local backends match filter keys the way Steam does.
            LobbyCreateOptions options = PublicOptions(new SessionRules(4));
            options.Data["Map"] = "desert";
            options.Data["Level"] = "3";
            CreateHost(CreateNode("Host"), options);
            Node seeker = CreateNode("Seeker");

            Assert.That(Search(seeker, new LobbySearchQuery().AddStringFilter("MAP", "desert")).Lobbies.Count, Is.EqualTo(1));
            Assert.That(Search(seeker, new LobbySearchQuery().AddNumericFilter("level", 3)).Lobbies.Count, Is.EqualTo(1));
            Assert.That(Search(seeker, new LobbySearchQuery().AddStringFilter("map", "forest")).Lobbies, Is.Empty);
            Assert.That(Search(seeker, new LobbySearchQuery().AddStringFilter("map", "desert", LobbyComparison.NotEqual)).Lobbies, Is.Empty);
            Assert.That(Search(seeker, new LobbySearchQuery().AddNumericFilter("LEVEL", 3, LobbyComparison.GreaterThan)).Lobbies, Is.Empty);
        }

        [Test]
        public void LobbySearch_ComparesStringValues_AsSteamDoes()
        {
            // FR-6, FR-38, #239, #286: every comparison of a string filter, as Steam made it, on
            // the lobby's values and on a key the lobby lacks or holds empty.
            LobbyCreateOptions options = PublicOptions(new SessionRules(4));
            foreach (KeyValuePair<string, string> pair in StringComparisonData())
            {
                options.Data[pair.Key] = pair.Value;
            }

            CreateHost(CreateNode("Host"), options);
            Node seeker = CreateNode("Seeker");

            List<string> mismatches = new List<string>();
            foreach ((string key, string filter, LobbyComparison comparison, bool isKept) in StringComparisonChecks())
            {
                bool isFound = Search(seeker, new LobbySearchQuery().AddStringFilter(key, filter, comparison)).Lobbies.Count == 1;
                if (isFound != isKept)
                {
                    mismatches.Add(DescribeStringComparison(key, filter, comparison, isKept));
                }
            }

            Assert.That(mismatches, Is.Empty);
        }

        [Test]
        public void LobbySearch_SkipsAFilterWhoseKeyIsNullOrEmpty_AsTheSteamBackendDoes()
        {
            // FR-6, FR-38, #314: the Steam backend hands Steam no filter without a key, so a
            // string or numeric filter with a null or empty key leaves out no lobby here either.
            LobbyCreateOptions options = PublicOptions(new SessionRules(4));
            options.Data[KeylessFilterLobbyKey] = KeylessFilterLobbyValue;
            CreateHost(CreateNode("Host"), options);
            Node seeker = CreateNode("Seeker");

            List<string> mismatches = new List<string>();
            foreach ((string description, LobbySearchQuery query, bool isKept) in KeylessFilterChecks())
            {
                if ((Search(seeker, query).Lobbies.Count == 1) != isKept)
                {
                    mismatches.Add($"{description} should {(isKept ? "keep" : "drop")} the lobby");
                }
            }

            Assert.That(mismatches, Is.Empty);
        }
        #endregion

        #region String Comparisons
        /// <summary>
        /// How Steam's string filters compared a lobby's value with a filter's value, as the sign
        /// of the lobby's value minus the filter's: what Steam's search on AppID 480 gave for all
        /// six comparisons of each pair on 2026-11-01 (#239). Every backend gives these results;
        /// <c>MultiplayerSteamIntegrationTests</c> checks Steam against them.
        /// </summary>
        internal static readonly (string Lobby, string Filter, int Sign)[] SteamStringComparisons =
        {
            ("desert", "desert", 0),
            ("desert", "DESERT", 0),
            ("Desert", "dESERT", 0),
            ("b", "a", 1),
            ("B", "a", 1),
            ("a", "B", -1),
            // Folded to upper case, '_' (0x5F) sorts after 'A' (0x41) and so after 'a' too.
            ("_", "a", 1),
            ("_", "A", 1),
            ("a", "_", -1),
            ("des", "DESERT", -1),
            ("desert", "", 1),
            // Only a to z fold; other letters compare by code point, as UTF-8 text sorts.
            ("\u00E9", "\u00C9", 1),
            ("\u03C3", "\u03A3", 1),
            ("\U0001F600", "\uFF21", 1),
        };

        /// <summary>
        /// A key that <see cref="StringComparisonData"/> lacks, and one it holds with an empty
        /// value. Steam's string filters dropped a lobby under all six comparisons on AppID 480 on
        /// 2026-11-01 for both (#286): a lobby created with a key written empty keeps no such key, on
        /// Steam as on every backend, since the manager leaves the key out (#313).
        /// </summary>
        internal const string MissingStringKey = "missing";
        internal const string EmptyStringKey = "empty";

        internal static readonly LobbyComparison[] AllLobbyComparisons =
        {
            LobbyComparison.Equal, LobbyComparison.NotEqual, LobbyComparison.LessThan,
            LobbyComparison.GreaterThan, LobbyComparison.EqualOrLessThan, LobbyComparison.EqualOrGreaterThan,
        };

        /// <summary>
        /// Lobby data that holds each lobby value of <see cref="SteamStringComparisons"/> under a
        /// key of its own, and <see cref="EmptyStringKey"/> with an empty value.
        /// </summary>
        internal static Dictionary<string, string> StringComparisonData()
        {
            Dictionary<string, string> data = new Dictionary<string, string>();
            for (int i = 0; i < SteamStringComparisons.Length; i++)
            {
                data["cmp" + i] = SteamStringComparisons[i].Lobby;
            }

            data[EmptyStringKey] = string.Empty;
            return data;
        }

        /// <summary>Each filter to search with on <see cref="StringComparisonData"/>, and whether it keeps the lobby.</summary>
        internal static IEnumerable<(string Key, string Filter, LobbyComparison Comparison, bool IsKept)> StringComparisonChecks()
        {
            for (int i = 0; i < SteamStringComparisons.Length; i++)
            {
                foreach (LobbyComparison comparison in AllLobbyComparisons)
                {
                    yield return ("cmp" + i, SteamStringComparisons[i].Filter, comparison, IsKeptBy(SteamStringComparisons[i].Sign, comparison));
                }
            }

            // A filter value that an empty value would equal, and one that it would sort before.
            foreach (string key in new[] { MissingStringKey, EmptyStringKey })
            {
                foreach (string filter in new[] { string.Empty, "x" })
                {
                    foreach (LobbyComparison comparison in AllLobbyComparisons)
                    {
                        yield return (key, filter, comparison, false);
                    }
                }
            }
        }

        /// <summary>
        /// The key that <see cref="EmptyValueFilterChecks"/> search a lobby by, which the lobby
        /// holds with an empty value: only a caller of a backend itself writes one, since the
        /// manager deletes a key written empty (#313).
        /// </summary>
        internal const string EmptyValueKey = "raw";

        /// <summary>
        /// Each filter to search with on <see cref="EmptyValueKey"/>, held with an empty value, and
        /// whether it keeps the lobby: what Steam's search on AppID 480 gave for all six
        /// comparisons of each on 2026-11-04 (#343). A string filter compares the empty value as an
        /// empty string, and a numeric filter reads it as 0. Every backend gives these results;
        /// <c>MultiplayerSteamIntegrationTests</c> checks Steam against them.
        /// </summary>
        internal static IEnumerable<(string Description, Func<LobbySearchQuery, LobbySearchQuery> AddTo, bool IsKept)> EmptyValueFilterChecks()
        {
            foreach (string filter in new[] { string.Empty, "x" })
            {
                foreach (LobbyComparison comparison in AllLobbyComparisons)
                {
                    yield return ($"the empty value {comparison} '{filter}'",
                        query => query.AddStringFilter(EmptyValueKey, filter, comparison),
                        IsKeptBy(filter.Length == 0 ? 0 : -1, comparison));
                }
            }

            foreach (int number in new[] { 0, 1, -1 })
            {
                foreach (LobbyComparison comparison in AllLobbyComparisons)
                {
                    yield return ($"the empty value {comparison} the number {number}",
                        query => query.AddNumericFilter(EmptyValueKey, number, comparison),
                        IsKeptBy(0.CompareTo(number), comparison));
                }
            }
        }

        /// <summary>The one game key, and its value, of the lobby that <see cref="KeylessFilterChecks"/> search for.</summary>
        internal const string KeylessFilterLobbyKey = "map";
        internal const string KeylessFilterLobbyValue = "desert";

        /// <summary>
        /// Searches with a string or numeric filter whose key is null or empty, under every
        /// comparison, and whether each keeps a lobby that holds only
        /// <see cref="KeylessFilterLobbyKey"/>: the Steam backend skips such a filter rather than
        /// hand Steam the key, so it keeps the lobby on every backend (#314), and a filter with a
        /// key beside it still decides.
        /// </summary>
        internal static IEnumerable<(string Description, LobbySearchQuery Query, bool IsKept)> KeylessFilterChecks()
        {
            foreach (string key in new[] { null, string.Empty })
            {
                string name = key == null ? "null" : "empty";
                foreach (LobbyComparison comparison in AllLobbyComparisons)
                {
                    yield return ($"a string filter with a {name} key, {comparison} 'x',", new LobbySearchQuery().AddStringFilter(key, "x", comparison), true);
                    yield return ($"a numeric filter with a {name} key, {comparison} 0,", new LobbySearchQuery().AddNumericFilter(key, 0, comparison), true);
                }

                yield return ($"a string filter with a {name} key beside one the lobby passes", new LobbySearchQuery()
                    .AddStringFilter(key, "x")
                    .AddStringFilter(KeylessFilterLobbyKey, KeylessFilterLobbyValue), true);
                yield return ($"a numeric filter with a {name} key beside one the lobby fails", new LobbySearchQuery()
                    .AddNumericFilter(key, 0)
                    .AddStringFilter(KeylessFilterLobbyKey, "forest"), false);
            }
        }

        internal static string DescribeStringComparison(string key, string filter, LobbyComparison comparison, bool isKept)
        {
            string lobby = key == MissingStringKey ? "a lobby without the key"
                : key == EmptyStringKey ? "a lobby with the key empty"
                : $"'{SteamStringComparisons[int.Parse(key.Substring(3), System.Globalization.CultureInfo.InvariantCulture)].Lobby}'";
            return $"{lobby} {comparison} '{filter}' should {(isKept ? "keep" : "drop")} the lobby";
        }

        private static bool IsKeptBy(int sign, LobbyComparison comparison)
        {
            switch (comparison)
            {
                case LobbyComparison.Equal: return sign == 0;
                case LobbyComparison.NotEqual: return sign != 0;
                case LobbyComparison.LessThan: return sign < 0;
                case LobbyComparison.GreaterThan: return sign > 0;
                case LobbyComparison.EqualOrLessThan: return sign <= 0;
                default: return sign >= 0;
            }
        }
        #endregion

        #region Participant Data
        [Test]
        public void ParticipantData_SetByEachPlayer_ReachesEveryPlayer()
        {
            // FR-9, FR-11
            (Node host, Node[] clients) = CreateSession(2);
            Node a = clients[0], b = clients[1];

            Assert.That(a.Manager.SetParticipantData("character", "knight").IsSuccess, Is.True);
            Assert.That(DataOf(a, a.Id), Is.Empty, "the host decides, and reports A's own data back");
            Assert.That(host.Manager.SetParticipantData("character", "wizard").IsSuccess, Is.True);
            Assert.That(host.DataChanges.Count, Is.EqualTo(1), "the host's own event arrives before the call returns");
            Pump();

            foreach (Node node in new[] { host, a, b })
            {
                string name = node.Backend.PlayerName;
                Assert.That(DataOf(node, host.Id)["character"], Is.EqualTo("wizard"), name);
                Assert.That(DataOf(node, a.Id)["character"], Is.EqualTo("knight"), name);
                Assert.That(DataOf(node, b.Id), Is.Empty, name);
                Assert.That(node.DataChanges.Select(change => (change.Participant.Id, change.Key)), Is.EqualTo(new[] { (host.Id, "character"), (a.Id, "character") }), name);
                Assert.That(node.DataChanges.Select(change => change.Participant.Data["character"]), Is.EqualTo(new[] { "wizard", "knight" }), name);
            }

            // A participant handed out before keeps the data it had then.
            Assert.That(a.Manager.SetParticipantData("character", "archer").IsSuccess, Is.True);
            Pump();
            Assert.That(b.DataChanges[1].Participant.Data["character"], Is.EqualTo("knight"));
            Assert.That(b.DataChanges[2].Participant.Data["character"], Is.EqualTo("archer"));
            Assert.That(DataOf(b, a.Id)["character"], Is.EqualTo("archer"));
            Assert.That(b.Changed, Is.Empty, "data changes have an event of their own");
        }

        [Test]
        public void ParticipantData_DeletedByItsPlayer_ReachesEveryPlayer_AndFreesItsRoom()
        {
            // FR-9, FR-11
            (Node host, Node[] clients) = CreateSession(2);
            Node a = clients[0], b = clients[1];
            Node[] everyone = { host, a, b };
            Assert.That(a.Manager.SetParticipantData("character", "knight").IsSuccess, Is.True);
            Assert.That(a.Manager.SetParticipantData("team", "red").IsSuccess, Is.True);
            Assert.That(host.Manager.SetParticipantData("character", "wizard").IsSuccess, Is.True);
            Pump();
            Dictionary<Node, int> earlier = everyone.ToDictionary(node => node, node => node.DataChanges.Count);

            Assert.That(a.Manager.DeleteParticipantData("team").IsSuccess, Is.True);
            Assert.That(DataOf(a, a.Id).ContainsKey("team"), Is.True, "the host decides, and reports A's own deletion back");
            Assert.That(host.Manager.DeleteParticipantData("character").IsSuccess, Is.True);
            Assert.That(host.DataChanges.Count, Is.EqualTo(earlier[host] + 1), "the host's own event arrives before the call returns");
            Assert.That(a.Manager.DeleteParticipantData("never").IsSuccess, Is.True, "deleting a key that is not there changes nothing");
            Assert.That(host.Manager.DeleteParticipantData("never").IsSuccess, Is.True);
            Pump();

            foreach (Node node in everyone)
            {
                string name = node.Backend.PlayerName;
                Assert.That(DataOf(node, a.Id), Is.EquivalentTo(new Dictionary<string, string> { ["character"] = "knight" }), name);
                Assert.That(DataOf(node, host.Id), Is.Empty, name);
                List<(Participant Participant, string Key)> deletions = node.DataChanges.Skip(earlier[node]).ToList();
                Assert.That(deletions.Select(change => (change.Participant.Id, change.Key)), Is.EqualTo(new[] { (host.Id, "character"), (a.Id, "team") }), name);
                Assert.That(deletions.All(change => !change.Participant.Data.ContainsKey(change.Key)), Is.True, $"{name} hears the player without the key");
            }

            // The room a deletion frees counts on the client at once, before the host reported it.
            int half = MultiplayerLimits.MaxParticipantDataSize / 2;
            Assert.That(b.Manager.SetParticipantData("a", new string('v', half + 100)).IsSuccess, Is.True);
            Assert.That(b.Manager.SetParticipantData("b", new string('v', half)).Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));
            Assert.That(b.Manager.DeleteParticipantData("a").IsSuccess, Is.True);
            Assert.That(b.Manager.SetParticipantData("b", new string('v', half)).IsSuccess, Is.True);
            Pump();
            foreach (Node node in everyone)
            {
                Assert.That(DataOf(node, b.Id).Keys, Is.EquivalentTo(new[] { "b" }), node.Backend.PlayerName);
            }

            Assert.That(a.Manager.DeleteParticipantData("mu3.ready").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(a.Manager.DeleteParticipantData(null).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(CreateNode("Lonely").Manager.DeleteParticipantData("character").Reason, Is.EqualTo(MultiplayerReason.InvalidState));
        }

        [Test]
        public void ParticipantData_DeletionFromAClient_DeletesOnlyItsOwn()
        {
            // FR-11, NFR-5: the request names no player, and the host's own deletions from a client are dropped.
            (Node host, Node[] clients) = CreateSession(1);
            Node a = clients[0];
            Assert.That(a.Manager.SetParticipantData("team", "blue").IsSuccess, Is.True);
            LocalBackend raw = CreateRawBackend("Raw", new RecordingListener());
            ConnectionId connection = ConnectAsPlayer(raw, host);
            raw.Send(connection, DataPacket(PacketKind.SetParticipantData, "team", "red"), DeliveryMode.Reliable);
            raw.Send(connection, DataPacket(PacketKind.SetParticipantData, "role", "scout"), DeliveryMode.Reliable);
            Pump();

            raw.Send(connection, KeyPacket(PacketKind.DeleteParticipantData, "team"), DeliveryMode.Reliable);
            LogAssert.Expect(LogType.Warning, new Regex("Dropped a participant data deletion from .*: InvalidArgument"));
            raw.Send(connection, KeyPacket(PacketKind.DeleteParticipantData, "mu3.ready"), DeliveryMode.Reliable);
            LogAssert.Expect(LogType.Warning, new Regex("Dropped ParticipantDataDeleted from client"));
            raw.Send(connection, ParticipantDataDeletedPacket(a.Id, "team"), DeliveryMode.Reliable);
            LogAssert.Expect(LogType.Warning, new Regex("Dropped LobbyDataDeleted from client"));
            raw.Send(connection, KeyPacket(PacketKind.LobbyDataDeleted, "map"), DeliveryMode.Reliable);
            Pump();

            foreach (Node node in new[] { host, a })
            {
                Assert.That(DataOf(node, raw.LocalPlayerId), Is.EquivalentTo(new Dictionary<string, string> { ["role"] = "scout" }), node.Backend.PlayerName);
                Assert.That(DataOf(node, a.Id)["team"], Is.EqualTo("blue"), node.Backend.PlayerName);
            }
        }

        [Test]
        public void ParticipantData_FromAClient_ChangesOnlyItsOwn()
        {
            // FR-11, NFR-5: the request names no player, and the host's own packets from a client are dropped.
            (Node host, Node[] clients) = CreateSession(1);
            Node a = clients[0];
            LocalBackend raw = CreateRawBackend("Raw", new RecordingListener());
            ConnectionId connection = ConnectAsPlayer(raw, host);

            raw.Send(connection, DataPacket(PacketKind.SetParticipantData, "team", "red"), DeliveryMode.Reliable);
            LogAssert.Expect(LogType.Warning, new Regex("Dropped ParticipantDataChanged from client"));
            raw.Send(connection, ParticipantDataChangedPacket(a.Id, "team", "spoofed"), DeliveryMode.Reliable);
            LogAssert.Expect(LogType.Warning, new Regex("Dropped LobbyDataChanged from client"));
            raw.Send(connection, DataPacket(PacketKind.LobbyDataChanged, "map", "spoofed"), DeliveryMode.Reliable);
            Pump();

            foreach (Node node in new[] { host, a })
            {
                Assert.That(DataOf(node, raw.LocalPlayerId)["team"], Is.EqualTo("red"));
                Assert.That(DataOf(node, a.Id), Is.Empty);
                Assert.That(node.Manager.LobbyData.ContainsKey("map"), Is.False);
                Assert.That(node.DataChanges.Select(change => change.Participant.Id), Is.EqualTo(new[] { raw.LocalPlayerId }));
            }
        }

        [Test]
        public void ParticipantData_RefusesReservedKeysAndTooMuchData_HereAndAtTheHost()
        {
            // FR-11, NFR-5: a client that skips its own check still cannot get past the host.
            (Node host, Node[] clients) = CreateSession(1);
            Node a = clients[0];

            Assert.That(a.Manager.SetParticipantData("mu3.ready", "true").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(a.Manager.SetParticipantData("MU3.Ready", "true").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(a.Manager.SetParticipantData("character", null).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            foreach (string text in UncleanTexts)
            {
                Assert.That(a.Manager.SetParticipantData(text, "value").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument), Escape(text));
                Assert.That(a.Manager.SetParticipantData("character", text).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument), Escape(text));
            }

            Assert.That(a.Manager.SetParticipantData("character", new string('v', MultiplayerLimits.MaxParticipantDataSize)).Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));
            Assert.That(host.Manager.SetParticipantData(LobbyDataKeys.State, "game").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(CreateNode("Lonely").Manager.SetParticipantData("character", "knight").Reason, Is.EqualTo(MultiplayerReason.InvalidState));

            LocalBackend raw = CreateRawBackend("Raw", new RecordingListener());
            ConnectionId connection = ConnectAsPlayer(raw, host);
            List<string> refusals = new List<string>();
            void Record(string message, string stackTrace, LogType type)
            {
                if (message.Contains("Dropped participant data"))
                {
                    refusals.Add(message);
                }
            }

            Application.logMessageReceived += Record;
            try
            {
                // A flood of bad requests logs each reason once. A lone surrogate turns into a
                // replacement character on its way, so only a NUL reaches the host unchanged.
                for (int i = 0; i < 3; i++)
                {
                    raw.Send(connection, DataPacket(PacketKind.SetParticipantData, "mu3.ready", "true"), DeliveryMode.Reliable);
                    raw.Send(connection, DataPacket(PacketKind.SetParticipantData, "map", "a\0b"), DeliveryMode.Reliable);
                    raw.Send(connection, DataPacket(PacketKind.SetParticipantData, "c", new string('v', MultiplayerLimits.MaxParticipantDataSize)), DeliveryMode.Reliable);
                }

                Pump();
            }
            finally
            {
                Application.logMessageReceived -= Record;
            }

            Assert.That(refusals.Count, Is.EqualTo(2), string.Join("\n", refusals));
            Assert.That(refusals[0], Does.Contain(": InvalidArgument."));
            Assert.That(refusals[1], Does.Contain(": MessageTooLarge."));
            Assert.That(DataOf(host, raw.LocalPlayerId), Is.Empty);
            Assert.That(host.DataChanges.Concat(a.DataChanges), Is.Empty);
        }

        [Test]
        public void ParticipantData_FromAClient_IsCheckedAgainstItsOwnRequests_NotTheCopyStillOnItsWay()
        {
            // FR-11: what a client is told is what the host does, before any change came back.
            (Node host, Node[] clients) = CreateSession(1);
            Node a = clients[0];
            int half = MultiplayerLimits.MaxParticipantDataSize / 2;
            List<string> refusals = new List<string>();
            void Record(string message, string stackTrace, LogType type)
            {
                if (message.Contains("Dropped participant data"))
                {
                    refusals.Add(message);
                }
            }

            Application.logMessageReceived += Record;
            try
            {
                Assert.That(a.Manager.SetParticipantData("a", new string('v', half + 100)).IsSuccess, Is.True);
                Assert.That(a.Manager.SetParticipantData("b", new string('v', half)).Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge),
                    "the first request has not come back yet, but the host will hold it");

                Assert.That(a.Manager.SetParticipantData("a", string.Empty).IsSuccess, Is.True);
                Assert.That(a.Manager.SetParticipantData("b", new string('v', half)).IsSuccess, Is.True,
                    "the room the second request freed counts before the host reported it");
                Assert.That(DataOf(a, a.Id), Is.Empty);
                Pump();
            }
            finally
            {
                Application.logMessageReceived -= Record;
            }

            Assert.That(refusals, Is.Empty);
            foreach (Node node in new[] { host, a })
            {
                Assert.That(DataOf(node, a.Id)["a"], Is.Empty);
                Assert.That(DataOf(node, a.Id)["b"].Length, Is.EqualTo(half));
                Assert.That(node.DataChanges.Count, Is.EqualTo(3));
            }
        }

        [Test]
        public void AFullSession_AtEveryDataLimit_StillAdmitsAPlayerWhoJoinsLate()
        {
            // FR-11, FR-24: the welcome carries the start data, the lobby data, and every
            // player's data at once, so it has to fit in one send at every limit together. The
            // data uses the shortest keys there are, whose length fields cost the most, and the
            // lobby data the shortest values it keeps, since an empty value deletes its key (#313).
            Dictionary<string, string> lobbyData = DataOfSize(MultiplayerLimits.MaxLobbyDataSize, isLobbyData: true);
            LobbyCreateOptions options = PublicOptions(new SessionRules(MultiplayerLimits.MaxCapacity, requireReady: false, allowLateJoin: true));
            foreach (KeyValuePair<string, string> pair in lobbyData)
            {
                options.Data.Add(pair.Key, pair.Value);
            }

            Node host = CreateHost(CreateNode("Host"), options);
            Dictionary<string, string> playerData = DataOfSize(MultiplayerLimits.MaxParticipantDataSize);
            List<LocalBackend> players = new List<LocalBackend>();
            for (int i = 0; i < MultiplayerLimits.MaxCapacity - 2; i++)
            {
                LocalBackend raw = CreateRawBackend($"Player {i:D2} {new string('n', MultiplayerLimits.MaxPlayerNameLength)}", new RecordingListener());
                ConnectionId connection = ConnectAsPlayer(raw, host);
                foreach (KeyValuePair<string, string> pair in playerData)
                {
                    raw.Send(connection, DataPacket(PacketKind.SetParticipantData, pair.Key, pair.Value), DeliveryMode.Reliable);
                }

                players.Add(raw);
            }

            foreach (KeyValuePair<string, string> pair in playerData)
            {
                Assert.That(host.Manager.SetParticipantData(pair.Key, pair.Value).IsSuccess, Is.True);
            }

            Pump();
            Assert.That(players.All(raw => DataOf(host, raw.LocalPlayerId).Count == playerData.Count), Is.True);
            Assert.That(host.Manager.StartGame(new byte[MultiplayerLimits.MaxStartDataSize]).IsSuccess, Is.True);
            Pump();

            Node late = CreateNode("Late");
            Assert.That(Join(late, host).IsSuccess, Is.True);

            Assert.That(late.Manager.State, Is.EqualTo(SessionState.InGame));
            Assert.That(late.Manager.Participants.Count, Is.EqualTo(MultiplayerLimits.MaxCapacity));
            Assert.That(late.Manager.StartData.Length, Is.EqualTo(MultiplayerLimits.MaxStartDataSize));
            Assert.That(late.Manager.LobbyData, Is.EquivalentTo(lobbyData));
            Assert.That(DataOf(late, host.Id), Is.EquivalentTo(playerData));
            Assert.That(players.All(raw => DataOf(late, raw.LocalPlayerId).Count == playerData.Count), Is.True);
        }

        [Test]
        public void Data_ReachesALateJoiner_AndOutlivesTheGame_ButNotTheSession()
        {
            // FR-11
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false, allowLateJoin: true));
            Node a = JoinNew("A", host);
            Assert.That(host.Manager.SetLobbyData("map", "desert").IsSuccess, Is.True);
            Assert.That(a.Manager.SetParticipantData("character", "knight").IsSuccess, Is.True);
            Pump();
            Assert.That(host.Manager.StartGame().IsSuccess, Is.True);
            Assert.That(host.Manager.SetParticipantData("character", "wizard").IsSuccess, Is.True, "in a game too");
            Pump();

            Node late = JoinNew("Late", host);
            Assert.That(late.Manager.LobbyData["map"], Is.EqualTo("desert"));
            Assert.That(DataOf(late, a.Id)["character"], Is.EqualTo("knight"));
            Assert.That(DataOf(late, host.Id)["character"], Is.EqualTo("wizard"));
            Assert.That(late.LobbyDataChanges, Is.Empty);
            Assert.That(late.DataChanges, Is.Empty);
            Assert.That(a.Joined.Single(participant => participant.Id == late.Id).Data, Is.Empty);

            Assert.That(host.Manager.EndGame().IsSuccess, Is.True);
            Pump();
            foreach (Node node in new[] { host, a, late })
            {
                Assert.That(node.Manager.LobbyData["map"], Is.EqualTo("desert"));
                Assert.That(DataOf(node, a.Id)["character"], Is.EqualTo("knight"));
            }

            a.Manager.Leave();
            Pump();
            Assert.That(a.Manager.LobbyData, Is.Empty);
            Assert.That(host.Manager.SetLobbyData("map", "forest").IsSuccess, Is.True);
            host.Manager.Leave();
            Assert.That(host.Manager.LobbyData, Is.Empty);
        }
        #endregion

        #region Data Helpers
        // A NUL, where Steam ends a string, and surrogates without their pair, which UTF-8 replaces.
        private static readonly string[] UncleanTexts = { "a\0b", "a\uD800", "\uDC00a", "\uDBFF" };

        private static string Escape(string text) => string.Concat(text.Select(c => c < 0x20 || char.IsSurrogate(c) ? $"\\u{(int)c:X4}" : c.ToString()));

        /// <summary>
        /// Data whose keys and values take exactly <paramref name="size"/> UTF-8 bytes in as many
        /// entries as distinct keys allow: every one-character key, then two-character keys. As
        /// <paramref name="isLobbyData"/>, no two keys differ only by case and every value holds a
        /// character, since lobby data keeps no empty value.
        /// </summary>
        private static Dictionary<string, string> DataOfSize(int size, bool isLobbyData = false)
        {
            Dictionary<string, string> data = new Dictionary<string, string>();
            HashSet<string> taken = new HashSet<string>(isLobbyData ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            string value = isLobbyData ? "v" : string.Empty;
            int left = size;
            for (char c = '!'; c <= '~' && left >= 1 + value.Length; c++)
            {
                if (taken.Add(c.ToString()))
                {
                    data.Add(c.ToString(), value);
                    left -= 1 + value.Length;
                }
            }

            for (char first = '!'; first <= '~' && left >= 2 + value.Length; first++)
            {
                for (char second = '!'; second <= '~' && left >= 2 + value.Length; second++)
                {
                    string key = new string(new[] { first, second });
                    if (taken.Add(key))
                    {
                        data.Add(key, value);
                        left -= 2 + value.Length;
                    }
                }
            }

            if (left > 0)
            {
                data["!"] += new string('v', left);
            }

            return data;
        }

        private static IReadOnlyDictionary<string, string> DataOf(Node node, PlayerId player)
        {
            Assert.That(node.Manager.TryGetParticipant(player, out Participant participant), Is.True, $"{player} at {node.Backend.PlayerName}");
            return participant.Data;
        }

        private static byte[] DataPacket(PacketKind kind, string key, string value)
        {
            PacketWriter writer = new PacketWriter();
            writer.Begin(kind);
            writer.WriteString(key);
            writer.WriteString(value);
            return writer.Written.ToArray();
        }

        private static byte[] ParticipantDataChangedPacket(PlayerId player, string key, string value)
        {
            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.ParticipantDataChanged);
            writer.WriteULong(player.Value);
            writer.WriteString(key);
            writer.WriteString(value);
            return writer.Written.ToArray();
        }

        private static byte[] KeyPacket(PacketKind kind, string key)
        {
            PacketWriter writer = new PacketWriter();
            writer.Begin(kind);
            writer.WriteString(key);
            return writer.Written.ToArray();
        }

        private static byte[] ParticipantDataDeletedPacket(PlayerId player, string key)
        {
            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.ParticipantDataDeleted);
            writer.WriteULong(player.Value);
            writer.WriteString(key);
            return writer.Written.ToArray();
        }
        #endregion
    }
}
