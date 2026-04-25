using Mu3Library.UI.MVP;
using Mu3Library.UI.MVP.Animation;
using UnityEngine;

namespace Mu3Library.Sample.Template.MVP
{
    [CreateAssetMenu(fileName = FileName, menuName = MenuName, order = 0)]
    public class TwoCurveScaleAnimation : TwoCurveAnimation
    {
        public const string FileName = "TwoCurveScaleAnimation";
        private const string ItemName = "Two Curve Scale Animation";
        private const string MenuName = MenuRoot + "/" + ItemName;



        protected override void AnimateOpen(IView view, float timeFactor) => Animate(view, timeFactor, Vector3.zero, Vector3.one, _openCurve);

        protected override void AnimateClose(IView view, float timeFactor) => Animate(view, 1.0f - timeFactor, Vector3.one, Vector3.zero, _closeCurve);

        private void Animate(IView view, float timeFactor, Vector3 startScale, Vector3 endScale, AnimationCurve curve)
        {
            view.LocalScale = Vector3.Lerp(startScale, endScale, curve.Evaluate(timeFactor));
        }
    }
}
