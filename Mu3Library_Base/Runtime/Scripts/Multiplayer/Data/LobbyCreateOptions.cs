using System.Collections.Generic;

namespace Mu3Library.Multiplayer.Data
{
    public sealed class LobbyCreateOptions
    {
        public LobbyVisibility Visibility { get; set; } = LobbyVisibility.FriendsOnly;

        /// <summary>
        /// The name lobby search shows. At most <see cref="MaxNameLength"/> characters. The manager
        /// writes it into the lobby data, so it follows the rules of the lobby data a game sets
        /// (<see cref="IMultiplayerManager.SetLobbyData"/>): a name that holds a NUL character, where
        /// Steam ends the name, or an unpaired UTF-16 surrogate, which every backend reads as
        /// U+FFFD, answers <see cref="MultiplayerReason.InvalidArgument"/>, since search would show
        /// another name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        public SessionRules Rules { get; set; } = SessionRules.Default;

        /// <summary>
        /// Game-defined lobby data that lobby search returns and filters on. Keys starting with
        /// <c>mu3.</c>, in any case, belong to the library and are refused; keys and values follow the rules of
        /// <see cref="IMultiplayerManager.SetLobbyData"/>, which changes them during the session,
        /// so two keys that differ only by case, such as <c>map</c> and <c>Map</c>, are refused too,
        /// and a key with an empty value is left out of the lobby, as that method deletes it.
        /// </summary>
        public Dictionary<string, string> Data { get; } = new Dictionary<string, string>();

        public const int MaxNameLength = 64;
    }
}
