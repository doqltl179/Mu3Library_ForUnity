using UnityEngine;
using Mu3Library.Extensions;

namespace Mu3Library.UI.Area
{
    /// <summary>
    /// Canvas that keeps a <see cref="SafeRect"/> of its own.
    /// The anchors of the safe rect are relative to its parent,
    /// so only a <see cref="SafeRect"/> on a direct child belongs to this canvas.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [DisallowMultipleComponent]
    public sealed class SafeCanvas : MonoBehaviour
    {
        private const string SafeRectObjectName = "SafeRect";

        private Canvas m_canvas;
        private Canvas _canvas => m_canvas ??= gameObject.GetOrAddComponent<Canvas>();
        public Canvas Canvas => _canvas;

        private RectTransform m_rectTransform;
        private RectTransform _rectTransform => m_rectTransform ??= gameObject.GetOrAddComponent<RectTransform>();
        public RectTransform RectTransform => _rectTransform;

        private SafeRect m_safeRect;
        /// <summary>
        /// Safe rect of this canvas, or null while it has none. Never creates one,
        /// so the children stay the only place that tells the safe rect apart.
        /// </summary>
        public SafeRect SafeRect
        {
            get
            {
                if (m_safeRect != null && m_safeRect.transform.parent == transform)
                {
                    return m_safeRect;
                }

                Transform self = transform;
                int childCount = self.childCount;

                for (int i = 0; i < childCount; i++)
                {
                    if (self.GetChild(i).TryGetComponent(out SafeRect safeRect))
                    {
                        m_safeRect = safeRect;
                        return safeRect;
                    }
                }

                m_safeRect = null;
                return null;
            }
        }



        private void OnEnable()
        {
            ResolveSafeRect();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Creating objects and touching the transform of another object are not allowed inside OnValidate,
            // which Unity also calls in play mode while an asset is deserialized.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null)
                {
                    return;
                }

                ResolveSafeRect();
            };
        }
#endif

        #region Utility
        /// <summary>
        /// Gives this canvas a safe rect, creating the missing one, and fits it to the screen.
        /// </summary>
        public void ResolveSafeRect()
        {
            SafeRect safeRect = SafeRect;
            if (safeRect == null)
            {
                safeRect = CreateSafeRect();
            }

            safeRect.Calculate();
        }

        /// <summary>
        /// Creates the safe rect child.
        /// Returns the one the canvas already has instead of creating a second one.
        /// </summary>
        public SafeRect CreateSafeRect()
        {
            SafeRect owner = SafeRect;
            if (owner != null)
            {
                Debug.LogWarning($"SafeRect is already held by [{owner.name}]. name: {name}", this);
                return owner;
            }

            GameObject go = new GameObject(SafeRectObjectName, typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);

            SafeRect safeRect = go.AddComponent<SafeRect>();
            m_safeRect = safeRect;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(go, $"Create {go.name}");
            }
#endif

            return safeRect;
        }
        #endregion
    }
}
