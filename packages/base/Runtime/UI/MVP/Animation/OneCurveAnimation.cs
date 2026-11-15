using UnityEngine;

namespace Mu3Library.UI.MVP.Animation
{
    public abstract class OneCurveAnimation : AnimationConfig
    {
        [SerializeField] protected AnimationCurve _curve;



        public override AnimateFunc AnimateOpen() => Animate;
        public override AnimateFunc AnimateClose() => Animate;

        protected abstract void Animate(IView view, float timeFactor);
    }
}