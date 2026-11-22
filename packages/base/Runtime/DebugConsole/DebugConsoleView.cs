using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.DebugConsole
{
    /// <summary>
    /// Draws an <see cref="IDebugConsoleManager"/> over the game with IMGUI, which reads the keyboard
    /// under either input backend. The toggle key opens and closes it, Enter runs the line, Up and
    /// Down walk the history, Tab completes a command name, and Escape closes it. On a device
    /// without a keyboard, call <see cref="IDebugConsoleManager.Open"/> from the game's own button or
    /// gesture. The keys also reach the game's own input, so pause it while the console is open,
    /// such as from <see cref="IDebugConsoleManagerEventBus.OnOpenChanged"/>. Create one with
    /// <see cref="Create"/>, or add the component and call <see cref="Bind"/>.
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
        private readonly List<int> _drawn = new();
        private readonly List<Action> _pending = new();
        private string _input = string.Empty;
        private Vector2 _scroll;
        private bool _scrollToEnd;
        private bool _focusInput;
        private bool _moveCursorToEnd;
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

        // IMGUI calls OnGUI once to lay out each event and once more to handle it, and both calls must
        // draw the same controls. So everything that changes what the console shows, such as running
        // a line or switching a filter, waits in _pending until the call that handles the event ends,
        // and the lines drawn are picked during the layout call.
        private void OnGUI()
        {
            if (_console == null)
            {
                return;
            }

            Event current = Event.current;
            HandleKeys(current);
            if (_console.IsOpen)
            {
                Draw(current);
            }

            if (current.type != EventType.Layout && _pending.Count > 0)
            {
                Action[] actions = _pending.ToArray();
                _pending.Clear();
                foreach (Action action in actions)
                {
                    action();
                }
            }
        }

        private void HandleKeys(Event current)
        {
            if (current.type != EventType.KeyDown)
            {
                return;
            }

            if (_toggleKey != KeyCode.None && current.keyCode == _toggleKey)
            {
                _pending.Add(() =>
                {
                    _console.Toggle();
                    _focusInput = _console.IsOpen;
                });
                current.Use();
                return;
            }

            if (!_console.IsOpen)
            {
                return;
            }

            // The toggle key's character arrives as an event of its own; keep it out of the line.
            if (current.keyCode == KeyCode.None && IsToggleCharacter(current.character))
            {
                current.Use();
                return;
            }

            // The line is the console's only text control, so its keys need no focus check.
            switch (current.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    _pending.Add(Submit);
                    current.Use();
                    break;
                case KeyCode.Escape:
                    _pending.Add(_console.Close);
                    current.Use();
                    break;
                case KeyCode.UpArrow:
                    _pending.Add(() => StepHistory(-1));
                    current.Use();
                    break;
                case KeyCode.DownArrow:
                    _pending.Add(() => StepHistory(1));
                    current.Use();
                    break;
                case KeyCode.Tab:
                    _pending.Add(Complete);
                    current.Use();
                    break;
            }
        }

        private bool IsToggleCharacter(char character) =>
            (int)_toggleKey >= 32 && (int)_toggleKey < 127 && character == (char)(int)_toggleKey;

        private void Draw(Event current)
        {
            if (_lineStyle == null)
            {
                _lineStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false };
            }

            float scale = _scale > 0f ? _scale : Mathf.Clamp(Screen.dpi > 0f ? Screen.dpi / 96f : 1f, 1f, 3f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale * _heightRatio;

            GUILayout.BeginArea(new Rect(0f, 0f, width, height), GUI.skin.box);
            DrawToolbar();
            DrawLines(current);
            DrawInput();
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal();
            bool showLogs = GUILayout.Toggle(_showLogs, "Log", GUILayout.ExpandWidth(false));
            bool showWarnings = GUILayout.Toggle(_showWarnings, "Warning", GUILayout.ExpandWidth(false));
            bool showErrors = GUILayout.Toggle(_showErrors, "Error", GUILayout.ExpandWidth(false));
            if (showLogs != _showLogs || showWarnings != _showWarnings || showErrors != _showErrors)
            {
                _pending.Add(() =>
                {
                    _showLogs = showLogs;
                    _showWarnings = showWarnings;
                    _showErrors = showErrors;
                    _scrollToEnd = true;
                });
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", GUILayout.ExpandWidth(false)))
            {
                _pending.Add(_console.Clear);
            }

            if (GUILayout.Button("Close", GUILayout.ExpandWidth(false)))
            {
                _pending.Add(_console.Close);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawLines(Event current)
        {
            IReadOnlyList<ConsoleLine> lines = _console.Lines;
            if (current.type == EventType.Layout)
            {
                PickLines(lines);
            }

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            Color previousColor = GUI.contentColor;
            foreach (int index in _drawn)
            {
                if (index >= lines.Count)
                {
                    break;
                }

                ConsoleLine line = lines[index];
                GUI.contentColor = GetColor(line.Kind);
                GUILayout.Label(line.Text, _lineStyle);
            }

            GUI.contentColor = previousColor;
            GUILayout.EndScrollView();
            if (_scrollToEnd && current.type == EventType.Repaint)
            {
                _scroll.y = float.MaxValue;
                _scrollToEnd = false;
            }
        }

        // The newest shown lines, at most MaxDrawnLines, since IMGUI lays out every label on every event.
        private void PickLines(IReadOnlyList<ConsoleLine> lines)
        {
            if (lines.Count != _lineCount)
            {
                _lineCount = lines.Count;
                _scrollToEnd = true;
            }

            _drawn.Clear();
            for (int i = lines.Count - 1; i >= 0 && _drawn.Count < MaxDrawnLines; i--)
            {
                if (IsShown(lines[i].Kind))
                {
                    _drawn.Add(i);
                }
            }

            _drawn.Reverse();
        }

        private void DrawInput()
        {
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName(InputControlName);
            _input = GUILayout.TextField(_input ?? string.Empty, GUILayout.ExpandWidth(true));
            if (_moveCursorToEnd && GUI.GetNameOfFocusedControl() == InputControlName)
            {
                var editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                editor.text = _input;
                editor.MoveTextEnd();
                _moveCursorToEnd = false;
            }

            if (GUILayout.Button("Run", GUILayout.ExpandWidth(false)))
            {
                _pending.Add(Submit);
            }

            GUILayout.EndHorizontal();
            if (_focusInput)
            {
                GUI.FocusControl(InputControlName);
                _focusInput = false;
                _moveCursorToEnd = true;
            }
        }

        private void Submit()
        {
            string line = _input;
            SetInput(string.Empty);
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

            int index = (_historyIndex < 0 ? history.Count : _historyIndex) + step;
            if (index >= history.Count)
            {
                _historyIndex = -1;
                SetInput(string.Empty);
                return;
            }

            _historyIndex = Mathf.Clamp(index, 0, history.Count - 1);
            SetInput(history[_historyIndex]);
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
                SetInput(_completions[0] + " ");
            }
            else if (_completions.Count > 1)
            {
                _console.Print(string.Join("  ", _completions));
            }
        }

        // A text field keeps its cursor where it was, so a line set from code moves it to the end.
        private void SetInput(string text)
        {
            _input = text;
            _moveCursorToEnd = true;
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
