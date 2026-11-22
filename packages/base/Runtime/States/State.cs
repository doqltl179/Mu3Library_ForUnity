using System.Collections;

namespace Mu3Library.States
{
    /// <summary>
    /// A state that does nothing until it overrides what it needs: <see cref="Enter"/> and
    /// <see cref="Exit"/>, which may <c>yield</c> to wait, and <see cref="Update"/>.
    /// </summary>
    public abstract class State : IState
    {
        public virtual IEnumerator Enter() => null;

        public virtual void Update()
        {
        }

        public virtual IEnumerator Exit() => null;
    }
}
