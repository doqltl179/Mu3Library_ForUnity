using System;
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

        /// <summary>
        /// A command that implements only <see cref="IBoardCommand"/> and does not report itself finished
        /// <br/> when it is disposed, the way a project's own command may be written.
        /// </summary>
        private sealed class RunCountingCommand : IBoardCommand
        {
            public bool IsRunning { get; private set; }
            public bool IsCompleted { get; private set; }

            public int RunCount { get; private set; }

            public void Run()
            {
                RunCount++;
                IsRunning = true;
            }

            public void Dispose()
            {
            }
        }

        /// <summary>
        /// A project's own command that runs a callback of its own once it is disposed, which can reach the
        /// <br/> board while the board is dropping its commands.
        /// </summary>
        private sealed class DisposeCallbackCommand : IBoardCommand
        {
            private readonly Action _onDispose;

            public bool IsRunning { get; private set; }
            public bool IsCompleted => false;

            public DisposeCallbackCommand(Action onDispose)
            {
                _onDispose = onDispose;
            }

            public void Run()
            {
                IsRunning = true;
            }

            public void Dispose()
            {
                _onDispose?.Invoke();
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
        public void PrepareWithSnapshotInRemovedHandler_LeavesTheRestoredItemsOnTheBoard()
        {
            _board.StartGame();
            BoardItem first = _board.SpawnItem(0, new Vector2(0.4f, 0.1f));
            BoardItem second = _board.SpawnItem(0, new Vector2(0.6f, 0.1f));
            MergingCommand merge = new(_board.Board.CommandContext, first, second);
            Assert.IsTrue(_board.Board.EnqueueCommand(merge));

            // The pair is saved as the two items it still is, so the restored board takes the
            // instances back out of the pool, the second item of the pair among them.
            BoardSnapshot snapshot = _board.Board.ExportSnapshot();
            int restoredCount = snapshot.BoardItemsSnapshot.Count;

            bool hasPrepared = false;
            bool isPrepared = false;
            int itemsRemovedAfterPrepare = 0;
            int mergesReported = 0;
            _board.Board.OnItemRemoved += _ =>
            {
                if (isPrepared)
                {
                    itemsRemovedAfterPrepare++;
                    return;
                }

                // Prepare takes the second item of the pair off the old board as well.
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare(snapshot);
            };
            _board.Board.OnBoardPrepared += () => isPrepared = true;
            _board.Board.OnItemMerged += _ => mergesReported++;

            _board.Board.Tick();

            Assert.IsTrue(hasPrepared, "The merge did not take its pair off the board.");
            Assert.AreEqual(2, restoredCount);
            Assert.AreEqual(restoredCount, _board.Board.Items.Count, "The merge took a restored item off the new board.");
            CollectionAssert.Contains(_board.Board.Items, second, "The restored board did not reuse the second item of the pair, so the case is not covered.");
            Assert.AreEqual(0, itemsRemovedAfterPrepare);
            Assert.AreEqual(0, CountItemsOfIndex(1), "The merged item fell onto the restored board.");
            Assert.AreEqual(snapshot.MergeCount, _board.Board.MergeCount);
            Assert.AreEqual(snapshot.Score, _board.Board.Score);
            Assert.AreEqual(0, mergesReported);
            Assert.IsTrue(merge.IsDisposed);
        }

        [Test]
        public void PrepareInMergeComboHandler_LeavesNothingOfTheMergeOnTheNewBoard()
        {
            _board.StartGame();
            MergingCommand merge = EnqueueMerge(0, 0.4f);

            bool hasPrepared = false;
            int mergesReported = 0;
            _board.Board.OnMergeComboChanged += _ =>
            {
                // Prepare drops the combo the merge started and reports that as well.
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare();
            };
            _board.Board.OnItemMerged += _ => mergesReported++;

            _board.Board.Tick();

            Assert.IsTrue(hasPrepared, "The merge sound did not move the combo.");
            Assert.AreEqual(0, _board.Board.Items.Count, "The merged item stayed on the new board.");
            Assert.AreEqual(0, _board.Board.MergeCount, "The merge was counted on the new board.");
            Assert.AreEqual(0, mergesReported, "The merge was reported after the board was prepared again.");
            Assert.IsTrue(merge.IsDisposed);
            Assert.IsNull(merge.MergedItem);
        }

        [Test]
        public void PrepareInRemovedHandler_RemoveItemsCommandPaysNothingIntoTheNewBoard()
        {
            _board.StartGame();
            BoardItem first = _board.SpawnItem(0, new Vector2(0.2f, 0.1f));
            BoardItem second = _board.SpawnItem(2, new Vector2(0.8f, 0.1f));
            int completions = 0;
            RemoveItemsCommand remove = new(_board.Board.CommandContext, new[] { first, second }, true, _ => completions++);
            Assert.IsTrue(_board.Board.EnqueueCommand(remove));

            bool hasPrepared = false;
            bool isPrepared = false;
            int scoresAddedAfterPrepare = 0;
            _board.Board.OnItemRemoved += _ =>
            {
                // Prepare takes the second item off the old board as well.
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare();
            };
            _board.Board.OnBoardPrepared += () => isPrepared = true;
            _board.Board.OnScoreAdded += (_, _) =>
            {
                if (isPrepared)
                {
                    scoresAddedAfterPrepare++;
                }
            };

            _board.Board.Tick();

            Assert.IsTrue(hasPrepared, "The command did not take its first item off the board.");
            Assert.IsTrue(_board.Board.IsPrepared);
            Assert.AreEqual(0, _board.Board.Score, "The removed item paid its points into the new board.");
            Assert.AreEqual(0, scoresAddedAfterPrepare);
            Assert.AreEqual(1, remove.RemovedCount);
            Assert.AreEqual(0, completions, "The command completed on the new board.");
            Assert.IsTrue(remove.IsDisposed);
        }

        [Test]
        public void PrepareWithSnapshotInRemovedHandler_RemoveItemsCommandLeavesTheRestoredItemsOnTheBoard()
        {
            _board.StartGame();
            BoardItem first = _board.SpawnItem(0, new Vector2(0.2f, 0.1f));
            BoardItem second = _board.SpawnItem(2, new Vector2(0.8f, 0.1f));
            RemoveItemsCommand remove = new(_board.Board.CommandContext, new[] { first, second });
            Assert.IsTrue(_board.Board.EnqueueCommand(remove));

            // The two items are saved as they are, so the restored board takes the instances back
            // out of the pool, the second item among them.
            BoardSnapshot snapshot = _board.Board.ExportSnapshot();
            int restoredCount = snapshot.BoardItemsSnapshot.Count;

            bool hasPrepared = false;
            bool isPrepared = false;
            int itemsRemovedAfterPrepare = 0;
            _board.Board.OnItemRemoved += _ =>
            {
                if (isPrepared)
                {
                    itemsRemovedAfterPrepare++;
                    return;
                }

                // Prepare takes the second item off the old board as well.
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare(snapshot);
            };
            _board.Board.OnBoardPrepared += () => isPrepared = true;

            _board.Board.Tick();

            Assert.IsTrue(hasPrepared, "The command did not take its first item off the board.");
            Assert.AreEqual(2, restoredCount);
            Assert.AreEqual(restoredCount, _board.Board.Items.Count, "The command took a restored item off the new board.");
            CollectionAssert.Contains(_board.Board.Items, second, "The restored board did not reuse the second item, so the case is not covered.");
            Assert.AreEqual(0, itemsRemovedAfterPrepare);
            Assert.IsTrue(remove.IsDisposed);
        }

        [Test]
        public void PrepareInRemovedHandler_PromoteItemCommandPutsNothingOnTheNewBoard()
        {
            _board.StartGame();
            BoardItem item = _board.SpawnItem(0, new Vector2(0.5f, 0.1f));
            int promotions = 0;
            PromoteItemCommand promote = new(_board.Board.CommandContext, item, onPromoted: _ => promotions++);
            Assert.IsTrue(_board.Board.EnqueueCommand(promote));

            bool hasPrepared = false;
            bool isPrepared = false;
            int itemsAddedAfterPrepare = 0;
            _board.Board.OnItemRemoved += _ =>
            {
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

            _board.Board.Tick();

            Assert.IsTrue(hasPrepared, "The promotion did not take the item off the board.");
            Assert.AreEqual(0, _board.Board.Items.Count, "The promoted item fell onto the new board.");
            Assert.AreEqual(0, itemsAddedAfterPrepare);
            Assert.AreEqual(0, _board.Board.Score, "The promotion paid its points into the new board.");
            Assert.AreEqual(0, promotions, "The promotion was reported after the board was prepared again.");
            Assert.IsTrue(promote.IsDisposed);
            Assert.IsNull(promote.PromotedItem);
        }

        [Test]
        public void PrepareWithSnapshotInAddedHandler_PromoteItemCommandPaysNothingIntoTheRestoredBoard()
        {
            _board.StartGame();
            _board.Board.AddScore(7);
            BoardItem item = _board.SpawnItem(0, new Vector2(0.5f, 0.1f));
            int promotions = 0;
            PromoteItemCommand promote = new(_board.Board.CommandContext, item, onPromoted: _ => promotions++);
            Assert.IsTrue(_board.Board.EnqueueCommand(promote));

            BoardSnapshot snapshot = _board.Board.ExportSnapshot();

            bool hasPrepared = false;
            int scoresAdded = 0;
            _board.Board.OnItemAdded += _ =>
            {
                // The restored board adds its own items as well.
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare(snapshot);
            };
            _board.Board.OnScoreAdded += (_, _) => scoresAdded++;

            _board.Board.Tick();

            Assert.IsTrue(hasPrepared, "The promotion did not put its item on the board.");
            Assert.AreEqual(snapshot.Score, _board.Board.Score, "The promotion paid its points into the restored board.");
            Assert.AreEqual(0, scoresAdded);
            Assert.AreEqual(snapshot.BoardItemsSnapshot.Count, _board.Board.Items.Count);
            Assert.AreEqual(0, promotions, "The promotion was reported after the board was prepared again.");
            Assert.IsTrue(promote.IsDisposed);
            Assert.IsNull(promote.PromotedItem);
        }

        [Test]
        public void PrepareWithSnapshotInScoreHandler_RemoveItemsCommandLeavesTheRestoredItemsOnTheBoard()
        {
            _board.StartGame();
            BoardItem first = _board.SpawnItem(0, new Vector2(0.2f, 0.1f));
            BoardItem second = _board.SpawnItem(2, new Vector2(0.5f, 0.1f));
            _board.SpawnItem(4, new Vector2(0.8f, 0.1f));
            RemoveItemsCommand remove = new(_board.Board.CommandContext, new[] { first, second }, true);
            Assert.IsTrue(_board.Board.EnqueueCommand(remove));

            // The three items are saved as they are, and the restored board takes three instances back out of
            // the pool, which holds the first item by then, so the second item is among them.
            BoardSnapshot snapshot = _board.Board.ExportSnapshot();
            int restoredCount = snapshot.BoardItemsSnapshot.Count;

            bool hasPrepared = false;
            bool isPrepared = false;
            int itemsRemovedAfterPrepare = 0;
            _board.Board.OnScoreChanged += _ =>
            {
                // Prepare reports the score it restores as well.
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare(snapshot);
            };
            _board.Board.OnBoardPrepared += () => isPrepared = true;
            _board.Board.OnItemRemoved += _ =>
            {
                if (isPrepared)
                {
                    itemsRemovedAfterPrepare++;
                }
            };

            _board.Board.Tick();

            Assert.IsTrue(hasPrepared, "The removed item paid no points.");
            Assert.AreEqual(3, restoredCount);
            Assert.AreEqual(restoredCount, _board.Board.Items.Count, "The command took a restored item off the new board.");
            CollectionAssert.Contains(_board.Board.Items, second, "The restored board did not reuse the second item, so the case is not covered.");
            Assert.AreEqual(0, itemsRemovedAfterPrepare);
            Assert.AreEqual(snapshot.Score, _board.Board.Score, "The command paid points into the restored board.");
            Assert.IsTrue(remove.IsDisposed);
        }

        [Test]
        public void PrepareInScoreHandler_PromoteItemCommandLeavesNoPromotedItem()
        {
            _board.StartGame();
            BoardItem item = _board.SpawnItem(0, new Vector2(0.5f, 0.1f));
            int promotions = 0;
            PromoteItemCommand promote = new(_board.Board.CommandContext, item, onPromoted: _ => promotions++);
            Assert.IsTrue(_board.Board.EnqueueCommand(promote));

            bool hasPrepared = false;
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

            _board.Board.Tick();

            Assert.IsTrue(hasPrepared, "The promotion paid no points.");
            Assert.AreEqual(0, _board.Board.Items.Count, "The promoted item stayed on the new board.");
            Assert.AreEqual(0, _board.Board.Score);
            Assert.AreEqual(0, promotions, "The promotion was reported after the board was prepared again.");
            Assert.IsTrue(promote.IsDisposed);
            Assert.IsNull(promote.PromotedItem, "The command kept the item that went back to the pool with the old board.");
        }

        [Test]
        public void PrepareWithSnapshotInRemovedHandler_TryReplaceItemPutsNoReplacementOnTheRestoredBoard()
        {
            _board.StartGame();
            BoardItem item = _board.SpawnItem(0, new Vector2(0.5f, 0.1f));

            BoardSnapshot snapshot = _board.Board.ExportSnapshot();
            int restoredCount = snapshot.BoardItemsSnapshot.Count;

            bool hasPrepared = false;
            bool isPrepared = false;
            int itemsAddedAfterPrepare = 0;
            _board.Board.OnItemRemoved += _ =>
            {
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare(snapshot);
            };
            _board.Board.OnBoardPrepared += () => isPrepared = true;
            _board.Board.OnItemAdded += _ =>
            {
                if (isPrepared)
                {
                    itemsAddedAfterPrepare++;
                }
            };

            bool isReplaced = _board.Board.TryReplaceItem(item, 1, out BoardItem replacement);

            Assert.IsTrue(hasPrepared, "The item did not leave the board.");
            Assert.IsFalse(isReplaced, "The item was replaced on the restored board.");
            Assert.IsNull(replacement);
            Assert.AreEqual(1, restoredCount);
            Assert.AreEqual(restoredCount, _board.Board.Items.Count, "The replacement fell onto the restored board.");
            Assert.AreEqual(0, CountItemsOfIndex(1));
            Assert.AreEqual(0, itemsAddedAfterPrepare);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PrepareInScoreHandler_AddScoreReportsNoAddedPoints(bool restoresSnapshot)
        {
            _board.StartGame();
            _board.Board.AddScore(3);
            BoardSnapshot snapshot = restoresSnapshot ? _board.Board.ExportSnapshot() : null;
            _board.Board.AddScore(7);
            int startingScore = restoresSnapshot ? snapshot.Score : 0;

            bool hasPrepared = false;
            List<int> addedPoints = new();
            _board.Board.OnScoreChanged += _ =>
            {
                // Prepare reports the score it starts over with as well.
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare(snapshot);
            };
            _board.Board.OnScoreAdded += (added, _) => addedPoints.Add(added);

            _board.Board.AddScore(5);

            Assert.IsTrue(hasPrepared);
            Assert.AreEqual(startingScore, _board.Board.Score);
            CollectionAssert.IsEmpty(addedPoints, "The points were reported after the board was prepared again.");
        }

        [Test]
        public void PrepareInEmptiedHandHandler_OnDragEndDropsNothingOntoTheNewBoard()
        {
            _board.StartGame();
            _board.Board.Tick();
            BoardItem heldItem = _board.Board.HoldingItem;
            Assert.IsNotNull(heldItem, "The running board did not hand out an item.");

            bool hasPrepared = false;
            int drops = 0;
            _board.Board.OnHoldingItemChanged += item =>
            {
                if (item != null || hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare();
            };
            _board.Board.OnItemDropped += _ => drops++;

            // Released where the item waits, so the release does not move it first.
            _board.Board.OnDragEnd(_board.GetScreenPosition(_board.Board.HoldingNormalizedX));

            Assert.IsTrue(hasPrepared, "The release did not empty the hand.");
            Assert.IsTrue(_board.Board.IsPrepared);
            Assert.IsFalse(_board.Board.IsRunning);
            Assert.AreEqual(0, _board.Board.Items.Count, "The held item fell onto the new board.");
            Assert.AreEqual(0, _board.Board.SpawnCount, "The drop was counted on the new board.");
            Assert.AreEqual(0, drops, "The drop was reported after the board was prepared again.");
            Assert.IsFalse(heldItem.gameObject.activeSelf, "The held item was left outside the pool.");
        }

        [Test]
        public void PrepareInAddedHandler_OnDragEndCountsNoDropOnTheNewBoard()
        {
            _board.StartGame();
            _board.Board.Tick();
            Assert.IsNotNull(_board.Board.HoldingItem, "The running board did not hand out an item.");

            bool hasPrepared = false;
            int drops = 0;
            _board.Board.OnItemAdded += _ =>
            {
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare();
            };
            _board.Board.OnItemDropped += _ => drops++;

            _board.Board.OnDragEnd(_board.GetScreenPosition(_board.Board.HoldingNormalizedX));

            Assert.IsTrue(hasPrepared, "The released item did not join the board.");
            Assert.AreEqual(0, _board.Board.Items.Count, "The dropped item stayed on the new board.");
            Assert.AreEqual(0, _board.Board.SpawnCount, "The drop was counted on the new board.");
            Assert.AreEqual(0, drops, "The drop was reported after the board was prepared again.");
        }

        [Test]
        public void PrepareInMovedHandler_OnDragEndDropsNothingOntoTheNewBoard()
        {
            _board.StartGame();
            _board.Board.Tick();
            Assert.IsNotNull(_board.Board.HoldingItem, "The running board did not hand out an item.");

            bool hasPrepared = false;
            int drops = 0;
            _board.Board.OnHoldingItemMoved += _ =>
            {
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare();
            };
            _board.Board.OnItemDropped += _ => drops++;

            // Released away from where the item waits, so the release moves it first.
            _board.Board.OnDragEnd(_board.GetScreenPosition(0.2f));

            Assert.IsTrue(hasPrepared, "The release did not move the held item.");
            Assert.IsNull(_board.Board.HoldingItem);
            Assert.AreEqual(0, _board.Board.Items.Count, "An item fell onto the new board.");
            Assert.AreEqual(0, _board.Board.SpawnCount, "The drop was counted on the new board.");
            Assert.AreEqual(0, drops, "The drop was reported after the board was prepared again.");
        }

        [Test]
        public void PrepareWithSnapshotInHoldingHandler_SpawnHoldingItemKeepsTheRestoredPreview()
        {
            _board.StartGame();
            _board.Board.Tick();
            BoardSnapshot snapshot = _board.Board.ExportSnapshot();
            Assert.GreaterOrEqual(snapshot.HoldingItemIndex, 0, "The snapshot holds no item in hand.");

            _board.Board.Prepare();
            Assert.IsNull(_board.Board.HoldingItem);

            bool hasPrepared = false;
            bool isPrepared = false;
            int previewsAfterPrepare = 0;
            _board.Board.OnHoldingItemChanged += item =>
            {
                // Prepare empties the hand again, which it reports as well.
                if (item == null || hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare(snapshot);
            };
            _board.Board.OnBoardPrepared += () => isPrepared = true;
            _board.Board.OnNextItemChanged += _ =>
            {
                if (isPrepared)
                {
                    previewsAfterPrepare++;
                }
            };

            bool isHolding = _board.Board.SpawnHoldingItem();

            Assert.IsTrue(hasPrepared, "No item came into the hand.");
            Assert.IsFalse(isHolding, "An item was reported in hand after the board was prepared again.");
            Assert.IsNull(_board.Board.HoldingItem);
            Assert.AreEqual(snapshot.HoldingItemIndex, _board.Board.NextItemIndex, "The restored preview was drawn over.");
            Assert.AreEqual(0, previewsAfterPrepare, "The preview was drawn again after the board was prepared again.");
        }

        [Test]
        public void PrepareWithSnapshotInRemovedHandlerOfPrepare_KeepsTheInnerBoard()
        {
            _board.StartGame();
            _board.SpawnItem(0, new Vector2(0.3f, 0.1f));
            BoardSnapshot innerSnapshot = _board.Board.ExportSnapshot();
            _board.SpawnItem(2, new Vector2(0.7f, 0.1f));
            BoardSnapshot outerSnapshot = _board.Board.ExportSnapshot();

            bool hasPrepared = false;
            int prepares = 0;
            _board.Board.OnItemRemoved += _ =>
            {
                // The inner Prepare clears the item that is left as well.
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare(innerSnapshot);
            };
            _board.Board.OnBoardPrepared += () => prepares++;

            _board.Board.Prepare(outerSnapshot);

            Assert.IsTrue(hasPrepared, "Prepare did not clear the board.");
            Assert.AreEqual(1, prepares, "The board was reported prepared more than once.");
            Assert.AreEqual(innerSnapshot.BoardItemsSnapshot.Count, _board.Board.Items.Count, "The outer Prepare cleared or restored over the inner board.");
            Assert.AreEqual(0, CountItemsOfIndex(2), "The outer snapshot was restored over the inner board.");
        }

        [Test]
        public void PrepareInAddedHandlerOfARestore_KeepsTheInnerBoard()
        {
            _board.StartGame();
            _board.SpawnItem(0, new Vector2(0.3f, 0.1f));
            _board.SpawnItem(2, new Vector2(0.7f, 0.1f));
            BoardSnapshot snapshot = _board.Board.ExportSnapshot();

            bool hasPrepared = false;
            int prepares = 0;
            _board.Board.OnItemAdded += _ =>
            {
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare();
            };
            _board.Board.OnBoardPrepared += () => prepares++;

            _board.Board.Prepare(snapshot);

            Assert.IsTrue(hasPrepared, "The snapshot restored no item.");
            Assert.AreEqual(1, prepares, "The board was reported prepared more than once.");
            Assert.AreEqual(0, _board.Board.Items.Count, "The rest of the snapshot was restored onto the inner board.");
        }

        [Test]
        public void PrepareInPreviewHandlerOfPrepare_ReportsTheBoardOnce()
        {
            _board.StartGame();

            bool hasPrepared = false;
            int prepares = 0;
            _board.Board.OnNextItemChanged += _ =>
            {
                // The inner Prepare draws its own preview as well.
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare();
            };
            _board.Board.OnBoardPrepared += () => prepares++;

            _board.Board.Prepare();

            Assert.IsTrue(hasPrepared, "Prepare drew no preview.");
            Assert.AreEqual(1, prepares, "The board was reported prepared more than once.");
        }

        [Test]
        public void PrepareInScoreHandlerOfPrepare_ReportsTheBoardOnce()
        {
            _board.StartGame();
            _board.Board.AddScore(5);

            bool hasPrepared = false;
            int prepares = 0;
            _board.Board.OnScoreChanged += _ =>
            {
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare();
            };
            _board.Board.OnBoardPrepared += () => prepares++;

            _board.Board.Prepare();

            Assert.IsTrue(hasPrepared, "Prepare did not report the score it starts over with.");
            Assert.AreEqual(1, prepares, "The board was reported prepared more than once.");
            Assert.AreEqual(0, _board.Board.Score);
        }

        [Test]
        public void PrepareInAddedHandler_TrySpawnItemHandsBackNoItem()
        {
            _board.StartGame();

            bool hasPrepared = false;
            _board.Board.OnItemAdded += _ =>
            {
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare();
            };

            bool isSpawned = _board.Board.CommandContext.TrySpawnItem(0, new Vector2(0.5f, 0.1f), out BoardItem item);

            Assert.IsTrue(hasPrepared, "The item did not join the board.");
            Assert.IsFalse(isSpawned, "The spawn was reported after the board was prepared again.");
            Assert.IsNull(item, "The item that went back to the pool was handed out.");
            Assert.AreEqual(0, _board.Board.Items.Count);
        }

        [Test]
        public void PrepareWithSnapshotInAddedHandler_SpawnItemCommandKeepsNoItem()
        {
            _board.StartGame();
            _board.SpawnItem(2, new Vector2(0.7f, 0.1f));
            BoardSnapshot snapshot = _board.Board.ExportSnapshot();

            int spawns = 0;
            SpawnItemCommand spawn = new(_board.Board.CommandContext, 0, new Vector2(0.3f, 0.1f), _ => spawns++);
            Assert.IsTrue(_board.Board.EnqueueCommand(spawn));

            bool hasPrepared = false;
            _board.Board.OnItemAdded += _ =>
            {
                // The restored board adds its own items as well.
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare(snapshot);
            };

            _board.Board.Tick();

            Assert.IsTrue(hasPrepared, "The command put no item on the board.");
            Assert.AreEqual(snapshot.BoardItemsSnapshot.Count, _board.Board.Items.Count);
            Assert.AreEqual(0, CountItemsOfIndex(0), "The spawned item stayed on the restored board.");
            Assert.IsNull(spawn.SpawnedItem, "The command kept the item that went back to the pool with the old board.");
            Assert.AreEqual(0, spawns, "The spawn was reported after the board was prepared again.");
            Assert.IsTrue(spawn.IsDisposed);
        }

        [Test]
        public void PrepareInEmptiedHandHandler_GameEndReportsNoEnd()
        {
            _board.StartGame();
            _board.Board.Tick();
            Assert.IsNotNull(_board.Board.HoldingItem, "The running board did not hand out an item.");

            bool hasPrepared = false;
            int ends = 0;
            _board.Board.OnHoldingItemChanged += item =>
            {
                if (item != null || hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare();
            };
            _board.Board.OnGameEnded += () => ends++;

            _board.Board.GameEnd();

            Assert.IsTrue(hasPrepared, "The game end did not empty the hand.");
            Assert.IsTrue(_board.Board.IsPrepared);
            Assert.IsFalse(_board.Board.IsEnded, "The new board was left ended.");
            Assert.AreEqual(0, ends, "The end of the old game was reported on the new board.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PrepareInPreviewHandlerOfPrepare_ReportsTheScoreTheBoardEndsWith(bool restoresSnapshot)
        {
            _board.StartGame();
            _board.Board.AddScore(3);
            BoardSnapshot snapshot = restoresSnapshot ? _board.Board.ExportSnapshot() : null;
            _board.Board.AddScore(7);

            bool hasPrepared = false;
            List<int> reportedScores = new();
            _board.Board.OnNextItemChanged += _ =>
            {
                // The inner Prepare draws its own preview as well.
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare(snapshot);
            };
            _board.Board.OnScoreChanged += reportedScores.Add;

            _board.Board.Prepare(snapshot);

            Assert.IsTrue(hasPrepared, "Prepare drew no preview.");
            Assert.AreEqual(restoresSnapshot ? 3 : 0, _board.Board.Score);
            Assert.IsNotEmpty(reportedScores, "Nobody was told that the score changed.");
            Assert.AreEqual(_board.Board.Score, reportedScores[reportedScores.Count - 1], "The last reported score is not the score of the board.");
        }

        [Test]
        public void PrepareInMergeComboHandlerOfPrepare_ReportsTheBoardOnce()
        {
            _board.StartGame();
            EnqueueMerge();
            _board.Board.Tick();
            Assert.AreEqual(1, _board.Board.MergeCount, "The merge that starts the combo did not run.");

            bool hasPrepared = false;
            int prepares = 0;
            RunCountingCommand innerCommand = new();
            _board.Board.OnMergeComboChanged += _ =>
            {
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare();
            };
            _board.Board.OnBoardPrepared += () =>
            {
                prepares++;

                // The board the game goes on with takes a command of its own.
                _board.Board.EnqueueCommand(innerCommand);
            };

            _board.Board.Prepare();

            Assert.IsTrue(hasPrepared, "Prepare did not drop the combo.");
            Assert.AreEqual(1, prepares, "The board was reported prepared more than once.");
            CollectionAssert.AreEqual(new IBoardCommand[] { innerCommand }, _board.Board.Commands, "The outer Prepare dropped the command of the inner board.");
        }

        [Test]
        public void PrepareInDisposeOfACommand_KeepsTheCommandsOfTheInnerBoard()
        {
            _board.StartGame();

            bool hasPrepared = false;
            int prepares = 0;
            RunCountingCommand innerCommand = new();
            DisposeCallbackCommand command = new(() =>
            {
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare();
            });
            Assert.IsTrue(_board.Board.EnqueueCommand(command));
            _board.Board.OnBoardPrepared += () =>
            {
                prepares++;

                // The board the game goes on with takes a command of its own and hands out its first item.
                _board.Board.EnqueueCommand(innerCommand);
                _board.Board.SpawnHoldingItem();
            };

            _board.Board.Prepare();

            Assert.IsTrue(hasPrepared, "Prepare did not dispose the command.");
            Assert.IsNotNull(_board.Board.HoldingItem, "The outer Prepare emptied the hand of the inner board.");
            Assert.AreEqual(1, prepares, "The board was reported prepared more than once.");
            CollectionAssert.AreEqual(new IBoardCommand[] { innerCommand }, _board.Board.Commands, "The outer Prepare dropped the command of the inner board.");
        }

        [Test]
        public void PrepareWithSnapshotInEmptiedHandHandlerOfPrepare_KeepsTheInnerBoard()
        {
            _board.StartGame();
            _board.SpawnItem(0, new Vector2(0.3f, 0.1f));
            _board.SpawnItem(2, new Vector2(0.7f, 0.1f));
            BoardSnapshot snapshot = _board.Board.ExportSnapshot();
            _board.Board.Tick();
            Assert.IsNotNull(_board.Board.HoldingItem, "The running board did not hand out an item.");

            bool hasPrepared = false;
            int prepares = 0;
            _board.Board.OnHoldingItemChanged += item =>
            {
                if (item != null || hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare(snapshot);
            };
            _board.Board.OnBoardPrepared += () => prepares++;

            _board.Board.Prepare();

            Assert.IsTrue(hasPrepared, "Prepare did not empty the hand.");
            Assert.AreEqual(1, prepares, "The board was reported prepared more than once.");
            Assert.AreEqual(snapshot.BoardItemsSnapshot.Count, _board.Board.Items.Count, "The outer Prepare cleared the restored board.");
            Assert.IsNull(_board.Board.HoldingItem);
        }

        [Test]
        public void PrepareInFirstPreviewHandler_SpawnHoldingItemPutsNothingIntoTheHand()
        {
            _board.StartGame();
            _board.Board.ForgetNextItemIndex();

            bool hasPrepared = false;
            _board.Board.OnNextItemChanged += _ =>
            {
                // The inner Prepare draws its own preview as well.
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare();
            };

            bool isHolding = _board.Board.SpawnHoldingItem();

            Assert.IsTrue(hasPrepared, "No preview was drawn before the item was handed out.");
            Assert.IsFalse(isHolding, "An item was reported in hand after the board was prepared again.");
            Assert.IsNull(_board.Board.HoldingItem, "An item came into the hand of the new board.");
        }

        [Test]
        public void PrepareInPreviewHandler_SpawnHoldingItemReportsAnEmptyHand()
        {
            _board.StartGame();
            Assert.IsNull(_board.Board.HoldingItem);

            bool hasPrepared = false;
            _board.Board.OnNextItemChanged += _ =>
            {
                // The inner Prepare draws its own preview as well.
                if (hasPrepared)
                {
                    return;
                }

                hasPrepared = true;
                _board.Board.Prepare();
            };

            bool isHolding = _board.Board.SpawnHoldingItem();

            Assert.IsTrue(hasPrepared, "No preview was drawn after the item was handed out.");
            Assert.IsFalse(isHolding, "An item was reported in hand after the board was prepared again.");
            Assert.IsNull(_board.Board.HoldingItem);
        }

        [Test]
        public void PrepareAndStartInMergeHandler_RunsNoOtherCommandOfTheGroupOnTheNewBoard()
        {
            _board.StartGame();
            MergingCommand firstMerge = CreateMerge(0, 0.2f);
            MergingCommand secondMerge = CreateMerge(3, 0.7f);
            RunCountingCommand plainCommand = new();
            ParallelCommand group = new(firstMerge, secondMerge, plainCommand);
            Assert.IsTrue(_board.Board.EnqueueCommand(group));

            List<BoardMergeInfo> merges = new();
            _board.Board.OnItemMerged += info =>
            {
                merges.Add(info);
                _board.Board.Prepare();
                _board.Board.GameStart();
            };

            _board.Board.Tick();

            Assert.IsTrue(_board.Board.IsRunning, "The board did not start again.");
            Assert.AreEqual(1, merges.Count, "The group merged again on the new board.");
            Assert.AreEqual(0, _board.Board.MergeCount);
            Assert.AreEqual(0, _board.Board.Score);
            Assert.AreEqual(0, CountItemsOfIndex(3), "The pair of the second merge is still on the board.");
            Assert.IsTrue(group.IsDisposed);
            Assert.IsTrue(secondMerge.IsDisposed);

            // The board runs again and its guard allows the group, but the group itself was dropped.
            Assert.AreEqual(0, plainCommand.RunCount, "A command of the dropped group ran on the new board.");
            Assert.AreEqual(0, _board.Board.CommandCount);
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
