namespace Mu3Library.Multiplayer.Protocol
{
    internal static class MessageHeader
    {
        /// <summary>Kind, delivery mode, target kind, target player, and message type.</summary>
        public const int ToHostSize = 1 + 1 + 1 + sizeof(ulong) + sizeof(ushort);

        /// <summary>Kind, delivery mode, sender, and message type.</summary>
        public const int FromHostSize = 1 + 1 + sizeof(ulong) + sizeof(ushort);
    }
}
