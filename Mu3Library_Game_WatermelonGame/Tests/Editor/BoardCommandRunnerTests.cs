using System.Collections.Generic;
using Mu3Library.Game.WatermelonGame.Board.Command;
using Mu3Library.Game.WatermelonGame.Board.Command.Flow;
using NUnit.Framework;

namespace Mu3Library.Game.WatermelonGame.Tests
{
    public class BoardCommandRunnerTests
    {
        private class FakeCommand : IBoardCommand, IUpdatableBoardCommand, ICancelableBoardCommand
        {
            public bool IsRunning { get; private set; }
            public bool IsCompleted { get; private set; }
            public bool IsCanceled { get; private set; }

            public int RunCount { get; private set; }
            public int UpdateCount { get; private set; }
            public bool IsDisposed { get; private set; }

            public int UpdatesUntilComplete = -1;

            public void Run()
            {
                IsRunning = true;
                RunCount++;

                if (UpdatesUntilComplete == 0)
                {
                    IsCompleted = true;
                }
            }

            public void Update(float deltaTime)
            {
                UpdateCount++;

                if (UpdatesUntilComplete > 0 && UpdateCount >= UpdatesUntilComplete)
                {
                    IsCompleted = true;
                }
            }

            public void Cancel()
            {
                if (IsCompleted)
                {
                    return;
                }

                IsCanceled = true;
                IsCompleted = true;
            }

            public void Dispose() => IsDisposed = true;
        }

        [Test]
        public void Advance_NullCommand_ReportsFinished()
        {
            Assert.IsFalse(BoardCommandRunner.Advance(null, 0.1f));
            Assert.IsTrue(BoardCommandRunner.IsFinished(null));
        }

        [Test]
        public void Advance_StartsCommandOnFirstCall()
        {
            FakeCommand command = new() { UpdatesUntilComplete = 1 };

            bool hasWorkLeft = BoardCommandRunner.Advance(command, 0.1f);

            Assert.IsTrue(hasWorkLeft);
            Assert.AreEqual(1, command.RunCount);
            Assert.AreEqual(0, command.UpdateCount);
        }

        [Test]
        public void Advance_UpdatesRunningCommandUntilComplete()
        {
            FakeCommand command = new() { UpdatesUntilComplete = 2 };

            BoardCommandRunner.Advance(command, 0.1f);
            Assert.IsTrue(BoardCommandRunner.Advance(command, 0.1f));
            Assert.IsFalse(BoardCommandRunner.Advance(command, 0.1f));

            Assert.AreEqual(1, command.RunCount);
            Assert.AreEqual(2, command.UpdateCount);
            Assert.IsTrue(command.IsCompleted);
        }

        [Test]
        public void Advance_CompletedCommand_DoesNothing()
        {
            FakeCommand command = new() { UpdatesUntilComplete = 0 };

            BoardCommandRunner.Advance(command, 0.1f);
            bool hasWorkLeft = BoardCommandRunner.Advance(command, 0.1f);

            Assert.IsFalse(hasWorkLeft);
            Assert.AreEqual(1, command.RunCount);
        }

        [Test]
        public void Cancel_CancelableCommand_CancelsIt()
        {
            FakeCommand command = new() { UpdatesUntilComplete = 5 };
            BoardCommandRunner.Advance(command, 0.1f);

            Assert.IsTrue(BoardCommandRunner.Cancel(command));
            Assert.IsTrue(command.IsCanceled);
            Assert.IsTrue(BoardCommandRunner.IsCanceled(command));
            Assert.IsTrue(BoardCommandRunner.IsFinished(command));
        }

        [Test]
        public void SequenceWithoutGuard_RunsEveryStepThatEndsAtOnceInOneAdvance()
        {
            List<int> ran = new();
            SequenceCommand sequence = new(
                new ActionCommand(() => ran.Add(0)),
                new ActionCommand(() => ran.Add(1)),
                new ActionCommand(() => ran.Add(2)));

            Assert.IsFalse(BoardCommandRunner.Advance(sequence, 0.1f));

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, ran);
            Assert.IsTrue(sequence.IsCompleted);
            Assert.IsFalse(sequence.IsCanceled);
        }

        [Test]
        public void ParallelWithoutGuard_AdvancesEveryCommandInOneAdvance()
        {
            List<int> ran = new();
            ParallelCommand group = new(
                new ActionCommand(() => ran.Add(0)),
                new ActionCommand(() => ran.Add(1)),
                new ActionCommand(() => ran.Add(2)));

            Assert.IsFalse(BoardCommandRunner.Advance(group, 0.1f));

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, ran);
            Assert.IsTrue(group.IsCompleted);
        }

        [Test]
        public void SequenceWithGuard_StopsBetweenStepsAndGoesOnOnceTheGuardAllowsIt()
        {
            bool canContinue = true;
            List<int> ran = new();
            SequenceCommand sequence = new(
                new ActionCommand(() =>
                {
                    ran.Add(0);
                    canContinue = false;
                }),
                new ActionCommand(() => ran.Add(1)),
                new ActionCommand(() => ran.Add(2)));
            sequence.SetStepGuard(() => canContinue);

            Assert.IsTrue(BoardCommandRunner.Advance(sequence, 0.1f));

            // The step that turned the guard off finished, the next one did not start.
            CollectionAssert.AreEqual(new[] { 0 }, ran);
            Assert.AreEqual(1, sequence.CurrentIndex);
            Assert.IsTrue(sequence.IsRunning);
            Assert.IsFalse(sequence.IsCompleted);

            Assert.IsTrue(BoardCommandRunner.Advance(sequence, 0.1f));
            CollectionAssert.AreEqual(new[] { 0 }, ran, "A step started while the guard said no.");

            canContinue = true;
            Assert.IsFalse(BoardCommandRunner.Advance(sequence, 0.1f));
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, ran);
            Assert.IsTrue(sequence.IsCompleted);
            Assert.IsFalse(sequence.IsCanceled);
        }

        [Test]
        public void ParallelWithGuard_StopsBetweenCommandsAndGoesOnOnceTheGuardAllowsIt()
        {
            bool canContinue = true;
            List<int> ran = new();
            ParallelCommand group = new(
                new ActionCommand(() =>
                {
                    ran.Add(0);
                    canContinue = false;
                }),
                new ActionCommand(() => ran.Add(1)),
                new ActionCommand(() => ran.Add(2)));
            group.SetStepGuard(() => canContinue);

            Assert.IsTrue(BoardCommandRunner.Advance(group, 0.1f));

            CollectionAssert.AreEqual(new[] { 0 }, ran);
            Assert.IsTrue(group.IsRunning);
            Assert.IsFalse(group.IsCompleted);

            canContinue = true;
            Assert.IsFalse(BoardCommandRunner.Advance(group, 0.1f));
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, ran, "A command ran twice or not at all.");
            Assert.IsTrue(group.IsCompleted);
        }

        [Test]
        public void GroupGuard_ReachesTheGroupsItHolds()
        {
            bool canContinue = true;
            List<int> ran = new();
            SequenceCommand inner = new(
                new ActionCommand(() =>
                {
                    ran.Add(0);
                    canContinue = false;
                }),
                new ActionCommand(() => ran.Add(1)));
            ParallelCommand outer = new(inner, new ActionCommand(() => ran.Add(2)));
            outer.SetStepGuard(() => canContinue);

            Assert.IsTrue(BoardCommandRunner.Advance(outer, 0.1f));

            CollectionAssert.AreEqual(new[] { 0 }, ran);
            Assert.AreEqual(1, inner.CurrentIndex);
        }
    }
}
