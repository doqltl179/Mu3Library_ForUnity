using UnityEngine;
using Mu3Library.Extensions;

namespace Mu3Library.UI.Area
{
    /// <summary>
    /// Anchors the owning RectTransform to the safe area of the screen.
    /// <para>
    /// The safe area drives the RectTransform properties it owns, which the inspector then shows as read only:
    /// the anchors, the position, the size and the pivot. The rotation and the scale are never driven.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class SafeRect : MonoBehaviour
    {
        private const DrivenTransformProperties DrivenProperties =
            DrivenTransformProperties.Anchors |
            DrivenTransformProperties.AnchoredPosition |
            DrivenTransformProperties.SizeDelta |
            DrivenTransformProperties.Pivot;

        private RectTransform m_rectTransform;
        protected RectTransform _rectTransform => m_rectTransform ??= gameObject.GetOrAddComponent<RectTransform>();
        public RectTransform RectTransform => _rectTransform;

        private Rect _appliedSafeArea = Rect.zero;
        private Vector2Int _appliedScreenSize = Vector2Int.zero;

        private DrivenRectTransformTracker _drivenTracker;



        protected virtual void OnEnable()
        {
            Calculate();
        }

        protected virtual void OnDisable()
        {
            _drivenTracker.Clear();
        }

        protected virtual void Update()
        {
            if (IsScreenChanged())
            {
                Calculate();
            }
        }

        #region Utility
        public void Calculate()
        {
            Rect safeAreaRect = Screen.safeArea;
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            if (screenWidth <= 0.0f || screenHeight <= 0.0f)
            {
                return;
            }

            if (safeAreaRect.width <= 0.0f || safeAreaRect.height <= 0.0f)
            {
                safeAreaRect = new Rect(0.0f, 0.0f, screenWidth, screenHeight);
            }

            Vector2 anchorMin = new Vector2(
                Mathf.Clamp01(safeAreaRect.xMin / screenWidth),
                Mathf.Clamp01(safeAreaRect.yMin / screenHeight));
            Vector2 anchorMax = new Vector2(
                Mathf.Clamp01(safeAreaRect.xMax / screenWidth),
                Mathf.Clamp01(safeAreaRect.yMax / screenHeight));

            RectTransform rectTransform = _rectTransform;

            UpdateDrivenProperties(rectTransform);

            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.AnchorTo(anchorMin, anchorMax);

            _appliedSafeArea = safeAreaRect;
            _appliedScreenSize = new Vector2Int(Screen.width, Screen.height);

            OnCalculated(safeAreaRect);
        }
        #endregion

        protected virtual void OnCalculated(Rect safeArea) { }

        private void UpdateDrivenProperties(RectTransform rectTransform)
        {
            _drivenTracker.Clear();

            if (rectTransform == null)
            {
                return;
            }

            _drivenTracker.Add(this, rectTransform, DrivenProperties);
        }

        private bool IsScreenChanged()
        {
            return _appliedScreenSize.x != Screen.width ||
                _appliedScreenSize.y != Screen.height ||
                _appliedSafeArea != Screen.safeArea;
        }
    }
}
