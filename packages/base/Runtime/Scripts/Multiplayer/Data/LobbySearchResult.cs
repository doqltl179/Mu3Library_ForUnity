using System;
using System.Collections.Generic;

namespace Mu3Library.Multiplayer.Data
{
    public readonly struct LobbySearchResult
    {
        public MultiplayerReason Reason { get; }
        public bool IsSuccess => Reason == MultiplayerReason.None;
        public IReadOnlyList<LobbyInfo> Lobbies { get; }

        public LobbySearchResult(MultiplayerReason reason, IReadOnlyList<LobbyInfo> lobbies)
        {
            Reason = reason;
            Lobbies = lobbies ?? Array.Empty<LobbyInfo>();
        }
    }
}
