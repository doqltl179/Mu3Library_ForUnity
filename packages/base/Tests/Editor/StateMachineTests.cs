using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mu3Library.States;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if MU3LIBRARY_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace Mu3Library.Tests
{
    /// <summary>
    /// The state machine as a game flow uses it: states that enter and exit, at once or waiting
    /// frames, guards, requests that come during a transition, and going back. Each Update of the
    /// machine stands for a frame.
    /// </summary>
    public class StateMachineTests
    {
        private enum Flow
        {
            Boot,
            Title,
            Lobby,
            Game,
            Result,
        }

        /// <summary>Records what the machine calls on it, and waits the frames it is told to.</summary>
        private sealed class RecordingState : State
        {
            private readonly string _name;
            private readonly List<string> _log;

            public int EnterFrames;
            public int ExitFrames;
            public int Updates;

            public RecordingState(string name, List<string> log)
            {
                _name = name;
                _log = log;
            }

            public override IEnumerator Enter()
            {
                _log.Add("enter " + _name);
                for (int i = 0; i < EnterFrames; i++)
                {
                    yield return null;
                }

                _log.Add("entered " + _name);
            }

            public override void Update() => Updates++;

            public override IEnumerator Exit()
            {
                _log.Add("exit " + _name);
                for (int i = 0; i < ExitFrames; i++)
                {
                    yield return null;
                }

                _log.Add("exited " + _name);
            }
        }

        private readonly List<string> _log = new List<string>();
        private readonly List<StateTransition<Flow>> _changes = new List<StateTransition<Flow>>();
        private StateMachine<Flow> _machine;
        private Dictionary<Flow, RecordingState> _states;



        [SetUp]
        public void SetUp()
        {
            _log.Clear();
            _changes.Clear();
            _machine = new StateMachine<Flow>();
            _states = new Dictionary<Flow, RecordingState>();
            foreach (Flow flow in Enum.GetValues(typeof(Flow)))
            {
                RecordingState state = new RecordingState(flow.ToString(), _log);
                _states.Add(flow, state);
                Assert.That(_machine.Add(flow, state), Is.True);
            }

            _machine.OnStateChanged += transition =>
            {
                _changes.Add(transition);
                _log.Add("changed " + transition);
            };
        }

        [TearDown]
        public void TearDown()
        {
            _machine.Dispose();
        }

        [Test]
        public void ChangeState_LeavesTheOldStateBeforeItEntersTheNew_ThenTellsTheChange()
        {
            Assert.That(_machine.ChangeState(Flow.Boot), Is.True);
            Assert.That(_machine.ChangeState(Flow.Title), Is.True);

            Assert.That(_log, Is.EqualTo(new[]
            {
                "enter Boot", "entered Boot", "changed (none) -> Boot",
                "exit Boot", "exited Boot", "enter Title", "entered Title", "changed Boot -> Title",
            }));
            Assert.That(_machine.Current, Is.EqualTo(Flow.Title));
            Assert.That(_machine.Previous, Is.EqualTo(Flow.Boot));
            Assert.That(_machine.IsTransitioning, Is.False);

            // The current state updates with the machine.
            _machine.Update();
            _machine.Update();
            Assert.That(_states[Flow.Title].Updates, Is.EqualTo(2));
            Assert.That(_states[Flow.Boot].Updates, Is.Zero);
            Assert.That(_machine.ChangeState((Flow)99), Is.False, "a key not registered");
        }

        [Test]
        public void Requests_DuringATransitionThatWaits_RunInTheOrderTheyCame()
        {
            _machine.ChangeState(Flow.Title);
            _states[Flow.Title].ExitFrames = 2;
            _states[Flow.Lobby].EnterFrames = 3;
            _log.Clear();

            Assert.That(_machine.ChangeState(Flow.Lobby), Is.True);
            Assert.That(_machine.IsTransitioning, Is.True);
            Assert.That(_machine.ChangeState(Flow.Game), Is.True);
            Assert.That(_machine.ChangeState(Flow.Result), Is.True);
            Assert.That(_machine.PendingCount, Is.EqualTo(2));

            // Two frames to leave the title, three to enter the lobby; no state updates meanwhile.
            for (int frame = 0; frame < 4; frame++)
            {
                _machine.Update();
                Assert.That(_machine.IsTransitioning, Is.True, $"frame {frame}");
            }

            _machine.Update();

            Assert.That(_machine.IsTransitioning, Is.False);
            Assert.That(_machine.Current, Is.EqualTo(Flow.Result));
            Assert.That(_log, Is.EqualTo(new[]
            {
                "exit Title", "exited Title", "enter Lobby", "entered Lobby", "changed Title -> Lobby",
                "exit Lobby", "exited Lobby", "enter Game", "entered Game", "changed Lobby -> Game",
                "exit Game", "exited Game", "enter Result", "entered Result", "changed Game -> Result",
            }));
            Assert.That(_states[Flow.Title].Updates + _states[Flow.Lobby].Updates, Is.Zero);
        }

        [Test]
        public void Guard_BlocksATransition_AndTheMachineStaysWhereItIs()
        {
            bool isReady = false;
            List<StateTransition<Flow>> blocked = new List<StateTransition<Flow>>();
            _machine.OnTransitionBlocked += blocked.Add;
            Assert.That(_machine.AddGuard(transition => transition.To != Flow.Game || isReady), Is.True);
            _machine.ChangeState(Flow.Lobby);
            _log.Clear();

            Assert.That(_machine.ChangeState(Flow.Game), Is.True);

            Assert.That(_machine.Current, Is.EqualTo(Flow.Lobby));
            Assert.That(_log, Is.Empty, "the lobby did not even exit");
            Assert.That(blocked.Count, Is.EqualTo(1));
            Assert.That(blocked[0].From, Is.EqualTo(Flow.Lobby));
            Assert.That(blocked[0].To, Is.EqualTo(Flow.Game));

            isReady = true;
            _machine.ChangeState(Flow.Game);
            Assert.That(_machine.Current, Is.EqualTo(Flow.Game));
        }

        [Test]
        public void Back_ReturnsToThePreviousState()
        {
            _machine.ChangeState(Flow.Title);
            Assert.That(_machine.Back(), Is.False, "nothing before the first state");

            _machine.ChangeState(Flow.Lobby);
            Assert.That(_machine.Back(), Is.True);

            Assert.That(_machine.Current, Is.EqualTo(Flow.Title));
            Assert.That(_machine.Previous, Is.EqualTo(Flow.Lobby));
            Assert.That(_changes[_changes.Count - 1].IsBack, Is.True);
        }

        [Test]
        public void Routines_WaitForWhatTheyYield_AsCoroutinesDo()
        {
            bool isLoaded = false;
            _machine.Add((Flow)10, new RoutineState(() => Waiting(() => isLoaded)));
            _machine.ChangeState((Flow)10);

            for (int frame = 0; frame < 5; frame++)
            {
                _machine.Update();
            }

            Assert.That(_machine.IsTransitioning, Is.True, "a nested routine waits for its WaitUntil");
            isLoaded = true;
            _machine.Update();
            Assert.That(_machine.IsTransitioning, Is.False);
            Assert.That(_machine.Current, Is.EqualTo((Flow)10));
        }

        [Test]
        public void StateThatThrows_IsLogged_AndTheFlowGoesOn()
        {
            _machine.Add((Flow)10, new RoutineState(() => Throwing()));
            LogAssert.Expect(LogType.Exception, new Regex("broken enter"));

            _machine.ChangeState((Flow)10);
            _machine.Update();

            Assert.That(_machine.IsTransitioning, Is.False);
            Assert.That(_machine.Current, Is.EqualTo((Flow)10));
            Assert.That(_machine.ChangeState(Flow.Title), Is.True);
            Assert.That(_machine.Current, Is.EqualTo(Flow.Title));
        }

#if MU3LIBRARY_UNITASK_SUPPORT
        [Test]
        public void AsyncState_IsAwaited_AndRequestsWaitForIt()
        {
            UniTaskCompletionSource loaded = new UniTaskCompletionSource();
            AwaitingState awaiting = new AwaitingState(loaded, _log);
            _machine.Add((Flow)10, awaiting);
            _machine.ChangeState(Flow.Title);
            _log.Clear();

            _machine.ChangeState((Flow)10);
            _machine.ChangeState(Flow.Result);
            _machine.Update();
            _machine.Update();

            Assert.That(_machine.IsTransitioning, Is.True);
            Assert.That(_machine.Current, Is.EqualTo((Flow)10));
            Assert.That(_log, Is.EqualTo(new[] { "exit Title", "exited Title", "enter async" }));

            loaded.TrySetResult();
            _machine.Update();

            Assert.That(_machine.Current, Is.EqualTo(Flow.Result));
            Assert.That(_log, Is.EqualTo(new[]
            {
                "exit Title", "exited Title", "enter async", "entered async", "changed Title -> 10",
                "exit async", "enter Result", "entered Result", "changed 10 -> Result",
            }));
        }

        [Test]
        public void AsyncState_ThatIsStillEntering_IsCancelledWhenTheMachineIsDisposed()
        {
            UniTaskCompletionSource never = new UniTaskCompletionSource();
            AwaitingState awaiting = new AwaitingState(never, _log);
            _machine.Add((Flow)10, awaiting);
            _machine.ChangeState((Flow)10);

            _machine.Dispose();

            Assert.That(awaiting.Token.IsCancellationRequested, Is.True);
            Assert.That(_machine.ChangeState(Flow.Title), Is.False);
        }

        private sealed class AwaitingState : AsyncState
        {
            private readonly UniTaskCompletionSource _loaded;
            private readonly List<string> _log;

            public CancellationToken Token;

            public AwaitingState(UniTaskCompletionSource loaded, List<string> log)
            {
                _loaded = loaded;
                _log = log;
            }

            public override async UniTask EnterAsync(CancellationToken cancellationToken)
            {
                Token = cancellationToken;
                _log.Add("enter async");
                await _loaded.Task;
                _log.Add("entered async");
            }

            public override UniTask ExitAsync(CancellationToken cancellationToken)
            {
                _log.Add("exit async");
                return UniTask.CompletedTask;
            }
        }
#endif

        private sealed class RoutineState : State
        {
            private readonly Func<IEnumerator> _enter;

            public RoutineState(Func<IEnumerator> enter)
            {
                _enter = enter;
            }

            public override IEnumerator Enter() => _enter();
        }

        private static IEnumerator Waiting(Func<bool> isDone)
        {
            yield return null;
            yield return new WaitUntil(isDone);
        }

        private static IEnumerator Throwing()
        {
            yield return null;
            throw new InvalidOperationException("broken enter");
        }
    }
}
