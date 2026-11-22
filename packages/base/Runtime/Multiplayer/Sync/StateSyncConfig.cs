using System;

namespace Mu3Library.Multiplayer.Sync
{
    /// <summary>
    /// How a <see cref="StateSyncManager"/> sends: the message type it travels as, how often it
    /// ticks, and how many bytes a tick sends one player at most. Every player of a game uses the
    /// same values. The manager copies them when it is created, so changing the config afterwards
    /// changes nothing.
    /// </summary>
    public sealed class StateSyncConfig
    {
        public const float DefaultTickIntervalSeconds = 0.05f;
        public const int DefaultMaxBytesPerTick = 512;
        public const float DefaultFullStateIntervalSeconds = 1f;

        /// <summary>The smallest <see cref="MaxBytesPerTick"/>, which still holds a tick with one small state.</summary>
        public const int MinBytesPerTick = 32;

        /// <summary>
        /// The game message type the manager registers with <see cref="Data.MessagePolicy.NoClientRelay"/>
        /// and sends every tick on, apart from the game's own types.
        /// </summary>
        public ushort MessageType { get; }

        /// <summary>
        /// Seconds between two ticks, <see cref="DefaultTickIntervalSeconds"/> (20 a second) by
        /// default. Each tick sends every player one message for its reliable states and one for
        /// its unreliable states, at most, whatever number of states changed.
        /// </summary>
        public float TickIntervalSeconds { get; set; } = DefaultTickIntervalSeconds;

        /// <summary>
        /// Most bytes one tick sends one player, <see cref="DefaultMaxBytesPerTick"/> by default,
        /// from <see cref="MinBytesPerTick"/> up: the payloads of the tick's messages to that
        /// player, without the library's header of each message. Within a session joined as a
        /// client the limit is also at most <see cref="IMultiplayerMessenger.MaxMessageSize"/>.
        /// States that do not fit wait for a later tick, the one that waited longest for its
        /// priority first, and a state that cannot fit even alone is never sent and is logged.
        /// </summary>
        public int MaxBytesPerTick { get; set; } = DefaultMaxBytesPerTick;

        /// <summary>
        /// How often an unreliable state goes to each player whole, changed or not, so a copy that
        /// missed a lost update catches up: <see cref="DefaultFullStateIntervalSeconds"/> by
        /// default, 0 never. Reliable states arrive in order and need no such repeat.
        /// </summary>
        public float FullStateIntervalSeconds { get; set; } = DefaultFullStateIntervalSeconds;

        /// <summary>
        /// Seconds since an arbitrary start, for the ticks. Null uses
        /// <c>Time.realtimeSinceStartupAsDouble</c>; tests hand in their own clock.
        /// </summary>
        public Func<double> Clock { get; set; }

        public StateSyncConfig(ushort messageType)
        {
            MessageType = messageType;
        }
    }
}
