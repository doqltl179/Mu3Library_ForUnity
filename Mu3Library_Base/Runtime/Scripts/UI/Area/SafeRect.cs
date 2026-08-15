using UnityEngine;
using Mu3Library.Extensions;
using Mu3Library.Utility;

namespace Mu3Library.UI.Area
{
    /// <summary>
    /// Anchors the owning RectTransform to the safe area of the screen.
    /// <para>
    /// The safe area drives the RectTransform properties it owns, which the inspector then shows as read only:
    /// the anchors, the position, the size and the pivot. The rotation and the scale are never driven.
    /// </para>
    /// <para>
    /// The screen is followed through what reports it instead of through a per frame check: Unity sends
    /// <see cref="OnRectTransformDimensionsChange"/> as soon as the canvas follows a screen that changed
    /// size or orientation, and <see cref="ScreenChangeNotifier"/> reports a safe area that changed while
    /// the screen kept its size, such as the one a device turned upside down leaves behind.
    /// </para>
    /// <para>
    /// <see cref="ScreenChangeNotifier"/> is the only screen this component reads. <see cref="Screen"/>
    /// answers with the render target of whatever asks it, and the messages that bring this component back
    /// arrive from inside a canvas rebuild, where a scene view repaint answers with the scene view and the
    /// game view repaint answers with the game view. Reading it from here would make the same frame produce
    /// two screens and the anchors alternate between them, so the once a frame read is what is trusted.
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

        private bool _isCalculating = false;



        protected virtual void OnEnable()
        {
            ScreenChangeNotifier.OnChanged += OnScreenChanged;

            Calculate();
        }

        protected virtual void OnDisable()
        {
            ScreenChangeNotifier.OnChanged -= OnScreenChanged;

            _drivenTracker.Clear();
        }

        /// <summary>
        /// Unity sends this when this RectTransform or the one of its parent changed size, which is
        /// what a canvas does as soon as the screen it covers changes resolution or orientation.
        /// </summary>
        protected virtual void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            OnScreenChanged();
        }

        #region Utility
        public void Calculate()
        {
            if (_isCalculating)
            {
                return;
            }

            Rect safeAreaRect = ScreenChangeNotifier.SafeArea;
            Vector2Int screenSize = ScreenChangeNotifier.ScreenSize;
            float screenWidth = screenSize.x;
            float screenHeight = screenSize.y;

            if (screenWidth <= 0.0f || screenHeight <= 0.0f)
            {
                return;
            }

            Rect appliedSafeArea = safeAreaRect;
            if (appliedSafeArea.width <= 0.0f || appliedSafeArea.height <= 0.0f)
            {
                appliedSafeArea = new Rect(0.0f, 0.0f, screenWidth, screenHeight);
            }

            Vector2 anchorMin = new Vector2(
                Mathf.Clamp01(appliedSafeArea.xMin / screenWidth),
                Mathf.Clamp01(appliedSafeArea.yMin / screenHeight));
            Vector2 anchorMax = new Vector2(
                Mathf.Clamp01(appliedSafeArea.xMax / screenWidth),
                Mathf.Clamp01(appliedSafeArea.yMax / screenHeight));

            RectTransform rectTransform = _rectTransform;

            UpdateDrivenProperties(rectTransform);

            // Anchoring resizes the rect, which sends OnRectTransformDimensionsChange back here.
            // Reporting the applied screen first is what leaves that message nothing to do.
            // What was read is reported, not what took its place: reporting the substitute
            // would leave a screen that reports no safe area looking changed on every check.
            _appliedSafeArea = safeAreaRect;
            _appliedScreenSize = screenSize;

            _isCalculating = true;
            try
            {
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.AnchorTo(anchorMin, anchorMax);
            }
            finally
            {
                _isCalculating = false;
            }

            OnCalculated(appliedSafeArea);
        }
        #endregion

        protected virtual void OnCalculated(Rect safeArea) { }

        private void OnScreenChanged()
        {
            if (!IsScreenChanged())
            {
                return;
            }

            Calculate();
        }

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
            return _appliedScreenSize != ScreenChangeNotifier.ScreenSize ||
                _appliedSafeArea != ScreenChangeNotifier.SafeArea;
        }
    }
}
