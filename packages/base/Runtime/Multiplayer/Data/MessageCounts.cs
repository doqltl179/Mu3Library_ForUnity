using System.Globalization;

namespace Mu3Library.Multiplayer.Data
{
    /// <summary>
    /// How many messages of one type a player sent and received since it registered the type,
    /// and how many payload bytes they carried, as <see cref="IMultiplayerMessenger.GetMessageCounts"/>
    /// reports them. The bytes are the payloads the game sent and handled, without the library's
    /// header of each message.
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

        /// <summary>The payload bytes of the messages <see cref="Sent"/> counts, each once.</summary>
        public long SentBytes { get; }

        /// <summary>The payload bytes of the messages <see cref="Received"/> counts.</summary>
        public long ReceivedBytes { get; }

        public MessageCounts(ushort messageType, long sent, long received) : this(messageType, sent, received, 0L, 0L)
        {
        }

        public MessageCounts(ushort messageType, long sent, long received, long sentBytes, long receivedBytes)
        {
            MessageType = messageType;
            Sent = sent;
            Received = received;
            SentBytes = sentBytes;
            ReceivedBytes = receivedBytes;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "type {0}: {1} sent ({2} B), {3} received ({4} B)",
                MessageType, Sent, SentBytes, Received, ReceivedBytes);
        }
    }
}
