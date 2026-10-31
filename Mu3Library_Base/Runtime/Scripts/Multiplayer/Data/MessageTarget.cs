namespace Mu3Library.Multiplayer.Data
{
    /// <summary>
    /// Who receives a message. A message a player sends to itself is delivered before the send
    /// returns; every other one travels through the host. Create one with the members below;
    /// <c>default</c> targets nobody and is refused.
    /// </summary>
    public readonly struct MessageTarget
    {
        public MessageTargetKind Kind { get; }

        /// <summary>The player that <see cref="MessageTargetKind.Player"/> and <see cref="MessageTargetKind.AllExcept"/> name.</summary>
        public PlayerId Player { get; }

        public static MessageTarget Host => new MessageTarget(MessageTargetKind.Host, PlayerId.None);
        public static MessageTarget All => new MessageTarget(MessageTargetKind.All, PlayerId.None);
        public static MessageTarget Others => new MessageTarget(MessageTargetKind.Others, PlayerId.None);

        public static MessageTarget To(PlayerId player) => new MessageTarget(MessageTargetKind.Player, player);
        public static MessageTarget AllExcept(PlayerId player) => new MessageTarget(MessageTargetKind.AllExcept, player);

        internal MessageTarget(MessageTargetKind kind, PlayerId player)
        {
            Kind = kind;
            Player = player;
        }

        public override string ToString() => Player.IsValid ? $"{Kind} {Player}" : Kind.ToString();
    }
}
