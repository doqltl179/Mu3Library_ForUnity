using System;
using System.Collections;
using System.Collections.Generic;
using Mu3Library.DI;
using UnityEngine;

namespace Mu3Library.States
{
    /// <summary>
    /// A state machine over states registered under keys of <typeparamref name="TKey"/>, usually
    /// an enum, as <see cref="IStateMachine{TKey}"/> describes.
    /// </summary>
    /// <remarks>
    /// A routine that <see cref="IState.Enter"/> or <see cref="IState.Exit"/> returns is stepped
    /// once as it starts and once each <see cref="Update"/> after, as Unity steps a coroutine:
    /// <c>yield return null</c> waits a frame, a nested <see cref="IEnumerator"/>, such as a
    /// <see cref="CustomYieldInstruction"/> like <see cref="WaitUntil"/> or
    /// <see cref="WaitForSecondsRealtime"/>, runs until it ends, and an <see cref="AsyncOperation"/>,
    /// such as a scene load, waits until it is done. The machine cannot read how long
    /// <see cref="WaitForSeconds"/> and the other <see cref="YieldInstruction"/>s wait, so it waits
    /// one frame for them and logs a warning once.
    /// </remarks>
    public sealed partial class StateMachine<TKey> : IStateMachine<TKey>, IUpdatable, IDisposable
    {
        private enum Phase
        {
            None,
            Exiting,
            Entering,
        }

        private struct Request
        {
            public TKey Key;
            public bool IsBack;
        }

        private readonly IEqualityComparer<TKey> _comparer;
        private readonly Dictionary<TKey, IState> _states;
        private readonly Queue<Request> _requests = new Queue<Request>();
        private readonly List<StateTransitionGuard<TKey>> _guards = new List<StateTransitionGuard<TKey>>();

        // The routine a transition waits for, the innermost on top, and the operation it yielded.
        private readonly Stack<IEnumerator> _routines = new Stack<IEnumerator>();
        private AsyncOperation _operation = null;
        private bool _loggedYieldInstruction = false;

        private Phase _phase = Phase.None;
        private StateTransition<TKey> _transition;
        private IState _next = null;
        private IState _currentState = null;
        private bool _isRunning = false;
        private bool _isDisposed = false;

        public bool HasCurrent { get; private set; } = false;
        public TKey Current { get; private set; }
        public bool HasPrevious { get; private set; } = false;
        public TKey Previous { get; private set; }
        public bool IsTransitioning => _phase != Phase.None;
        public int PendingCount => _requests.Count;

        public event Action<StateTransition<TKey>> OnStateChanged;
        public event Action<StateTransition<TKey>> OnTransitionBlocked;



        /// <param name="comparer">Compares the keys; null uses the default comparer of <typeparamref name="TKey"/>.</param>
        public StateMachine(IEqualityComparer<TKey> comparer = null)
        {
            _comparer = comparer ?? EqualityComparer<TKey>.Default;
            _states = new Dictionary<TKey, IState>(_comparer);
        }

        /// <summary>
        /// Stops the machine where it is, without exiting the current state, drops the requests
        /// that wait, and cancels an async enter or exit that runs.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _requests.Clear();
            _routines.Clear();
            _operation = null;
            _phase = Phase.None;
            _next = null;
            CancelAsync();
        }

        #region States
        public bool Add(TKey key, IState state)
        {
            if (_isDisposed || state == null || key == null || _states.ContainsKey(key))
            {
                return false;
            }

            _states.Add(key, state);
            return true;
        }

        public bool Remove(TKey key)
        {
            if (key == null || !_states.ContainsKey(key) || IsInUse(key))
            {
                return false;
            }

            return _states.Remove(key);
        }

        public bool Contains(TKey key) => key != null && _states.ContainsKey(key);

        private bool IsInUse(TKey key)
        {
            if ((HasCurrent && _comparer.Equals(Current, key)) || (_phase != Phase.None && _comparer.Equals(_transition.To, key)))
            {
                return true;
            }

            foreach (Request request in _requests)
            {
                if (!request.IsBack && _comparer.Equals(request.Key, key))
                {
                    return true;
                }
            }

            // A back request runs to the previous state.
            return HasPrevious && _comparer.Equals(Previous, key) && _requests.Count > 0;
        }
        #endregion

        #region Requests
        public bool ChangeState(TKey key)
        {
            if (_isDisposed || key == null || !_states.ContainsKey(key))
            {
                return false;
            }

            _requests.Enqueue(new Request { Key = key });
            Run();
            return true;
        }

        public bool Back()
        {
            if (_isDisposed || (!HasPrevious && _phase == Phase.None && _requests.Count == 0))
            {
                return false;
            }

            _requests.Enqueue(new Request { IsBack = true });
            Run();
            return true;
        }

        public bool AddGuard(StateTransitionGuard<TKey> guard)
        {
            if (guard == null || _guards.Contains(guard))
            {
                return false;
            }

            _guards.Add(guard);
            return true;
        }

        public bool RemoveGuard(StateTransitionGuard<TKey> guard) => guard != null && _guards.Remove(guard);
        #endregion

        #region Running
        /// <summary>
        /// Steps the transition that waits, going on with the rest of it and with the requests
        /// after it once it ends; with no transition, updates the current state.
        /// </summary>
        public void Update()
        {
            if (_isDisposed || _isRunning)
            {
                return;
            }

            if (_phase != Phase.None)
            {
                Step();
                Run();
                return;
            }

            if (_currentState != null)
            {
                try
                {
                    _currentState.Update();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <summary>
        /// Runs transitions until one waits or no request is left. A request made meanwhile, as
        /// from a state or a handler, only joins the queue, which this loop works through.
        /// </summary>
        private void Run()
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;
            try
            {
                while (!_isDisposed)
                {
                    if (_phase != Phase.None)
                    {
                        if (IsWaiting())
                        {
                            return;
                        }

                        if (_phase == Phase.Exiting)
                        {
                            BeginEnter();
                        }
                        else
                        {
                            EndTransition();
                        }

                        continue;
                    }

                    if (_requests.Count == 0)
                    {
                        return;
                    }

                    Begin(_requests.Dequeue());
                }
            }
            finally
            {
                _isRunning = false;
            }
        }

        private void Begin(Request request)
        {
            TKey to = request.Key;
            if (request.IsBack)
            {
                if (!HasPrevious)
                {
                    return;
                }

                to = Previous;
            }

            if (!_states.TryGetValue(to, out IState next))
            {
                return;
            }

            StateTransition<TKey> transition = new StateTransition<TKey>(HasCurrent, Current, to, request.IsBack);
            if (!IsAllowed(transition))
            {
                Raise(OnTransitionBlocked, transition);
                return;
            }

            _transition = transition;
            _next = next;
            if (_currentState != null)
            {
                _phase = Phase.Exiting;
                StartWaiting(_currentState, entering: false);
            }
            else
            {
                BeginEnter();
            }
        }

        private void BeginEnter()
        {
            if (_transition.HasFrom)
            {
                Previous = _transition.From;
                HasPrevious = true;
            }

            Current = _transition.To;
            HasCurrent = true;
            _currentState = _next;
            _next = null;
            _phase = Phase.Entering;
            StartWaiting(_currentState, entering: true);
        }

        private void EndTransition()
        {
            _phase = Phase.None;
            Raise(OnStateChanged, _transition);
        }

        private bool IsAllowed(StateTransition<TKey> transition)
        {
            for (int i = 0; i < _guards.Count; i++)
            {
                try
                {
                    if (!_guards[i](transition))
                    {
                        return false;
                    }
                }
                catch (Exception e)
                {
                    // A guard that throws blocks, as it could not say yes.
                    Debug.LogException(e);
                    return false;
                }
            }

            return true;
        }

        /// <summary>Invokes each handler on its own, so one that throws keeps the event from none after it.</summary>
        private static void Raise(Action<StateTransition<TKey>> handlers, StateTransition<TKey> transition)
        {
            if (handlers == null)
            {
                return;
            }

            Delegate[] list = handlers.GetInvocationList();
            for (int i = 0; i < list.Length; i++)
            {
                try
                {
                    ((Action<StateTransition<TKey>>)list[i])(transition);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
        #endregion

        #region Waiting
        private void StartWaiting(IState state, bool entering)
        {
            _routines.Clear();
            _operation = null;

            bool isAsync = false;
            TryStartAsync(state, entering, ref isAsync);
            if (isAsync)
            {
                return;
            }

            IEnumerator routine = null;
            try
            {
                routine = entering ? state.Enter() : state.Exit();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (routine != null)
            {
                _routines.Push(routine);
                Step();
            }
        }

        private bool IsWaiting()
        {
            bool isAsync = false;
            IsAsyncPending(ref isAsync);
            return isAsync || _operation != null || _routines.Count > 0;
        }

        /// <summary>Moves what the transition waits for one frame on.</summary>
        private void Step()
        {
            StepAsync();
            if (_operation != null)
            {
                if (!_operation.isDone)
                {
                    return;
                }

                _operation = null;
            }

            while (_routines.Count > 0)
            {
                IEnumerator routine = _routines.Peek();
                object yielded = null;
                bool hasNext;
                try
                {
                    hasNext = routine.MoveNext();
                    if (hasNext)
                    {
                        yielded = routine.Current;
                    }
                }
                catch (Exception e)
                {
                    // The whole routine ends with the part that threw, as a coroutine does.
                    Debug.LogException(e);
                    _routines.Clear();
                    return;
                }

                // The routine may have disposed the machine, which dropped what it waited for.
                if (_isDisposed)
                {
                    return;
                }

                if (!hasNext)
                {
                    // The routine that waited for this one goes on in the same frame.
                    _routines.Pop();
                    continue;
                }

                if (yielded is IEnumerator nested)
                {
                    _routines.Push(nested);
                    continue;
                }

                if (yielded is AsyncOperation operation)
                {
                    _operation = operation.isDone ? null : operation;
                    return;
                }

                if (yielded is YieldInstruction && !_loggedYieldInstruction)
                {
                    _loggedYieldInstruction = true;
                    Debug.LogWarning($"[StateMachine] A state yielded {yielded.GetType().Name}, which the machine cannot wait for, so it waited one frame. "
                        + "Yield WaitForSecondsRealtime, WaitUntil, or a routine of your own instead.");
                }

                return;
            }
        }

        partial void TryStartAsync(IState state, bool entering, ref bool isAsync);
        partial void IsAsyncPending(ref bool isPending);
        partial void StepAsync();
        partial void CancelAsync();
        #endregion
    }
}
