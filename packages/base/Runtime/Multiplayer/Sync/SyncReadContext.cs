using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Sync
{
    /// <summary>
    /// Where a state's update came from and when, as <see cref="ISyncState.ReadFull"/> and
    /// <see cref="ISyncState.ReadDelta"/> receive it.
    /// </summary>
    public readonly struct SyncReadContext
    {
        /// <summary>
        /// The player whose tick carried the update, as the connection proved it: on the host the
        /// state's owner, and on a client the host.
        /// </summary>
        public PlayerId Sender { get; }

        /// <summary>
        /// The sender's tick: it grows by at least one each tick and roughly follows the sender's
        /// clock, so <c>Tick * TickIntervalSeconds</c> is about when the sender wrote the update,
        /// on the sender's clock. An unreliable update of the same tick as the last one this copy
        /// read, or of an earlier one, does not reach the state, unless it is 64 or more ticks
        /// earlier: such a tick comes from a sender whose ticks started again, as a game started
        /// anew on another player does, and reaches the state, so a state that orders its updates
        /// by tick starts its order again there. Reliable updates arrive in order and always reach
        /// the state, and their tick can start again the same way.
        /// </summary>
        public uint Tick { get; }

        /// <summary>The tick interval of this player's manager, which every player of a game shares.</summary>
        public float TickIntervalSeconds { get; }

        /// <summary>When this player read the update, on its manager's clock (<see cref="StateSyncConfig.Clock"/>).</summary>
        public double ReceivedAt { get; }

        public DeliveryMode DeliveryMode { get; }

        public SyncReadContext(PlayerId sender, uint tick, float tickIntervalSeconds, double receivedAt, DeliveryMode deliveryMode)
        {
            Sender = sender;
            Tick = tick;
            TickIntervalSeconds = tickIntervalSeconds;
            ReceivedAt = receivedAt;
            DeliveryMode = deliveryMode;
        }
    }
}
