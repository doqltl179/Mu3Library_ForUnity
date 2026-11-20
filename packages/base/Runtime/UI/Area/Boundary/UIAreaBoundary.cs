using UnityEngine;

namespace Mu3Library.UI.Area
{
    /// <summary>
    /// A pair of normalized cut lines that splits one axis into three slots.
    /// </summary>
    [System.Serializable]
    public struct UIAreaBoundary
    {
        [SerializeField, Range(0.0f, 1.0f)] private float m_min;
        [SerializeField, Range(0.0f, 1.0f)] private float m_max;

        /// <summary>
        /// Cut line between the first and the second slot.
        /// </summary>
        public float Min
        {
            get => m_min;
            set
            {
                m_min = Mathf.Clamp01(value);
                m_max = Mathf.Clamp(m_max, m_min, 1.0f);
            }
        }

        /// <summary>
        /// Cut line between the second and the third slot.
        /// </summary>
        public float Max
        {
            get => m_max;
            set
            {
                m_max = Mathf.Clamp01(value);
                m_min = Mathf.Clamp(m_min, 0.0f, m_max);
            }
        }

        private const float DefaultSideSize = 0.08f;

        /// <summary>
        /// Cut lines that give the first and the third slot 0.08 each, which leaves 0.84 to the second one.
        /// </summary>
        public static readonly UIAreaBoundary Default = new UIAreaBoundary(DefaultSideSize, 1.0f - DefaultSideSize);

        public UIAreaBoundary(float min, float max)
        {
            m_min = Mathf.Clamp01(min);
            m_max = Mathf.Clamp(max, m_min, 1.0f);
        }

        #region Utility
        /// <summary>
        /// Returns a copy with both cut lines inside [0, 1] and ordered.
        /// </summary>
        public UIAreaBoundary Clamped()
        {
            return new UIAreaBoundary(m_min, m_max);
        }

        /// <summary>
        /// Normalized range of the slot at <paramref name="slotIndex"/>. x is the lower edge, y is the upper edge.
        /// </summary>
        public Vector2 GetSlot(int slotIndex)
        {
            switch (slotIndex)
            {
                case 0: return new Vector2(0.0f, m_min);
                case 1: return new Vector2(m_min, m_max);
                case 2: return new Vector2(m_max, 1.0f);
                default: return new Vector2(0.0f, 1.0f);
            }
        }
        #endregion
    }
}
