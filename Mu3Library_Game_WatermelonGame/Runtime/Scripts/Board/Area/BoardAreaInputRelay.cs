using System;
using Mu3Library.Game.WatermelonGame.Helpers;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Area
{
    /// <summary>
    /// Forwards the raw touches of an <see cref="InputHandler"/> as board touches.
    /// <br/> A drag only starts inside the board area. Moves are reported while they remain inside,
    /// <br/> while the end is always reported so the board never reacts to a touch that began elsewhere.
    /// </summary>
    public sealed class BoardAreaInputRelay
    {
        private readonly InputHandler _inputHandler;
        private readonly Func<Vector2, bool> _isInsideArea;

        private bool _isListening;

        private bool _isDragging;
        public bool IsDragging => _isDragging;

        public event Action<Vector2> TouchBegan;
        public event Action<Vector2> TouchMoved;
        public event Action<Vector2> TouchEnded;

        /// <param name="inputHandler">The handler producing the raw touches.</param>
        /// <param name="isInsideArea">Tells whether a screen position lies inside the board area.</param>
        public BoardAreaInputRelay(InputHandler inputHandler, Func<Vector2, bool> isInsideArea)
        {
            _inputHandler = inputHandler;
            _isInsideArea = isInsideArea;
        }

        public void Enable()
        {
            if (_isListening || _inputHandler == null)
            {
                return;
            }

            _inputHandler.OnTouchBegan.AddListener(OnTouchBeganEvent);
            _inputHandler.OnTouchMoved.AddListener(OnTouchMovedEvent);
            _inputHandler.OnTouchEnded.AddListener(OnTouchEndedEvent);

            _isListening = true;
        }

        public void Disable()
        {
            if (!_isListening || _inputHandler == null)
            {
                return;
            }

            _inputHandler.OnTouchBegan.RemoveListener(OnTouchBeganEvent);
            _inputHandler.OnTouchMoved.RemoveListener(OnTouchMovedEvent);
            _inputHandler.OnTouchEnded.RemoveListener(OnTouchEndedEvent);

            _isListening = false;

            // The end of this drag is never delivered, so it must not block the next one.
            _isDragging = false;
        }

        private void OnTouchBeganEvent(Vector2 screenPos)
        {
            if (_isDragging || !IsInsideArea(screenPos))
            {
                return;
            }

            _isDragging = true;

            TouchBegan?.Invoke(screenPos);
        }

        private void OnTouchMovedEvent(Vector2 screenPos)
        {
            if (!_isDragging || !IsInsideArea(screenPos))
            {
                return;
            }

            TouchMoved?.Invoke(screenPos);
        }

        private void OnTouchEndedEvent(Vector2 screenPos)
        {
            if (!_isDragging)
            {
                return;
            }

            _isDragging = false;

            TouchEnded?.Invoke(screenPos);
        }

        private bool IsInsideArea(Vector2 screenPos)
            => _isInsideArea == null || _isInsideArea(screenPos);
    }
}
