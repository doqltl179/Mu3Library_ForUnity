using System;
using Mu3Library.DI;
using Steamworks;
using UnityEngine;

namespace Mu3Library.Steam
{
    /// <summary>
    /// Starts Steam for the game, runs Steam's callbacks, and shuts Steam down, so a game uses Steam
    /// without the multiplayer backend, for example for the achievements of
    /// <c>Mu3Library.Steam.Stats</c> or the Steam Cloud storage of <c>Mu3Library.Steam.Cloud</c>,
    /// which use the Steam the game started and start none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="TryStart"/> starts Steamworks.NET when it does not run yet, and the manager then
    /// owns that Steam (<see cref="OwnsSteam"/>) and shuts it down when disposed. When Steam runs
    /// already, started by the game or by <c>SteamBackend</c>, the manager uses it and leaves it
    /// running. Call <see cref="Update"/> every frame, which runs Steam's callbacks, and
    /// <see cref="Dispose"/> when the game is done with Steam; a DI core that registers the manager
    /// does both. A game that also runs a multiplayer session on <c>SteamBackend</c> disposes the
    /// multiplayer manager and the backend first, so the other players learn the reason the session
    /// ended before Steam shuts down.
    /// </para>
    /// <para>
    /// No exception reaches the game for Steam's sake. A run of Steam's callbacks that fails in
    /// Steam's native library, as an outdated <c>steam_api64</c> that still starts Steam can, logs a
    /// warning, and the manager is no longer available (<see cref="IsAvailable"/>) and runs no more
    /// callbacks; disposed, it still shuts down a Steam it started. Once the manager finds Steam not
    /// running, after the game shut it down under the manager, it takes that Steam for gone: it
    /// stays unavailable, and does not shut down a Steam started again later, which it did not start.
    /// </para>
    /// <para>
    /// The assembly compiles only with Steamworks.NET 2025.164.1 or later, and only in the editor
    /// and in 64-bit standalone players, as <c>Mu3Library.Steam.Multiplayer</c> does; game code that
    /// uses it needs the same gate (the package README's Platforms And The Gate).
    /// </para>
    /// </remarks>
    public sealed class SteamManager : IUpdatable, IDisposable
    {
        private readonly ISteamApi _steam;
        private bool _isDisposed = false;

        // True once a run of Steam's callbacks threw what SteamCalls.IsSteamUnavailable tells.
        private bool _isBroken = false;

        // True once the manager found Steam not running, for good; see SteamRuns.
        private bool _hasSeenSteamStop = false;

        /// <summary>True when this manager started Steam, and so shuts it down when disposed.</summary>
        public bool OwnsSteam { get; }

        /// <summary>
        /// False once disposed, once the game shut Steam down under this manager, also after the game
        /// started Steam again, or once a run of Steam's callbacks failed in Steam's native library.
        /// </summary>
        public bool IsAvailable => !_isDisposed && !_isBroken && SteamRuns;

        // Whether Steamworks.NET runs. Once the manager finds it not running, it takes Steam for gone
        // for good, so a Steam the game starts again is one this manager neither started nor shuts down.
        private bool SteamRuns
        {
            get
            {
                if (!_hasSeenSteamStop && !_steam.IsRunning)
                {
                    _hasSeenSteamStop = true;
                }

                return !_hasSeenSteamStop;
            }
        }



        private SteamManager(ISteamApi steam, bool ownsSteam)
        {
            _steam = steam;
            OwnsSteam = ownsSteam;
        }

        /// <summary>
        /// Starts Steam with <paramref name="appId"/> unless it runs already, and makes the manager.
        /// </summary>
        /// <param name="appId">
        /// The app ID to start Steam with when it does not run yet. 0 leaves it to
        /// <c>steam_appid.txt</c> or to the launch through Steam. Steam's test app 480 works during
        /// development.
        /// </param>
        /// <returns>
        /// <see cref="SteamStartFailureReason.SteamNotRunning"/> when the Steam client is not running
        /// or Steam cannot start, a Steam native library that cannot be loaded or lacks a function
        /// included, and <see cref="SteamStartFailureReason.VersionMismatch"/> for a Steam client
        /// older than the native library. Either logs a warning and leaves
        /// <paramref name="manager"/> null; no exception reaches the caller for Steam's sake.
        /// </returns>
        public static SteamStartResult TryStart(uint appId, out SteamManager manager)
        {
            return TryStartWith(SteamworksApi.Instance, appId, out manager);
        }

        /// <summary>
        /// <see cref="TryStart"/> on the Steam calls of <paramref name="steam"/>; the tests hand in a
        /// stand-in.
        /// </summary>
        internal static SteamStartResult TryStartWith(ISteamApi steam, uint appId, out SteamManager manager)
        {
            manager = null;
            try
            {
                ESteamAPIInitResult result = SteamCalls.Start(steam, appId, out bool started, out string message);
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    Debug.LogWarning($"[Steam] Steam could not start ({result}): {message}");
                    return SteamStartResult.Failure(FromInitResult(result), message);
                }

                manager = new SteamManager(steam, started);
                return SteamStartResult.Success;
            }
            catch (Exception exception) when (SteamCalls.IsSteamUnavailable(exception, out Exception cause))
            {
                Debug.LogWarning($"[Steam] Steam could not start ({cause.GetType().Name}): {cause.Message}");
                return SteamStartResult.Failure(SteamStartFailureReason.SteamNotRunning, cause.Message);
            }
        }

        internal static SteamStartFailureReason FromInitResult(ESteamAPIInitResult result)
        {
            switch (result)
            {
                case ESteamAPIInitResult.k_ESteamAPIInitResult_OK: return SteamStartFailureReason.None;
                case ESteamAPIInitResult.k_ESteamAPIInitResult_VersionMismatch: return SteamStartFailureReason.VersionMismatch;
                default: return SteamStartFailureReason.SteamNotRunning;
            }
        }

        /// <summary>Runs Steam's callbacks while the manager is available.</summary>
        public void Update()
        {
            if (!IsAvailable)
            {
                return;
            }

            try
            {
                _steam.RunCallbacks();
            }
            catch (Exception exception) when (SteamCalls.IsSteamUnavailable(exception, out Exception cause))
            {
                _isBroken = true;
                Debug.LogWarning($"[Steam] Steam's callbacks no longer run: a Steam call failed ({cause.GetType().Name}): {cause.Message}");
            }
        }

        /// <summary>
        /// Shuts Steam down when this manager started it and it still runs; otherwise leaves Steam
        /// as it is.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            bool steamRuns = SteamRuns;
            _isDisposed = true;
            if (!OwnsSteam || !steamRuns)
            {
                return;
            }

            try
            {
                _steam.Shutdown();
            }
            catch (Exception exception) when (SteamCalls.IsSteamUnavailable(exception, out Exception cause))
            {
                Debug.LogWarning($"[Steam] Steam did not shut down: a Steam call failed ({cause.GetType().Name}): {cause.Message}");
            }
        }
    }
}
