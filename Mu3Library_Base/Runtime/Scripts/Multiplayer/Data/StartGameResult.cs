using System;
using System.Collections.Generic;

namespace Mu3Library.Multiplayer.Data
{
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
