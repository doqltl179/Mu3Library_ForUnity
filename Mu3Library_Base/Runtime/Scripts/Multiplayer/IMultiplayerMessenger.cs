using System;
using System.Collections.Generic;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer
{
    /// <summary>
    /// Game messages between the players of a session. Every message travels through the host,
    /// which checks who sent it and where it may go before passing it on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Message types are numbers the game chooses. Every player registers the same types with the
    /// same policy; a player drops a type it has not registered, and the host drops one it has not
    /// registered even when it only passes the message on. Registrations outlive sessions.
    /// </para>
    /// <para>
    /// The host passes a client's message on before its own handler runs, so the handler cannot
    /// stop it. To check a client's message before anyone else sees it, register its type with
    /// <see cref="MessagePolicy.NoClientRelay"/> and have the host send what it accepts.
    /// </para>
    /// </remarks>
    public interface IMultiplayerMessenger
    {
        /// <summary>
        /// Largest payload this player may send: <see cref="MultiplayerConfig.MaxMessageSize"/>,
        /// or in a session joined as a client, the smaller of that and the host's limit. A
        /// received message can be as large as the host's limit.
        /// </summary>
        public int MaxMessageSize { get; }

        /// <summary>
        /// Registers a message type. A null handler registers the type without handling it here,
        /// as a host that only passes the type on between clients does. A type registers once;
        /// unregister it before registering it again.
        /// </summary>
        public MultiplayerResult RegisterMessage(ushort messageType, MessageHandler handler, MessagePolicy policy = MessagePolicy.Default);

        /// <summary>False when the type was not registered.</summary>
        public bool UnregisterMessage(ushort messageType);

        /// <summary>
        /// Sends a message in the lobby or in a game. A copy addressed to this player is handled
        /// before the call returns; the others arrive at their players' next update after the
        /// network delivers them. A reliable message arrives in the order it was sent from the
        /// same sender; an unreliable one may be lost.
        /// </summary>
        /// <returns>
        /// <see cref="MultiplayerReason.MessageTooLarge"/> above <see cref="MaxMessageSize"/>;
        /// <see cref="MultiplayerReason.NotHost"/> when the policy keeps this player from sending
        /// it there; <see cref="MultiplayerReason.InvalidArgument"/> for an unset target, an
        /// unknown delivery mode, or <see cref="MessageTarget.To"/> a player outside the session
        /// (<see cref="MessageTarget.AllExcept"/> such a player reaches everyone);
        /// <see cref="MultiplayerReason.ConnectionLost"/> when the message cannot leave, or when
        /// the one player it is for is dropped because a reliable message cannot reach them;
        /// <see cref="MultiplayerReason.InvalidState"/> outside a session; and
        /// <see cref="MultiplayerReason.NotInitialized"/> before initialization.
        /// </returns>
        public MultiplayerResult Send(ushort messageType, ReadOnlySpan<byte> payload, MessageTarget target, DeliveryMode deliveryMode = DeliveryMode.Reliable);

        /// <summary>
        /// Fills <paramref name="results"/>, which it clears first, with one entry per registered
        /// message type: how many messages of the type this player sent, and how many from other
        /// players reached it, since the type was registered. The counts outlive sessions, and
        /// unregistering a type drops them. With room in the list it allocates nothing, so a game
        /// can show the counts every frame, as the editor's Multiplayer Debugger window shows them.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="results"/> is null.</exception>
        public void GetMessageCounts(List<MessageCounts> results);
    }
}
