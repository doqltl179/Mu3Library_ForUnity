using Mu3Library.Game.WatermelonGame.Board;
using Mu3Library.Game.WatermelonGame.Board.Command.Item;
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
        {
            BoardItem first = _board.SpawnItem(0, new Vector2(0.4f, 0.1f));
            BoardItem second = _board.SpawnItem(0, new Vector2(0.6f, 0.1f));

            Assert.IsTrue(_board.Board.EnqueueCommand(new MergingCommand(_board.Board.CommandContext, first, second)));
        }

        private Vector2 GetScreenPosition(Vector2 boardNormalizedPosition)
        {
            Vector3 screenPosition = _board.Area.BoardLocalNormalizedPositionToScreen(boardNormalizedPosition);
            return new Vector2(screenPosition.x, screenPosition.y);
        }
    }
}
