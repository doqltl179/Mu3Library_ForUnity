using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;

namespace Mu3Library.Multiplayer.Steam
{
    public sealed class SteamBackendOptions
    {
        /// <summary>
        /// The app ID to start Steam with when the game has not started it already. 0 leaves it
        /// to <c>steam_appid.txt</c> or to the launch through Steam. Steam's test app 480 works
        /// during development; give lobbies a unique <see cref="MultiplayerConfig.GameId"/> there.
        /// </summary>
        public uint AppId { get; set; }

        /// <summary>
        /// Runs <c>SteamAPI.RunCallbacks</c> on every update. Harmless when the game runs them as
        /// well; turn it off only when the game must decide exactly when Steam callbacks run.
        /// </summary>
        public bool RunSteamCallbacks { get; set; } = true;

        /// <summary>
        /// False, the default, routes every connection through Valve's relays, so no player
        /// learns another player's IP address. True also allows direct connections.
        /// </summary>
        public bool AllowDirectConnections { get; set; }

        /// <summary>How long a connected player may go silent before the connection counts as lost.</summary>
        public float ConnectionTimeoutSeconds { get; set; } = 10f;

        /// <summary>The P2P virtual port. Change it only to keep several independent games of one app apart.</summary>
        public int VirtualPort { get; set; }

        /// <summary>
        /// In the editor, when Steam cannot start, <see cref="SteamBackend.CreateOrFallback"/>
        /// returns a <see cref="LocalBackend"/> on this network instead. A network from
        /// <see cref="LocalNetwork.CreateMachineWide"/> lets that editor play with other editors and
        /// builds on the same computer. Null turns the fallback off; a player build never falls
        /// back.
        /// </summary>
        public LocalNetwork EditorFallbackNetwork { get; set; }

        public string EditorFallbackPlayerName { get; set; } = "Editor";

        /// <summary>
        /// A rich presence localization token, such as <c>#Status</c>, that the backend sets as
        /// Steam's <c>steam_display</c> key while a status from
        /// <see cref="IMultiplayerManager.SetPresenceStatus"/> is set, and deletes with the status.
        /// Steam's friends list shows a friend's rich presence line only through that key, so a
        /// game that wants the status there defines the token for its own app ID in the rich
        /// presence localization on the Steamworks partner site, for example
        /// <c>"#Status" "%status%"</c>, where <c>%status%</c> stands for the status. Null or
        /// empty, the default, leaves <c>steam_display</c> to the game. A token starts with
        /// <c>#</c> and holds at most <see cref="MultiplayerLimits.MaxPresenceStatusSize"/>
        /// UTF-8 bytes; another value makes <see cref="SteamBackend.TryCreate"/> answer
        /// <see cref="MultiplayerReason.InvalidArgument"/>.
        /// </summary>
        public string PresenceDisplayToken { get; set; }
    }
}
