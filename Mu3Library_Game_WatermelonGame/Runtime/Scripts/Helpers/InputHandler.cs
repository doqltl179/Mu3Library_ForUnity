using UnityEngine;
using UnityEngine.Events;

namespace Mu3Library.Game.WatermelonGame.Helpers
{
    public class InputHandler : MonoBehaviour
    {
        private const int NoActiveFingerId = -1;

        /// <summary>
        /// Finger id the mouse drag runs under. Touches never carry a negative id, so the mouse
        /// shares the drag pipeline without ever colliding with a real finger.
        /// </summary>
        private const int MouseFingerId = -2;

        protected bool _isDragging;
        public bool IsDragging => _isDragging;

        public UnityEvent<Vector2> OnTouchBegan;
        public UnityEvent<Vector2> OnTouchMoved;
        public UnityEvent<Vector2> OnTouchEnded;

        private int _activeFingerId = NoActiveFingerId;
        private Vector2 _lastMousePosition;


        protected virtual void OnDisable()
        {
            if (_isDragging)
            {
                OnTouchEndedHandler(default);
            }
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
            if (Input.mousePresent && Input.GetMouseButtonDown(0))
            {
                OnMouseDragBegan(Input.mousePosition);
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
            Vector2 mousePosition = Input.mousePosition;

            if (!Input.GetMouseButton(0))
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
            int touchCount = Input.touchCount;
            for (int i = 0; i < touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.fingerId == _activeFingerId)
                {
                    activeTouch = touch;
                    return true;
                }
            }

            activeTouch = default;
            return false;
        }

        private static bool TryGetBeganTouch(out Touch beganTouch)
        {
            int touchCount = Input.touchCount;
            for (int i = 0; i < touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began)
                {
                    beganTouch = touch;
                    return true;
                }
            }

            beganTouch = default;
            return false;
        }
    }
}
