using UnityEngine;
using Mu3Library.UI.DesignPattern.MPV;
using Mu3Library.Attribute;
using TMPro;

namespace Mu3Library.Sample.MVP
{
    public class TestPopupView : View
    {
        [Title("Open/Close Animation Properties")]
        [SerializeField] private RectTransform _animationRect;
        [SerializeField] private AnimationCurve _openCloseAnimationCurve;
        [SerializeField, Range(0.0f, 1.0f)] private float _openCloseAnimationTime = 0.4f;
        private float _scaleFactor = 0.0f;

        [Title("Components")]
        [SerializeField] private TextMeshProUGUI _text;



        public override void Init<TModel>(TModel model)
        {
            base.Init(model);

            if (model is TestPopupModel testPopupModel)
            {
                _text.text = testPopupModel.Message;

                AddConfirmEvent(testPopupModel.OnClickConfirm);
                AddCancelEvent(testPopupModel.OnClickCancel);
            }
        }

        public override void Destroyed()
        {
            base.Destroyed();

            if (_model is TestPopupModel testPopupModel)
            {
                RemoveConfirmEvent(testPopupModel.OnClickConfirm);
                RemoveCancelEvent(testPopupModel.OnClickCancel);
            }
        }

        protected override bool OpenAnimationTrigger()
        {
            _scaleFactor += Time.deltaTime / _openCloseAnimationTime;
            _scaleFactor = Mathf.Min(_scaleFactor, 1.0f);
            _animationRect.localScale = Vector3.one * _openCloseAnimationCurve.Evaluate(_scaleFactor);

            return _scaleFactor == 1;
        }

        protected override bool CloseAnimationTrigger()
        {
            _scaleFactor -= Time.deltaTime / _openCloseAnimationTime;
            _scaleFactor = Mathf.Max(_scaleFactor, 0.0f);
            _animationRect.localScale = Vector3.one * _openCloseAnimationCurve.Evaluate(_scaleFactor);

            return _scaleFactor == 0;
        }
    }
}