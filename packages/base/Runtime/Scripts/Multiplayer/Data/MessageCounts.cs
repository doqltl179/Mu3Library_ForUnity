using System.Globalization;

namespace Mu3Library.Multiplayer.Data
{
    /// <summary>
    /// How many messages of one type a player sent and received since it registered the type,
    /// as <see cref="IMultiplayerMessenger.GetMessageCounts"/> reports them.
    /// </summary>
    public readonly struct MessageCounts
    {
        public ushort MessageType { get; }

        /// <summary>
        /// Messages of the type this player sent. Each <see cref="IMultiplayerMessenger.Send"/>
        /// that succeeded counts once, however many players it reached.
        /// </summary>
        public long Sent { get; }

        /// <summary>
        /// Messages of the type that other players addressed to this player and that it took in,
        /// whether it handles them or registered the type without a handler. Messages it dropped,
        /// such as a host-only type that came from a client, messages the host only passed on
        /// between clients, and this player's own copies do not count.
        /// </summary>
        public long Received { get; }

        public MessageCounts(ushort messageType, long sent, long received)
        {
            MessageType = messageType;
            Sent = sent;
            Received = received;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "type {0}: {1} sent, {2} received", MessageType, Sent, Received);
        }
    }
}
