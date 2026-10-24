using System.Collections.Generic;
using Mu3Library.Game.WatermelonGame.Board;
using Mu3Library.Game.WatermelonGame.Board.Item;
using Mu3Library.Game.WatermelonGame.Helpers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Mu3Library.Game.WatermelonGame.Tests
{
    /// <summary>
    /// The drag pipeline of <see cref="InputHandler"/> and the board it drives, played through the test board's
    /// <br/> input handler so it runs the same under every input backend.
    /// </summary>
    public class InputHandlerTests
    {
        /// <summary>
        /// Hits whatever lies inside its screen rectangle, as a uGUI canvas or a UI Toolkit panel does.
        /// <br/> Declared inside the test class, so it has no MonoScript of its own.
        /// </summary>
        private sealed class UIRaycaster : BaseRaycaster
        {
            public Rect ScreenRect { get; set; }

            public override Camera eventCamera => null;

            // Edit mode runs only the callbacks a test sends, so the raycaster reports itself as running.
            public override bool IsActive() => true;

            public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
            {
                if (ScreenRect.Contains(eventData.position))
                {
                    resultAppendList.Add(new RaycastResult { gameObject = gameObject, module = this, screenPosition = eventData.position });
                }
            }
        }

        /// <summary>
        /// Hits a scene object under every position, as a <c>Physics2DRaycaster</c> on the camera hits the board items.
        /// </summary>
        private sealed class SceneRaycaster : PhysicsRaycaster
        {
            public override bool IsActive() => true;

            public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
                => resultAppendList.Add(new RaycastResult { gameObject = gameObject, module = this, screenPosition = eventData.position });
        }

        private TestBoard _board;

        private readonly List<Object> _objects = new();
        private readonly List<MonoBehaviour> _enabledComponents = new();

        private readonly List<Vector2> _endedPositions = new();
        private readonly List<Vector2> _canceledPositions = new();
        private readonly List<BoardItem> _droppedItems = new();

        [SetUp]
        public void SetUp()
        {
            _board = new TestBoard();

            _board.Input.OnTouchEnded.AddListener(_endedPositions.Add);
            _board.Input.OnTouchCanceled.AddListener(_canceledPositions.Add);
            _board.Board.OnItemDropped += _droppedItems.Add;
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = _enabledComponents.Count - 1; index >= 0; index--)
            {
                if (_enabledComponents[index] != null)
                {
                    TestBoard.SendOnDisable(_enabledComponents[index]);
                }
            }

            _enabledComponents.Clear();

            _board?.Dispose();
            _board = null;

            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null)
                {
                    Object.DestroyImmediate(_objects[index]);
                }
            }

            _objects.Clear();
            _endedPositions.Clear();
            _canceledPositions.Clear();
            _droppedItems.Clear();
        }

        [Test]
        public void MouseDrag_ReleasedOnBoard_DropsHeldItemWhereReleased()
        {
            BoardItem heldItem = StartRunningBoard();

            _board.Input.PressMouse(_board.GetScreenPosition(0.35f));
            Assert.IsTrue(_board.Area.IsDragging, "The press on the board did not start a board drag.");
            float pressedX = _board.Board.HoldingNormalizedX;

            Vector2 releasePosition = _board.GetScreenPosition(0.65f);
            _board.Input.MoveMouse(releasePosition);
            float movedX = _board.Board.HoldingNormalizedX;
            Assert.Greater(movedX, pressedX, "The held item did not follow the mouse.");

            _board.Input.ReleaseMouse(releasePosition);

            CollectionAssert.AreEqual(new[] { releasePosition }, _endedPositions);
            CollectionAssert.IsEmpty(_canceledPositions);
            CollectionAssert.AreEqual(new[] { heldItem }, _droppedItems);
            Assert.AreEqual(movedX, _board.Board.HoldingNormalizedX, "The item was not dropped where the mouse was released.");
            Assert.IsNull(_board.Board.HoldingItem);
        }

        [Test]
        public void TouchDrag_LiftedOnBoard_DropsHeldItemWhereLifted()
        {
            BoardItem heldItem = StartRunningBoard();

            _board.Input.BeginTouch(3, _board.GetScreenPosition(0.35f));
            Assert.IsTrue(_board.Area.IsDragging, "The touch on the board did not start a board drag.");
            float touchedX = _board.Board.HoldingNormalizedX;

            Vector2 liftPosition = _board.GetScreenPosition(0.65f);
            _board.Input.MoveTouch(3, liftPosition);
            Assert.Greater(_board.Board.HoldingNormalizedX, touchedX, "The held item did not follow the touch.");

            _board.Input.EndTouch(3, liftPosition);

            CollectionAssert.AreEqual(new[] { liftPosition }, _endedPositions);
            CollectionAssert.IsEmpty(_canceledPositions);
            CollectionAssert.AreEqual(new[] { heldItem }, _droppedItems);
        }

        [Test]
        public void DisablingInputHandlerWhileMouseDragging_CancelsDragAndKeepsHeldItem()
        {
            BoardItem heldItem = StartRunningBoard();

            _board.Input.PressMouse(_board.GetScreenPosition(0.35f));
            Vector2 lastPosition = _board.GetScreenPosition(0.65f);
            _board.Input.MoveMouse(lastPosition);
            float heldX = _board.Board.HoldingNormalizedX;
            int spawnCount = _board.Board.SpawnCount;

            TestBoard.SendOnDisable(_board.Input);

            AssertCanceledAt(lastPosition, heldItem, heldX, spawnCount);

            // The handler takes the next press as a drag of its own.
            Vector2 pressPosition = _board.GetScreenPosition(0.5f);
            _board.Input.PressMouse(pressPosition);
            _board.Input.ReleaseMouse(pressPosition);

            CollectionAssert.AreEqual(new[] { heldItem }, _droppedItems);
        }

        [Test]
        public void DisablingInputHandlerWhileTouchDragging_CancelsDragAndKeepsHeldItem()
        {
            BoardItem heldItem = StartRunningBoard();

            _board.Input.BeginTouch(0, _board.GetScreenPosition(0.35f));
            Vector2 lastPosition = _board.GetScreenPosition(0.65f);
            _board.Input.MoveTouch(0, lastPosition);
            float heldX = _board.Board.HoldingNormalizedX;
            int spawnCount = _board.Board.SpawnCount;

            TestBoard.SendOnDisable(_board.Input);

            AssertCanceledAt(lastPosition, heldItem, heldX, spawnCount);
        }

        [Test]
        public void TrackedTouchLost_CancelsDragAndKeepsHeldItem()
        {
            BoardItem heldItem = StartRunningBoard();

            _board.Input.BeginTouch(1, _board.GetScreenPosition(0.35f));
            Vector2 lastPosition = _board.GetScreenPosition(0.65f);
            _board.Input.MoveTouch(1, lastPosition);
            float heldX = _board.Board.HoldingNormalizedX;
            int spawnCount = _board.Board.SpawnCount;

            _board.Input.LoseTouch(1);

            AssertCanceledAt(lastPosition, heldItem, heldX, spawnCount);
        }

        [Test]
        public void TouchCanceledBySystem_CancelsDragAndKeepsHeldItem()
        {
            BoardItem heldItem = StartRunningBoard();

            _board.Input.BeginTouch(1, _board.GetScreenPosition(0.35f));
            Vector2 lastPosition = _board.GetScreenPosition(0.65f);
            _board.Input.MoveTouch(1, lastPosition);
            float heldX = _board.Board.HoldingNormalizedX;
            int spawnCount = _board.Board.SpawnCount;

            _board.Input.CancelTouch(1, _board.GetScreenPosition(0.9f));

            AssertCanceledAt(lastPosition, heldItem, heldX, spawnCount);
        }

        [Test]
        public void MouseDisconnectedWhileDragging_CancelsDragAndKeepsHeldItem()
        {
            BoardItem heldItem = StartRunningBoard();

            _board.Input.PressMouse(_board.GetScreenPosition(0.35f));
            Vector2 lastPosition = _board.GetScreenPosition(0.65f);
            _board.Input.MoveMouse(lastPosition);
            float heldX = _board.Board.HoldingNormalizedX;
            int spawnCount = _board.Board.SpawnCount;

            _board.Input.DisconnectMouse();

            AssertCanceledAt(lastPosition, heldItem, heldX, spawnCount);
        }

        [Test]
        public void PressOverUI_DoesNotStartDrag_OnMouseAndTouch()
        {
            StartRunningBoard();
            CreateEventSystem();
            CreateUIRaycaster(new Rect(0.0f, 0.0f, _board.GetScreenPosition(0.5f).x, 10000.0f));

            Vector2 uiPosition = _board.GetScreenPosition(0.25f);

            _board.Input.PressMouse(uiPosition);
            AssertNoDrag("A mouse press over UI");
            _board.Input.MoveMouse(_board.GetScreenPosition(0.75f));
            _board.Input.ReleaseMouse(_board.GetScreenPosition(0.75f));

            _board.Input.BeginTouch(2, uiPosition);
            AssertNoDrag("A touch over UI");
            _board.Input.MoveTouch(2, _board.GetScreenPosition(0.75f));
            _board.Input.EndTouch(2, _board.GetScreenPosition(0.75f));

            CollectionAssert.IsEmpty(_endedPositions, "A press over UI reported an end.");
            CollectionAssert.IsEmpty(_droppedItems, "A press over UI dropped the held item.");

            // A press beside the UI is a board drag as before.
            _board.Input.PressMouse(_board.GetScreenPosition(0.75f));
            Assert.IsTrue(_board.Area.IsDragging, "A press beside the UI did not start a board drag.");
        }

        [Test]
        public void PressOverUI_WithIgnorePressOverUIOff_StartsDrag()
        {
            BoardItem heldItem = StartRunningBoard();
            CreateEventSystem();
            CreateUIRaycaster(new Rect(0.0f, 0.0f, 10000.0f, 10000.0f));

            _board.Input.IgnorePressOverUI = false;

            Vector2 position = _board.GetScreenPosition(0.5f);
            _board.Input.PressMouse(position);
            Assert.IsTrue(_board.Area.IsDragging, "A press over UI did not start a drag with the check turned off.");

            _board.Input.ReleaseMouse(position);
            CollectionAssert.AreEqual(new[] { heldItem }, _droppedItems);
        }

        [Test]
        public void PressOverPhysicsRaycasterHit_StartsDrag()
        {
            StartRunningBoard();
            CreateEventSystem();

            SceneRaycaster sceneRaycaster = _board.Camera.gameObject.AddComponent<SceneRaycaster>();
            _objects.Add(sceneRaycaster);
            Enable(sceneRaycaster);

            _board.Input.BeginTouch(0, _board.GetScreenPosition(0.5f));
            Assert.IsTrue(_board.Area.IsDragging, "A scene object hit by a physics raycaster kept the touch from starting a drag.");
        }

        [Test]
        public void DragStartFilter_RefusingPress_KeepsMouseAndTouchFromStartingDrag()
        {
            StartRunningBoard();

            List<Vector2> askedPositions = new();
            _board.Input.DragStartFilter = position =>
            {
                askedPositions.Add(position);
                return false;
            };

            Vector2 mousePosition = _board.GetScreenPosition(0.4f);
            _board.Input.PressMouse(mousePosition);
            AssertNoDrag("A refused mouse press");
            _board.Input.ReleaseMouse(mousePosition);

            Vector2 touchPosition = _board.GetScreenPosition(0.6f);
            _board.Input.BeginTouch(5, touchPosition);
            AssertNoDrag("A refused touch");
            _board.Input.EndTouch(5, touchPosition);

            CollectionAssert.AreEqual(new[] { mousePosition, touchPosition }, askedPositions);
            CollectionAssert.IsEmpty(_endedPositions);
            CollectionAssert.IsEmpty(_droppedItems);

            _board.Input.DragStartFilter = null;
            _board.Input.BeginTouch(5, touchPosition);
            Assert.IsTrue(_board.Area.IsDragging, "The touch did not start a drag without the filter.");
        }

        [Test]
        public void DragStartFilter_EveryFilterHasToAllow()
        {
            StartRunningBoard();

            _board.Input.DragStartFilter += position => true;
            _board.Input.DragStartFilter += position => false;
            _board.Input.DragStartFilter += position => true;

            _board.Input.PressMouse(_board.GetScreenPosition(0.5f));

            AssertNoDrag("A press one of the filters refused");
        }

        [Test]
        public void BoardAreaAddedInCode_EnablesAndForwardsTouches()
        {
            GameObject areaObject = new GameObject("CodeBoardArea");
            _objects.Add(areaObject);

            // The board area adds its input handler itself, which no serialized scene data fills in.
            BoardArea area = areaObject.AddComponent<BoardArea>();
            InputHandler inputHandler = areaObject.GetComponent<InputHandler>();
            Assert.IsNotNull(inputHandler, "The board area did not add an input handler.");
            Assert.IsNotNull(inputHandler.OnTouchBegan, nameof(InputHandler.OnTouchBegan));
            Assert.IsNotNull(inputHandler.OnTouchMoved, nameof(InputHandler.OnTouchMoved));
            Assert.IsNotNull(inputHandler.OnTouchEnded, nameof(InputHandler.OnTouchEnded));
            Assert.IsNotNull(inputHandler.OnTouchCanceled, nameof(InputHandler.OnTouchCanceled));

            area.SetBoardImage(_board.SmallBoardSprite);
            Assert.DoesNotThrow(() => Enable(area));

            List<Vector2> began = new();
            List<Vector2> moved = new();
            List<Vector2> ended = new();
            area.OnTouchBegan += began.Add;
            area.OnTouchMoved += moved.Add;
            area.OnTouchEnded += ended.Add;

            Vector2 beganPosition = area.BoardLocalNormalizedPositionToScreen(new Vector2(0.4f, 0.5f));
            Vector2 endedPosition = area.BoardLocalNormalizedPositionToScreen(new Vector2(0.6f, 0.5f));
            inputHandler.OnTouchBegan.Invoke(beganPosition);
            inputHandler.OnTouchMoved.Invoke(endedPosition);
            inputHandler.OnTouchEnded.Invoke(endedPosition);

            CollectionAssert.AreEqual(new[] { beganPosition }, began);
            CollectionAssert.AreEqual(new[] { endedPosition }, moved);
            CollectionAssert.AreEqual(new[] { endedPosition }, ended);
        }

        /// <summary>
        /// Starts the game with its input connected and lets the board hand out the first item.
        /// </summary>
        private BoardItem StartRunningBoard()
        {
            _board.StartGame();
            _board.ConnectInput();
            _board.Board.Tick();

            BoardItem heldItem = _board.Board.HoldingItem;
            Assert.IsNotNull(heldItem, "The running board did not hand out an item.");

            return heldItem;
        }

        private void AssertCanceledAt(Vector2 lastPosition, BoardItem heldItem, float heldX, int spawnCount)
        {
            CollectionAssert.AreEqual(new[] { lastPosition }, _canceledPositions, "The drag was not canceled where it was last reported.");
            CollectionAssert.IsEmpty(_endedPositions, "The canceled drag was reported as released.");
            CollectionAssert.IsEmpty(_droppedItems, "The canceled drag dropped the held item.");

            Assert.IsFalse(_board.Input.IsDragging, "The input handler is still dragging.");
            Assert.IsFalse(_board.Area.IsDragging, "The board area is still dragging.");
            Assert.AreSame(heldItem, _board.Board.HoldingItem, "The held item left the hand.");
            Assert.AreEqual(heldX, _board.Board.HoldingNormalizedX, "The held item moved.");
            Assert.AreEqual(spawnCount, _board.Board.SpawnCount, "The canceled drag was counted as a drop.");
        }

        private void AssertNoDrag(string press)
        {
            Assert.IsFalse(_board.Input.IsDragging, $"{press} started an input drag.");
            Assert.IsFalse(_board.Area.IsDragging, $"{press} started a board drag.");
        }

        private void CreateEventSystem()
        {
            GameObject eventSystemObject = new GameObject("TestEventSystem");
            _objects.Add(eventSystemObject);

            EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
            Enable(eventSystem);
            EventSystem.current = eventSystem;
        }

        private void CreateUIRaycaster(Rect screenRect)
        {
            GameObject raycasterObject = new GameObject("TestUIRaycaster");
            _objects.Add(raycasterObject);

            UIRaycaster raycaster = raycasterObject.AddComponent<UIRaycaster>();
            raycaster.ScreenRect = screenRect;
            Enable(raycaster);
        }

        private void Enable(MonoBehaviour component)
        {
            TestBoard.SendOnEnable(component);
            _enabledComponents.Add(component);
        }
    }
}
