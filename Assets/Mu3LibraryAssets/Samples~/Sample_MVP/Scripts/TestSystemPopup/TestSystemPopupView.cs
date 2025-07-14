using UnityEngine;
using Mu3Library.UI.DesignPattern.MPV;
using Mu3Library.Attribute;
using TMPro;

namespace Mu3Library.Sample.MVP
{
    public class TestSystemPopupView : View
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

            if (model is TestSystemPopupModel testSystemPopupModel)
            {
                _text.text = testSystemPopupModel.Message;

                AddConfirmEvent(testSystemPopupModel.OnClickConfirm);
                AddCancelEvent(testSystemPopupModel.OnClickCancel);
            }
        }

        public override void Destroyed()
        {
            base.Destroyed();

            if (_model is TestSystemPopupModel testSystemPopupModel)
            {
                RemoveConfirmEvent(testSystemPopupModel.OnClickConfirm);
                RemoveCancelEvent(testSystemPopupModel.OnClickCancel);
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