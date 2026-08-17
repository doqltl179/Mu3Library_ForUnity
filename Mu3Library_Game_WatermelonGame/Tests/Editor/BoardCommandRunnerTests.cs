using Mu3Library.Game.WatermelonGame.Board.Command;
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
    }
}
