using System;
using System.Collections.Generic;
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
        /// Raised when another player's connection drops during a game and the host keeps their
        /// slot for <see cref="SessionRules.ReconnectReservationSeconds"/>, with the player as
        /// <see cref="Participant.IsConnected"/> false. <see cref="OnParticipantReconnected"/>
        /// follows when they come back, or <see cref="OnParticipantLeft"/> with
        /// <see cref="MultiplayerReason.ConnectionLost"/> when the time runs out. The player whose
        /// own connection dropped sees <see cref="OnStateChanged"/> turn to
        /// <see cref="SessionState.Reconnecting"/> instead.
        /// </summary>
        public event Action<Participant> OnParticipantDisconnected;

        /// <summary>
        /// Raised on every player, the returning one included, when a player comes back to the
        /// slot the host kept for them, with the player as <see cref="Participant.IsConnected"/>
        /// true. The returning player raises it last, after its state change and after
        /// <see cref="OnReturnedToLobby"/> or <see cref="OnGameStarted"/> when the game it left
        /// ended or another started meanwhile. It takes the roster, rules, lobby data, and start
        /// data the host holds then, without events for what changed while it was away, and
        /// nothing that was sent to it meanwhile reaches it, so a game brings it up to date here.
        /// </summary>
        public event Action<Participant> OnParticipantReconnected;

        /// <summary>
        /// Raised when a player's ready flag or loaded flag (<see cref="Participant.IsLoaded"/>)
        /// changes, this player's own included. Returning to the lobby clears every ready,
        /// late-join, and loaded flag at once, which <see cref="OnReturnedToLobby"/> reports
        /// instead, so does the start of a game for the loaded flags, and a change of a player's
        /// data raises <see cref="OnParticipantDataChanged"/>.
        /// </summary>
        public event Action<Participant> OnParticipantChanged;

        /// <summary>
        /// Raised when a value of a player's participant data changes, this player's own included,
        /// with the player as the host now reports them and the key that changed. A joining player
        /// reads the data set before it joined from <see cref="IMultiplayerManager.Participants"/> instead.
        /// </summary>
        public event Action<Participant, string> OnParticipantDataChanged;

        /// <summary>
        /// Raised on every player, the host included, when the host sets a value of the lobby data,
        /// with the key and the new value. A joining player reads the data set before it joined
        /// from <see cref="IMultiplayerManager.LobbyData"/> instead.
        /// </summary>
        public event Action<string, string> OnLobbyDataChanged;

        /// <summary>
        /// Raised for every lobby chat message, this player's own included, with its text and its
        /// sender as the connection proved it: the host names the sender from the connection a
        /// message arrived on, never from what the message says.
        /// </summary>
        public event Action<Participant, string> OnChatReceived;

        public event Action<SessionRules> OnRulesChanged;

        /// <summary>Raised after the state turns to <see cref="SessionState.InGame"/>, with the start data.</summary>
        public event Action<ReadOnlyMemory<byte>> OnGameStarted;

        /// <summary>Raised after a game ends and the state is back to <see cref="SessionState.Lobby"/>.</summary>
        public event Action OnReturnedToLobby;

        /// <summary>
        /// Raised on the host, once a game, when every player the game waits for reported with
        /// <see cref="IMultiplayerManager.ReportLoaded"/> before
        /// <see cref="SessionRules.LoadTimeoutSeconds"/> passed; the last one may be a player
        /// who left instead.
        /// </summary>
        public event Action OnAllPlayersLoaded;

        /// <summary>
        /// Raised on the host, once a game, when <see cref="SessionRules.LoadTimeoutSeconds"/>
        /// passed since the game started before every player it waits for reported with
        /// <see cref="IMultiplayerManager.ReportLoaded"/>, with the players who did not, in roster
        /// order. The wait ends there: later reports still set <see cref="Participant.IsLoaded"/>,
        /// but neither this event nor <see cref="OnAllPlayersLoaded"/> follows.
        /// </summary>
        public event Action<IReadOnlyList<PlayerId>> OnLoadTimedOut;

        /// <summary>
        /// Raised when the player asked, outside the game, to join a lobby. The manager then leaves
        /// any current session and joins it, as <see cref="IMultiplayerManager.JoinLobby"/> does,
        /// unless a handler already ended or started a session itself; then the manager leaves
        /// the decision to that handler.
        /// </summary>
        public event Action<LobbyId> OnJoinRequested;

        /// <summary>
        /// Raised on the host when a client goes past a rate limit of the rules,
        /// <see cref="SessionRules.MaxMessagesPerSecond"/> or <see cref="SessionRules.MaxBytesPerSecond"/>,
        /// at most once a second for each client: for the first of its packets in a one-second
        /// window that goes past one. The host drops that client's game and chat messages past the
        /// limit for the rest of the window, and with <see cref="SessionRules.RateLimitKickSeconds"/>
        /// it kicks a client that keeps going past it, which <see cref="OnParticipantLeft"/>
        /// reports with <see cref="MultiplayerReason.RateLimited"/> instead.
        /// </summary>
        public event Action<Participant> OnRateLimitExceeded;
    }
}
