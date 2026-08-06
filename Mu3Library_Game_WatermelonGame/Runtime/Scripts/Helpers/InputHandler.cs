using UnityEngine;
using UnityEngine.Events;

namespace Mu3Library.Game.WatermelonGame.Helpers
{
    public class InputHandler : MonoBehaviour
    {
        protected bool _isDragging;
        public bool IsDragging => _isDragging;

        public UnityEvent<Vector2> OnTouchBegan;
        public UnityEvent<Vector2> OnTouchMoved;
        public UnityEvent<Vector2> OnTouchEnded;



        protected virtual void OnDisable()
        {
            if (_isDragging)
            {
                OnTouchEndedHandler(default);
            }
        }

        protected virtual void Update()
        {
            if (Input.touchCount == 0)
            {
                if (_isDragging)
                {
                    OnTouchEndedHandler(default);
                }

                return;
            }

            Touch touch = Input.GetTouch(0);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    OnTouchBeganHandler(touch);
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    OnTouchEndedHandler(touch);
                    break;

                case TouchPhase.Moved:
                    // case TouchPhase.Stationary:
                    OnTouchMovedHandler(touch);
                    break;
            }
        }

        protected virtual void OnTouchBeganHandler(Touch touch)
        {
            _isDragging = true;

            OnTouchBegan?.Invoke(touch.position);
        }

        protected virtual void OnTouchEndedHandler(Touch touch)
        {
            _isDragging = false;

            OnTouchEnded?.Invoke(touch.position);
        }

        protected virtual void OnTouchMovedHandler(Touch touch)
        {
            if (!_isDragging)
            {
                return;
            }

            OnTouchMoved?.Invoke(touch.position);
        }
    }
}
