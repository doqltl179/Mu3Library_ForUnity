using UnityEngine;

namespace Mu3Library.UI.MVP.Animation
{
    /// <summary>
    /// <br/> view: view
    /// <br/> timeFactor: time factor
    /// </summary>
    public delegate void AnimateFunc(IView view, float timeFactor);

    public abstract class AnimationConfig : ScriptableObject
    {
        protected const string MenuRoot = "Mu3Library/UI/MVP/Animation Config";



        #region Utility
        public abstract AnimateFunc AnimateOpen();
        public abstract AnimateFunc AnimateClose();
        #endregion
    }
}