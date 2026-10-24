using UnityEngine;
using UnityEngine.Events;

namespace Mu3Library.Game.WatermelonGame.Helpers
{
    /// <summary>
    /// Turns the mouse and the touches into one drag at a time.
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

        protected bool _isDragging;
        public bool IsDragging => _isDragging;

        public UnityEvent<Vector2> OnTouchBegan = new UnityEvent<Vector2>();
        public UnityEvent<Vector2> OnTouchMoved = new UnityEvent<Vector2>();
        public UnityEvent<Vector2> OnTouchEnded = new UnityEvent<Vector2>();

        private int _activeFingerId = NoActiveFingerId;
        private Vector2 _lastMousePosition;


        protected virtual void OnDisable()
        {
            if (_isDragging)
            {
                OnTouchEndedHandler(default);
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

            if (TryGetBeganTouch(out Touch beganTouch))
            {
                OnTouchBeganHandler(beganTouch);
                return;
            }

            // The mouse drives the same drag pipeline on a platform without a touch screen,
            // which is what makes the game playable in the editor and on desktop.
            if (IsMousePresent && GetMouseButtonDown())
            {
                OnMouseDragBegan(MousePosition);
            }
        }

        private void UpdateTouchDrag()
        {
            if (!TryGetActiveTouch(out Touch activeTouch))
            {
                OnTouchEndedHandler(default);
                return;
            }

            switch (activeTouch.phase)
            {
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    OnTouchEndedHandler(activeTouch);
                    break;

                case TouchPhase.Moved:
                    // case TouchPhase.Stationary:
                    OnTouchMovedHandler(activeTouch);
                    break;
            }
        }

        private void UpdateMouseDrag()
        {
            Vector2 mousePosition = MousePosition;

            if (!GetMouseButton())
            {
                OnMouseDragEnded(mousePosition);
                return;
            }

            if (mousePosition != _lastMousePosition)
            {
                _lastMousePosition = mousePosition;
                OnTouchMoved?.Invoke(mousePosition);
            }
        }

        private void OnMouseDragBegan(Vector2 position)
        {
            _isDragging = true;
            _activeFingerId = MouseFingerId;
            _lastMousePosition = position;

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

            OnTouchMoved?.Invoke(touch.position);
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

        private bool TryGetBeganTouch(out Touch beganTouch)
        {
            int touchCount = TouchCount;
            for (int i = 0; i < touchCount; i++)
            {
                Touch touch = GetTouch(i);
                if (touch.phase == TouchPhase.Began)
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
