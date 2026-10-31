using System.Collections.Generic;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Local.Machine
{
    /// <summary>
    /// One lobby as its owner lists it in the network's folder.
    /// </summary>
    internal sealed class MachineDirectoryEntry
    {
        public LobbyId Lobby;
        public PlayerId Owner;
        public int Port;

        /// <summary>When the owner last wrote its file, in UTC ticks.</summary>
        public long Written;
        public LobbyVisibility Visibility;
        public int Capacity;
        public int MemberCount;
        public bool IsJoinable;
        public readonly Dictionary<string, string> Data = new Dictionary<string, string>();
    }
}
