using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Mu3Library.Game.WatermelonGame.Helpers
{
    /// <summary>
    /// Turns the mouse and the touches into one drag at a time, which begins, moves, and then ends where it was
    /// <br/> released or is canceled when it is taken away before that.
    /// <br/> It reads the input backend the project enables. With the Input System package installed and its backend
    /// <br/> on, under the Active Input Handling settings "Input System Package (New)" and "Both", it reads the
    /// <br/> Input System. Otherwise it reads the Input Manager, which is what "Input Manager (Old)" and "Both" without
    /// <br/> the package give.
    /// </summary>
    public partial class InputHandler : MonoBehaviour
    {
        private const int NoActiveFingerId = -1;

        /// <summary>
        /// Finger id the mouse drag runs under. Touches never carry a negative id, so the mouse
        /// shares the drag pipeline without ever colliding with a real finger.
        /// </summary>
        private const int MouseFingerId = -2;

        [Tooltip("A press that lands on a UI element the scene's EventSystem reaches does not start a drag.")]
        [SerializeField] protected bool _ignorePressOverUI = true;

        /// <summary>
        /// Whether a press that lands on a UI element is left to the UI instead of starting a drag, on by default.
        /// <br/> See <see cref="IsPointerOverUI"/> for what counts as UI.
        /// </summary>
        public bool IgnorePressOverUI
        {
            get => _ignorePressOverUI;
            set => _ignorePressOverUI = value;
        }

        protected bool _isDragging;
        public bool IsDragging => _isDragging;

        public UnityEvent<Vector2> OnTouchBegan = new UnityEvent<Vector2>();
        public UnityEvent<Vector2> OnTouchMoved = new UnityEvent<Vector2>();
        public UnityEvent<Vector2> OnTouchEnded = new UnityEvent<Vector2>();

        /// <summary>
        /// Raised instead of <see cref="OnTouchEnded"/> when a drag is taken away before it was released: the handler
        /// <br/> was disabled, the tracked touch vanished or was canceled by the system, or the mouse was disconnected.
        /// <br/> It carries the position the drag was last reported at.
        /// </summary>
        public UnityEvent<Vector2> OnTouchCanceled = new UnityEvent<Vector2>();

        /// <summary>
        /// Asked with the screen position of every press before it starts a drag, on the mouse and on the touch path.
        /// <br/> Every function added has to return true, a press one of them refuses never becomes a drag.
        /// </summary>
        public Func<Vector2, bool> DragStartFilter;

        private int _activeFingerId = NoActiveFingerId;

        /// <summary>
        /// Where the running drag was last reported, which is where a canceled drag is reported to end.
        /// </summary>
        private Vector2 _lastPosition;

        private readonly List<RaycastResult> _uiRaycastResults = new();


        /// <summary>
        /// Cancels a running drag and gives back the EnhancedTouch use of the Input System backend, so an override
        /// <br/> calls <c>base.OnDisable()</c>.
        /// </summary>
        protected virtual void OnDisable()
        {
            // The player did not release this drag, so it is canceled where it was last reported.
            if (_isDragging)
            {
                CancelDrag();
            }

#if MU3LIBRARY_INPUTSYSTEM_SUPPORT && ENABLE_INPUT_SYSTEM
            DisableEnhancedTouch();
#endif
        }

        protected virtual void Update()
        {
            if (_isDragging)
            {
                if (_activeFingerId == MouseFingerId)
                {
                    UpdateMouseDrag();
                }
                else
                {
                    UpdateTouchDrag();
                }

                return;
            }

            if (TryGetBeganTouch(out Touch beganTouch, out bool hasBeganTouch))
            {
                OnTouchBeganHandler(beganTouch);
                return;
            }

            // The mouse drives the same drag pipeline on a platform without a touch screen,
            // which is what makes the game playable in the editor and on desktop.
            // A platform that also reports a touch as a mouse press would ask about a refused
            // touch a second time, so the mouse waits while a touch begins.
            if (!hasBeganTouch && IsMousePresent && GetMouseButtonDown())
            {
                Vector2 mousePosition = MousePosition;
                if (CanStartDrag(mousePosition))
                {
                    OnMouseDragBegan(mousePosition);
                }
            }
        }

        private void UpdateTouchDrag()
        {
            // A touch that is gone without an end was taken away, it was not lifted.
            if (!TryGetActiveTouch(out Touch activeTouch))
            {
                CancelDrag();
                return;
            }

            switch (activeTouch.phase)
            {
                case TouchPhase.Ended:
                    OnTouchEndedHandler(activeTouch);
                    break;

                // The system took the touch over, for a gesture of its own or a call, so the player did not lift it.
                case TouchPhase.Canceled:
                    CancelDrag();
                    break;

                case TouchPhase.Moved:
                    // case TouchPhase.Stationary:
                    OnTouchMovedHandler(activeTouch);
                    break;
            }
        }

        private void UpdateMouseDrag()
        {
            // A mouse that is gone cannot tell where it was released.
            if (!IsMousePresent)
            {
                CancelDrag();
                return;
            }

            Vector2 mousePosition = MousePosition;

            if (!GetMouseButton())
            {
                OnMouseDragEnded(mousePosition);
                return;
            }

            if (mousePosition != _lastPosition)
            {
                _lastPosition = mousePosition;
                OnTouchMoved?.Invoke(mousePosition);
            }
        }

        private void OnMouseDragBegan(Vector2 position)
        {
            _isDragging = true;
            _activeFingerId = MouseFingerId;
            _lastPosition = position;

            OnTouchBegan?.Invoke(position);
        }

        private void OnMouseDragEnded(Vector2 position)
        {
            _isDragging = false;
            _activeFingerId = NoActiveFingerId;

            OnTouchEnded?.Invoke(position);
        }

        protected virtual void OnTouchBeganHandler(Touch touch)
        {
            _isDragging = true;
            _activeFingerId = touch.fingerId;
            _lastPosition = touch.position;

            OnTouchBegan?.Invoke(touch.position);
        }

        protected virtual void OnTouchEndedHandler(Touch touch)
        {
            _isDragging = false;
            _activeFingerId = NoActiveFingerId;

            OnTouchEnded?.Invoke(touch.position);
        }

        protected virtual void OnTouchMovedHandler(Touch touch)
        {
            if (!_isDragging || touch.fingerId != _activeFingerId)
            {
                return;
            }

            _lastPosition = touch.position;

            OnTouchMoved?.Invoke(touch.position);
        }

        /// <summary>
        /// Ends the running drag without a release and reports <see cref="OnTouchCanceled"/> with the position it was
        /// <br/> last reported at.
        /// </summary>
        protected virtual void CancelDrag()
        {
            _isDragging = false;
            _activeFingerId = NoActiveFingerId;

            OnTouchCanceled?.Invoke(_lastPosition);
        }

        /// <summary>
        /// Asked with the screen position of a press, mouse or touch, before it starts a drag.
        /// <br/> It refuses a press over UI while <see cref="IgnorePressOverUI"/> is on, and a press that a
        /// <br/> <see cref="DragStartFilter"/> refuses. A refused press stays no drag until it is released.
        /// </summary>
        protected virtual bool CanStartDrag(Vector2 screenPosition)
        {
            if (_ignorePressOverUI && IsPointerOverUI(screenPosition))
            {
                return false;
            }

            if (DragStartFilter != null)
            {
                foreach (Func<Vector2, bool> filter in DragStartFilter.GetInvocationList())
                {
                    if (!filter(screenPosition))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Whether a screen position lies on UI: on anything a raycaster of the current <c>EventSystem</c> hits,
        /// <br/> which covers uGUI canvases and UI Toolkit panels. A UI Toolkit panel is reached only while that
        /// <br/> <c>EventSystem</c> has an input module and keeps its UI Toolkit integration on, as it does by default.
        /// <br/> A hit of a <c>PhysicsRaycaster</c> or <c>Physics2DRaycaster</c> is a scene object, such as a board item,
        /// <br/> and does not count. Without an <c>EventSystem</c> nothing counts.
        /// </summary>
        protected virtual bool IsPointerOverUI(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            // The raycast asks for the press position itself rather than for the pointer state of the input module,
            // which only learns about a new touch when the EventSystem updates, maybe after this handler.
            PointerEventData eventData = new PointerEventData(eventSystem) { position = screenPosition };
            eventSystem.RaycastAll(eventData, _uiRaycastResults);

            bool isOverUI = false;
            foreach (RaycastResult result in _uiRaycastResults)
            {
                if (!(result.module is PhysicsRaycaster))
                {
                    isOverUI = true;
                    break;
                }
            }

            _uiRaycastResults.Clear();

            return isOverUI;
        }

        private bool TryGetActiveTouch(out Touch activeTouch)
        {
            int touchCount = TouchCount;
            for (int i = 0; i < touchCount; i++)
            {
                Touch touch = GetTouch(i);
                if (touch.fingerId == _activeFingerId)
                {
                    activeTouch = touch;
                    return true;
                }
            }

            activeTouch = default;
            return false;
        }

        private bool TryGetBeganTouch(out Touch beganTouch, out bool hasBeganTouch)
        {
            hasBeganTouch = false;

            int touchCount = TouchCount;
            for (int i = 0; i < touchCount; i++)
            {
                Touch touch = GetTouch(i);
                if (touch.phase != TouchPhase.Began)
                {
                    continue;
                }

                hasBeganTouch = true;
                if (CanStartDrag(touch.position))
                {
                    beganTouch = touch;
                    return true;
                }
            }

            beganTouch = default;
            return false;
        }

        #region Input Backend
        // These read the input the drag is made of, from the backend the project enables. A subclass can override
        // them together to drive the handler from another source.

        /// <summary>
        /// Whether a mouse is connected.
        /// </summary>
        protected virtual bool IsMousePresent
        {
            get
            {
#if MU3LIBRARY_INPUTSYSTEM_SUPPORT && ENABLE_INPUT_SYSTEM
                return IsInputSystemMousePresent;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.mousePresent;
#else
                ReportNoInputBackend();
                return false;
#endif
            }
        }

        /// <summary>
        /// The mouse position in screen pixels.
        /// </summary>
        protected virtual Vector2 MousePosition
        {
            get
            {
#if MU3LIBRARY_INPUTSYSTEM_SUPPORT && ENABLE_INPUT_SYSTEM
                return InputSystemMousePosition;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.mousePosition;
#else
                return Vector2.zero;
#endif
            }
        }

        /// <summary>
        /// Whether the left mouse button went down this frame.
        /// </summary>
        protected virtual bool GetMouseButtonDown()
        {
#if MU3LIBRARY_INPUTSYSTEM_SUPPORT && ENABLE_INPUT_SYSTEM
            return GetInputSystemMouseButtonDown();
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        /// <summary>
        /// Whether the left mouse button is held down.
        /// </summary>
        protected virtual bool GetMouseButton()
        {
#if MU3LIBRARY_INPUTSYSTEM_SUPPORT && ENABLE_INPUT_SYSTEM
            return GetInputSystemMouseButton();
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(0);
#else
            return false;
#endif
        }

        /// <summary>
        /// How many touches <see cref="GetTouch"/> reports this frame, the ones in progress and the ones that ended
        /// <br/> this frame.
        /// </summary>
        protected virtual int TouchCount
        {
            get
            {
#if MU3LIBRARY_INPUTSYSTEM_SUPPORT && ENABLE_INPUT_SYSTEM
                return GetInputSystemTouchCount();
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.touchCount;
#else
                ReportNoInputBackend();
                return 0;
#endif
            }
        }

        /// <summary>
        /// One of the <see cref="TouchCount"/> touches of this frame. Its <c>fingerId</c> stays the same while the
        /// <br/> touch lasts and is never negative.
        /// <br/> The Input System backend fills <c>fingerId</c> with the touch id, which a new touch does not reuse
        /// <br/> the way the Input Manager reuses a finger index, plus <c>position</c>, <c>rawPosition</c>,
        /// <br/> <c>deltaPosition</c>, <c>phase</c>, <c>tapCount</c>, <c>pressure</c>, <c>maximumPossiblePressure</c>,
        /// <br/> and <c>type</c>.
        /// </summary>
        protected virtual Touch GetTouch(int index)
        {
#if MU3LIBRARY_INPUTSYSTEM_SUPPORT && ENABLE_INPUT_SYSTEM
            return GetInputSystemTouch(index);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetTouch(index);
#else
            return default;
#endif
        }

#if !(MU3LIBRARY_INPUTSYSTEM_SUPPORT && ENABLE_INPUT_SYSTEM) && !ENABLE_LEGACY_INPUT_MANAGER
        private static bool s_isNoInputBackendReported;

        private static void ReportNoInputBackend()
        {
            if (s_isNoInputBackendReported)
            {
                return;
            }

            s_isNoInputBackendReported = true;
            Debug.LogWarning(
                "InputHandler reads no input: Active Input Handling enables only the Input System, " +
                "and the Input System package (com.unity.inputsystem) is not installed.");
        }
#endif
        #endregion
    }
}
