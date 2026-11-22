using Steamworks;

namespace Mu3Library.Steam
{
    /// <summary>
    /// The calls that start Steam, run its callbacks, and shut it down, so the edit-mode tests check
    /// <see cref="SteamManager"/> without a Steam client.
    /// </summary>
    internal interface ISteamApi
    {
        /// <summary>True while Steamworks.NET runs in this process (<see cref="SteamCalls.IsSteamRunning"/>).</summary>
        public bool IsRunning { get; }

        public ESteamAPIInitResult Init(out string message);
        public void RunCallbacks();
        public void Shutdown();
    }

    internal sealed class SteamworksApi : ISteamApi
    {
        public static readonly SteamworksApi Instance = new SteamworksApi();

        public bool IsRunning => SteamCalls.IsSteamRunning;



        public ESteamAPIInitResult Init(out string message) => SteamAPI.InitEx(out message);
        public void RunCallbacks() => SteamAPI.RunCallbacks();
        public void Shutdown() => SteamAPI.Shutdown();
    }
}
