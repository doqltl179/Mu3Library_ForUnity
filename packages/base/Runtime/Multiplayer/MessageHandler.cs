using System;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer
{
    /// <summary>
    /// Handles one received message. The payload is valid only during the call; copy what must
    /// outlive it.
    /// </summary>
    public delegate void MessageHandler(MessageContext context, ReadOnlySpan<byte> payload);
}
