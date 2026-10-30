using System;
using System.Collections.Generic;
using System.Reflection;
using Mu3Library.Game.WatermelonGame.Board;
using Mu3Library.Game.WatermelonGame.Board.Config;
using Mu3Library.Game.WatermelonGame.Board.Item;
using Mu3Library.Game.WatermelonGame.Helpers;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mu3Library.Game.WatermelonGame.Tests
{
    /// <summary>
    /// A complete board built in the open edit mode scene: a main camera rendering into a texture
    /// <br/> of a fixed size, a board area, a board item resource and a controller, plus the board
    /// <br/> configurations a test asks for. Dispose it to destroy all of it again.
    /// <br/> Edit mode runs no component callbacks, so <see cref="Controller.Tick"/> stands in for
    /// <br/> a frame, <see cref="ConnectInput"/> for the board being enabled, and <see cref="SendOnDisable"/>
    /// <br/> for a hierarchy being hidden.
    /// </summary>
    internal sealed class TestBoard : IDisposable
    {
        /// <summary>
        /// Declared inside the test class, so it has no MonoScript of its own and can be added
        /// <br/> to a GameObject in an editor assembly.
        /// </summary>
        internal sealed class Controller : BoardController
        {
            /// <summary>
            /// The screen positions every <see cref="BoardController.OnDragCancel"/> was called with.
            /// </summary>
            public List<Vector2> DragCancelPositions { get; } = new();

            public void Setup(BoardArea area, BoardItem itemResource)
            {
                _boardArea = area;
                _itemResource = itemResource;

                // Time does not move inside one edit mode test, so every item is handed out at once.
                _dropInterval = 0.0f;
            }

            /// <summary>
            /// Runs one frame of the board.
            /// </summary>
            public void Tick() => Update();

            /// <summary>
            /// Draws the next item index the way the board does when it hands out an item.
            /// </summary>
            public int Draw() => DrawNextItemIndex();

            /// <summary>
            /// Puts an item into the player's hand whatever state the board is in, as a subclass
            /// <br/> or an event handler could.
            /// </summary>
            public bool SpawnHoldingItem() => TrySpawnHoldingItem();

            /// <summary>
            /// Forgets the preview, as on a board that was never prepared, so the next item put into
            /// <br/> the hand draws its preview first.
            /// </summary>
            public void ForgetNextItemIndex() => _nextItemIndex = -1;

            public override void OnDragCancel(Vector2 screenPos)
            {
                DragCancelPositions.Add(screenPos);
                base.OnDragCancel(screenPos);
            }
        }

        /// <summary>
        /// The input handler of the test board. It plays the mouse and the touches its methods are given,
        /// <br/> one frame per call, unless <see cref="IsFake"/> is turned off.
        /// </summary>
        internal sealed class Handler : InputHandler
        {
            private readonly List<Touch> _touches = new();

            private bool _isMousePresent = true;
            private bool _isMousePressed;
            private bool _wasMousePressedThisFrame;
            private Vector2 _mousePosition;

            /// <summary>
            /// While true, the default, the handler reads the input played to it. While false it reads the
            /// <br/> project's input backend, as the handler of a board in a game does.
            /// </summary>
            public bool IsFake { get; set; } = true;

            /// <summary>
            /// Runs one frame of the handler. The touches then move on the way the input backends report them:
            /// <br/> one that began or moved stands still, one that ended or was canceled is gone.
            /// </summary>
            public void Tick()
            {
                Update();

                _wasMousePressedThisFrame = false;

                for (int index = _touches.Count - 1; index >= 0; index--)
                {
                    Touch touch = _touches[index];
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        _touches.RemoveAt(index);
                    }
                    else
                    {
                        touch.phase = TouchPhase.Stationary;
                        _touches[index] = touch;
                    }
                }
            }

            public void PressMouse(Vector2 position)
            {
                _isMousePresent = true;
                _isMousePressed = true;
                _wasMousePressedThisFrame = true;
                _mousePosition = position;

                Tick();
            }

            public void MoveMouse(Vector2 position)
            {
                _mousePosition = position;

                Tick();
            }

            public void ReleaseMouse(Vector2 position)
            {
                _isMousePressed = false;
                _mousePosition = position;

                Tick();
            }

            public void DisconnectMouse()
            {
                _isMousePresent = false;
                _isMousePressed = false;
                _mousePosition = Vector2.zero;

                Tick();
            }

            public void BeginTouch(int fingerId, Vector2 position) => PlayTouch(fingerId, position, TouchPhase.Began);

            /// <summary>
            /// Begins a touch that the platform also reports as a mouse press in the same frame, as the
            /// <br/> Input Manager does while it simulates the mouse with touches.
            /// </summary>
            public void BeginTouchWithMousePress(int fingerId, Vector2 position)
            {
                _isMousePresent = true;
                _isMousePressed = true;
                _wasMousePressedThisFrame = true;
                _mousePosition = position;

                PlayTouch(fingerId, position, TouchPhase.Began);
            }

            public void MoveTouch(int fingerId, Vector2 position) => PlayTouch(fingerId, position, TouchPhase.Moved);

            public void EndTouch(int fingerId, Vector2 position) => PlayTouch(fingerId, position, TouchPhase.Ended);

            public void CancelTouch(int fingerId, Vector2 position) => PlayTouch(fingerId, position, TouchPhase.Canceled);

            /// <summary>
            /// Sends what Unity sends when the application loses or regains focus.
            /// </summary>
            public void ChangeFocus(bool hasFocus) => OnApplicationFocus(hasFocus);

            /// <summary>
            /// Sends what Unity sends when the application is paused or resumed.
            /// </summary>
            public void ChangePause(bool isPaused) => OnApplicationPause(isPaused);

            /// <summary>
            /// Takes a touch away without an end, as a backend that loses track of it does.
            /// </summary>
            public void LoseTouch(int fingerId)
            {
                _touches.RemoveAll(touch => touch.fingerId == fingerId);

                Tick();
            }

            private void PlayTouch(int fingerId, Vector2 position, TouchPhase phase)
            {
                Touch touch = new Touch { fingerId = fingerId, position = position, phase = phase };

                int index = _touches.FindIndex(playedTouch => playedTouch.fingerId == fingerId);
                if (index >= 0)
                {
                    _touches[index] = touch;
                }
                else
                {
                    _touches.Add(touch);
                }

                Tick();
            }

            protected override bool IsMousePresent => IsFake ? _isMousePresent : base.IsMousePresent;

            protected override Vector2 MousePosition => IsFake ? _mousePosition : base.MousePosition;

            protected override bool GetMouseButtonDown() => IsFake ? _wasMousePressedThisFrame : base.GetMouseButtonDown();

            protected override bool GetMouseButton() => IsFake ? _isMousePressed : base.GetMouseButton();

            protected override int TouchCount => IsFake ? _touches.Count : base.TouchCount;

            protected override Touch GetTouch(int index) => IsFake ? _touches[index] : base.GetTouch(index);
        }

        private const int ScreenWidth = 1080;
        private const int ScreenHeight = 1920;

        private const int BoardTextureWidth = 100;
        private const int BoardTextureHeight = 200;

        private readonly List<Object> _objects = new();
        private readonly List<Camera> _disabledSceneCameras = new();
        private readonly RenderTexture _screen;

        public Camera Camera { get; }
        public Handler Input { get; }
        public BoardArea Area { get; }
        public BoardItem ItemResource { get; }
        public Controller Board { get; }

        /// <summary>
        /// The eleven fruit sprites, one per catalog index, all of them 0.64 units wide.
        /// </summary>
        public IReadOnlyList<Sprite> ItemSprites { get; }

        /// <summary>
        /// A board sprite 1 by 2 units large.
        /// </summary>
        public Sprite SmallBoardSprite { get; }

        /// <summary>
        /// The same board picture at half the pixels per unit, 2 by 4 units large.
        /// </summary>
        public Sprite LargeBoardSprite { get; }

        public TestBoard()
        {
            // The board measures itself against Camera.main, the first enabled camera tagged
            // MainCamera, and the scene the test runner opens can already carry one. It is
            // switched off while the test board exists.
            foreach (Camera sceneCamera in Camera.allCameras)
            {
                if (sceneCamera.CompareTag("MainCamera"))
                {
                    sceneCamera.enabled = false;
                    _disabledSceneCameras.Add(sceneCamera);
                }
            }

            // A setup that fails never reaches the fixture's teardown, so it gives the scene back itself.
            try
            {
                GameObject cameraObject = Track(new GameObject("TestBoardCamera") { tag = "MainCamera" });
                cameraObject.transform.position = new Vector3(0.0f, 0.0f, -10.0f);

                _screen = Track(new RenderTexture(ScreenWidth, ScreenHeight, 0));

                Camera = cameraObject.AddComponent<Camera>();
                Camera.orthographic = true;
                Camera.orthographicSize = 10.0f;
                Camera.targetTexture = _screen;

                Assert.AreSame(Camera, Camera.main, "The test board camera is not Camera.main.");

                Texture2D boardTexture = Track(new Texture2D(BoardTextureWidth, BoardTextureHeight));
                SmallBoardSprite = CreateSprite(boardTexture, 100.0f);
                LargeBoardSprite = CreateSprite(boardTexture, 50.0f);

                Texture2D itemTexture = Track(new Texture2D(64, 64));
                Sprite[] itemSprites = new Sprite[BoardItemsConfig.FruitItemCount];
                for (int index = 0; index < itemSprites.Length; index++)
                {
                    itemSprites[index] = CreateSprite(itemTexture, 100.0f);
                }

                ItemSprites = itemSprites;

                // The handler goes on first, so the board area takes it instead of adding one of its own.
                GameObject areaObject = Track(new GameObject("TestBoardArea"));
                Input = areaObject.AddComponent<Handler>();
                Area = areaObject.AddComponent<BoardArea>();
                Assert.AreSame(Input, Area.GetComponent<InputHandler>(), "The board area added an input handler of its own.");

                // The board hides its item resource in Start, which edit mode never calls.
                GameObject itemObject = Track(new GameObject("TestBoardItem"));
                ItemResource = itemObject.AddComponent<BoardItem>();
                itemObject.SetActive(false);

                Board = Track(new GameObject("TestBoardController")).AddComponent<Controller>();
                Board.Setup(Area, ItemResource);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// A board configuration on the given board sprite, every fruit entry carrying its sprite
        /// <br/> from <see cref="ItemSprites"/> except the ones listed.
        /// </summary>
        public BoardConfig CreateConfig(Sprite boardSprite, params int[] indicesWithoutSprite)
        {
            BoardConfig config = Track(ScriptableObject.CreateInstance<BoardConfig>());
            config.name = "TestBoardConfig";

            // The entries are written the way an asset holds them, as serialized list elements.
            SerializedObject serializedConfig = new SerializedObject(config);
            serializedConfig.FindProperty("_boardSprite").objectReferenceValue = boardSprite;

            SerializedProperty itemInfos = serializedConfig.FindProperty("_itemConfig._itemInfos");
            itemInfos.arraySize = BoardItemsConfig.FruitItemCount;
            for (int index = 0; index < BoardItemsConfig.FruitItemCount; index++)
            {
                SerializedProperty sprite = itemInfos.GetArrayElementAtIndex(index).FindPropertyRelative("_sprite");
                sprite.objectReferenceValue = Array.IndexOf(indicesWithoutSprite, index) >= 0 ? null : ItemSprites[index];
            }

            serializedConfig.ApplyModifiedPropertiesWithoutUndo();

            return config;
        }

        /// <summary>
        /// Takes the sprite away from one entry of a configuration that may already be applied.
        /// </summary>
        public static void RemoveItemSprite(BoardConfig config, int index)
        {
            SerializedObject serializedConfig = new SerializedObject(config);
            serializedConfig.FindProperty("_itemConfig._itemInfos")
                .GetArrayElementAtIndex(index)
                .FindPropertyRelative("_sprite")
                .objectReferenceValue = null;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Applies a configuration on the small board sprite and prepares the board on it.
        /// </summary>
        public BoardConfig Prepare(Sprite boardSprite = null)
        {
            BoardConfig config = CreateConfig(boardSprite != null ? boardSprite : SmallBoardSprite);

            Board.SetBoardConfig(config);
            Assert.AreSame(config, Board.Config, "The test configuration was refused.");

            Board.Prepare();
            Assert.IsTrue(Board.IsPrepared, "The test board could not be prepared.");

            return config;
        }

        /// <summary>
        /// Prepares the board and starts the game on it.
        /// </summary>
        public BoardConfig StartGame(Sprite boardSprite = null)
        {
            BoardConfig config = Prepare(boardSprite);

            Board.GameStart();
            Assert.IsTrue(Board.IsRunning, "The test board could not be started.");

            return config;
        }

        /// <summary>
        /// Enables the board area and the controller the way play mode does, so the touches of the input
        /// <br/> handler reach the board.
        /// </summary>
        public void ConnectInput()
        {
            SendOnEnable(Area);
            SendOnEnable(Board);
        }

        /// <summary>
        /// The screen position of a fraction of the board area.
        /// </summary>
        public Vector2 GetScreenPosition(float boardNormalizedX, float boardNormalizedY = 0.5f)
            => Area.BoardLocalNormalizedPositionToScreen(new Vector2(boardNormalizedX, boardNormalizedY));

        public BoardItem SpawnItem(int itemIndex, Vector2 boardNormalizedPosition)
        {
            Assert.IsTrue(
                Board.CommandContext.TrySpawnItem(itemIndex, boardNormalizedPosition, out BoardItem item),
                $"No item could be spawned for index {itemIndex}.");

            return item;
        }

        /// <summary>
        /// Where an item sits as a fraction of the board area. Unlike the board conversions it is
        /// <br/> not clamped, so an item above the top edge reads above 1.
        /// </summary>
        public Vector2 GetBoardNormalizedPosition(BoardItem item)
        {
            Rect boardRect = GetBoardRect();
            Vector3 localPosition = item.transform.localPosition;

            return new Vector2(
                (localPosition.x - boardRect.xMin) / boardRect.width,
                (localPosition.y - boardRect.yMin) / boardRect.height);
        }

        /// <summary>
        /// Puts an item on a fraction of the board area, also outside of it, which the board's own
        /// <br/> spawning clamps away.
        /// </summary>
        public void SetBoardNormalizedPosition(BoardItem item, Vector2 boardNormalizedPosition)
        {
            Rect boardRect = GetBoardRect();
            Transform itemTransform = item.transform;

            itemTransform.localPosition = new Vector3(
                boardRect.xMin + boardNormalizedPosition.x * boardRect.width,
                boardRect.yMin + boardNormalizedPosition.y * boardRect.height,
                itemTransform.localPosition.z);
        }

        private Rect GetBoardRect()
        {
            Rect boardRect = Area.LocalRect;
            Assert.IsTrue(boardRect.width > 0.0f && boardRect.height > 0.0f, "The board area has no rectangle.");

            return boardRect;
        }

        /// <summary>
        /// Sends OnDisable to a component the way play mode does when its hierarchy is hidden;
        /// <br/> edit mode only does it for components that run in edit mode.
        /// </summary>
        public static void SendOnDisable(MonoBehaviour component)
            => SendCallback(component, "OnDisable");

        /// <summary>
        /// Sends OnEnable to a component the way play mode does when it is added or shown;
        /// <br/> edit mode only does it for components that run in edit mode.
        /// </summary>
        public static void SendOnEnable(MonoBehaviour component)
            => SendCallback(component, "OnEnable");

        private static void SendCallback(MonoBehaviour component, string methodName)
        {
            MethodInfo method = null;
            for (Type type = component.GetType(); type != null && method == null; type = type.BaseType)
            {
                method = type.GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            }

            Assert.IsNotNull(method, $"{component.GetType().Name} has no {methodName}.");
            method.Invoke(component, null);
        }

        public void Dispose()
        {
            // A handler that read the Input System gives its EnhancedTouch use back here.
            if (Input != null)
            {
                SendOnDisable(Input);
            }

            if (Camera != null)
            {
                Camera.targetTexture = null;
            }

            if (_screen != null)
            {
                _screen.Release();
            }

            // The items the board created are children of the board area and go with it.
            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null)
                {
                    Object.DestroyImmediate(_objects[index]);
                }
            }

            _objects.Clear();

            foreach (Camera sceneCamera in _disabledSceneCameras)
            {
                if (sceneCamera != null)
                {
                    sceneCamera.enabled = true;
                }
            }

            _disabledSceneCameras.Clear();
        }

        private Sprite CreateSprite(Texture2D texture, float pixelsPerUnit)
            => Track(Sprite.Create(
                texture,
                new Rect(0.0f, 0.0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit));

        private T Track<T>(T obj) where T : Object
        {
            _objects.Add(obj);
            return obj;
        }
    }
}
