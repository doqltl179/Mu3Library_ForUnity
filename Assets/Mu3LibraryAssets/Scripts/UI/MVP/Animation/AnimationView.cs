using System.Collections;

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

            _animationHandler.Open();
        }

        protected override bool WaitOpeningUntil()
        {
            return _animationHandler.State == AnimationState.Opened;
        }

        protected override void CloseStart()
        {
            base.CloseStart();

            _animationHandler.Close();
        }

        protected override bool WaitClosingUntil()
        {
            return _animationHandler.State == AnimationState.Closed;
        }
    }
}