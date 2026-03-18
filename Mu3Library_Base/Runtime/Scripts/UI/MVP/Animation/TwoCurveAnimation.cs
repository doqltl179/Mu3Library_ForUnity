using UnityEngine;

namespace Mu3Library.UI.MVP.Animation
{
    public abstract class TwoCurveAnimation : AnimationConfig
    {
        [SerializeField] protected AnimationCurve _openCurve;
        [SerializeField] protected AnimationCurve _closeCurve;



        public override AnimateFunc AnimateOpen() => AnimateOpen;
        public override AnimateFunc AnimateClose() => AnimateClose;

        protected abstract void AnimateOpen(IView view, float timeFactor);
        protected abstract void AnimateClose(IView view, float timeFactor);
    }
}