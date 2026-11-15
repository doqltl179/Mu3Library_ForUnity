using Mu3Library.Editor.Window;

namespace Mu3Library.Tests
{
    /// <summary>
    /// A <see cref="Mu3Window"/> whose script ships in the Base package, so it finds its drawer list the way a window
    /// of an installed package does.
    /// </summary>
    public class Mu3WindowTestWindow : Mu3Window
    {
        public void SimulateBecameVisible()
        {
            OnBecameVisible();
        }
    }
}
