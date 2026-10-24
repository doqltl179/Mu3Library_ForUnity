#if MU3LIBRARY_INPUTSYSTEM_SUPPORT && ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch;

namespace Mu3Library.Game.WatermelonGame.Helpers
{
    /// <summary>
    /// The Input System side of <see cref="InputHandler"/>: the current mouse, and the touches of the
    /// <br/> EnhancedTouch API, which reports every touch began and ended in frames of their own the way the
    /// <br/> Input Manager does, where the raw <c>Touchscreen</c> state can lose a short touch within one frame.
    /// </summary>
    public partial class InputHandler
    {
        /// <summary>
        /// Whether this handler holds one of the balanced <c>EnhancedTouchSupport.Enable</c> calls.
        /// </summary>
        private bool _isEnhancedTouchEnabled;

        private static bool IsInputSystemMousePresent => Mouse.current != null;

        private static Vector2 InputSystemMousePosition
        {
            get
            {
                Mouse mouse = Mouse.current;
                return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            }
        }

        private static bool GetInputSystemMouseButtonDown()
        {
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        private static bool GetInputSystemMouseButton()
        {
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.isPressed;
        }

        private int GetInputSystemTouchCount()
        {
            // EnhancedTouch only records touches while it is enabled, so the first read enables it
            // and OnDisable gives it back.
            EnableEnhancedTouch();

            return EnhancedTouch.Touch.activeTouches.Count;
        }

        private static Touch GetInputSystemTouch(int index)
        {
            EnhancedTouch.Touch touch = EnhancedTouch.Touch.activeTouches[index];

            return new Touch
            {
                // The touch id is unique among the active touches and lasts as long as the touch.
                fingerId = touch.touchId,
                position = touch.screenPosition,
                phase = ToTouchPhase(touch.phase),
            };
        }

        private void EnableEnhancedTouch()
        {
            if (_isEnhancedTouchEnabled)
            {
                return;
            }

            EnhancedTouch.EnhancedTouchSupport.Enable();
            _isEnhancedTouchEnabled = true;
        }

        private void DisableEnhancedTouch()
        {
            if (!_isEnhancedTouchEnabled)
            {
                return;
            }

            EnhancedTouch.EnhancedTouchSupport.Disable();
            _isEnhancedTouchEnabled = false;
        }

        private static UnityEngine.TouchPhase ToTouchPhase(UnityEngine.InputSystem.TouchPhase phase)
        {
            switch (phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began: return UnityEngine.TouchPhase.Began;
                case UnityEngine.InputSystem.TouchPhase.Moved: return UnityEngine.TouchPhase.Moved;
                case UnityEngine.InputSystem.TouchPhase.Ended: return UnityEngine.TouchPhase.Ended;
                case UnityEngine.InputSystem.TouchPhase.Canceled: return UnityEngine.TouchPhase.Canceled;
                default: return UnityEngine.TouchPhase.Stationary;
            }
        }
    }
}
#endif
