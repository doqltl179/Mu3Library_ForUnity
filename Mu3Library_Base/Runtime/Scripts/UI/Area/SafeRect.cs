using UnityEngine;
using Mu3Library.Extensions;

namespace Mu3Library.UI.Area
{
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class SafeRect : MonoBehaviour
    {
        private RectTransform m_rectTransform;
        protected RectTransform _rectTransform => m_rectTransform ??= gameObject.GetOrAddComponent<RectTransform>();
        public RectTransform RectTransform => _rectTransform;

        private Rect _appliedSafeArea = Rect.zero;
        private Vector2Int _appliedScreenSize = Vector2Int.zero;



        protected virtual void OnEnable()
        {
            Calculate();
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

            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            _appliedSafeArea = safeAreaRect;
            _appliedScreenSize = new Vector2Int(Screen.width, Screen.height);

            OnCalculated(safeAreaRect);
        }
        #endregion

        protected virtual void OnCalculated(Rect safeArea) { }

        private bool IsScreenChanged()
        {
            return _appliedScreenSize.x != Screen.width ||
                _appliedScreenSize.y != Screen.height ||
                _appliedSafeArea != Screen.safeArea;
        }
    }
}
