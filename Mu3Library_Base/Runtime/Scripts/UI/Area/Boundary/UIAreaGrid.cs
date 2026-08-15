using System.Collections.Generic;
using UnityEngine;
using Mu3Library.Extensions;

namespace Mu3Library.UI.Area
{
    /// <summary>
    /// Splits the owning RectTransform into nine areas and anchors the child <see cref="UIAreaElement"/> objects to them.
    /// <para>
    /// The horizontal cut lines and the vertical cut lines have their own <see cref="UIAreaEditMode"/>,
    /// so a cut line can either be shared by the whole grid or belong to a single row/column.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class UIAreaGrid : MonoBehaviour
    {
        private const string ElementObjectNamePrefix = "Area_";

        private RectTransform m_rectTransform;
        protected RectTransform _rectTransform => m_rectTransform ??= gameObject.GetOrAddComponent<RectTransform>();
        public RectTransform RectTransform => _rectTransform;

        [SerializeField] private UIAreaEditMode m_horizontalEditMode = UIAreaEditMode.Uniform;
        /// <summary>
        /// Scope of the left/right cut lines.
        /// <see cref="UIAreaEditMode.Uniform"/> moves the cut line of every row, <see cref="UIAreaEditMode.Independent"/>
        /// moves the cut line of one row only. Switching to <see cref="UIAreaEditMode.Independent"/> copies the shared
        /// cut lines into every row first, so the layout does not jump.
        /// </summary>
        public UIAreaEditMode HorizontalEditMode
        {
            get => m_horizontalEditMode;
            set
            {
                if (m_horizontalEditMode == value)
                {
                    return;
                }

                m_horizontalEditMode = value;

                SyncEditModes();
                Apply();
            }
        }

        [SerializeField] private UIAreaEditMode m_verticalEditMode = UIAreaEditMode.Uniform;
        /// <summary>
        /// Scope of the bottom/top cut lines.
        /// <see cref="UIAreaEditMode.Uniform"/> moves the cut line of every column, <see cref="UIAreaEditMode.Independent"/>
        /// moves the cut line of one column only. Switching to <see cref="UIAreaEditMode.Independent"/> copies the shared
        /// cut lines into every column first, so the layout does not jump.
        /// </summary>
        public UIAreaEditMode VerticalEditMode
        {
            get => m_verticalEditMode;
            set
            {
                if (m_verticalEditMode == value)
                {
                    return;
                }

                m_verticalEditMode = value;

                SyncEditModes();
                Apply();
            }
        }

        [SerializeField] private UIAreaBoundary m_horizontal = UIAreaBoundary.Default;
        [SerializeField] private UIAreaBoundary m_vertical = UIAreaBoundary.Default;

        [SerializeField] private UIAreaBoundary[] m_rowHorizontals = CreateBoundaries(UIAreaUtility.RowCount);
        private UIAreaBoundary[] _rowHorizontals
        {
            get
            {
                if (m_rowHorizontals == null || m_rowHorizontals.Length != UIAreaUtility.RowCount)
                {
                    m_rowHorizontals = ResizeBoundaries(m_rowHorizontals, UIAreaUtility.RowCount);
                }

                return m_rowHorizontals;
            }
        }

        [SerializeField] private UIAreaBoundary[] m_columnVerticals = CreateBoundaries(UIAreaUtility.ColumnCount);
        private UIAreaBoundary[] _columnVerticals
        {
            get
            {
                if (m_columnVerticals == null || m_columnVerticals.Length != UIAreaUtility.ColumnCount)
                {
                    m_columnVerticals = ResizeBoundaries(m_columnVerticals, UIAreaUtility.ColumnCount);
                }

                return m_columnVerticals;
            }
        }

        [SerializeField] private bool m_createElementsAutomatically = true;
        /// <summary>
        /// Creates a child <see cref="UIAreaElement"/> for every area that has none.
        /// Turn it off to keep only the areas that were created by hand.
        /// </summary>
        public bool CreateElementsAutomatically
        {
            get => m_createElementsAutomatically;
            set
            {
                if (m_createElementsAutomatically == value)
                {
                    return;
                }

                m_createElementsAutomatically = value;

                ResolveElements();
                Apply();
            }
        }

        private UIAreaElement[] m_elements;
        /// <summary>
        /// Element that owns each area. The children are what tells an area apart, so this is only
        /// a lookup that <see cref="GetElement"/> rebuilds on demand and never something that is saved.
        /// </summary>
        private UIAreaElement[] _elements => m_elements ??= new UIAreaElement[UIAreaUtility.AreaCount];

        [SerializeField] private bool m_drawAreaGizmo = true;
        /// <summary>
        /// Draws the nine areas in the scene view.
        /// </summary>
        public bool DrawAreaGizmo
        {
            get => m_drawAreaGizmo;
            set => m_drawAreaGizmo = value;
        }

        [SerializeField] private Color m_areaGizmoColor = new Color(0.3f, 0.8f, 1.0f, 0.7f);
        /// <summary>
        /// Color of the area gizmo.
        /// </summary>
        public Color AreaGizmoColor
        {
            get => m_areaGizmoColor;
            set => m_areaGizmoColor = value;
        }

        [SerializeField, HideInInspector] private UIAreaEditMode m_appliedHorizontalEditMode = UIAreaEditMode.Uniform;
        [SerializeField, HideInInspector] private UIAreaEditMode m_appliedVerticalEditMode = UIAreaEditMode.Uniform;

        private readonly List<UIAreaElement> m_elementBuffer = new List<UIAreaElement>();



        protected virtual void OnEnable()
        {
            SyncEditModes();
            ResolveElements();
            Apply();
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            ValidateBoundaries();
            SyncEditModes();

            if (Application.isPlaying)
            {
                ResolveElements();
                Apply();
                return;
            }

            // Creating objects and touching the transform of another object are not allowed inside OnValidate.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null)
                {
                    return;
                }

                ResolveElements();
                Apply();
            };
        }

        protected virtual void OnDrawGizmos()
        {
            if (!m_drawAreaGizmo || !isActiveAndEnabled)
            {
                return;
            }

            RectTransform rectTransform = _rectTransform;
            if (rectTransform == null)
            {
                return;
            }

            Rect localRect = rectTransform.rect;
            if (localRect.width <= 0.0f || localRect.height <= 0.0f)
            {
                return;
            }

            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;

            // Drawing in local space keeps the outline on the rect through any rotation and scale.
            Gizmos.matrix = rectTransform.localToWorldMatrix;
            Gizmos.color = m_areaGizmoColor;

            for (int i = 0; i < UIAreaUtility.AreaCount; i++)
            {
                Rect areaRect = GetAreaRect((UIAreaType)i);

                DrawWireRect(
                    localRect.x + areaRect.xMin * localRect.width,
                    localRect.y + areaRect.yMin * localRect.height,
                    localRect.x + areaRect.xMax * localRect.width,
                    localRect.y + areaRect.yMax * localRect.height);
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private static void DrawWireRect(float xMin, float yMin, float xMax, float yMax)
        {
            Vector3 leftBottom = new Vector3(xMin, yMin, 0.0f);
            Vector3 leftTop = new Vector3(xMin, yMax, 0.0f);
            Vector3 rightTop = new Vector3(xMax, yMax, 0.0f);
            Vector3 rightBottom = new Vector3(xMax, yMin, 0.0f);

            Gizmos.DrawLine(leftBottom, leftTop);
            Gizmos.DrawLine(leftTop, rightTop);
            Gizmos.DrawLine(rightTop, rightBottom);
            Gizmos.DrawLine(rightBottom, leftBottom);
        }
#endif

        #region Utility
        /// <summary>
        /// Cut lines that split the given row along the x axis.
        /// In <see cref="UIAreaEditMode.Uniform"/> every row reports the shared cut lines.
        /// </summary>
        public UIAreaBoundary GetHorizontalBoundary(UIAreaRow row)
        {
            if (m_horizontalEditMode == UIAreaEditMode.Uniform)
            {
                return m_horizontal.Clamped();
            }

            return _rowHorizontals[row.GetRowIndex()].Clamped();
        }

        /// <summary>
        /// Moves the cut lines that split the given row along the x axis.
        /// In <see cref="UIAreaEditMode.Uniform"/> the cut lines of every row are moved.
        /// </summary>
        public void SetHorizontalBoundary(UIAreaRow row, UIAreaBoundary boundary)
        {
            if (m_horizontalEditMode == UIAreaEditMode.Uniform)
            {
                m_horizontal = boundary.Clamped();
            }
            else
            {
                _rowHorizontals[row.GetRowIndex()] = boundary.Clamped();
            }

            Apply();
        }

        /// <summary>
        /// Cut lines that split the given column along the y axis.
        /// In <see cref="UIAreaEditMode.Uniform"/> every column reports the shared cut lines.
        /// </summary>
        public UIAreaBoundary GetVerticalBoundary(UIAreaColumn column)
        {
            if (m_verticalEditMode == UIAreaEditMode.Uniform)
            {
                return m_vertical.Clamped();
            }

            return _columnVerticals[column.GetColumnIndex()].Clamped();
        }

        /// <summary>
        /// Moves the cut lines that split the given column along the y axis.
        /// In <see cref="UIAreaEditMode.Uniform"/> the cut lines of every column are moved.
        /// </summary>
        public void SetVerticalBoundary(UIAreaColumn column, UIAreaBoundary boundary)
        {
            if (m_verticalEditMode == UIAreaEditMode.Uniform)
            {
                m_vertical = boundary.Clamped();
            }
            else
            {
                _columnVerticals[column.GetColumnIndex()] = boundary.Clamped();
            }

            Apply();
        }

        /// <summary>
        /// Moves every cut line back to <see cref="UIAreaBoundary.Default"/>,
        /// the shared ones and the per row/column ones alike.
        /// </summary>
        public void ResetBoundaries()
        {
            m_horizontal = UIAreaBoundary.Default;
            m_vertical = UIAreaBoundary.Default;

            UIAreaBoundary[] rowHorizontals = _rowHorizontals;
            for (int i = 0; i < rowHorizontals.Length; i++)
            {
                rowHorizontals[i] = UIAreaBoundary.Default;
            }

            UIAreaBoundary[] columnVerticals = _columnVerticals;
            for (int i = 0; i < columnVerticals.Length; i++)
            {
                columnVerticals[i] = UIAreaBoundary.Default;
            }

            Apply();
        }

        /// <summary>
        /// Normalized rect of the area inside this grid.
        /// </summary>
        public Rect GetAreaRect(UIAreaType areaType)
        {
            UIAreaColumn column = areaType.GetColumn();
            UIAreaRow row = areaType.GetRow();

            Vector2 horizontal = GetHorizontalBoundary(row).GetSlot(column.GetColumnIndex());
            Vector2 vertical = GetVerticalBoundary(column).GetSlot(row.GetRowIndex());

            return Rect.MinMaxRect(horizontal.x, vertical.x, horizontal.y, vertical.y);
        }

        /// <summary>
        /// Anchors of the area inside this grid.
        /// </summary>
        public void GetAreaAnchors(UIAreaType areaType, out Vector2 anchorMin, out Vector2 anchorMax)
        {
            Rect areaRect = GetAreaRect(areaType);

            anchorMin = areaRect.min;
            anchorMax = areaRect.max;
        }

        /// <summary>
        /// RectTransform of the element that owns the area, or null while the area has none.
        /// </summary>
        public RectTransform GetRectTransform(UIAreaType areaType)
        {
            UIAreaElement element = GetElement(areaType);

            return element == null ? null : element.RectTransform;
        }

        /// <summary>
        /// RectTransform of the element that owns the area.
        /// Returns false while the area has none, so the caller can skip it without a null check.
        /// </summary>
        public bool TryGetRectTransform(UIAreaType areaType, out RectTransform rectTransform)
        {
            rectTransform = GetRectTransform(areaType);

            return rectTransform != null;
        }

        /// <summary>
        /// Element that owns the area, or null while the area has none. Never creates one.
        /// </summary>
        public UIAreaElement GetElement(UIAreaType areaType)
        {
            int areaIndex = areaType.GetAreaIndex();
            UIAreaElement[] elements = _elements;

            UIAreaElement cached = elements[areaIndex];
            if (cached != null && cached.transform.parent == transform && cached.AreaType == areaType)
            {
                return cached;
            }

            Transform self = transform;
            int childCount = self.childCount;

            for (int i = 0; i < childCount; i++)
            {
                if (self.GetChild(i).TryGetComponent(out UIAreaElement element) && element.AreaType == areaType)
                {
                    elements[areaIndex] = element;
                    return element;
                }
            }

            elements[areaIndex] = null;
            return null;
        }

        /// <summary>
        /// Maps every area to its child element and creates the missing ones
        /// while <see cref="CreateElementsAutomatically"/> is on.
        /// An area holds one element, so an element that claims a taken area is moved to a free one.
        /// </summary>
        public void ResolveElements()
        {
            UIAreaElement[] elements = _elements;

            for (int i = 0; i < elements.Length; i++)
            {
                elements[i] = null;
            }

            GetElements(m_elementBuffer);

            // The first element of an area keeps it.
            for (int i = 0; i < m_elementBuffer.Count; i++)
            {
                UIAreaElement element = m_elementBuffer[i];

                int areaIndex = element.AreaType.GetAreaIndex();
                if (elements[areaIndex] == null)
                {
                    elements[areaIndex] = element;
                }
            }

            // The remaining ones claimed an area that is already taken, so they move to a free area.
            for (int i = 0; i < m_elementBuffer.Count; i++)
            {
                UIAreaElement element = m_elementBuffer[i];
                if (elements[element.AreaType.GetAreaIndex()] == element)
                {
                    continue;
                }

                int freeAreaIndex = GetFreeAreaIndex(elements);
                if (freeAreaIndex < 0)
                {
                    Debug.LogWarning($"Every area is already taken, so [{element.name}] keeps the duplicated area. area: {element.AreaType}, name: {name}", element);
                    continue;
                }

                element.AreaType = (UIAreaType)freeAreaIndex;
                elements[freeAreaIndex] = element;
            }

            m_elementBuffer.Clear();

            if (m_createElementsAutomatically)
            {
                CreateMissingElements();
            }
        }

        /// <summary>
        /// Creates a child element for every area that has none.
        /// </summary>
        public void CreateMissingElements()
        {
            UIAreaElement[] elements = _elements;

            for (int i = 0; i < elements.Length; i++)
            {
                UIAreaType areaType = (UIAreaType)i;

                if (GetElement(areaType) == null)
                {
                    elements[i] = CreateElement(areaType);
                }
            }
        }

        /// <summary>
        /// Creates a child element for the area.
        /// Returns the element the area already owns instead of creating a second one.
        /// </summary>
        public UIAreaElement CreateElement(UIAreaType areaType)
        {
            UIAreaElement owner = GetElement(areaType);
            if (owner != null)
            {
                Debug.LogWarning($"Area is already taken by [{owner.name}]. area: {areaType}, name: {name}", this);
                return owner;
            }

            GameObject go = new GameObject(GetElementName(areaType), typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);

            UIAreaElement element = go.AddComponent<UIAreaElement>();
            element.AreaType = areaType;
            element.Apply();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(go, $"Create {go.name}");
            }
#endif

            return element;
        }

        /// <summary>
        /// Anchors every child <see cref="UIAreaElement"/> to its area.
        /// </summary>
        public void Apply()
        {
            GetElements(m_elementBuffer);

            for (int i = 0; i < m_elementBuffer.Count; i++)
            {
                m_elementBuffer[i].Apply();
            }

            m_elementBuffer.Clear();
        }

        /// <summary>
        /// Anchors the target to the area. The target must be a direct child of this grid,
        /// because the anchors are relative to its own parent.
        /// </summary>
        public void Apply(RectTransform target, UIAreaType areaType, bool fillArea = true)
        {
            if (target == null)
            {
                Debug.LogWarning($"Target RectTransform is null. name: {name}", this);
                return;
            }

            GetAreaAnchors(areaType, out Vector2 anchorMin, out Vector2 anchorMax);

            target.AnchorTo(anchorMin, anchorMax, fillArea);
        }

        /// <summary>
        /// Collects the child <see cref="UIAreaElement"/> objects this grid drives, including the inactive ones.
        /// </summary>
        public void GetElements(List<UIAreaElement> results)
        {
            if (results == null)
            {
                Debug.LogWarning($"Result list is null. name: {name}", this);
                return;
            }

            results.Clear();

            Transform self = transform;
            int childCount = self.childCount;

            for (int i = 0; i < childCount; i++)
            {
                if (self.GetChild(i).TryGetComponent(out UIAreaElement element))
                {
                    results.Add(element);
                }
            }
        }

        /// <summary>
        /// Name given to an element created by this grid.
        /// </summary>
        public static string GetElementName(UIAreaType areaType)
        {
            return $"{ElementObjectNamePrefix}{areaType}";
        }
        #endregion

        private static int GetFreeAreaIndex(UIAreaElement[] elements)
        {
            for (int i = 0; i < elements.Length; i++)
            {
                if (elements[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }

        private void SyncEditModes()
        {
            if (m_horizontalEditMode != m_appliedHorizontalEditMode)
            {
                if (m_horizontalEditMode == UIAreaEditMode.Independent)
                {
                    UIAreaBoundary[] rowHorizontals = _rowHorizontals;
                    for (int i = 0; i < rowHorizontals.Length; i++)
                    {
                        rowHorizontals[i] = m_horizontal.Clamped();
                    }
                }

                m_appliedHorizontalEditMode = m_horizontalEditMode;
            }

            if (m_verticalEditMode != m_appliedVerticalEditMode)
            {
                if (m_verticalEditMode == UIAreaEditMode.Independent)
                {
                    UIAreaBoundary[] columnVerticals = _columnVerticals;
                    for (int i = 0; i < columnVerticals.Length; i++)
                    {
                        columnVerticals[i] = m_vertical.Clamped();
                    }
                }

                m_appliedVerticalEditMode = m_verticalEditMode;
            }
        }

        private void ValidateBoundaries()
        {
            m_horizontal = m_horizontal.Clamped();
            m_vertical = m_vertical.Clamped();

            UIAreaBoundary[] rowHorizontals = _rowHorizontals;
            for (int i = 0; i < rowHorizontals.Length; i++)
            {
                rowHorizontals[i] = rowHorizontals[i].Clamped();
            }

            UIAreaBoundary[] columnVerticals = _columnVerticals;
            for (int i = 0; i < columnVerticals.Length; i++)
            {
                columnVerticals[i] = columnVerticals[i].Clamped();
            }
        }

        private static UIAreaBoundary[] CreateBoundaries(int length)
        {
            return ResizeBoundaries(null, length);
        }

        private static UIAreaBoundary[] ResizeBoundaries(UIAreaBoundary[] source, int length)
        {
            UIAreaBoundary[] boundaries = new UIAreaBoundary[length];

            for (int i = 0; i < length; i++)
            {
                boundaries[i] = source != null && i < source.Length
                    ? source[i].Clamped()
                    : UIAreaBoundary.Default;
            }

            return boundaries;
        }
    }
}
