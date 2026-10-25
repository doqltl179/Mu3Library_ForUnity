#if MU3LIBRARY_INPUTSYSTEM_SUPPORT && ENABLE_INPUT_SYSTEM
using System.Collections.Generic;
using Mu3Library.Game.WatermelonGame.Board.Item;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

namespace Mu3Library.Game.WatermelonGame.Tests
{
    /// <summary>
    /// The board driven by Input System devices, which <see cref="Helpers.InputHandler"/> reads under the Active
    /// <br/> Input Handling settings "Input System Package (New)" and "Both" while the package is installed.
    /// <br/> <see cref="InputTestFixture"/> gives every test an input system of its own with only the devices it adds.
    /// </summary>
    public class InputHandlerInputSystemTests
    {
        private InputTestFixture _input;
        private TestBoard _board;

        private readonly List<Vector2> _canceledPositions = new();
        private readonly List<BoardItem> _droppedItems = new();

        private BoardItem _heldItem;

        [SetUp]
        public void SetUp()
        {
            _input = new InputTestFixture();
            _input.Setup();

            // NUnit skips the teardown of a failed setup, and the real input system has to come back either way.
            try
            {
                _board = new TestBoard();
                _board.Input.IsFake = false;
                _board.Input.OnTouchCanceled.AddListener(_canceledPositions.Add);
                _board.Board.OnItemDropped += _droppedItems.Add;

                _board.StartGame();
                _board.ConnectInput();
                _board.Board.Tick();

                _heldItem = _board.Board.HoldingItem;
                Assert.IsNotNull(_heldItem, "The running board did not hand out an item.");
            }
            catch
            {
                TearDown();
                throw;
            }
        }

        [TearDown]
        public void TearDown()
        {
            // The board gives its EnhancedTouch use back to the input system of the test before that one goes.
            _board?.Dispose();
            _board = null;

            _input?.TearDown();
            _input = null;

            _canceledPositions.Clear();
            _droppedItems.Clear();
            _heldItem = null;
        }

        [Test]
        public void Mouse_PressMoveRelease_DropsHeldItemWhereReleased()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Assert.AreSame(mouse, Mouse.current);

            _input.Move(mouse.position, _board.GetScreenPosition(0.35f));
            _board.Input.Tick();
            _input.Press(mouse.leftButton);
            _board.Input.Tick();
            Assert.IsTrue(_board.Area.IsDragging, "The mouse press did not start a board drag.");
            float pressedX = _board.Board.HoldingNormalizedX;

            _input.Move(mouse.position, _board.GetScreenPosition(0.65f));
            _board.Input.Tick();
            float movedX = _board.Board.HoldingNormalizedX;
            Assert.Greater(movedX, pressedX, "The held item did not follow the mouse.");

            _input.Release(mouse.leftButton);
            _board.Input.Tick();

            CollectionAssert.AreEqual(new[] { _heldItem }, _droppedItems);
            Assert.AreEqual(movedX, _board.Board.HoldingNormalizedX, "The item was not dropped where the mouse was released.");
            Assert.IsFalse(_board.Area.IsDragging);
        }

        [Test]
        public void Mouse_RemovedWhileDragging_KeepsHeldItemInHand()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();

            _input.Move(mouse.position, _board.GetScreenPosition(0.35f));
            _board.Input.Tick();
            _input.Press(mouse.leftButton);
            _board.Input.Tick();
            Vector2 lastPosition = _board.GetScreenPosition(0.65f);
            _input.Move(mouse.position, lastPosition);
            _board.Input.Tick();
            float heldX = _board.Board.HoldingNormalizedX;

            InputSystem.RemoveDevice(mouse);
            _board.Input.Tick();

            CollectionAssert.AreEqual(new[] { lastPosition }, _canceledPositions);
            CollectionAssert.AreEqual(new[] { lastPosition }, _board.Board.DragCancelPositions);
            CollectionAssert.IsEmpty(_droppedItems);
            Assert.AreSame(_heldItem, _board.Board.HoldingItem);
            Assert.AreEqual(heldX, _board.Board.HoldingNormalizedX);
        }

        [Test]
        public void Mouse_ResetWhenFocusIsLostInGameView_KeepsHeldItemInHand()
        {
            // The fixture makes the editor behave like a player that does not run in the background. The Editor's own
            // default, under which the play mode keeps running without the Game view focus, goes the way of a player
            // that runs in the background.
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus;

            Mouse mouse = StartMouseDrag(out Vector2 lastPosition, out float heldX);

            // The default background behavior resets the mouse once the focus is gone, which releases its button.
            // This test sends no Unity focus message.
            _input.ScheduleFocusChangedEvent(false);
            InputSystem.Update();
            Assert.IsFalse(mouse.leftButton.isPressed, "Losing the focus did not reset the mouse.");
            _board.Input.Tick();

            AssertCanceledAt(lastPosition, heldX);

            // The mouse comes back with the focus, and its next press is a drag of its own.
            _input.ScheduleFocusChangedEvent(true);
            InputSystem.Update();
            _input.Press(mouse.leftButton);
            _board.Input.Tick();
            _input.Release(mouse.leftButton);
            _board.Input.Tick();

            CollectionAssert.AreEqual(new[] { _heldItem }, _droppedItems);
        }

        [Test]
        public void Mouse_ResetWhenFocusComesBack_KeepsHeldItemInHand()
        {
            Mouse mouse = StartMouseDrag(out Vector2 lastPosition, out float heldX);

            // A player that does not run in the background leaves the mouse as it is while the focus is gone, and it
            // resets the mouse once the focus comes back, as a mouse that cannot be synced to its current state is.
            _input.ScheduleFocusChangedEvent(false);
            InputSystem.Update();
            _board.Input.Tick();
            Assert.IsTrue(mouse.leftButton.isPressed, "Losing the focus reset the mouse.");
            Assert.IsTrue(_board.Area.IsDragging, "The drag ended without a reset.");

            _input.ScheduleFocusChangedEvent(true);
            InputSystem.Update();
            Assert.IsFalse(mouse.leftButton.isPressed, "Regaining the focus did not reset the mouse.");
            _board.Input.Tick();

            AssertCanceledAt(lastPosition, heldX);
        }

        [Test]
        public void Touch_CanceledWhenFocusIsLostInGameView_KeepsHeldItemInHand()
        {
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus;

            InputSystem.AddDevice<Touchscreen>();
            _board.Input.Tick();

            _input.BeginTouch(1, _board.GetScreenPosition(0.35f));
            _board.Input.Tick();
            Vector2 lastPosition = _board.GetScreenPosition(0.65f);
            _input.MoveTouch(1, lastPosition);
            _board.Input.Tick();
            float heldX = _board.Board.HoldingNormalizedX;

            // Resetting the touch screen cancels the touches it carries.
            _input.ScheduleFocusChangedEvent(false);
            InputSystem.Update();
            _board.Input.Tick();

            AssertCanceledAt(lastPosition, heldX);
        }

        [Test]
        public void Touch_BeginMoveEnd_DropsHeldItemWhereLifted()
        {
            InputSystem.AddDevice<Touchscreen>();

            // The handler of a running board reads every frame, so it is reading before the touch begins.
            _board.Input.Tick();

            _input.BeginTouch(1, _board.GetScreenPosition(0.35f));
            _board.Input.Tick();
            Assert.IsTrue(_board.Area.IsDragging, "The touch did not start a board drag.");
            float touchedX = _board.Board.HoldingNormalizedX;

            Vector2 liftPosition = _board.GetScreenPosition(0.65f);
            _input.MoveTouch(1, liftPosition);
            _board.Input.Tick();
            float movedX = _board.Board.HoldingNormalizedX;
            Assert.Greater(movedX, touchedX, "The held item did not follow the touch.");

            _input.EndTouch(1, liftPosition);
            _board.Input.Tick();

            CollectionAssert.AreEqual(new[] { _heldItem }, _droppedItems);
            Assert.AreEqual(movedX, _board.Board.HoldingNormalizedX, "The item was not dropped where the touch was lifted.");
            CollectionAssert.IsEmpty(_canceledPositions);
        }

        [Test]
        public void Touch_BeganAndEndedInOneFrame_DropsHeldItem()
        {
            InputSystem.AddDevice<Touchscreen>();
            _board.Input.Tick();

            Vector2 position = _board.GetScreenPosition(0.5f);
            _input.BeginTouch(1, position, queueEventOnly: true);
            _input.EndTouch(1, position, queueEventOnly: true);

            InputSystem.Update();
            _board.Input.Tick();
            InputSystem.Update();
            _board.Input.Tick();

            CollectionAssert.AreEqual(new[] { _heldItem }, _droppedItems, "A tap shorter than a frame was lost.");
            CollectionAssert.IsEmpty(_canceledPositions);
        }

        [Test]
        public void Touch_CanceledBySystem_KeepsHeldItemInHand()
        {
            InputSystem.AddDevice<Touchscreen>();
            _board.Input.Tick();

            _input.BeginTouch(1, _board.GetScreenPosition(0.35f));
            _board.Input.Tick();
            Vector2 lastPosition = _board.GetScreenPosition(0.65f);
            _input.MoveTouch(1, lastPosition);
            _board.Input.Tick();
            float heldX = _board.Board.HoldingNormalizedX;

            _input.CancelTouch(1, lastPosition);
            _board.Input.Tick();

            CollectionAssert.AreEqual(new[] { lastPosition }, _canceledPositions);
            CollectionAssert.AreEqual(new[] { lastPosition }, _board.Board.DragCancelPositions);
            CollectionAssert.IsEmpty(_droppedItems);
            Assert.AreSame(_heldItem, _board.Board.HoldingItem);
            Assert.AreEqual(heldX, _board.Board.HoldingNormalizedX);
        }

        [Test]
        public void DisablingWhileTouching_KeepsHeldItemAndGivesEnhancedTouchBack()
        {
            InputSystem.AddDevice<Touchscreen>();
            _board.Input.Tick();
            Assert.IsTrue(EnhancedTouchSupport.enabled, "Reading the touches did not enable EnhancedTouch.");

            Vector2 position = _board.GetScreenPosition(0.35f);
            _input.BeginTouch(1, position);
            _board.Input.Tick();

            TestBoard.SendOnDisable(_board.Input);

            CollectionAssert.AreEqual(new[] { position }, _canceledPositions);
            CollectionAssert.AreEqual(new[] { position }, _board.Board.DragCancelPositions);
            CollectionAssert.IsEmpty(_droppedItems);
            Assert.AreSame(_heldItem, _board.Board.HoldingItem);
            Assert.IsFalse(EnhancedTouchSupport.enabled, "The handler kept EnhancedTouch enabled.");
        }

        /// <summary>
        /// Presses a new mouse on the board and drags it along, reporting where the drag was last reported and where
        /// <br/> the held item is.
        /// </summary>
        private Mouse StartMouseDrag(out Vector2 lastPosition, out float heldX)
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();

            _input.Move(mouse.position, _board.GetScreenPosition(0.35f));
            _board.Input.Tick();
            _input.Press(mouse.leftButton);
            _board.Input.Tick();
            lastPosition = _board.GetScreenPosition(0.65f);
            _input.Move(mouse.position, lastPosition);
            _board.Input.Tick();
            Assert.IsTrue(_board.Area.IsDragging, "The mouse press did not start a board drag.");

            heldX = _board.Board.HoldingNormalizedX;

            return mouse;
        }

        private void AssertCanceledAt(Vector2 lastPosition, float heldX)
        {
            CollectionAssert.AreEqual(new[] { lastPosition }, _canceledPositions, "The drag was not canceled where it was last reported.");
            CollectionAssert.AreEqual(new[] { lastPosition }, _board.Board.DragCancelPositions, "The controller was not told about the cancel.");
            CollectionAssert.IsEmpty(_droppedItems, "The canceled drag dropped the held item.");
            Assert.IsFalse(_board.Area.IsDragging, "The board area is still dragging.");
            Assert.AreSame(_heldItem, _board.Board.HoldingItem, "The held item left the hand.");
            Assert.AreEqual(heldX, _board.Board.HoldingNormalizedX, "The held item moved.");
            Assert.AreEqual(0, _board.Board.SpawnCount, "The canceled drag was counted as a drop.");
        }
    }
}
#endif
