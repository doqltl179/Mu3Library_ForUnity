using System;
using System.Collections.Generic;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer
{
    /// <summary>The events of a <see cref="IMultiplayerManager"/>.</summary>
    /// <remarks>
    /// The manager invokes the handlers of an event one by one, in the order they subscribed,
    /// and catches an exception that a handler throws: the handlers after it still receive the
    /// event, so a bug in one system's handler does not keep the event from the others, and the
    /// session and every later event go on. It logs the exception with <c>Debug.LogException</c>,
    /// with a warning that later ones go unlogged, only the first time the handlers of that event
    /// throw that exception type in a session, so another player's chat flood cannot become a log
    /// flood while a handler of <see cref="OnChatReceived"/> has a bug. The rule is the same whether a packet from another
    /// player or a call of this player's own raised the event: many events come from both, such
    /// as <see cref="OnStateChanged"/> or the host's own <see cref="OnLobbyDataChanged"/>, so a
    /// handler's failure is logged the same way wherever the event came from. The change to
    /// Offline that ends a session and the events raised between sessions count with that
    /// session, and the next session logs each cause again. Catch an exception in the handler to
    /// log each one yourself. The callbacks that answer this player's own calls, such as those of
    /// <see cref="IMultiplayerManager.CreateLobby"/> and <see cref="IMultiplayerManager.JoinLobby"/>,
    /// log every exception they throw, since each answers one call. Subscribing and unsubscribing
    /// copy the event's list of handlers, so raising an event allocates nothing, and a handler that
    /// subscribes or unsubscribes while the event is raised changes only the next raise, as with a
    /// multicast delegate.
    /// </remarks>
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
        /// Raised on a client back in its slot when it could not enter its lobby again, with the
        /// reason. The lobby dropped the player while its connection was down, as a Steam lobby
        /// may, or a moment after it came back, and then refused it until
        /// <see cref="MultiplayerConfig.JoinTimeoutSeconds"/> passed from the return, or banned
        /// it. Steam answers a lobby it will not let the player into as
        /// <see cref="MultiplayerReason.LobbyNotFound"/>. The session goes on over the host's
        /// connection, but outside the lobby the player does not show in the friends list's
        /// group of the session, and only that connection tells it that the host left. Raised
        /// at most once for each return.
        /// </summary>
        public event Action<MultiplayerReason> OnLobbyRejoinFailed;

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
        /// with the player as the host now reports them and the key that changed. When the player
        /// deleted the key (<see cref="IMultiplayerManager.DeleteParticipantData"/>), the
        /// participant's <see cref="Participant.Data"/> no longer holds it. A joining player reads
        /// the data set before it joined from <see cref="IMultiplayerManager.Participants"/> instead.
        /// </summary>
        public event Action<Participant, string> OnParticipantDataChanged;

        /// <summary>
        /// Raised on every player, the host included, when the host sets a value of the lobby data,
        /// with the key and the new value, or deletes a key (<see cref="IMultiplayerManager.DeleteLobbyData"/>,
        /// or <see cref="IMultiplayerManager.SetLobbyData"/> with an empty value), with the key and
        /// a null value. A joining player reads the data set before it joined
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
        /// Raised on every player, the host included, when the host began another load phase of
        /// the running game with <see cref="IMultiplayerManager.BeginLoadPhase"/>, with the data
        /// it began the phase with, such as the level to load. Every
        /// <see cref="Participant.IsLoaded"/> is clear by then: load what the data names, then
        /// call <see cref="IMultiplayerManager.ReportLoaded"/>. A player who comes back to its
        /// slot in the same game after the phase began hears it as it comes back, before
        /// <see cref="OnParticipantReconnected"/>, and a player who joins the game while a phase
        /// the host began runs hears it after <see cref="OnGameStarted"/>, both with the data of
        /// the phase that runs then.
        /// </summary>
        public event Action<ReadOnlyMemory<byte>> OnLoadPhaseBegan;

        /// <summary>
        /// Raised on the host, once a load phase, when every player the phase waits for reported
        /// with <see cref="IMultiplayerManager.ReportLoaded"/> before
        /// <see cref="SessionRules.LoadTimeoutSeconds"/> passed; the last one may be a player
        /// who left instead. A game's first load phase begins as it starts, and each
        /// <see cref="IMultiplayerManager.BeginLoadPhase"/> begins another.
        /// </summary>
        public event Action OnAllPlayersLoaded;

        /// <summary>
        /// Raised on the host, once a load phase, when <see cref="SessionRules.LoadTimeoutSeconds"/>
        /// passed since the phase began, as the game started or with
        /// <see cref="IMultiplayerManager.BeginLoadPhase"/>, before every player it waits for
        /// reported with <see cref="IMultiplayerManager.ReportLoaded"/>, with the players who did
        /// not, in roster order. The wait ends there: later reports still set
        /// <see cref="Participant.IsLoaded"/>, but neither this event nor
        /// <see cref="OnAllPlayersLoaded"/> follows.
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
        /// window that goes past one; an account outside the session, such as one whose hello to
        /// join again waits for room, raises nothing. The host drops that client's game and chat
        /// messages past the limit for the rest of the window and holds back its ready changes,
        /// load reports, and data requests for later windows, and with
        /// <see cref="SessionRules.RateLimitKickSeconds"/> it kicks a client that keeps going past
        /// it, which <see cref="OnParticipantLeft"/> reports with
        /// <see cref="MultiplayerReason.RateLimited"/> instead.
        /// </summary>
        public event Action<Participant> OnRateLimitExceeded;

        /// <summary>
        /// Raised when a texture that <see cref="IMultiplayerManager.LoadAvatar"/> handed out shows
        /// a new image, with the player, the size, and that same texture. The backend told that
        /// the player's avatar changed, as Steam does when a player picks a new picture and when
        /// it downloaded the picture of an account it did not know, which replaces the default
        /// image Steam handed out first; the manager read again every size it holds of that
        /// player and drew the new image into the texture in place. A game that
        /// shows the texture needs to do nothing, as it shows the new image already; one that
        /// copied its pixels copies them again. A texture keeps its size on Steam, whose sizes
        /// are fixed, and takes the new image's size on a backend that changes it. Nothing is
        /// raised for an avatar the manager does not hold, an image that did not change, or one
        /// that could not be read, which leaves the image the texture shows: the manager tries a
        /// failed read again up to 3 times, 1 second after each failure, and raises the event once
        /// a try brings a new image (see <see cref="IMultiplayerManager.LoadAvatar"/>).
        /// </summary>
        public event Action<AvatarResult> OnAvatarChanged;
    }
}
