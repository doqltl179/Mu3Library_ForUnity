using Mu3Library.UI.MVP;
using Mu3Library.UI.MVP.Animation;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Fades a view in and out through its alpha, so a test reads how far an animation went.
    /// It sits in its own file because Unity looks a ScriptableObject's script up by the class name.
    /// </summary>
    public sealed class TestAlphaAnimation : AnimationConfig
    {
        public override AnimateFunc AnimateOpen() => Animate;
        public override AnimateFunc AnimateClose() => Animate;

        private static void Animate(IView view, float timeFactor)
        {
            view.Alpha = timeFactor;
        }
    }
}
