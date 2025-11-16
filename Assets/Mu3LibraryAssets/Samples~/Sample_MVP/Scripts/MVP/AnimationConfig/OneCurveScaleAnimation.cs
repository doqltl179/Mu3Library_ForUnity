using Mu3Library.UI.MVP;
using Mu3Library.UI.MVP.Animation;
using UnityEngine;

namespace Mu3Library.Sample.MVP
{
    [CreateAssetMenu(fileName = FileName, menuName = MenuName, order = 0)]
    public class OneCurveScaleAnimation : OneCurveAnimation
    {
        public const string FileName = "OneCurveScaleAnimation";
        private const string ItemName = "One Curve Scale Animation";
        private const string MenuName = MenuRoot + "/" + ItemName;



        public override AnimateFunc AnimateOpen() => Animate;
        public override AnimateFunc AnimateClose() => Animate;

        protected override void Animate(IView view, float timeFactor)
        {
            view.RectTransform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, _curve.Evaluate(timeFactor));
        }
    }
}