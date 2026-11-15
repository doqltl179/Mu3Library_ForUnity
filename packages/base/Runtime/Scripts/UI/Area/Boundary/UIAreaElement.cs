using UnityEngine;
using Mu3Library.Extensions;

namespace Mu3Library.UI.Area
{
    /// <summary>
    /// Anchors the owning RectTransform to one area of the <see cref="UIAreaGrid"/> on its parent.
    /// One area holds one element, so an area another element already owns cannot be taken.
    /// <para>
    /// The grid drives the RectTransform properties it owns, which the inspector then shows as read only:
    /// the anchors always, and the position and the size while <see cref="FillArea"/> is on.
    /// The pivot, the rotation and the scale are never driven.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class UIAreaElement : MonoBehaviour
    {
        private RectTransform m_rectTransform;
        protected RectTransform _rectTransform => m_rectTransform ??= gameObject.GetOrAddComponent<RectTransform>();
        public RectTransform RectTransform => _rectTransform;

        [SerializeField] private UIAreaType m_areaType = UIAreaType.Middle;
        /// <summary>
        /// Area this element is anchored to. Setting an area another element of the same grid
        /// already owns is refused, so that an area is never claimed twice.
        /// </summary>
        public UIAreaType AreaType
        {
            get => m_areaType;
            set
            {
                if (m_areaType == value)
                {
                    return;
                }

                if (IsAreaTaken(value))
                {
                    Debug.LogWarning($"Area is already taken by another element. area: {value}, name: {name}", this);
                    return;
                }

                m_areaType = value;

                Apply();
            }
        }

        [SerializeField] private bool m_fillArea = true;
        /// <summary>
        /// Clears the offsets so that the rect fills its area exactly, which also hands the position
        /// and the size to the grid. Turn it off to place and size the rect inside its area by hand.
        /// </summary>
        public bool FillArea
        {
            get => m_fillArea;
            set
            {
                if (m_fillArea == value)
                {
                    return;
                }

                m_fillArea = value;

                Apply();
            }
        }

        /// <summary>
        /// Grid this element belongs to. The anchors are relative to the parent,
        /// so only a <see cref="UIAreaGrid"/> on the direct parent can drive this element.
        /// </summary>
        public UIAreaGrid Grid
        {
            get
            {
                Transform parent = transform.parent;
                if (parent == null)
                {
                    return null;
                }

                parent.TryGetComponent(out UIAreaGrid grid);
                return grid;
            }
        }

        private DrivenRectTransformTracker m_drivenTracker;



        protected virtual void OnEnable()
        {
            Apply();
        }

        protected virtual void OnDisable()
        {
            m_drivenTracker.Clear();
        }

        protected virtual void OnTransformParentChanged()
        {
            // The element gained or lost a grid, so what it drives changes with it.
            Apply();
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            // Touching a transform is not allowed inside OnValidate,
            // which Unity also calls in play mode while an asset is deserialized.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null)
                {
                    return;
                }

                ResolveArea();
            };
        }
#endif

        #region Utility
        /// <summary>
        /// Anchors this element to its area. Does nothing while the parent has no <see cref="UIAreaGrid"/>.
        /// </summary>
        public void Apply()
        {
            UIAreaGrid grid = Grid;

            UpdateDrivenProperties(grid);

            if (grid == null)
            {
                return;
            }

            grid.Apply(_rectTransform, m_areaType, m_fillArea);
        }

        /// <summary>
        /// True while another element of the same grid already owns the area.
        /// </summary>
        public bool IsAreaTaken(UIAreaType areaType)
        {
            return IsAreaTaken(Grid, areaType);
        }
        #endregion

        private bool IsAreaTaken(UIAreaGrid grid, UIAreaType areaType)
        {
            if (grid == null)
            {
                return false;
            }

            UIAreaElement element = grid.GetElement(areaType);

            return element != null && element != this;
        }

        private void UpdateDrivenProperties(UIAreaGrid grid)
        {
            m_drivenTracker.Clear();

            // An element without a grid drives nothing, so its RectTransform stays editable.
            RectTransform rectTransform = _rectTransform;
            if (rectTransform == null || grid == null)
            {
                return;
            }

            DrivenTransformProperties drivenProperties = DrivenTransformProperties.Anchors;
            if (m_fillArea)
            {
                drivenProperties |= DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.SizeDelta;
            }

            m_drivenTracker.Add(this, rectTransform, drivenProperties);
        }

        private void ResolveArea()
        {
            UIAreaGrid grid = Grid;

            if (IsAreaTaken(grid, m_areaType))
            {
                // A copied element claims an area that is already owned, so the grid moves it to a free one.
                grid.ResolveElements();
            }

            Apply();
        }
    }
}
