using Mu3Library.Attribute;
using Mu3Library.Game.WatermelonGame.Board.Area;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board
{
    public partial class BoardArea
    {
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_bounds.IsValid)
            {
                return;
            }

            CoordinateBounds world = _bounds.World;

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(new Vector2(world.Min.x, world.Min.y), new Vector2(world.Min.x, world.Max.y));
            Gizmos.DrawLine(new Vector2(world.Min.x, world.Max.y), new Vector2(world.Max.x, world.Max.y));
            Gizmos.DrawLine(new Vector2(world.Max.x, world.Max.y), new Vector2(world.Max.x, world.Min.y));
            Gizmos.DrawLine(new Vector2(world.Max.x, world.Min.y), new Vector2(world.Min.x, world.Min.y));

            CoordinateBounds itemArea = ItemAreaLocalBounds;
            Vector3 itemAreaBottomLeft = transform.TransformPoint(new Vector3(itemArea.Min.x, itemArea.Min.y, 0.0f));
            Vector3 itemAreaTopLeft = transform.TransformPoint(new Vector3(itemArea.Min.x, itemArea.Max.y, 0.0f));
            Vector3 itemAreaTopRight = transform.TransformPoint(new Vector3(itemArea.Max.x, itemArea.Max.y, 0.0f));
            Vector3 itemAreaBottomRight = transform.TransformPoint(new Vector3(itemArea.Max.x, itemArea.Min.y, 0.0f));

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(itemAreaBottomLeft, itemAreaTopLeft);
            Gizmos.DrawLine(itemAreaTopLeft, itemAreaTopRight);
            Gizmos.DrawLine(itemAreaTopRight, itemAreaBottomRight);
            Gizmos.DrawLine(itemAreaBottomRight, itemAreaBottomLeft);

            Gizmos.color = Color.cyan;
            Vector2 lineNormalizedPosition = Vector2.up * _boardLocalNormalizedItemOutPosY;
            Vector3 lineLeft = BoardLocalNormalizedPositionToWorld(lineNormalizedPosition);
            lineNormalizedPosition.x = 1.0f;
            Vector3 lineRight = BoardLocalNormalizedPositionToWorld(lineNormalizedPosition);
            Gizmos.DrawLine(lineLeft, lineRight);
        }
#endif

        protected virtual void OnEnable()
        {
            if (Camera.main != null)
            {
                CalculateBounds();
            }

            _inputRelay.Enable();
        }

        protected virtual void OnDisable()
        {
            _inputRelay.Disable();
            _view.SetSpawnGuideLineVisible(false);
        }

        #region Utility
        [ButtonInvoke("Fit Board", ButtonHeight = 30f)]
        public void Fit()
            => Fit(Camera.main, _view.BoardSprite);

        public void Fit(Sprite sprite)
            => Fit(Camera.main, sprite);

        public void Fit(Camera cam, Sprite sprite)
        {
            if (!_view.Fit(cam, sprite, Clamp01(_viewportPadding)))
            {
                return;
            }

            CalculateBounds(cam);
        }

        public void SetBoardImage(Sprite boardSprite)
        {
            if (boardSprite == null)
            {
                return;
            }

            _view.SetBoardSprite(boardSprite);
            Fit();
        }

        public void SetBoardLocalItemOutLine()
            => SetBoardLocalItemOutLine(_view.LineSprite);

        public void SetBoardLocalItemOutLine(Sprite sprite)
        {
            _view.SetItemOutLine(sprite, _bounds, BoardLocalNormalizedItemOutPosY);

            // The line cannot be placed without the board rectangle,
            // and calculating it applies the sprite assigned above.
            if (sprite != null && !_bounds.IsValid)
            {
                CalculateBounds();
            }
        }

        public void SetBoardSpawnImage()
            => SetBoardSpawnImage(_view.SpawnSprite);

        /// <summary>
        /// Assigns the image that hangs on the top edge of the board area and follows the held item.
        /// </summary>
        /// <param name="sprite">The spawn sprite, null hides the marker.</param>
        public void SetBoardSpawnImage(Sprite sprite)
        {
            _view.SetSpawnMarker(sprite, _bounds, _view.SpawnNormalizedX);
            _view.SetSpawnGuideLine(_view.SpawnGuideLineSprite, _bounds);

            // The marker cannot be placed without the board rectangle,
            // and calculating it applies the sprite assigned above.
            if (sprite != null && !_bounds.IsValid)
            {
                CalculateBounds();
            }
        }

        public void SetBoardSpawnGuideLine()
            => SetBoardSpawnGuideLine(_view.SpawnGuideLineSprite);

        /// <summary>
        /// Assigns the vertical tiled guide line below the spawn marker.
        /// </summary>
        /// <param name="sprite">The one-segment guide line sprite, null hides the line.</param>
        public void SetBoardSpawnGuideLine(Sprite sprite)
        {
            _view.SetSpawnGuideLine(sprite, _bounds);

            // The guide line cannot be placed without the board rectangle,
            // and calculating it applies the sprite assigned above.
            if (sprite != null && !_bounds.IsValid)
            {
                CalculateBounds();
            }
        }

        /// <summary>
        /// Shows or hides the spawn guide line without changing its configured sprite.
        /// </summary>
        public void SetBoardSpawnGuideLineVisible(bool visible)
            => _view.SetSpawnGuideLineVisible(visible);

        /// <summary>
        /// Moves the spawn marker along the top edge of the board area.
        /// </summary>
        /// <param name="localX">The marker position in the board local space.</param>
        public void SetSpawnLocalPositionX(float localX)
        {
            // Without the board rectangle the local position cannot be normalized,
            // and a wrong fraction would move the marker once the board is calculated.
            if (!_bounds.IsValid)
            {
                return;
            }

            SetSpawnBoardLocalNormalizedPositionX(_bounds.Local.Normalize(new Vector2(localX, 0.0f)).x);
        }

        /// <summary>
        /// Moves the spawn marker along the top edge of the board area.
        /// </summary>
        /// <param name="normalizedX">The marker position as a fraction of the board width.</param>
        public void SetSpawnBoardLocalNormalizedPositionX(float normalizedX)
        {
            _view.SetSpawnMarker(_view.SpawnSprite, _bounds, normalizedX);
            _view.SetSpawnGuideLine(_view.SpawnGuideLineSprite, _bounds);
        }

        public void SetOutColliders()
        {
            if (!_bounds.IsValid)
            {
                CalculateBounds();
                return;
            }

            _outColliders.Rebuild(ItemAreaLocalBounds);
        }

        public void Refresh()
            => CalculateBounds();

        public void CalculateBounds()
            => CalculateBounds(Camera.main);

        public void CalculateBounds(Camera cam)
            => CalculateBounds(cam, _pivot, AspectRatio);

        public void CalculateBounds(Camera cam, Vector2 pivot)
            => CalculateBounds(cam, pivot, AspectRatio);

        public void CalculateBounds(Camera cam, float aspectRatio)
            => CalculateBounds(cam, _pivot, aspectRatio);

        public void CalculateBounds(Camera cam, Vector2 pivot, float aspectRatio)
        {
            _converter.SetCamera(cam);
            _converter.ClearBounds();

            if (!BoardAreaBoundsCalculator.TryCalculate(
                    cam,
                    transform,
                    Clamp01(_viewportPadding),
                    Clamp01(pivot),
                    aspectRatio,
                    out BoardAreaBounds bounds))
            {
                return;
            }

            _converter.SetBounds(bounds);

            _outColliders.Rebuild(ItemAreaLocalBounds);
            _view.SetItemOutLine(_view.LineSprite, bounds, BoardLocalNormalizedItemOutPosY);
            _view.SetSpawnMarker(_view.SpawnSprite, bounds, _view.SpawnNormalizedX);
            _view.SetSpawnGuideLine(_view.SpawnGuideLineSprite, bounds);
        }
        #endregion

        private BoardAreaInputRelay CreateInputRelay()
        {
            BoardAreaInputRelay relay = new BoardAreaInputRelay(_inputHandler, screenPos => IsInScreenArea(screenPos));

            relay.TouchBegan += screenPos => OnTouchBegan?.Invoke(screenPos);
            relay.TouchMoved += screenPos => OnTouchMoved?.Invoke(screenPos);
            relay.TouchEnded += screenPos => OnTouchEnded?.Invoke(screenPos);

            return relay;
        }

        private void OnValidate()
        {
            _viewportPadding = Clamp01(_viewportPadding);
            _itemAreaViewportPadding = Clamp01(_itemAreaViewportPadding);
            _pivot = Clamp01(_pivot);
            _boardLocalNormalizedItemOutPosY = Mathf.Clamp01(_boardLocalNormalizedItemOutPosY);
        }

        private static Vector2 Clamp01(Vector2 value)
            => BoardAreaGeometry.Clamp01(value);

        private static Vector4 Clamp01(Vector4 value)
            => BoardAreaGeometry.Clamp01(value);
    }
}
