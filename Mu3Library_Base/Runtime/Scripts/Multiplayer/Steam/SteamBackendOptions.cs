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
        /// returns a <see cref="LocalBackend"/> on this network instead. Null turns the fallback
        /// off; a player build never falls back.
        /// </summary>
        public LocalNetwork EditorFallbackNetwork { get; set; }

        public string EditorFallbackPlayerName { get; set; } = "Editor";
    }
}
