using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mu3Library.Sample.Template.Global
{
    /// <summary>
    /// Reports the preferred size of the TextMesh Pro text on this object to the layout system in a player built
    /// without the TMP Essential Resources. TextMesh Pro's own preferred width and height are 0 while the project has
    /// no TMP Settings, so a <c>ContentSizeFitter</c> or a layout group that controls its children's sizes would lay
    /// the text out as if it were empty. This measures the text with <c>TMP_Text.GetPreferredValues(width, height)</c>,
    /// which works without TMP Settings, and reports it above TextMesh Pro's own layout priority. While TMP Settings
    /// exist, and in the Editor, which draws no TextMesh Pro text without them, it reports nothing and TextMesh Pro's
    /// own values apply.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public class SampleTextPreferredSize : UIBehaviour, ILayoutElement
    {
#if !UNITY_EDITOR
        private static bool? _isMeasuring;
#endif

        private TMP_Text _text;
        private float _preferredWidth = -1f;
        private float _preferredHeight = -1f;

        // A negative value leaves that property to the other layout elements on the object.
        public float minWidth => -1f;
        public float preferredWidth => _preferredWidth;
        public float flexibleWidth => -1f;
        // uGUI 2.6 added the maximum sizes to ILayoutElement; under earlier versions they are plain properties.
        public float maxWidth => -1f;
        public float minHeight => -1f;
        public float preferredHeight => _preferredHeight;
        public float flexibleHeight => -1f;
        public float maxHeight => -1f;
        // TMP_Text reports its own layout values at priority 0.
        public int layoutPriority => 1;

        private static bool IsMeasuring
        {
            get
            {
#if UNITY_EDITOR
                return false;
#else
                _isMeasuring ??= TMP_Settings.LoadDefaultSettings() == null;
                return _isMeasuring.Value;
#endif
            }
        }

        private TMP_Text Text => _text != null ? _text : _text = GetComponent<TMP_Text>();



        protected override void OnEnable()
        {
            base.OnEnable();

            SetDirty();
        }

        protected override void OnDisable()
        {
            SetDirty();

            base.OnDisable();
        }

        public void CalculateLayoutInputHorizontal()
        {
            _preferredWidth = IsMeasuring ? Text.GetPreferredValues(TMP_Math.FLOAT_MAX, TMP_Math.FLOAT_MAX).x : -1f;
        }

        public void CalculateLayoutInputVertical()
        {
            if (!IsMeasuring)
            {
                _preferredHeight = -1f;
                return;
            }

            // TextMesh Pro measures its preferred height within the width its margins leave.
            Vector4 margin = Text.margin;
            float width = Text.rectTransform.rect.width - margin.x - margin.z;
            _preferredHeight = Text.GetPreferredValues(width != 0f ? width : TMP_Math.FLOAT_MAX, TMP_Math.FLOAT_MAX).y;
        }

        private void SetDirty()
        {
            if (IsActive())
            {
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
            }
        }
    }
}
