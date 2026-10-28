using System.Collections.Generic;
using Mu3Library.Game.WatermelonGame.Board;
using Mu3Library.Game.WatermelonGame.Board.Command;
using Mu3Library.Game.WatermelonGame.Board.Command.Flow;
using Mu3Library.Game.WatermelonGame.Board.Command.Item;
using Mu3Library.Game.WatermelonGame.Board.Command.Score;
using Mu3Library.Game.WatermelonGame.Board.Item;
using NUnit.Framework;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Tests
{
    public class BoardControllerLifecycleTests
    {
        private readonly struct ItemState
        {
            private readonly int _index;
            private readonly BoardItemInfo _info;
            private readonly Sprite _sprite;
            private readonly float _colliderRadius;
            private readonly Vector2 _colliderOffset;
            private readonly bool _colliderEnabled;
            private readonly RigidbodyType2D _bodyType;
            private readonly float _gravityScale;
            private readonly float _linearDamping;
            private readonly float _angularDamping;
            private readonly Vector3 _localPosition;
            private readonly Vector3 _localScale;

            public ItemState(BoardItem item)
            {
                _index = item.Index;
                _info = item.Info;
                _sprite = item.GetComponent<SpriteRenderer>().sprite;
                _colliderRadius = item.Collider.radius;
                _colliderOffset = item.Collider.offset;
                _colliderEnabled = item.ColliderEnabled;
                _bodyType = item.BodyType;
                _gravityScale = item.GravityScale;
                _linearDamping = item.LinearDamping;
                _angularDamping = item.AngularDamping;
                _localPosition = item.transform.localPosition;
                _localScale = item.transform.localScale;
            }

            public void AssertUnchanged(BoardItem item, string itemName)
            {
                Assert.AreEqual(_index, item.Index, $"{itemName} index");
                Assert.AreSame(_info, item.Info, $"{itemName} catalog entry");
                Assert.AreSame(_sprite, item.GetComponent<SpriteRenderer>().sprite, $"{itemName} sprite");
                Assert.AreEqual(_colliderRadius, item.Collider.radius, $"{itemName} collider radius");
                Assert.AreEqual(_colliderOffset, item.Collider.offset, $"{itemName} collider offset");
                Assert.AreEqual(_colliderEnabled, item.ColliderEnabled, $"{itemName} collider enabled");
                Assert.AreEqual(_bodyType, item.BodyType, $"{itemName} body type");
                Assert.AreEqual(_gravityScale, item.GravityScale, $"{itemName} gravity scale");
                Assert.AreEqual(_linearDamping, item.LinearDamping, $"{itemName} linear damping");
                Assert.AreEqual(_angularDamping, item.AngularDamping, $"{itemName} angular damping");
                Assert.AreEqual(_localPosition, item.transform.localPosition, $"{itemName} position");
                Assert.AreEqual(_localScale, item.transform.localScale, $"{itemName} scale");
            }
        }

        private TestBoard _board;

        [SetUp]
        public void SetUp()
        {
            _board = new TestBoard();
        }

        [TearDown]
        public void TearDown()
        {
            _board?.Dispose();
            _board = null;
        }

        [Test]
        public void HidingBoardHierarchy_KeepsBoardItemsAndHeldItem()
        {
            _board.StartGame();
            BoardItem boardItem = _board.SpawnItem(2, new Vector2(0.3f, 0.2f));

            _board.Board.Tick();
            BoardItem heldItem = _board.Board.HoldingItem;
            Assert.IsNotNull(heldItem, "The running board did not hand out an item.");

            ItemState boardItemState = new ItemState(boardItem);
            ItemState heldItemState = new ItemState(heldItem);

            _board.Area.gameObject.SetActive(false);
            TestBoard.SendOnDisable(boardItem);
            TestBoard.SendOnDisable(heldItem);
            _board.Area.gameObject.SetActive(true);

            boardItemState.AssertUnchanged(boardItem, "Board item");
            heldItemState.AssertUnchanged(heldItem, "Held item");

            CollectionAssert.Contains(_board.Board.Items, boardItem);
            Assert.AreSame(heldItem, _board.Board.HoldingItem);

            // An item that kept its index still counts for the rest of the board.
            BoardSnapshot snapshot = _board.Board.ExportSnapshot();
            Assert.AreEqual(1, snapshot.BoardItemsSnapshot.Count);
            Assert.AreEqual(2, snapshot.BoardItemsSnapshot[0].Index);
            Assert.AreEqual(heldItem.Index, snapshot.HoldingItemIndex);
        }

        [Test]
        public void RemoveItem_ResetsTheItemForThePool()
        {
            _board.StartGame();
            BoardItem item = _board.SpawnItem(2, new Vector2(0.3f, 0.2f));

            Assert.IsTrue(_board.Board.CommandContext.RemoveItem(item));

            Assert.AreEqual(-1, item.Index);
            Assert.IsNull(item.Info);
            Assert.IsNull(item.GetComponent<SpriteRenderer>().sprite);
            Assert.AreEqual(RigidbodyType2D.Kinematic, item.BodyType);
            Assert.IsFalse(item.gameObject.activeSelf);
        }

        [Test]
        public void GameEndInMergeHandler_HandsOutNoItemAfterTheEnd()
        {
            _board.StartGame();
            EnqueueMerge();

            bool hasEnded = false;
            int itemsHandedOutAfterEnd = 0;
            _board.Board.OnItemMerged += _ => _board.Board.GameEnd();
            _board.Board.OnGameEnded += () => hasEnded = true;
            _board.Board.OnHoldingItemChanged += item =>
            {
                if (hasEnded && item != null)
                {
                    itemsHandedOutAfterEnd++;
                }
            };

            _board.Board.Tick();

            Assert.IsTrue(hasEnded, "The merge did not run.");
            Assert.IsTrue(_board.Board.IsEnded);
            Assert.IsNull(_board.Board.HoldingItem);
            Assert.AreEqual(0, itemsHandedOutAfterEnd);
        }

        [Test]
        public void GameEndInMergeHandler_LeavesTheOtherCommandsOfThatFrame()
        {
            _board.StartGame();
            MergingCommand firstMerge = EnqueueMerge(0, 0.2f);
            MergingCommand secondMerge = EnqueueMerge(3, 0.7f);

            List<BoardMergeInfo> merges = new();
            List<int> scoresAfterEnd = new();
            _board.Board.OnItemMerged += info =>
            {
                merges.Add(info);
                _board.Board.GameEnd();
            };
            _board.Board.OnScoreChanged += score =>
            {
                if (_board.Board.IsEnded)
                {
                    scoresAfterEnd.Add(score);
                }
            };

            _board.Board.Tick();

            Assert.IsTrue(_board.Board.IsEnded, "The merge did not run.");
            AssertOnlyFirstMergeRan(firstMerge, secondMerge, merges);
            CollectionAssert.IsEmpty(scoresAfterEnd, "The score changed after the game ended.");

            // The ended board advances no command, and preparing it again drops what was left.
            _board.Board.Tick();
            Assert.AreEqual(1, merges.Count, "A command ran on the ended board.");

            _board.Board.Prepare();
            Assert.AreEqual(0, _board.Board.CommandCount);
            Assert.IsTrue(secondMerge.IsDisposed, "Prepare did not drop the command that was left.");
        }

        [Test]
        public void GameEndInScoreHandler_LeavesTheOtherCommandsOfThatFrame()
        {
            _board.StartGame();
            MergingCommand firstMerge = EnqueueMerge(0, 0.2f);
            MergingCommand secondMerge = EnqueueMerge(3, 0.7f);

            List<BoardMergeInfo> merges = new();
            List<int> scores = new();
            _board.Board.OnItemMerged += merges.Add;
            _board.Board.OnScoreChanged += score =>
            {
                scores.Add(score);
                _board.Board.GameEnd();
            };

            _board.Board.Tick();

            Assert.IsTrue(_board.Board.IsEnded, "The merge did not run.");

            // The merge that paid the points finishes its step and still reports itself.
            AssertOnlyFirstMergeRan(firstMerge, secondMerge, merges);
            CollectionAssert.AreEqual(new[] { _board.Board.ScoreRule.GetScore(0) }, scores);
        }

        [Test]
        public void PrepareInMergeHandler_HandsOutNoItemBeforeTheStart()
        {
            _board.StartGame();
            EnqueueMerge();

            int mergeCount = 0;
            _board.Board.OnItemMerged += _ =>
            {
                mergeCount++;
                _board.Board.Prepare();
            };

            _board.Board.Tick();

            Assert.AreEqual(1, mergeCount, "The merge did not run.");
            Assert.IsTrue(_board.Board.IsPrepared);
            Assert.IsFalse(_board.Board.IsRunning);
            Assert.IsNull(_board.Board.HoldingItem);
            Assert.AreEqual(0, _board.Board.Items.Count);
        }

        [Test]
        public void GameEndInMergeHandler_StopsTheParallelGroupOfThatFrame()
        {
            _board.StartGame();
            MergingCommand firstMerge = CreateMerge(0, 0.2f);
            MergingCommand secondMerge = CreateMerge(3, 0.7f);
            ParallelCommand group = new(firstMerge, secondMerge);
            Assert.IsTrue(_board.Board.EnqueueCommand(group));

            List<BoardMergeInfo> merges = new();
            _board.Board.OnItemMerged += info =>
            {
                merges.Add(info);
                _board.Board.GameEnd();
            };

            _board.Board.Tick();

            Assert.IsTrue(_board.Board.IsEnded, "The merge did not run.");
            Assert.AreEqual(1, merges.Count, "The group merged again after the game ended.");
            Assert.AreEqual(firstMerge.ItemIndex, merges[0].ItemIndex);
            Assert.AreEqual(_board.Board.ScoreRule.GetScore(firstMerge.ItemIndex), _board.Board.Score);
            Assert.AreEqual(1, _board.Board.MergeCount);

            Assert.IsFalse(secondMerge.IsRunning || secondMerge.IsCompleted, "The second merge was advanced.");
            Assert.AreEqual(2, CountItemsOfIndex(secondMerge.ItemIndex), "The pair of the second merge left the board.");
            Assert.IsFalse(group.IsCompleted);
            CollectionAssert.AreEqual(new IBoardCommand[] { group }, _board.Board.Commands);

            _board.Board.Prepare();
            Assert.IsTrue(secondMerge.IsDisposed, "Prepare did not drop the group that was left.");
        }

        [Test]
        public void GameEndInMergeHandler_StopsTheSequenceOfThatFrame()
        {
            _board.StartGame();
            MergingCommand merge = CreateMerge(0, 0.4f);
            AddScoreCommand bonus = new(_board.Board.CommandContext, 100);
            SequenceCommand sequence = new(merge, bonus);
            Assert.IsTrue(_board.Board.EnqueueCommand(sequence));

            _board.Board.OnItemMerged += _ => _board.Board.GameEnd();

            _board.Board.Tick();

            Assert.IsTrue(_board.Board.IsEnded, "The merge did not run.");
            Assert.AreEqual(_board.Board.ScoreRule.GetScore(0), _board.Board.Score, "The step after the merge paid into the ended game.");
            Assert.IsFalse(bonus.IsRunning || bonus.IsCompleted, "The step after the merge started.");
            Assert.AreEqual(1, sequence.CurrentIndex);
            Assert.IsFalse(sequence.IsCompleted);
        }

        [Test]
        public void GameEndInMergeHandler_StopsTheGroupsInsideAGroup()
        {
            _board.StartGame();
            MergingCommand merge = CreateMerge(0, 0.4f);
            AddScoreCommand bonus = new(_board.Board.CommandContext, 100);
            ParallelCommand group = new(new SequenceCommand(merge, bonus));
            Assert.IsTrue(_board.Board.EnqueueCommand(group));

            _board.Board.OnItemMerged += _ => _board.Board.GameEnd();

            _board.Board.Tick();

            Assert.IsTrue(_board.Board.IsEnded, "The merge did not run.");
            Assert.AreEqual(_board.Board.ScoreRule.GetScore(0), _board.Board.Score, "The inner sequence paid into the ended game.");
            Assert.IsFalse(bonus.IsRunning || bonus.IsCompleted, "The step after the merge started.");
        }

        [Test]
        public void PrepareInScoreHandler_LeavesNothingOfTheMergeOnTheNewBoard()
        {
            _board.StartGame();
            MergingCommand merge = EnqueueMerge(0, 0.4f);

            bool hasPrepared = false;
            bool isPrepared = false;
            int mergesReported = 0;
            int itemsAddedAfterPrepare = 0;
            _board.Board.OnScoreChanged += _ =>
            {
                // Prepare reports the score it starts over with as well.
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare();
            };
            _board.Board.OnBoardPrepared += () => isPrepared = true;
            _board.Board.OnItemAdded += _ =>
            {
                if (isPrepared)
                {
                    itemsAddedAfterPrepare++;
                }
            };
            _board.Board.OnItemMerged += _ => mergesReported++;

            _board.Board.Tick();

            Assert.IsTrue(hasPrepared, "The merge did not pay its points.");
            Assert.IsTrue(_board.Board.IsPrepared);
            Assert.IsFalse(_board.Board.IsRunning);
            Assert.AreEqual(0, _board.Board.Items.Count, "The merged item fell onto the new board.");
            Assert.AreEqual(0, itemsAddedAfterPrepare);
            Assert.AreEqual(0, _board.Board.MergeCount, "The merge was counted on the new board.");
            Assert.AreEqual(0, _board.Board.Score);
            Assert.AreEqual(0, mergesReported, "The merge was reported after the board was prepared again.");
            Assert.IsTrue(merge.IsDisposed);
            Assert.IsNull(merge.MergedItem);
        }

        [Test]
        public void DragOnRunningBoard_MovesAndDropsTheHeldItem()
        {
            _board.StartGame();
            _board.Board.Tick();
            BoardItem heldItem = _board.Board.HoldingItem;
            Assert.IsNotNull(heldItem, "The running board did not hand out an item.");

            int dropCount = 0;
            _board.Board.OnItemDropped += _ => dropCount++;

            Vector2 screenPosition = GetScreenPosition(new Vector2(0.25f, 0.5f));
            _board.Board.OnDragging(screenPosition);

            Assert.AreEqual(0.25f, _board.Board.HoldingNormalizedX, 0.01f);

            _board.Board.OnDragEnd(screenPosition);

            Assert.IsNull(_board.Board.HoldingItem);
            Assert.AreEqual(1, dropCount);
            Assert.AreEqual(1, _board.Board.SpawnCount);
            CollectionAssert.Contains(_board.Board.Items, heldItem);
        }

        [Test]
        public void DragOnBoardThatIsNotRunning_NeitherMovesNorDropsTheHeldItem()
        {
            _board.StartGame();
            _board.Board.GameEnd();

            // Only a subclass or a handler could still put an item into the hand of an ended board.
            Assert.IsTrue(_board.Board.SpawnHoldingItem());
            BoardItem heldItem = _board.Board.HoldingItem;
            Vector3 heldPosition = heldItem.transform.localPosition;

            int dropCount = 0;
            _board.Board.OnItemDropped += _ => dropCount++;

            Vector2 screenPosition = GetScreenPosition(new Vector2(0.25f, 0.5f));
            _board.Board.OnDragging(screenPosition);

            Assert.AreEqual(heldPosition, heldItem.transform.localPosition);

            _board.Board.OnDragEnd(screenPosition);

            Assert.AreSame(heldItem, _board.Board.HoldingItem);
            Assert.AreEqual(heldPosition, heldItem.transform.localPosition);
            Assert.AreEqual(0, dropCount);
            Assert.AreEqual(0, _board.Board.SpawnCount);
            CollectionAssert.DoesNotContain(_board.Board.Items, heldItem);
        }

        /// <summary>
        /// Puts two items of one entry on the board and hands the board the merge of the pair,
        /// <br/> which runs on the next frame and raises the merge and score events from there.
        /// </summary>
        private void EnqueueMerge()
            => EnqueueMerge(0, 0.4f);

        /// <summary>
        /// Puts two items of one entry on the board, side by side from a fraction of the board width on,
        /// <br/> and hands the board the merge of the pair.
        /// </summary>
        private MergingCommand EnqueueMerge(int itemIndex, float boardNormalizedX)
        {
            MergingCommand merge = CreateMerge(itemIndex, boardNormalizedX);
            Assert.IsTrue(_board.Board.EnqueueCommand(merge));

            return merge;
        }

        /// <summary>
        /// Puts two items of one entry on the board, side by side from a fraction of the board width on,
        /// <br/> and builds the merge of the pair without handing it to the board.
        /// </summary>
        private MergingCommand CreateMerge(int itemIndex, float boardNormalizedX)
        {
            BoardItem first = _board.SpawnItem(itemIndex, new Vector2(boardNormalizedX, 0.1f));
            BoardItem second = _board.SpawnItem(itemIndex, new Vector2(boardNormalizedX + 0.2f, 0.1f));

            return new MergingCommand(_board.Board.CommandContext, first, second);
        }

        /// <summary>
        /// The first merge ran and paid its points alone, and the second one is still held, with its pair on the board.
        /// </summary>
        private void AssertOnlyFirstMergeRan(MergingCommand firstMerge, MergingCommand secondMerge, List<BoardMergeInfo> merges)
        {
            Assert.AreEqual(1, merges.Count, "Another merge was reported after the game ended.");
            Assert.AreEqual(firstMerge.ItemIndex, merges[0].ItemIndex);
            Assert.AreEqual(_board.Board.ScoreRule.GetScore(firstMerge.ItemIndex), _board.Board.Score);
            Assert.AreEqual(1, _board.Board.MergeCount);

            Assert.IsFalse(secondMerge.IsRunning || secondMerge.IsCompleted, "The second merge was advanced.");
            CollectionAssert.AreEqual(new IBoardCommand[] { secondMerge }, _board.Board.Commands);
            Assert.AreEqual(2, CountItemsOfIndex(secondMerge.ItemIndex), "The pair of the second merge left the board.");
        }

        private int CountItemsOfIndex(int itemIndex)
        {
            int count = 0;
            foreach (BoardItem item in _board.Board.Items)
            {
                if (item != null && item.Index == itemIndex)
                {
                    count++;
                }
            }

            return count;
        }

        private Vector2 GetScreenPosition(Vector2 boardNormalizedPosition)
        {
            Vector3 screenPosition = _board.Area.BoardLocalNormalizedPositionToScreen(boardNormalizedPosition);
            return new Vector2(screenPosition.x, screenPosition.y);
        }
    }
}
