using Mu3Library.UI.MVP;
using Mu3Library.UI.MVP.Animation;
using UnityEngine;

namespace Mu3Library.Sample.Template.MVP
{
    [CreateAssetMenu(fileName = FileName, menuName = MenuName, order = 0)]
    public class BottomToMiddleAnimation : AnimationConfig
    {
        public const string FileName = "BottomToMiddleAnimation";
        private const string ItemName = "Bottom To Middle Animation";
        private const string MenuName = MenuRoot + "/" + ItemName;

        [SerializeField] private AnimationCurve _curve;



        public override AnimateFunc AnimateOpen() => Animate;
        public override AnimateFunc AnimateClose() => Animate;

        private void Animate(IView view, float timeFactor)
        {
            view.AnchoredPosition = Vector3.LerpUnclamped(
                -Vector3.up * Screen.height, Vector3.one, _curve.Evaluate(timeFactor));
        }
    }
}
