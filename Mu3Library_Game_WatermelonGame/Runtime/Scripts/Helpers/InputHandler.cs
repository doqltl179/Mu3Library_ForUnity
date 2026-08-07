using UnityEngine;
using UnityEngine.Events;

namespace Mu3Library.Game.WatermelonGame.Helpers
{
    public class InputHandler : MonoBehaviour
    {
        private const int NoActiveFingerId = -1;

        protected bool _isDragging;
        public bool IsDragging => _isDragging;

        public UnityEvent<Vector2> OnTouchBegan;
        public UnityEvent<Vector2> OnTouchMoved;
        public UnityEvent<Vector2> OnTouchEnded;

        private int _activeFingerId = NoActiveFingerId;


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

                return;
            }

            if (TryGetBeganTouch(out Touch beganTouch))
            {
                OnTouchBeganHandler(beganTouch);
            }
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
