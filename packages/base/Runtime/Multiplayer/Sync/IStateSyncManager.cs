using System;
using System.Collections.Generic;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Sync
{
    /// <summary>
    /// Keeps game states the same on every player of a game, within a byte budget per player and
    /// tick. Each system decides what its states hold (<see cref="ISyncState"/>); the manager
    /// decides how much goes and when: every <see cref="StateSyncConfig.TickIntervalSeconds"/> it
    /// gathers the states that changed into one message per player and delivery mode, never one
    /// per state, and sends each player at most <see cref="StateSyncConfig.MaxBytesPerTick"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The host has the authority: it sends the states it owns to every client, and the states a
    /// client owns, such as its own character, travel from that client to the host under the same
    /// budget, and the host sends them on to the other players, never back to their owner. Every
    /// player registers the same states under the same IDs and owners.
    /// </para>
    /// <para>
    /// When the states that changed do not fit a player's budget, the manager picks them by
    /// priority and by how long they waited, and the rest wait for a later tick; a player that
    /// missed a change then gets the whole state. A player who joins, comes back to their slot, or
    /// comes into a state's interest gets the whole state too.
    /// </para>
    /// <para>
    /// It syncs only while the session is <see cref="SessionState.InGame"/>, and starts each game
    /// by sending every player every state whole. Call <c>Update</c> every frame after the
    /// multiplayer manager's, on the main thread.
    /// </para>
    /// </remarks>
    public interface IStateSyncManager
    {
        /// <summary>This player's latest tick; 0 before the first.</summary>
        public uint Tick { get; }

        /// <summary>The number of registered states.</summary>
        public int Count { get; }

        public float TickIntervalSeconds { get; }
        public int MaxBytesPerTick { get; }

        /// <summary>
        /// Registers a state, which every player registers with the same ID, owner, and delivery
        /// mode. A state registered during a game goes whole to every player at the next tick, so
        /// register it on every player before then, for example while handling the message that
        /// spawns what it belongs to: a player drops an update of a state it has not registered.
        /// </summary>
        /// <param name="owner">
        /// The player who sends the state. <see cref="PlayerId.None"/>, the default, and the host's
        /// own ID mean the host, whoever hosts; another player's ID means that client.
        /// </param>
        /// <param name="deliveryMode">
        /// <see cref="DeliveryMode.Unreliable"/>, the default, suits a state that changes often,
        /// such as a position: a lost update is not sent again, and the state goes whole every
        /// <see cref="StateSyncConfig.FullStateIntervalSeconds"/> so a copy catches up.
        /// <see cref="DeliveryMode.Reliable"/> suits a state that changes now and then, such as a
        /// door, and arrives in order.
        /// </param>
        /// <param name="priority">
        /// How much the state counts when it competes for the budget, above 0; 1 by default. Each
        /// tick a state waits for a player adds its priority to its claim on that player, and the
        /// largest claims go first, so a state of priority 2 goes about twice as often as one of
        /// priority 1 when both keep changing and only one fits.
        /// </param>
        /// <param name="interest">
        /// On the host, whether a player receives the state this tick; null sends it to every
        /// player. A client sends the states it owns to the host only, so it never asks the filter.
        /// </param>
        /// <returns>
        /// <see cref="MultiplayerReason.InvalidArgument"/> for a null state, an ID already
        /// registered, a priority that is not above 0 or not finite, or an unknown delivery mode;
        /// <see cref="MultiplayerReason.InvalidState"/> after <c>Dispose</c> or during a tick, as from
        /// an interest filter.
        /// </returns>
        public MultiplayerResult Register(ISyncState state, PlayerId owner = default, DeliveryMode deliveryMode = DeliveryMode.Unreliable, float priority = 1f, SyncInterestFilter interest = null);

        /// <summary>False when no state has the ID, after <c>Dispose</c>, or during a tick.</summary>
        public bool Unregister(uint id);

        public bool TryGetState(uint id, out ISyncState state);

        /// <summary>
        /// Whether what this manager sends a second from a client, at most, fits the rate limits of
        /// <paramref name="rules"/>: one message a tick for each delivery mode that the states a
        /// client owns use, and <see cref="MaxBytesPerTick"/> a tick plus the library's header of
        /// each message, at the most ticks a second can hold. The host's own sends are never
        /// limited. The manager runs the check itself as each game starts, and as a client-owned
        /// state registers during one, and logs a warning naming the numbers the first time it
        /// fails in a session, since a client past a limit has its sync messages dropped by the
        /// host. Leave room within the limits for the game's other messages.
        /// </summary>
        /// <returns><see cref="MultiplayerReason.InvalidArgument"/> when it does not fit.</returns>
        public MultiplayerResult CheckRateLimits(SessionRules rules);

        /// <summary>
        /// Fills <paramref name="results"/>, which it clears first, with what this player sent each
        /// player it sends to in this game: on the host every connected client, on a client the
        /// host. With room in the list it allocates nothing, so a game can show it every frame, as
        /// the editor's Multiplayer Debugger window shows it.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="results"/> is null.</exception>
        public void GetPlayerStats(List<SyncPlayerStats> results);
    }
}
