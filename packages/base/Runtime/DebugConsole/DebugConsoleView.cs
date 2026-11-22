using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.DebugConsole
{
    /// <summary>
    /// Draws an <see cref="IDebugConsoleManager"/> over the game with IMGUI, which reads the keyboard
    /// under either input backend. The toggle key opens and closes it, Enter runs the line, Up and
    /// Down walk the history, Tab completes a command name, and Escape closes it. On a device
    /// without a keyboard, call <see cref="IDebugConsoleManager.Open"/> from the game's own button or
    /// gesture. Create one with <see cref="Create"/>, or add the component and call
    /// <see cref="Bind"/>.
    /// </summary>
    public class DebugConsoleView : MonoBehaviour
    {
        private const string InputControlName = "Mu3DebugConsoleInput";
        private const int MaxDrawnLines = 300;

        [SerializeField] private KeyCode _toggleKey = KeyCode.BackQuote;
        [Tooltip("Share of the screen height the console covers.")]
        [SerializeField, Range(0.2f, 1f)] private float _heightRatio = 0.5f;
        [Tooltip("Scale of the console's text and controls; 0 follows the screen DPI.")]
        [SerializeField, Min(0f)] private float _scale;
        [SerializeField] private bool _showLogs = true;
        [SerializeField] private bool _showWarnings = true;
        [SerializeField] private bool _showErrors = true;

        private IDebugConsoleManager _console;
        private readonly List<string> _completions = new();
        private string _input = string.Empty;
        private Vector2 _scroll;
        private bool _scrollToEnd;
        private bool _focusInput;
        private int _historyIndex = -1;
        private int _lineCount;
        private GUIStyle _lineStyle;

        public IDebugConsoleManager Console => _console;

        public KeyCode ToggleKey
        {
            get => _toggleKey;
            set => _toggleKey = value;
        }



        /// <summary>
        /// Creates a GameObject with a view that draws <paramref name="console"/>, kept across scene
        /// loads unless <paramref name="dontDestroyOnLoad"/> is false.
        /// </summary>
        public static DebugConsoleView Create(IDebugConsoleManager console, bool dontDestroyOnLoad = true)
        {
            var go = new GameObject("Debug Console");
            if (dontDestroyOnLoad && Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }

            var view = go.AddComponent<DebugConsoleView>();
            view.Bind(console);
            return view;
        }

        public void Bind(IDebugConsoleManager console)
        {
            _console = console;
            _lineCount = -1;
            _scrollToEnd = true;
        }

        private void OnGUI()
        {
            if (_console == null)
            {
                return;
            }

            Event current = Event.current;
            if (current.type == EventType.KeyDown && current.keyCode == _toggleKey && _toggleKey != KeyCode.None)
            {
                _console.Toggle();
                _focusInput = _console.IsOpen;
                current.Use();
                return;
            }

            if (!_console.IsOpen)
            {
                return;
            }

            EnsureStyles();
            float scale = _scale > 0f ? _scale : Mathf.Clamp(Screen.dpi > 0f ? Screen.dpi / 96f : 1f, 1f, 3f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale * _heightRatio;

            HandleInputKeys(current);
            GUILayout.BeginArea(new Rect(0f, 0f, width, height), GUI.skin.box);
            DrawToolbar();
            DrawLines();
            DrawInput();
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }

        private void EnsureStyles()
        {
            if (_lineStyle == null)
            {
                _lineStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false };
            }
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal();
            _showLogs = GUILayout.Toggle(_showLogs, "Log", GUILayout.ExpandWidth(false));
            _showWarnings = GUILayout.Toggle(_showWarnings, "Warning", GUILayout.ExpandWidth(false));
            _showErrors = GUILayout.Toggle(_showErrors, "Error", GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", GUILayout.ExpandWidth(false)))
            {
                _console.Clear();
            }

            if (GUILayout.Button("Close", GUILayout.ExpandWidth(false)))
            {
                _console.Close();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawLines()
        {
            IReadOnlyList<ConsoleLine> lines = _console.Lines;
            if (lines.Count != _lineCount)
            {
                _lineCount = lines.Count;
                _scrollToEnd = true;
            }

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            Color previousColor = GUI.contentColor;
            // IMGUI lays out every label on every event, so draw only the newest shown lines.
            int first = lines.Count;
            for (int shown = 0; first > 0 && shown < MaxDrawnLines; first--)
            {
                if (IsShown(lines[first - 1].Kind))
                {
                    shown++;
                }
            }

            for (int i = first; i < lines.Count; i++)
            {
                ConsoleLine line = lines[i];
                if (!IsShown(line.Kind))
                {
                    continue;
                }

                GUI.contentColor = GetColor(line.Kind);
                GUILayout.Label(line.Text, _lineStyle);
            }

            GUI.contentColor = previousColor;
            GUILayout.EndScrollView();
            if (_scrollToEnd && Event.current.type == EventType.Repaint)
            {
                _scroll.y = float.MaxValue;
                _scrollToEnd = false;
            }
        }

        private void DrawInput()
        {
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName(InputControlName);
            _input = GUILayout.TextField(_input ?? string.Empty, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Run", GUILayout.ExpandWidth(false)))
            {
                Submit();
            }

            GUILayout.EndHorizontal();
            if (_focusInput)
            {
                GUI.FocusControl(InputControlName);
                _focusInput = false;
            }
        }

        private void HandleInputKeys(Event current)
        {
            if (current.type != EventType.KeyDown || GUI.GetNameOfFocusedControl() != InputControlName)
            {
                return;
            }

            switch (current.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    Submit();
                    current.Use();
                    break;
                case KeyCode.Escape:
                    _console.Close();
                    current.Use();
                    break;
                case KeyCode.UpArrow:
                    StepHistory(-1);
                    current.Use();
                    break;
                case KeyCode.DownArrow:
                    StepHistory(1);
                    current.Use();
                    break;
                case KeyCode.Tab:
                    Complete();
                    current.Use();
                    break;
            }
        }

        private void Submit()
        {
            string line = _input;
            _input = string.Empty;
            _historyIndex = -1;
            _focusInput = true;
            if (!string.IsNullOrWhiteSpace(line))
            {
                _console.Execute(line);
            }
        }

        private void StepHistory(int step)
        {
            IReadOnlyList<string> history = _console.History;
            if (history.Count == 0)
            {
                return;
            }

            int index = _historyIndex < 0 ? history.Count : _historyIndex;
            index += step;
            if (index >= history.Count)
            {
                _historyIndex = -1;
                _input = string.Empty;
                return;
            }

            _historyIndex = Mathf.Clamp(index, 0, history.Count - 1);
            _input = history[_historyIndex];
        }

        private void Complete()
        {
            string text = _input ?? string.Empty;
            if (text.Contains(" "))
            {
                return;
            }

            _completions.Clear();
            _console.GetCompletions(text, _completions);
            if (_completions.Count == 1)
            {
                _input = _completions[0] + " ";
            }
            else if (_completions.Count > 1)
            {
                _console.Print(string.Join("  ", _completions));
            }
        }

        private bool IsShown(ConsoleLineKind kind) => kind switch
        {
            ConsoleLineKind.Log => _showLogs,
            ConsoleLineKind.Warning => _showWarnings,
            ConsoleLineKind.LogError => _showErrors,
            _ => true,
        };

        private static Color GetColor(ConsoleLineKind kind) => kind switch
        {
            ConsoleLineKind.Input => new Color(0.6f, 0.85f, 1f),
            ConsoleLineKind.Error => new Color(1f, 0.45f, 0.45f),
            ConsoleLineKind.LogError => new Color(1f, 0.45f, 0.45f),
            ConsoleLineKind.Warning => new Color(1f, 0.85f, 0.4f),
            _ => Color.white,
        };
    }
}
