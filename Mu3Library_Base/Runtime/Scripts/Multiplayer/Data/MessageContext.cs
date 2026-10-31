namespace Mu3Library.Multiplayer.Data
{
    /// <summary>
    /// Where a received message came from.
    /// </summary>
    public readonly struct MessageContext
    {
        /// <summary>
        /// The player who sent the message, as the connection proved it: the host fills it in
        /// from the connection a message arrived on, never from what the message says.
        /// </summary>
        public PlayerId Sender { get; }
        public ushort MessageType { get; }
        public DeliveryMode DeliveryMode { get; }

        public MessageContext(PlayerId sender, ushort messageType, DeliveryMode deliveryMode)
        {
            Sender = sender;
            MessageType = messageType;
            DeliveryMode = deliveryMode;
        }
    }
}
