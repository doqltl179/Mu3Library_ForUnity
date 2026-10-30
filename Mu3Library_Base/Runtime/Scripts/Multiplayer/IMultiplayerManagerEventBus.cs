using System;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer
{
    public interface IMultiplayerManagerEventBus
    {
        /// <summary>
        /// Raised once for every state transition. A failed request or an ended session carries
        /// its reason. A handler that starts another transition, such as joining again from the
        /// change to Offline, raises that change before the remaining handlers receive this one,
        /// so read <see cref="SessionStateChange.Current"/> rather than
        /// <see cref="IMultiplayerManager.State"/> inside a handler.
        /// </summary>
        public event Action<SessionStateChange> OnStateChanged;

        /// <summary>
        /// Raised when another player joins the session this player is in. A joining player reads
        /// the players already there from <see cref="IMultiplayerManager.Participants"/> instead.
        /// </summary>
        public event Action<Participant> OnParticipantJoined;

        /// <summary>Raised when another player leaves the session, with the reason.</summary>
        public event Action<Participant, MultiplayerReason> OnParticipantLeft;

        /// <summary>
        /// Raised when a player's ready flag changes, this player's own included. Returning to the
        /// lobby clears every ready and late-join flag at once, which
        /// <see cref="OnReturnedToLobby"/> reports instead.
        /// </summary>
        public event Action<Participant> OnParticipantChanged;

        public event Action<SessionRules> OnRulesChanged;

        /// <summary>Raised after the state turns to <see cref="SessionState.InGame"/>, with the start data.</summary>
        public event Action<ReadOnlyMemory<byte>> OnGameStarted;

        /// <summary>Raised after a game ends and the state is back to <see cref="SessionState.Lobby"/>.</summary>
        public event Action OnReturnedToLobby;

        /// <summary>
        /// Raised when the player asked, outside the game, to join a lobby. The manager then leaves
        /// any current session and joins it, as <see cref="IMultiplayerManager.JoinLobby"/> does,
        /// unless a handler already ended or started a session itself; then the manager leaves
        /// the decision to that handler.
        /// </summary>
        public event Action<LobbyId> OnJoinRequested;
    }
}
