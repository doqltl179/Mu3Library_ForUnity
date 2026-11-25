using System;
using System.Globalization;
using Steamworks;

namespace Mu3Library.Steam
{
    /// <summary>
    /// What every Steam assembly of the package shares: starting Steam, telling whether it runs, and
    /// telling an exception that says Steam cannot run in this process.
    /// </summary>
    internal static class SteamCalls
    {
        /// <summary>
        /// True while Steamworks.NET runs in this process, and false also when its managed side
        /// cannot load.
        /// </summary>
        public static bool IsSteamRunning
        {
            get
            {
                try
                {
                    return CallbackDispatcher.IsInitialized;
                }
                catch (Exception exception) when (IsSteamUnavailable(exception))
                {
                    return false;
                }
            }
        }



        /// <summary>
        /// Starts Steamworks.NET when it does not run yet, setting <paramref name="started"/> once it
        /// did; a Steam that runs already answers OK and is left as it is. An
        /// <paramref name="appId"/> other than 0 is the app ID Steam starts with; 0 leaves it to
        /// <c>steam_appid.txt</c> or to the launch through Steam. A Steam native library that cannot
        /// be loaded or lacks a function throws, as <see cref="IsSteamUnavailable(Exception, out Exception)"/> tells.
        /// </summary>
        public static ESteamAPIInitResult Start(uint appId, out bool started, out string message)
        {
            return Start(SteamworksApi.Instance, appId, out started, out message);
        }

        public static ESteamAPIInitResult Start(ISteamApi steam, uint appId, out bool started, out string message)
        {
            started = false;
            message = null;

            // The dispatcher, not the native client, tells whether Steamworks.NET runs: after a
            // domain reload the native Steam stays up while the dispatcher starts over, and
            // without it no callback would ever arrive. Steam counts its own initializations.
            if (steam.IsRunning)
            {
                return ESteamAPIInitResult.k_ESteamAPIInitResult_OK;
            }

            if (appId != 0)
            {
                // Steam reads these before steam_appid.txt; a launch through Steam sets them itself.
                string id = appId.ToString(CultureInfo.InvariantCulture);
                Environment.SetEnvironmentVariable("SteamAppId", id);
                Environment.SetEnvironmentVariable("SteamGameId", id);
            }

            ESteamAPIInitResult result = steam.Init(out message);
            started = result == ESteamAPIInitResult.k_ESteamAPIInitResult_OK;
            return result;
        }

        /// <summary>
        /// True for an exception that tells Steam cannot run in this process, with the exception
        /// that caused it: Steamworks.NET's own check that Steam started
        /// (<see cref="InvalidOperationException"/>), and a native library that is missing, lacks a
        /// function, or is built for another architecture, also inside a
        /// <see cref="TypeInitializationException"/>. Which of them a missing, outdated, or
        /// wrong-architecture <c>steam_api64</c> causes depends on the runtime. Any other exception is
        /// a mistake in the code and reaches the caller.
        /// </summary>
        public static bool IsSteamUnavailable(Exception exception, out Exception cause)
        {
            cause = exception;
            while (cause is TypeInitializationException && cause.InnerException != null)
            {
                cause = cause.InnerException;
            }

            return cause is DllNotFoundException
                || cause is EntryPointNotFoundException
                || cause is BadImageFormatException
                || cause is InvalidOperationException;
        }

        /// <inheritdoc cref="IsSteamUnavailable(Exception, out Exception)"/>
        public static bool IsSteamUnavailable(Exception exception)
        {
            return IsSteamUnavailable(exception, out _);
        }
    }
}
