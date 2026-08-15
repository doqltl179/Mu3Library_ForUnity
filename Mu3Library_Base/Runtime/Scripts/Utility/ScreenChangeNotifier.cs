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
    /// </summary>
    public static class ScreenChangeNotifier
    {
        /// <summary>
        /// Raised after <see cref="ScreenSize"/> and <see cref="SafeArea"/> took the values that
        /// the read found. A listener that throws is reported and does not stop the others.
        /// </summary>
        public static event Action OnChanged;

        /// <summary>
        /// Screen size the last read found.
        /// </summary>
        public static Vector2Int ScreenSize => _screenSize;
        private static Vector2Int _screenSize = Vector2Int.zero;

        /// <summary>
        /// Safe area the last read found.
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

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InitializeInEditor()
        {
            Capture();

            // Edit mode runs no player loop, so the editor loop is what reads the screen there.
            UnityEditor.EditorApplication.update -= Read;
            UnityEditor.EditorApplication.update += Read;
        }
#endif

        private static void Read()
        {
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
