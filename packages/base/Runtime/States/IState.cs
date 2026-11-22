using System.Collections;

namespace Mu3Library.States
{
    /// <summary>
    /// One state of a <see cref="StateMachine{TKey}"/>, such as a game's title screen or an
    /// enemy's chase: what it does as the machine enters it, every frame while it is current, and
    /// as the machine leaves it. Derive from <see cref="State"/> to override only what the state needs.
    /// </summary>
    /// <remarks>
    /// <see cref="Enter"/> and <see cref="Exit"/> may return a routine, which the machine steps
    /// from its own <c>Update</c>, as Unity steps a coroutine, so a state can wait for a scene to
    /// load before the next one comes in; with UniTask a state can implement
    /// <c>IAsyncState</c> and await instead. The machine catches and logs an exception a state
    /// throws and goes on as if that part had ended, so one broken state does not stop the flow.
    /// </remarks>
    public interface IState
    {
        /// <summary>
        /// Called as the machine enters the state. Null, or a routine that ends at once, enters
        /// it within the call; otherwise the machine is <see cref="IStateMachine{TKey}.IsTransitioning"/>
        /// until the routine ends.
        /// </summary>
        public IEnumerator Enter();

        /// <summary>Called every <c>Update</c> of the machine while the state is current and no transition runs.</summary>
        public void Update();

        /// <summary>Called as the machine leaves the state, before it enters the next; it may return a routine as <see cref="Enter"/> does.</summary>
        public IEnumerator Exit();
    }
}
