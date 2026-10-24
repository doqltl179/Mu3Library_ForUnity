using UnityEngine;
#if TEMPLATE_INPUTSYSTEM_SUPPORT && ENABLE_INPUT_SYSTEM
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
#endif

namespace Mu3Library.Sample.Template.Global
{
    /// <summary>
    /// Reads the keyboard and mouse for the sample scripts through the input backend the project enables.
    /// With the Input System package installed and its backend on, under the Active Input Handling settings
    /// "Input System Package (New)" and "Both", it reads the Input System. Otherwise it reads the Input
    /// Manager, which throws on every call while its own backend is off.
    /// </summary>
    public static class SampleInput
    {
#if TEMPLATE_INPUTSYSTEM_SUPPORT && ENABLE_INPUT_SYSTEM
        private static readonly Dictionary<int, Key> _keys = new();
#endif

        /// <summary>
        /// Whether the key went down this frame.
        /// </summary>
        public static bool GetKeyDown(KeyCode keyCode)
        {
#if TEMPLATE_INPUTSYSTEM_SUPPORT && ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            Key key = ToKey(keyCode);

            return keyboard != null && key != Key.None && keyboard[key].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(keyCode);
#else
            return false;
#endif
        }

        /// <summary>
        /// Whether the mouse button went down this frame: 0 is the left button, 1 the right, 2 the middle.
        /// </summary>
        public static bool GetMouseButtonDown(int button)
        {
#if TEMPLATE_INPUTSYSTEM_SUPPORT && ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            switch (button)
            {
                case 0: return mouse.leftButton.wasPressedThisFrame;
                case 1: return mouse.rightButton.wasPressedThisFrame;
                case 2: return mouse.middleButton.wasPressedThisFrame;
                default: return false;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(button);
#else
            return false;
#endif
        }

        /// <summary>
        /// The mouse position in screen pixels, as <c>Input.mousePosition</c> reports it.
        /// </summary>
        public static Vector3 MousePosition
        {
            get
            {
#if TEMPLATE_INPUTSYSTEM_SUPPORT && ENABLE_INPUT_SYSTEM
                Mouse mouse = Mouse.current;

                return mouse != null ? (Vector3)mouse.position.ReadValue() : Vector3.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.mousePosition;
#else
                return Vector3.zero;
#endif
            }
        }

#if TEMPLATE_INPUTSYSTEM_SUPPORT && ENABLE_INPUT_SYSTEM
        private static Key ToKey(KeyCode keyCode)
        {
            if (!_keys.TryGetValue((int)keyCode, out Key key))
            {
                key = FindKey(keyCode);
                if (key == Key.None && keyCode != KeyCode.None)
                {
                    Debug.LogWarning($"The Input System has no keyboard key for {keyCode}.");
                }

                _keys.Add((int)keyCode, key);
            }

            return key;
        }

        // KeyCode and Key share most names, such as A, Space, Escape, F1, or LeftArrow. The number row,
        // the keypad, Return, and the Control keys are named differently.
        private static Key FindKey(KeyCode keyCode)
        {
            string name;
            if (keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9)
            {
                name = "Digit" + (keyCode - KeyCode.Alpha0);
            }
            else
            {
                switch (keyCode)
                {
                    case KeyCode.Return: name = nameof(Key.Enter); break;
                    case KeyCode.LeftControl: name = nameof(Key.LeftCtrl); break;
                    case KeyCode.RightControl: name = nameof(Key.RightCtrl); break;
                    default:
                        name = keyCode.ToString();
                        if (name.StartsWith("Keypad", StringComparison.Ordinal))
                        {
                            name = "Numpad" + name.Substring("Keypad".Length);
                        }
                        break;
                }
            }

            return Enum.TryParse(name, true, out Key key) ? key : Key.None;
        }
#endif
    }
}
