#if MU3LIBRARY_STEAMWORKS_SUPPORT && UNITY_STANDALONE
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using Mu3Library.Multiplayer.Steam;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using Steamworks;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mu3Library.Tests
{
    /// <summary>
    /// The parts of <see cref="SteamBackend"/> that need no Steam client: how Steam's answers map
    /// to the library's reasons, a Steam that cannot load, a Steam call that fails once the
    /// backend exists, the launch arguments, the order of the rich presence keys, and the editor
    /// fallback (FR-1, FR-5 to FR-7, FR-13, FR-19, FR-39). The rest is checked by hand against
    /// Steam (spec section 13).
    /// </summary>
    public class MultiplayerSteamTests
    {
        [TestCase(ESteamAPIInitResult.k_ESteamAPIInitResult_OK, MultiplayerReason.None)]
        [TestCase(ESteamAPIInitResult.k_ESteamAPIInitResult_NoSteamClient, MultiplayerReason.SteamNotRunning)]
        [TestCase(ESteamAPIInitResult.k_ESteamAPIInitResult_FailedGeneric, MultiplayerReason.SteamNotRunning)]
        [TestCase(ESteamAPIInitResult.k_ESteamAPIInitResult_VersionMismatch, MultiplayerReason.BackendError)]
        public void InitResult_MapsToAReason(ESteamAPIInitResult result, MultiplayerReason expected)
        {
            // FR-1
            Assert.That(SteamReasons.FromInitResult(result), Is.EqualTo(expected));
        }

        [TestCase(EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess, false, MultiplayerReason.None)]
        [TestCase(EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist, false, MultiplayerReason.LobbyNotFound)]
        [TestCase(EChatRoomEnterResponse.k_EChatRoomEnterResponseFull, false, MultiplayerReason.SessionFull)]
        [TestCase(EChatRoomEnterResponse.k_EChatRoomEnterResponseBanned, false, MultiplayerReason.Banned)]
        [TestCase(EChatRoomEnterResponse.k_EChatRoomEnterResponseNotAllowed, true, MultiplayerReason.GameInProgress)]
        [TestCase(EChatRoomEnterResponse.k_EChatRoomEnterResponseNotAllowed, false, MultiplayerReason.LobbyNotFound)]
        [TestCase(EChatRoomEnterResponse.k_EChatRoomEnterResponseLimited, false, MultiplayerReason.BackendError)]
        public void EnterResponse_MapsToAReason(EChatRoomEnterResponse response, bool isInGame, MultiplayerReason expected)
        {
            // FR-7, FR-19: a lobby that refuses joins during a game answers GameInProgress.
            Assert.That(SteamReasons.FromEnterResponse(response, isInGame), Is.EqualTo(expected));
        }

        [TestCase(EResult.k_EResultOK, MultiplayerReason.None)]
        [TestCase(EResult.k_EResultNoConnection, MultiplayerReason.SteamNotRunning)]
        [TestCase(EResult.k_EResultLimitExceeded, MultiplayerReason.BackendError)]
        public void CreateResult_MapsToAReason(EResult result, MultiplayerReason expected)
        {
            Assert.That(SteamReasons.FromCreateResult(result), Is.EqualTo(expected));
        }

        [TestCase(LobbyVisibility.InviteOnly, ELobbyType.k_ELobbyTypePrivate)]
        [TestCase(LobbyVisibility.FriendsOnly, ELobbyType.k_ELobbyTypeFriendsOnly)]
        [TestCase(LobbyVisibility.Public, ELobbyType.k_ELobbyTypePublic)]
        public void Visibility_MapsToALobbyType(LobbyVisibility visibility, ELobbyType expected)
        {
            // FR-5
            Assert.That(SteamReasons.ToLobbyType(visibility), Is.EqualTo(expected));
        }

        [Test]
        public void SearchEnums_MatchSteamsValues()
        {
            // FR-6: the backend passes comparisons and distances to Steam by value.
            Assert.That((int)ELobbyComparison.k_ELobbyComparisonEqualToOrLessThan, Is.EqualTo((int)LobbyComparison.EqualOrLessThan));
            Assert.That((int)ELobbyComparison.k_ELobbyComparisonLessThan, Is.EqualTo((int)LobbyComparison.LessThan));
            Assert.That((int)ELobbyComparison.k_ELobbyComparisonEqual, Is.EqualTo((int)LobbyComparison.Equal));
            Assert.That((int)ELobbyComparison.k_ELobbyComparisonGreaterThan, Is.EqualTo((int)LobbyComparison.GreaterThan));
            Assert.That((int)ELobbyComparison.k_ELobbyComparisonEqualToOrGreaterThan, Is.EqualTo((int)LobbyComparison.EqualOrGreaterThan));
            Assert.That((int)ELobbyComparison.k_ELobbyComparisonNotEqual, Is.EqualTo((int)LobbyComparison.NotEqual));

            Assert.That((int)ELobbyDistanceFilter.k_ELobbyDistanceFilterClose, Is.EqualTo((int)LobbyDistance.Close));
            Assert.That((int)ELobbyDistanceFilter.k_ELobbyDistanceFilterDefault, Is.EqualTo((int)LobbyDistance.Default));
            Assert.That((int)ELobbyDistanceFilter.k_ELobbyDistanceFilterFar, Is.EqualTo((int)LobbyDistance.Far));
            Assert.That((int)ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide, Is.EqualTo((int)LobbyDistance.Worldwide));
        }

        [Test]
        public void LaunchArguments_NameTheLobbyOfAnAcceptedInvite()
        {
            // FR-7: a game launched from an invite gets +connect_lobby <lobby ID>.
            Assert.That(SteamLaunchArguments.TryGetConnectLobby(new[] { "game.exe", "+connect_lobby", "109775241000000001" }, out LobbyId lobby), Is.True);
            Assert.That(lobby.Value, Is.EqualTo(109775241000000001UL));

            Assert.That(SteamLaunchArguments.TryGetConnectLobby(new[] { "game.exe" }, out _), Is.False);
            Assert.That(SteamLaunchArguments.TryGetConnectLobby(new[] { "game.exe", "+connect_lobby" }, out _), Is.False);
            Assert.That(SteamLaunchArguments.TryGetConnectLobby(new[] { "+connect_lobby", "not-a-number" }, out _), Is.False);
            Assert.That(SteamLaunchArguments.TryGetConnectLobby(new[] { "+connect_lobby", "0" }, out _), Is.False);
            Assert.That(SteamLaunchArguments.TryGetConnectLobby(null, out _), Is.False);
        }

        [Test]
        public void Fallback_UsesTheLocalBackendInTheEditorOnly_WhenAsked()
        {
            // FR-39
            LocalNetwork network = new LocalNetwork();
            SteamBackendOptions withFallback = new SteamBackendOptions { EditorFallbackNetwork = network, EditorFallbackPlayerName = "Editor" };

            LogAssert.Expect(LogType.Warning, new Regex("Steam is unavailable \\(SteamNotRunning\\)"));
            using (IMultiplayerBackend fallback = SteamBackend.ChooseFallback(MultiplayerReason.SteamNotRunning, isEditor: true, withFallback))
            {
                Assert.That(fallback, Is.InstanceOf<LocalBackend>());
                Assert.That(fallback.GetPlayerName(fallback.LocalPlayerId), Is.EqualTo("Editor"));
            }

            Assert.That(SteamBackend.ChooseFallback(MultiplayerReason.SteamNotRunning, isEditor: false, withFallback), Is.Null);
            Assert.That(SteamBackend.ChooseFallback(MultiplayerReason.InvalidArgument, isEditor: true, withFallback), Is.Null, "unusable options are a mistake to fix, not a reason to fall back");
            Assert.That(SteamBackend.ChooseFallback(MultiplayerReason.SteamNotRunning, isEditor: true, new SteamBackendOptions()), Is.Null);
        }

        [TestCase(typeof(DllNotFoundException))]
        [TestCase(typeof(EntryPointNotFoundException))]
        [TestCase(typeof(BadImageFormatException))]
        [TestCase(typeof(InvalidOperationException))]
        public void TryCreate_ASteamThatCannotLoad_AnswersSteamNotRunning_WithoutAnException(Type thrown)
        {
            // FR-1: what the runtime throws for a steam_api64 it cannot load or that lacks a
            // function; which one depends on the runtime.
            Exception exception = (Exception)Activator.CreateInstance(thrown, "simulated");
            LogAssert.Expect(LogType.Warning, new Regex(@"Steam could not start \(" + thrown.Name + @"\): simulated"));

            MultiplayerResult result = SteamBackend.TryCreateWith(new SteamBackendOptions(), Throwing(exception), out SteamBackend backend);

            Assert.That(result.Reason, Is.EqualTo(MultiplayerReason.SteamNotRunning));
            Assert.That(backend, Is.Null);
        }

        [Test]
        public void TryCreate_ATypeInitializerThatMetABrokenLibrary_AnswersSteamNotRunning()
        {
            Exception wrapped = new TypeInitializationException("Steamworks.Simulated",
                new TypeInitializationException("Steamworks.Inner", new EntryPointNotFoundException("simulated")));
            LogAssert.Expect(LogType.Warning, new Regex(@"Steam could not start \(EntryPointNotFoundException\): simulated"));

            MultiplayerResult result = SteamBackend.TryCreateWith(new SteamBackendOptions(), Throwing(wrapped), out SteamBackend backend);

            Assert.That(result.Reason, Is.EqualTo(MultiplayerReason.SteamNotRunning));
            Assert.That(backend, Is.Null);
        }

        [Test]
        public void TryCreate_AnyOtherException_ReachesTheCaller()
        {
            // A mistake in the code is not hidden behind a reason.
            Assert.Throws<NullReferenceException>(() =>
                SteamBackend.TryCreateWith(new SteamBackendOptions(), Throwing(new NullReferenceException()), out _));
            Assert.Throws<TypeInitializationException>(() =>
                SteamBackend.TryCreateWith(new SteamBackendOptions(), Throwing(new TypeInitializationException("Steamworks.Simulated", new NullReferenceException())), out _));
        }

        [Test]
        public void CreateOrFallback_ASteamThatCannotLoad_FallsBackInTheEditor_WhenAsked()
        {
            // FR-39: a broken native library is a Steam that cannot start, so the editor falls back.
            SteamBackendOptions withFallback = new SteamBackendOptions { EditorFallbackNetwork = new LocalNetwork(), EditorFallbackPlayerName = "Editor" };
            LogAssert.Expect(LogType.Warning, new Regex(@"Steam could not start \(BadImageFormatException\)"));
            LogAssert.Expect(LogType.Warning, new Regex(@"Steam is unavailable \(SteamNotRunning\)"));

            MultiplayerResult result = SteamBackend.CreateOrFallbackWith(withFallback, Throwing(new BadImageFormatException("simulated")), out IMultiplayerBackend fallback);
            using (fallback)
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(fallback, Is.InstanceOf<LocalBackend>());
            }

            LogAssert.Expect(LogType.Warning, new Regex(@"Steam could not start \(EntryPointNotFoundException\)"));
            result = SteamBackend.CreateOrFallbackWith(new SteamBackendOptions(), Throwing(new EntryPointNotFoundException("simulated")), out IMultiplayerBackend none);
            Assert.That(result.Reason, Is.EqualTo(MultiplayerReason.SteamNotRunning), "without the fallback the reason reaches the game");
            Assert.That(none, Is.Null);
        }

        [Test]
        public void ABackendWhoseSteamCallFailed_AnswersEveryRequestAsUnavailable_WithoutAnException()
        {
            // FR-1 once the backend exists: the first failed Steam call, here the callback run of
            // the first update, stops the backend, and every request answers from then on.
            AssumeSteamCallsFail();
            List<string> answers = new List<string>();
            using (SteamBackend backend = SteamBackend.CreateWithoutSteam(new SteamBackendOptions()))
            {
                Assert.That(backend.IsAvailable, Is.True, "a backend made without Steam takes Steam for running");
                LogAssert.Expect(LogType.Warning, FailedCallWarning);

                backend.Update();
                Assert.That(backend.IsAvailable, Is.False);

                backend.CreateLobby(LobbyVisibility.Public, 4, null, (reason, lobby) => answers.Add("create " + reason));
                backend.JoinLobby(SomeLobby, reason => answers.Add("join " + reason));
                backend.SearchLobbies(null, (reason, lobbies) => answers.Add("search " + reason));
                backend.LoadAvatar(SomeAccount, AvatarSize.Small, (reason, avatar) => answers.Add("avatar " + reason));
                Assert.That(backend.OpenInviteDialog(SomeLobby).Reason, Is.EqualTo(MultiplayerReason.SteamNotRunning), "the reason the lobby requests answer");
                Assert.That(backend.InviteFriend(SomeLobby, SomeAccount).Reason, Is.EqualTo(MultiplayerReason.SteamNotRunning));
                Assert.That(backend.StartListening(), Is.False);
                Assert.That(backend.Connect(SomeAccount), Is.EqualTo(ConnectionId.None));
                Assert.That(backend.SetLobbyData(SomeLobby, "key", "value"), Is.False);
                Assert.That(backend.GetLobbyData(SomeLobby, "key"), Is.Null);
                Assert.That(backend.SetPresence("Waiting", SomeLobby, 2), Is.False);
                Assert.That(backend.GetPlayerName(SomeAccount), Is.EqualTo(SomeAccount.ToString()));

                backend.Update();
                Assert.That(answers, Is.EquivalentTo(new[] { "create SteamNotRunning", "join SteamNotRunning", "search SteamNotRunning", "avatar BackendError" }));

                backend.Update();
                Assert.That(answers.Count, Is.EqualTo(4), "every request answers once");
            }

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Invites_AnswerSteamNotRunning_WhileSteamCannotBeUsed_AndNotInitialized_OnceTheBackendIsDisposed()
        {
            // The boundary of the two reasons: Steam that cannot be used, as the lobby requests
            // answer it, and a backend that is gone, as a disposed manager answers it.
            AssumeSteamCallsFail();
            SteamBackend backend = SteamBackend.CreateWithoutSteam(new SteamBackendOptions());
            LogAssert.Expect(LogType.Warning, FailedCallWarning);
            backend.Update();

            Assert.That(backend.OpenInviteDialog(SomeLobby).Reason, Is.EqualTo(MultiplayerReason.SteamNotRunning));
            Assert.That(backend.InviteFriend(SomeLobby, SomeAccount).Reason, Is.EqualTo(MultiplayerReason.SteamNotRunning));

            backend.Dispose();
            Assert.That(backend.OpenInviteDialog(SomeLobby).Reason, Is.EqualTo(MultiplayerReason.NotInitialized));
            Assert.That(backend.InviteFriend(SomeLobby, SomeAccount).Reason, Is.EqualTo(MultiplayerReason.NotInitialized));
        }

        [TestCaseSource(nameof(FirstSteamCalls))]
        public void ASteamCallThatFails_StopsTheBackend_WithoutAnException(SteamBackendOptions options, Func<SteamBackend, Action<MultiplayerReason>, object> call, object returns, MultiplayerReason? answers)
        {
            // FR-1 once the backend exists, for each request whose Steam call fails first.
            AssumeSteamCallsFail();
            List<MultiplayerReason> heard = new List<MultiplayerReason>();
            using (SteamBackend backend = SteamBackend.CreateWithoutSteam(options))
            {
                LogAssert.Expect(LogType.Warning, FailedCallWarning);

                Assert.That(call(backend, heard.Add), Is.EqualTo(returns));
                Assert.That(backend.IsAvailable, Is.False, "the failed call stops the backend");

                backend.Update();
                Assert.That(heard, answers.HasValue ? (IResolveConstraint)Is.EqualTo(new[] { answers.Value }) : Is.Empty);
            }
        }

        [Test]
        public void ManagerCreateLobby_OnABackendThatCannotUseSteam_AnswersSteamNotRunning_AtOnce()
        {
            // #273: the manager starts listening before it creates the lobby, and on a backend
            // whose Steam calls fail that step fails first. The manager answers the reason the
            // backend's own lobby creation gives then, so a game that asks the player to start
            // Steam on SteamNotRunning sees it here too.
            AssumeSteamCallsFail();
            using (SteamBackend backend = SteamBackend.CreateWithoutSteam(new SteamBackendOptions()))
            {
                MultiplayerManager manager = new MultiplayerManager();
                try
                {
                    Assert.That(manager.Initialize(backend, new MultiplayerConfig("tests.steam", 1)).IsSuccess, Is.True);
                    List<SessionStateChange> states = new List<SessionStateChange>();
                    manager.OnStateChanged += states.Add;

                    LogAssert.Expect(LogType.Warning, FailedCallWarning);
                    MultiplayerResult? created = null;
                    manager.CreateLobby(new LobbyCreateOptions(), result => created = result);
                    Assert.That(created?.Reason, Is.EqualTo(MultiplayerReason.SteamNotRunning), "the call that starts listening failed");
                    Assert.That(backend.IsAvailable, Is.False);

                    created = null;
                    manager.CreateLobby(new LobbyCreateOptions(), result => created = result);
                    Assert.That(created?.Reason, Is.EqualTo(MultiplayerReason.SteamNotRunning), "on a backend that could not use Steam before the request");
                    Assert.That(states, Is.Empty, "a request that fails at once changes nothing");
                }
                finally
                {
                    manager.Dispose();
                }
            }

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ManagerCreateLobby_OnADisposedSteamBackend_AnswersConnectionFailed()
        {
            // The boundary of #273: a disposed backend is no Steam that cannot be used, so the
            // manager keeps the reason it gives when the host cannot listen.
            SteamBackend backend = SteamBackend.CreateWithoutSteam(new SteamBackendOptions());
            MultiplayerManager manager = new MultiplayerManager();
            try
            {
                Assert.That(manager.Initialize(backend, new MultiplayerConfig("tests.steam", 1)).IsSuccess, Is.True);
                backend.Dispose();

                MultiplayerResult? created = null;
                manager.CreateLobby(new LobbyCreateOptions(), result => created = result);
                Assert.That(created?.Reason, Is.EqualTo(MultiplayerReason.ConnectionFailed));
            }
            finally
            {
                manager.Dispose();
                backend.Dispose();
            }
        }

        private static readonly LobbyId SomeLobby = new LobbyId(109775241000000001UL);
        private static readonly PlayerId SomeAccount = new PlayerId(76561197960435530UL);
        private static readonly Regex FailedCallWarning = new Regex(@"The Steam backend is no longer available: a Steam call failed \(\w+\)");

        private static IEnumerable<TestCaseData> FirstSteamCalls()
        {
            yield return FirstCall("UpdateRunningSteamsCallbacks", (backend, answer) => { backend.Update(); return null; }, null, runsCallbacks: true);
            yield return FirstCall("UpdateReceivingMessages", (backend, answer) => { backend.Update(); return null; }, null);
            yield return FirstCall("GetPlayerName", (backend, answer) => backend.GetPlayerName(SomeAccount), SomeAccount.ToString());
            yield return FirstCall("OpenInviteDialog", (backend, answer) => backend.OpenInviteDialog(SomeLobby).Reason, MultiplayerReason.SteamNotRunning);
            yield return FirstCall("InviteFriend", (backend, answer) => backend.InviteFriend(SomeLobby, SomeAccount).Reason, MultiplayerReason.SteamNotRunning);
            yield return FirstCall("StartListening", (backend, answer) => backend.StartListening(), false);
            yield return FirstCall("Connect", (backend, answer) => backend.Connect(SomeAccount), ConnectionId.None);
            yield return FirstCall("LeaveLobby", (backend, answer) => { backend.LeaveLobby(SomeLobby); return null; }, null);
            yield return FirstCall("GetLobbyData", (backend, answer) => backend.GetLobbyData(SomeLobby, "key"), null);
            yield return FirstCall("SetLobbyData", (backend, answer) => backend.SetLobbyData(SomeLobby, "key", "value"), false);
            yield return FirstCall("DeleteLobbyData", (backend, answer) => backend.DeleteLobbyData(SomeLobby, "key"), false);
            yield return FirstCall("SetLobbyJoinable", (backend, answer) => backend.SetLobbyJoinable(SomeLobby, false), false);
            yield return FirstCall("SetLobbyCapacity", (backend, answer) => backend.SetLobbyCapacity(SomeLobby, 4), false);
            yield return FirstCall("GetLobbyOwner", (backend, answer) => backend.GetLobbyOwner(SomeLobby), PlayerId.None);
            yield return FirstCall("IsLobbyMember", (backend, answer) => backend.IsLobbyMember(SomeLobby, SomeAccount), false);
            yield return FirstCall("SetPresence", (backend, answer) => backend.SetPresence("Waiting", SomeLobby, 2), false);
            yield return FirstCall("CreateLobby", (backend, answer) => { backend.CreateLobby(LobbyVisibility.Public, 4, null, (reason, lobby) => answer(reason)); return null; }, null, MultiplayerReason.SteamNotRunning);
            yield return FirstCall("JoinLobby", (backend, answer) => { backend.JoinLobby(SomeLobby, answer); return null; }, null, MultiplayerReason.SteamNotRunning);
            yield return FirstCall("SearchLobbies", (backend, answer) => { backend.SearchLobbies(null, (reason, lobbies) => answer(reason)); return null; }, null, MultiplayerReason.SteamNotRunning);
            yield return FirstCall("LoadAvatar", (backend, answer) => { backend.LoadAvatar(SomeAccount, AvatarSize.Small, (reason, avatar) => answer(reason)); return null; }, null, MultiplayerReason.BackendError);
        }

        /// <param name="runsCallbacks">Off by default, so an update fails at the messages it receives instead.</param>
        private static TestCaseData FirstCall(string name, Func<SteamBackend, Action<MultiplayerReason>, object> call, object returns, MultiplayerReason? answers = null, bool runsCallbacks = false)
        {
            SteamBackendOptions options = new SteamBackendOptions { RunSteamCallbacks = runsCallbacks };
            return new TestCaseData(options, call, returns, answers).SetName($"ASteamCallThatFails_StopsTheBackend_WithoutAnException({name})");
        }

        /// <summary>
        /// A backend made without Steam fails at its first Steam call only while Steam has not
        /// started in this process: Steamworks.NET then throws <see cref="InvalidOperationException"/>
        /// where an outdated steam_api64 throws <see cref="EntryPointNotFoundException"/>.
        /// </summary>
        private static void AssumeSteamCallsFail()
        {
            Assume.That(CallbackDispatcher.IsInitialized, Is.False, "Steam runs in this process, so its calls do not fail.");
            Assume.That(() => SteamUser.GetSteamID(), Throws.Exception, "Steam runs in this process, so its calls do not fail.");
        }

        [Test]
        public void TryCreate_RefusesADisplayTokenThatIsNoToken()
        {
            // FR-13: a steam_display value names a localization token, which starts with #.
            foreach (string token in new[] { "Status", "#", "#a\0b", "#" + new string('x', MultiplayerLimits.MaxPresenceStatusSize) })
            {
                MultiplayerResult result = SteamBackend.TryCreate(new SteamBackendOptions { PresenceDisplayToken = token }, out SteamBackend backend);
                Assert.That(result.Reason, Is.EqualTo(MultiplayerReason.InvalidArgument), token);
                Assert.That(backend, Is.Null);
            }

            Assert.That(SteamBackend.IsValidDisplayToken(null), Is.True);
            Assert.That(SteamBackend.IsValidDisplayToken(string.Empty), Is.True);
            Assert.That(SteamBackend.IsValidDisplayToken("#Status"), Is.True);
        }

        [Test]
        public void TryCreate_RefusesANonPositiveTimeout()
        {
            MultiplayerResult result = SteamBackend.TryCreate(new SteamBackendOptions { ConnectionTimeoutSeconds = 0f }, out SteamBackend backend);

            Assert.That(result.Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(backend, Is.Null);
        }

        [Test]
        public void Presence_SetsTheTokenOnlyAfterTheStatus_AndDeletesTheStatusOnlyAfterTheToken()
        {
            // FR-13: the friends list never shows the line with an empty status, even when Steam
            // refuses a key; the group keys go in either way.
            List<string> writes = new List<string>();
            HashSet<string> refused = new HashSet<string>();
            SteamBackend.PresenceKeys keys = new SteamBackend.PresenceKeys("#Status", (key, value) =>
            {
                if (refused.Contains(key))
                {
                    return false;
                }

                writes.Add(key + "=" + value);
                return true;
            });

            refused.Add(SteamBackend.StatusKey);
            Assert.That(keys.Set("Waiting", new LobbyId(5), 2), Is.False);
            Assert.That(writes, Is.EqualTo(new[] { "steam_player_group=5", "steam_player_group_size=2" }), "no token without its status");

            refused.Clear();
            writes.Clear();
            Assert.That(keys.Set("Waiting", new LobbyId(5), 2), Is.True);
            Assert.That(writes, Is.EqualTo(new[] { "status=Waiting", "steam_display=#Status" }));

            refused.Add(SteamBackend.DisplayKey);
            writes.Clear();
            Assert.That(keys.Set(null, new LobbyId(5), 3), Is.False);
            Assert.That(writes, Is.EqualTo(new[] { "steam_player_group_size=3" }), "the status stays while the token that shows it does");

            refused.Clear();
            writes.Clear();
            Assert.That(keys.Set(null, new LobbyId(5), 3), Is.True);
            Assert.That(writes, Is.EqualTo(new[] { "steam_display=", "status=" }), "the token comes out first");

            // Disposing the backend deletes in the same order.
            Assert.That(keys.Set("Again", new LobbyId(5), 3), Is.True);
            refused.Add(SteamBackend.DisplayKey);
            writes.Clear();
            keys.Clear(deleteOnSteam: true);
            Assert.That(writes, Is.EqualTo(new[] { "steam_player_group=", "steam_player_group_size=" }));

            refused.Clear();
            writes.Clear();
            keys.Clear(deleteOnSteam: true);
            Assert.That(writes, Is.Empty, "a cleared backend forgets what it set, so it deletes nothing twice");
        }

        [Test]
        public void Presence_WithoutAToken_NeverTouchesSteamDisplay()
        {
            List<string> writes = new List<string>();
            SteamBackend.PresenceKeys keys = new SteamBackend.PresenceKeys(null, (key, value) =>
            {
                writes.Add(key + "=" + value);
                return true;
            });

            Assert.That(keys.Set("Waiting", LobbyId.None, 0), Is.True);
            Assert.That(keys.Set(null, LobbyId.None, 0), Is.True);
            Assert.That(writes, Is.EqualTo(new[] { "status=Waiting", "status=" }));
        }

        /// <summary>A start of Steam that throws what Steamworks.NET throws, before Steam starts.</summary>
        private static SteamBackend.SteamStarter Throwing(Exception exception)
        {
            return (SteamBackendOptions options, ref bool ownsSteam, out SteamBackend backend) => throw exception;
        }
    }
}
#endif
