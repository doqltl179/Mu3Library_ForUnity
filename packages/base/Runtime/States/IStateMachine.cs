using System;

namespace Mu3Library.States
{
    /// <summary>
    /// Moves between states that a game registers under keys, such as a game flow from boot to
    /// title, lobby, game, and results, a UI flow, or an enemy's behavior. It is tied to no scene
    /// loader or UI system; a state's <see cref="IState.Enter"/> and <see cref="IState.Exit"/> may
    /// wait for whatever they start.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A transition leaves the current state, waiting until its <see cref="IState.Exit"/> ends,
    /// then makes the new state current and enters it, waiting until its <see cref="IState.Enter"/>
    /// ends, and then raises <see cref="OnStateChanged"/>. A transition that waits for nothing
    /// ends within the call that asked for it. Requests made while a transition runs, from a state,
    /// a handler, or anywhere else, wait and run one by one in the order they came.
    /// </para>
    /// <para>
    /// The owner calls <c>Update</c> every frame, as Base's managers are called through
    /// <c>IUpdatable</c>: it steps a transition that waits, and otherwise updates the current state.
    /// Everything runs on the main thread.
    /// </para>
    /// </remarks>
    public interface IStateMachine<TKey>
    {
        /// <summary>False before the first state is entered.</summary>
        public bool HasCurrent { get; }

        /// <summary>The state the machine is in, which during a transition is the new one from the time it starts entering.</summary>
        public TKey Current { get; }

        /// <summary>False until the machine left a state.</summary>
        public bool HasPrevious { get; }

        /// <summary>The state the machine was in before <see cref="Current"/>.</summary>
        public TKey Previous { get; }

        /// <summary>True while a transition waits for a state's enter or exit to end.</summary>
        public bool IsTransitioning { get; }

        /// <summary>Requests that wait for the transition that runs.</summary>
        public int PendingCount { get; }

        /// <summary>
        /// Raised once a transition ended: the new state is current and entered. A handler may
        /// ask for another transition, which runs after it returns.
        /// </summary>
        public event Action<StateTransition<TKey>> OnStateChanged;

        /// <summary>Raised when a guard blocked a transition, which the machine then drops.</summary>
        public event Action<StateTransition<TKey>> OnTransitionBlocked;

        /// <summary>False for a null state, a key already registered, or after <c>Dispose</c>.</summary>
        public bool Add(TKey key, IState state);

        /// <summary>False for a key not registered, the current state, or a state a request waits for.</summary>
        public bool Remove(TKey key);

        public bool Contains(TKey key);

        /// <summary>
        /// Asks to move to the state of <paramref name="key"/>, which may be the current one to
        /// enter it again. The request runs at once when no transition runs, otherwise after the
        /// ones before it. False, with nothing asked, for a key not registered or after <c>Dispose</c>.
        /// </summary>
        public bool ChangeState(TKey key);

        /// <summary>
        /// Asks to move back to the state before the current one, as it is when the request runs;
        /// a request that finds none is dropped. False, with nothing asked, when the machine has no
        /// previous state and no transition runs or waits, or after <c>Dispose</c>.
        /// </summary>
        public bool Back();

        /// <summary>
        /// Adds a condition every transition must meet, checked as the transition starts, before
        /// the current state exits. Guards run in the order they were added, until one blocks.
        /// False for a null guard or one already added.
        /// </summary>
        public bool AddGuard(StateTransitionGuard<TKey> guard);

        public bool RemoveGuard(StateTransitionGuard<TKey> guard);
    }
}
