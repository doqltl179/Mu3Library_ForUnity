using System.Globalization;

namespace Mu3Library.States
{
    /// <summary>A move of a <see cref="StateMachine{TKey}"/> from one state to another.</summary>
    public readonly struct StateTransition<TKey>
    {
        /// <summary>False for the machine's first state, which it enters from none.</summary>
        public bool HasFrom { get; }

        /// <summary>The state the machine leaves; <c>default</c> when <see cref="HasFrom"/> is false.</summary>
        public TKey From { get; }

        public TKey To { get; }

        /// <summary>True when <see cref="IStateMachine{TKey}.Back"/> asked for it.</summary>
        public bool IsBack { get; }

        public StateTransition(bool hasFrom, TKey from, TKey to, bool isBack)
        {
            HasFrom = hasFrom;
            From = from;
            To = to;
            IsBack = isBack;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} -> {1}{2}",
                HasFrom ? (object)From : "(none)", To, IsBack ? " (back)" : string.Empty);
        }
    }

    /// <summary>
    /// Answers whether the machine may make <paramref name="transition"/>; false blocks it, and
    /// the machine goes on with the next request (<see cref="IStateMachine{TKey}.AddGuard"/>).
    /// </summary>
    public delegate bool StateTransitionGuard<TKey>(StateTransition<TKey> transition);
}
