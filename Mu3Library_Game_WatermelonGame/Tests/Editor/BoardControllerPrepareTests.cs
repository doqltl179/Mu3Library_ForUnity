using System.Collections.Generic;
using Mu3Library.Game.WatermelonGame.Board;
using NUnit.Framework;

namespace Mu3Library.Game.WatermelonGame.Tests
{
    public class BoardControllerPrepareTests
    {
        private TestBoard _board;
        private List<int> _scoreChanges;

        [SetUp]
        public void SetUp()
        {
            _board = new TestBoard();

            _scoreChanges = new List<int>();
            _board.Board.OnScoreChanged += score => _scoreChanges.Add(score);
        }

        [TearDown]
        public void TearDown()
        {
            _board?.Dispose();
            _board = null;
        }

        [Test]
        public void Prepare_SnapshotInALaterStage_DrawsFromThatStageBag()
        {
            _board.Prepare();

            _board.Board.Prepare(new BoardSnapshot { SpawnCount = 60 });

            // The preview took the first draw of the restored board, the rest of the bag follows.
            int laterStageIndexCount = _board.Board.NextItemIndex > 2 ? 1 : 0;
            for (int draw = 0; draw < 98; draw++)
            {
                if (_board.Board.Draw() > 2)
                {
                    laterStageIndexCount++;
                }
            }

            Assert.Greater(laterStageIndexCount, 0);
        }

        [Test]
        public void Prepare_AfterScoring_ReportsTheScoreStartingOver()
        {
            _board.StartGame();
            _board.Board.AddScore(30);
            _scoreChanges.Clear();

            _board.Board.Prepare();

            Assert.AreEqual(0, _board.Board.Score);
            CollectionAssert.AreEqual(new[] { 0 }, _scoreChanges);
        }

        [Test]
        public void Prepare_ReportsTheScoreOnThePreparedBoard()
        {
            _board.StartGame();
            _board.Board.AddScore(30);

            bool wasPrepared = false;
            int nextItemIndex = -1;
            bool boardPreparedFirst = false;
            _board.Board.OnScoreChanged += _ =>
            {
                wasPrepared = _board.Board.IsPrepared;
                nextItemIndex = _board.Board.NextItemIndex;
            };
            _board.Board.OnBoardPrepared += () => boardPreparedFirst = nextItemIndex < 0;

            _board.Board.Prepare();

            Assert.IsTrue(wasPrepared);
            Assert.GreaterOrEqual(nextItemIndex, 0);
            Assert.IsFalse(boardPreparedFirst, "OnBoardPrepared came before OnScoreChanged.");
        }

        [Test]
        public void Prepare_ScoreAlreadyZero_ReportsNothing()
        {
            _board.Prepare();
            _scoreChanges.Clear();

            _board.Board.Prepare();

            CollectionAssert.IsEmpty(_scoreChanges);
        }

        [Test]
        public void Prepare_SnapshotWithAnotherScore_ReportsTheRestoredScoreOnce()
        {
            _board.StartGame();
            _board.Board.AddScore(30);
            _scoreChanges.Clear();

            _board.Board.ImportSnapshotJson(new BoardSnapshot { Score = 250 }.ToJson());

            Assert.AreEqual(250, _board.Board.Score);
            CollectionAssert.AreEqual(new[] { 250 }, _scoreChanges);
        }

        [Test]
        public void Prepare_SnapshotWithTheSameScore_ReportsNothing()
        {
            _board.StartGame();
            _board.Board.AddScore(30);
            _scoreChanges.Clear();

            _board.Board.Prepare(new BoardSnapshot { Score = 30 });

            CollectionAssert.IsEmpty(_scoreChanges);
        }
    }
}
