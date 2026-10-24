namespace Mu3Library.UI.MVP.Animation
{
    public enum AnimationState
    {
        None = 0,

        Opening = 1,
        Opened = 2,
        Closing = 3,
        Closed = 4,
    }

    [UnityEngine.RequireComponent(typeof(AnimationHandler))]
    public abstract class AnimationView : View
    {
        private AnimationHandler m_animationHandler;
        protected AnimationHandler _animationHandler
        {
            get
            {
                if (m_animationHandler == null)
                {
                    m_animationHandler = GetComponent<AnimationHandler>();
                }

                return m_animationHandler;
            }
        }



        protected override void OpenStart()
        {
            base.OpenStart();

            if (_animationHandler != null)
            {
                _animationHandler.Open();
            }
        }

        protected override bool WaitOpeningUntil()
        {
            if (_animationHandler != null)
            {
                return _animationHandler.State == AnimationState.Opened;
            }
            else
            {
                return true;
            }
        }

        protected override void CloseStart()
        {
            base.CloseStart();

            if (_animationHandler != null)
            {
                _animationHandler.Close();
            }
        }

        internal override void SkipCloseTransition()
        {
            base.SkipCloseTransition();

            // The handler is inactive as well, so it takes its closed pose at once. Without it, a view
            // opened and closed under a hidden host would keep its opened pose and skip the next open.
            if (_animationHandler != null)
            {
                _animationHandler.Close();
            }
        }

        protected override bool WaitClosingUntil()
        {
            if (_animationHandler != null)
            {
                return _animationHandler.State == AnimationState.Closed;
            }
            else
            {
                return true;
            }
        }
    }
}