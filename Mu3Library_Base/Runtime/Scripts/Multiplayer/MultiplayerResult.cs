using System;
using System.Collections.Generic;

namespace Mu3Library.Multiplayer
{
    public readonly struct MultiplayerResult
    {
        public MultiplayerReason Reason { get; }
        public bool IsSuccess => Reason == MultiplayerReason.None;

        public static MultiplayerResult Success => default;

        public MultiplayerResult(MultiplayerReason reason)
        {
            Reason = reason;
        }

        public static MultiplayerResult Failure(MultiplayerReason reason) => new MultiplayerResult(reason);

        public override string ToString() => IsSuccess ? "Success" : Reason.ToString();
    }

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

    public readonly struct StartGameResult
    {
        public MultiplayerReason Reason { get; }
        public bool IsSuccess => Reason == MultiplayerReason.None;

        /// <summary>
        /// With <see cref="MultiplayerReason.StartConditionNotMet"/>, the players who still have to
        /// get ready. Empty when the player count is what falls short.
        /// </summary>
        public IReadOnlyList<PlayerId> NotReadyPlayers { get; }

        public StartGameResult(MultiplayerReason reason, IReadOnlyList<PlayerId> notReadyPlayers)
        {
            Reason = reason;
            NotReadyPlayers = notReadyPlayers ?? Array.Empty<PlayerId>();
        }
    }
}
