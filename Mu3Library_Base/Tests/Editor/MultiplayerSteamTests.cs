#if MU3LIBRARY_STEAMWORKS_SUPPORT && UNITY_STANDALONE
using System;
using System.Text.RegularExpressions;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using Mu3Library.Multiplayer.Steam;
using NUnit.Framework;
using Steamworks;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mu3Library.Tests
{
    /// <summary>
    /// The parts of <see cref="SteamBackend"/> that need no Steam client: how Steam's answers map
    /// to the library's reasons, the launch arguments, and the editor fallback (FR-1, FR-5 to
    /// FR-7, FR-19, FR-39). The rest is checked by hand against Steam (spec section 13).
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
            // FR-1: a missing steam_api64 throws DllNotFoundException, an outdated one
            // EntryPointNotFoundException, and one of another architecture BadImageFormatException.
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

        /// <summary>A start of Steam that throws what Steamworks.NET throws, before Steam starts.</summary>
        private static SteamBackend.SteamStarter Throwing(Exception exception)
        {
            return (SteamBackendOptions options, ref bool ownsSteam, out SteamBackend backend) => throw exception;
        }
    }
}
#endif
