using System;
using UnityEngine;

namespace Mu3Library.Utility
{
    /// <summary>
    /// Reports a screen that changed its size or its safe area.
    /// <para>
    /// Unity raises no event of its own for <see cref="Screen.safeArea"/>, so the screen is read once
    /// a frame from here instead of from every listener. The read costs the same whether one listener
    /// or a hundred wait on it, and a listener that only needs the resolution can stay on the
    /// <c>OnRectTransformDimensionsChange</c> message Unity already sends it.
    /// </para>
    /// <para>
    /// The screen is followed only while the game runs. Edit mode holds no screen to follow:
    /// <see cref="Screen"/> answers the editor with the view it is drawing, which is a game view on one
    /// tick and a scene view or an inspector on the next, so a read there reports a screen that changed
    /// on every tick and leaves the listeners resizing what they own between two answers every frame.
    /// <see cref="ScreenSize"/> and <see cref="SafeArea"/> stay empty until the game reads the screen,
    /// which is how a listener tells a screen it does not know yet from one it does.
    /// </para>
    /// </summary>
    public static class ScreenChangeNotifier
    {
        /// <summary>
        /// Raised after <see cref="ScreenSize"/> and <see cref="SafeArea"/> took the values that
        /// the read found. A listener that throws is reported and does not stop the others.
        /// </summary>
        public static event Action OnChanged;

        /// <summary>
        /// Screen size the last read found. Empty while the game has not read the screen.
        /// </summary>
        public static Vector2Int ScreenSize => _screenSize;
        private static Vector2Int _screenSize = Vector2Int.zero;

        /// <summary>
        /// Safe area the last read found. Empty while the game has not read the screen.
        /// </summary>
        public static Rect SafeArea => _safeArea;
        private static Rect _safeArea = Rect.zero;



        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            Capture();

            // Subscribing twice is what a domain reload that was turned off would cause.
            Application.onBeforeRender -= Read;
            Application.onBeforeRender += Read;
        }

        private static void Read()
        {
            // The player loop is the only place the screen is read from. A domain reload that was turned
            // off leaves this attached while edit mode comes back, where the screen the editor answers
            // with is the one of the view it happens to be drawing.
            if (!Application.isPlaying)
            {
                return;
            }

            if (_screenSize.x == Screen.width &&
                _screenSize.y == Screen.height &&
                _safeArea == Screen.safeArea)
            {
                return;
            }

            Capture();
            Raise();
        }

        private static void Capture()
        {
            _screenSize = new Vector2Int(Screen.width, Screen.height);
            _safeArea = Screen.safeArea;
        }

        private static void Raise()
        {
            Delegate[] listeners = OnChanged?.GetInvocationList();
            if (listeners == null)
            {
                return;
            }

            for (int i = 0; i < listeners.Length; i++)
            {
                try
                {
                    ((Action)listeners[i]).Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
