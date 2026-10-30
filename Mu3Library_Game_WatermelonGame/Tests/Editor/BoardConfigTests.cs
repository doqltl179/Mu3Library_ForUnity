using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Mu3Library.Game.WatermelonGame.Board.Config;
using Mu3Library.Game.WatermelonGame.Board.Item;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mu3Library.Game.WatermelonGame.Tests
{
    public class BoardConfigTests
    {
        private const float PositionTolerance = 0.0001f;

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
        public void IsComplete_EntriesWithoutSprite_ListsTheirIndices()
        {
            BoardConfig config = _board.CreateConfig(_board.SmallBoardSprite, 3, 7);
            List<int> incompleteIndices = new() { 99 };

            Assert.IsFalse(config.ItemConfig.IsComplete(incompleteIndices));
            Assert.IsFalse(config.ItemConfig.IsComplete());
            CollectionAssert.AreEqual(new[] { 3, 7 }, incompleteIndices);
        }

        [Test]
        public void IsComplete_EveryEntryWithSprite_IsTrue()
        {
            BoardConfig config = _board.CreateConfig(_board.SmallBoardSprite);
            List<int> incompleteIndices = new() { 99 };

            Assert.IsTrue(config.ItemConfig.IsComplete(incompleteIndices));
            Assert.IsTrue(config.ItemConfig.IsComplete());
            CollectionAssert.IsEmpty(incompleteIndices);
        }

        [Test]
        public void SetBoardConfig_EntryWithoutSprite_IsRefusedNamingTheIndices()
        {
            BoardConfig config = _board.CreateConfig(_board.SmallBoardSprite, 3, 7);

            LogAssert.Expect(LogType.Error, new Regex(@"cannot make the items at index 3, 7"));
            _board.Board.SetBoardConfig(config);

            Assert.IsNull(_board.Board.Config);
        }

        [Test]
        public void Prepare_EntryLostItsSprite_IsRefused()
        {
            BoardConfig config = _board.CreateConfig(_board.SmallBoardSprite);
            _board.Board.SetBoardConfig(config);
            Assert.AreSame(config, _board.Board.Config);

            // The configuration is an asset that can still be edited after it was applied.
            TestBoard.RemoveItemSprite(config, 5);

            LogAssert.Expect(LogType.Error, new Regex(@"cannot make the items at index 5\b"));
            _board.Board.Prepare();

            Assert.IsFalse(_board.Board.IsPrepared);
        }

        [Test]
        public void OnValidate_EntryWithoutSprite_WarnsNamingTheIndices()
        {
            BoardConfig config = _board.CreateConfig(_board.SmallBoardSprite, 0, 10);

            LogAssert.Expect(LogType.Warning, new Regex(@"no sprite for the items at index 0, 10"));
            typeof(BoardConfig)
                .GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(config, null);
        }

        [Test]
        public void SetBoardConfig_BoardSpriteOfAnotherSize_KeepsItemsOnTheirBoardPlaces()
        {
            _board.StartGame(_board.LargeBoardSprite);

            BoardItem[] items = SpawnItems(ItemBoardPositions);

            _board.Board.Tick();
            BoardItem heldItem = _board.Board.HoldingItem;
            Assert.IsNotNull(heldItem, "The running board did not hand out an item.");
            float holdingNormalizedX = _board.Board.HoldingNormalizedX;

            float largeBoardScale = _board.Area.transform.localScale.x;

            // The small sprite is the same picture at twice the pixels per unit, so the board is
            // fitted at twice the scale and its local units shrink to half.
            _board.Board.SetBoardConfig(_board.CreateConfig(_board.SmallBoardSprite));

            Assert.AreEqual(largeBoardScale * 2.0f, _board.Area.transform.localScale.x, 0.001f, "The board was not refitted.");
            AssertItemsOnBoardPlaces(items, ItemBoardPositions);
            Assert.AreEqual(holdingNormalizedX, _board.GetBoardNormalizedPosition(heldItem).x, PositionTolerance);

            _board.Board.SetBoardConfig(_board.CreateConfig(_board.LargeBoardSprite));

            Assert.AreEqual(largeBoardScale, _board.Area.transform.localScale.x, 0.001f, "The board was not refitted.");
            AssertItemsOnBoardPlaces(items, ItemBoardPositions);
            Assert.AreEqual(holdingNormalizedX, _board.GetBoardNormalizedPosition(heldItem).x, PositionTolerance);
        }

        [Test]
        public void SetBoardConfig_BoardSpriteOfTheSameSize_LeavesItemsWhereTheyAre()
        {
            _board.StartGame(_board.SmallBoardSprite);

            BoardItem[] items = SpawnItems(ItemBoardPositions);
            Vector3[] localPositions = new Vector3[items.Length];
            for (int index = 0; index < items.Length; index++)
            {
                localPositions[index] = items[index].transform.localPosition;
            }

            _board.Board.SetBoardConfig(_board.CreateConfig(_board.SmallBoardSprite));

            for (int index = 0; index < items.Length; index++)
            {
                Assert.AreEqual(localPositions[index], items[index].transform.localPosition, $"Item {index}");
            }
        }

        /// <summary>
        /// Places by the side walls, in the middle, and one above the top edge, where an item of
        /// <br/> a pile that overflows the board waits for the game end check.
        /// </summary>
        private static readonly Vector2[] ItemBoardPositions =
        {
            new Vector2(0.05f, 0.1f),
            new Vector2(0.95f, 0.1f),
            new Vector2(0.5f, 0.6f),
            new Vector2(0.3f, 1.1f),
        };

        private BoardItem[] SpawnItems(Vector2[] boardPositions)
        {
            BoardItem[] items = new BoardItem[boardPositions.Length];
            for (int index = 0; index < items.Length; index++)
            {
                // Spawning clamps into the board, so the item is moved to its place afterwards.
                items[index] = _board.SpawnItem(index, new Vector2(0.5f, 0.5f));
                _board.SetBoardNormalizedPosition(items[index], boardPositions[index]);
            }

            return items;
        }

        private void AssertItemsOnBoardPlaces(BoardItem[] items, Vector2[] boardPositions)
        {
            Rect worldRect = _board.Area.WorldRect;

            for (int index = 0; index < items.Length; index++)
            {
                Vector2 boardPosition = _board.GetBoardNormalizedPosition(items[index]);
                Assert.AreEqual(boardPositions[index].x, boardPosition.x, PositionTolerance, $"Item {index} x");
                Assert.AreEqual(boardPositions[index].y, boardPosition.y, PositionTolerance, $"Item {index} y");

                Vector3 worldPosition = items[index].transform.position;
                Assert.IsTrue(
                    worldPosition.x > worldRect.xMin && worldPosition.x < worldRect.xMax,
                    $"Item {index} left the board between its walls: {worldPosition.x} outside {worldRect.xMin} to {worldRect.xMax}.");
            }
        }
    }
}
