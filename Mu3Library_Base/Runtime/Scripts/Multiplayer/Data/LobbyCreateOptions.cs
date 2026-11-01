using System.Collections.Generic;

namespace Mu3Library.Multiplayer.Data
{
    public sealed class LobbyCreateOptions
    {
        public LobbyVisibility Visibility { get; set; } = LobbyVisibility.FriendsOnly;

        /// <summary>The name lobby search shows. At most <see cref="MaxNameLength"/> characters.</summary>
        public string Name { get; set; } = string.Empty;

        public SessionRules Rules { get; set; } = SessionRules.Default;

        /// <summary>
        /// Game-defined lobby data that lobby search returns and filters on. Keys starting with
        /// <c>mu3.</c>, in any case, belong to the library and are refused; keys and values follow the rules of
        /// <see cref="IMultiplayerManager.SetLobbyData"/>, which changes them during the session,
        /// so two keys that differ only by case, such as <c>map</c> and <c>Map</c>, are refused too.
        /// </summary>
        public Dictionary<string, string> Data { get; } = new Dictionary<string, string>();

        public const int MaxNameLength = 64;
    }
}
