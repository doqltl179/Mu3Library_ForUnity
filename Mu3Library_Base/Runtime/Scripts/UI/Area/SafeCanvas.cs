using UnityEngine;
using Mu3Library.Extensions;

namespace Mu3Library.UI.Area
{
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

        [SerializeField, HideInInspector] private SafeRect m_safeRect;
        private SafeRect _safeRect
        {
            get
            {
                if (m_safeRect == null)
                {
                    SafeRect safeRect = transform.GetComponentInChildren<SafeRect>(true);
                    if (safeRect == null)
                    {
                        GameObject go = new GameObject(SafeRectObjectName, typeof(RectTransform));
                        go.layer = gameObject.layer;
                        go.transform.SetParent(transform, false);

                        safeRect = go.AddComponent<SafeRect>();
                    }

                    m_safeRect = safeRect;
                }

                return m_safeRect;
            }
        }
        public SafeRect SafeRect => _safeRect;



        private void OnEnable()
        {
            ResolveSafeRect();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            // Creating or reparenting objects is not allowed inside OnValidate.
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

        private void ResolveSafeRect()
        {
            SafeRect safeRect = _safeRect;
            if (safeRect == null)
            {
                Debug.LogWarning($"Failed to resolve SafeRect. name: {name}", this);
                return;
            }

            Transform safeRectTransform = safeRect.transform;
            if (safeRectTransform.parent != transform)
            {
                safeRectTransform.SetParent(transform, false);
            }

            safeRect.Calculate();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
        }
    }
}
