using System;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using UnityEngine;
#if MULTIPLAYER_STEAMWORKS_SUPPORT && UNITY_STANDALONE
using Mu3Library.Multiplayer.Steam;
#endif

namespace Mu3Library.Sample.Multiplayer
{
    /// <summary>
    /// Picks the backend the sample plays on: Steam when Steamworks.NET is installed and the Steam
    /// client runs, otherwise the local backend on a network that every process of this computer
    /// shares, so that an editor and a player build, or two builds, meet without Steam. The
    /// manager does not own its backend; the core disposes this after the manager.
    /// </summary>
    public sealed class SampleBackend : IDisposable
    {
        public IMultiplayerBackend Backend { get; }

        /// <summary>
        /// True on Steam, where the overlay sends invites. The local backend has no friends to
        /// invite, so there a player shares the lobby ID and the other joins with it.
        /// </summary>
        public bool SupportsInvites
        {
            get
            {
#if MULTIPLAYER_STEAMWORKS_SUPPORT && UNITY_STANDALONE
                return Backend is SteamBackend;
#else
                return false;
#endif
            }
        }

        private SampleBackend(IMultiplayerBackend backend)
        {
            Backend = backend;
        }

        /// <param name="gameId">Names the machine-wide local network, as it names the game's lobbies.</param>
        /// <param name="steamAppId">The app ID Steam starts with; Steam's test app 480 works during development.</param>
        /// <param name="localPlayerName">The player's name on the local backend; Steam uses the account's name.</param>
        /// <param name="forceLocal">Skips Steam, for example to try two builds on one Steam account.</param>
        public static SampleBackend Create(string gameId, uint steamAppId, string localPlayerName, bool forceLocal)
        {
            LocalNetwork network = LocalNetwork.CreateMachineWide(gameId);

#if MULTIPLAYER_STEAMWORKS_SUPPORT && UNITY_STANDALONE
            if (!forceLocal)
            {
                SteamBackendOptions options = new SteamBackendOptions
                {
                    AppId = steamAppId,
                    // In the editor, when Steam cannot start, CreateOrFallback hands back a local
                    // backend on this network with a warning.
                    EditorFallbackNetwork = network,
                    EditorFallbackPlayerName = localPlayerName,
                };

                MultiplayerResult result = SteamBackend.CreateOrFallback(options, out IMultiplayerBackend backend);
                if (result.IsSuccess)
                {
                    return new SampleBackend(backend);
                }

                // A player build never falls back by itself. The sample does, so that it also
                // runs on a computer without Steam.
                Debug.LogWarning($"[MultiplayerSample] Steam is unavailable ({result.Reason}); the sample uses the local backend.");
            }
#endif

            return new SampleBackend(new LocalBackend(network, localPlayerName));
        }

        /// <summary>Opens the Steam overlay's invite dialog for the lobby; elsewhere it answers <see cref="MultiplayerReason.NotSupported"/>.</summary>
        public MultiplayerResult OpenInviteDialog(LobbyId lobby)
        {
#if MULTIPLAYER_STEAMWORKS_SUPPORT && UNITY_STANDALONE
            if (Backend is SteamBackend steam)
            {
                return steam.OpenInviteDialog(lobby);
            }
#endif

            return MultiplayerResult.Failure(MultiplayerReason.NotSupported);
        }

        public void Dispose()
        {
            Backend.Dispose();
        }
    }
}
