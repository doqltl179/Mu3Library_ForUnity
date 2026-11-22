using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Mu3Library.DebugConsole
{
    /// <summary>
    /// The console's commands, lines, and access rules. See <see cref="IDebugConsoleManager"/>.
    /// Construct and use it on the main thread. Dispose it to stop capturing Unity's log.
    /// </summary>
    public class DebugConsoleManager : IDebugConsoleManager, IDebugConsoleManagerEventBus, IDisposable
    {
        public const int MaxCommandNameLength = 64;
        public const int DefaultMaxLines = 1000;
        public const int MaxHistory = 50;

        private sealed class Entry
        {
            public ConsoleCommand Command;
            public Action<object[], ConsoleCall> Invoke;
        }

        private readonly Dictionary<string, Entry> _commands = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ConsoleLine> _lines = new();
        private readonly List<string> _history = new();
        private readonly List<string> _tokens = new();
        private readonly bool _capturesUnityLog;
        private bool _isDisposed;

        public bool IsDevelopmentBuild { get; }
        private bool _playerConsoleEnabled;
        public bool PlayerConsoleEnabled
        {
            get => _playerConsoleEnabled;
            set
            {
                _playerConsoleEnabled = value;
                if (!CanOpen)
                {
                    Close();
                }
            }
        }
        public bool CanOpen => IsDevelopmentBuild || PlayerConsoleEnabled;
        public bool IsOpen { get; private set; }
        public bool HasModifiedProgress { get; private set; }
        public IReadOnlyList<ConsoleLine> Lines => _lines;
        public IReadOnlyList<string> History => _history;

        private int _maxLines = DefaultMaxLines;
        public int MaxLines
        {
            get => _maxLines;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The console keeps at least one line.");
                }

                _maxLines = value;
                TrimLines();
            }
        }

        public event Action<ConsoleLine> OnLineAdded;
        public event Action OnCleared;
        public event Action<bool> OnOpenChanged;
        public event Action<string> OnProgressModified;
        public event Action<string, ConsoleResult> OnExecuted;



        /// <summary>
        /// Creates the console with the <c>help</c> and <c>clear</c> commands.
        /// </summary>
        /// <param name="captureUnityLog">Whether to add Unity's log messages to <see cref="Lines"/>.</param>
        /// <param name="isDevelopmentBuild">Whether every command runs; by default, whether this is the
        /// editor or a Development Build.</param>
        public DebugConsoleManager(bool captureUnityLog = true, bool? isDevelopmentBuild = null)
        {
            IsDevelopmentBuild = isDevelopmentBuild ?? (Application.isEditor || Debug.isDebugBuild);
            _capturesUnityLog = captureUnityLog;
            if (_capturesUnityLog)
            {
                Application.logMessageReceived += HandleUnityLog;
            }

            RegisterCommand("help", HelpCommand, new ConsoleCommandOptions("Lists the commands, or shows one command's usage.", ConsoleAccess.Player));
            RegisterCommand("clear", Clear, new ConsoleCommandOptions("Clears the console.", ConsoleAccess.Player));
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            if (_capturesUnityLog)
            {
                Application.logMessageReceived -= HandleUnityLog;
            }

            Close();
        }

        #region Open
        public bool Open()
        {
            if (!CanOpen)
            {
                return false;
            }

            if (!IsOpen)
            {
                IsOpen = true;
                OnOpenChanged?.Invoke(true);
            }

            return true;
        }

        public void Close()
        {
            if (IsOpen)
            {
                IsOpen = false;
                OnOpenChanged?.Invoke(false);
            }
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }
        #endregion

        #region Registration
        public void RegisterCommand(string name, Action handler, ConsoleCommandOptions options = null) =>
            Register(name, handler, options, (_, _) => handler());

        public void RegisterCommand<T1>(string name, Action<T1> handler, ConsoleCommandOptions options = null) =>
            Register(name, handler, options, (args, _) => handler((T1)args[0]));

        public void RegisterCommand<T1, T2>(string name, Action<T1, T2> handler, ConsoleCommandOptions options = null) =>
            Register(name, handler, options, (args, _) => handler((T1)args[0], (T2)args[1]));

        public void RegisterCommand<T1, T2, T3>(string name, Action<T1, T2, T3> handler, ConsoleCommandOptions options = null) =>
            Register(name, handler, options, (args, _) => handler((T1)args[0], (T2)args[1], (T3)args[2]));

        public void RegisterCommand<T1, T2, T3, T4>(string name, Action<T1, T2, T3, T4> handler, ConsoleCommandOptions options = null) =>
            Register(name, handler, options, (args, _) => handler((T1)args[0], (T2)args[1], (T3)args[2], (T4)args[3]));

        public void RegisterCommand(string name, Action<ConsoleCall> handler, ConsoleCommandOptions options = null)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            ThrowIfInvalidName(name);
            var command = new ConsoleCommand(name, options ?? new ConsoleCommandOptions(), Array.Empty<ConsoleParameter>(), takesRawArguments: true);
            _commands.Add(name, new Entry { Command = command, Invoke = (_, call) => handler(call) });
        }

        public bool UnregisterCommand(string name) => name != null && _commands.Remove(name);

        public bool TryGetCommand(string name, out ConsoleCommand command)
        {
            if (name != null && _commands.TryGetValue(name, out Entry entry))
            {
                command = entry.Command;
                return true;
            }

            command = null;
            return false;
        }

        public void GetCommands(List<ConsoleCommand> results, bool runnableOnly = true)
        {
            var sorted = new List<ConsoleCommand>(_commands.Count);
            foreach (Entry entry in _commands.Values)
            {
                if (!runnableOnly || CanRun(entry.Command))
                {
                    sorted.Add(entry.Command);
                }
            }

            sorted.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            results.AddRange(sorted);
        }

        public void GetCompletions(string prefix, List<string> results)
        {
            var commands = new List<ConsoleCommand>();
            GetCommands(commands);
            foreach (ConsoleCommand command in commands)
            {
                if (string.IsNullOrEmpty(prefix) || command.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(command.Name);
                }
            }
        }
        #endregion

        #region Execute
        public ConsoleResult Execute(string line)
        {
            line = line?.Trim() ?? string.Empty;
            if (line.Length == 0)
            {
                return Finish(null, ConsoleResult.Failure(ConsoleFailureReason.Empty, null, "Type a command, or help."));
            }

            AddLine(ConsoleLineKind.Input, "> " + line);
            AddHistory(line);
            if (!ConsoleArgumentParser.TryTokenize(line, _tokens))
            {
                return Finish(null, ConsoleResult.Failure(ConsoleFailureReason.SyntaxError, null, "A quote is not closed."));
            }

            string name = _tokens[0];
            if (!_commands.TryGetValue(name, out Entry entry))
            {
                return Finish(null, ConsoleResult.Failure(ConsoleFailureReason.UnknownCommand, name, $"Unknown command '{name}'. Type help for the list."));
            }

            ConsoleCommand command = entry.Command;
            if (!IsDevelopmentBuild && command.Access == ConsoleAccess.Developer)
            {
                // A release build lists, completes, and answers only what it can run.
                return Finish(null, ConsoleResult.Failure(ConsoleFailureReason.UnknownCommand, name, $"Unknown command '{name}'. Type help for the list."));
            }

            // What is left is a player command while the player console is off.
            if (!CanRun(command))
            {
                return Finish(command.Name, ConsoleResult.Failure(ConsoleFailureReason.AccessDenied, command.Name,
                    $"'{command.Name}' runs once the game turns on the player console."));
            }

            var words = new string[_tokens.Count - 1];
            _tokens.CopyTo(1, words, 0, words.Length);
            object[] arguments = null;
            if (!command.TakesRawArguments)
            {
                ConsoleResult bound = Bind(command, words, out arguments);
                if (!bound.IsSuccess)
                {
                    return Finish(command.Name, bound);
                }
            }

            Exception failure = null;
            try
            {
                entry.Invoke(arguments, new ConsoleCall(this, command.Name, words));
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            // A command that ran may have changed the progress before it threw, so it counts.
            if (command.AffectsProgress)
            {
                HasModifiedProgress = true;
                OnProgressModified?.Invoke(command.Name);
            }

            if (failure != null)
            {
                Debug.LogException(failure);
                return Finish(command.Name, ConsoleResult.Failure(ConsoleFailureReason.CommandFailed, command.Name,
                    $"'{command.Name}' failed: {failure.GetType().Name}: {failure.Message}"));
            }

            return Finish(command.Name, ConsoleResult.Success(command.Name));
        }

        public void Print(string message) => AddLine(ConsoleLineKind.Output, message ?? string.Empty);
        public void PrintError(string message) => AddLine(ConsoleLineKind.Error, message ?? string.Empty);

        public void Clear()
        {
            _lines.Clear();
            OnCleared?.Invoke();
        }

        public void ResetProgressModified() => HasModifiedProgress = false;
        #endregion

        #region Helpers
        private bool CanRun(ConsoleCommand command) =>
            IsDevelopmentBuild || (command.Access == ConsoleAccess.Player && PlayerConsoleEnabled);

        private ConsoleResult Finish(string commandName, ConsoleResult result)
        {
            if (!result.IsSuccess && result.Reason != ConsoleFailureReason.Empty)
            {
                AddLine(ConsoleLineKind.Error, result.Message);
            }

            OnExecuted?.Invoke(commandName, result);
            return result;
        }

        private void Register(string name, Delegate handler, ConsoleCommandOptions options, Action<object[], ConsoleCall> invoke)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            ThrowIfInvalidName(name);
            ParameterInfo[] infos = handler.Method.GetParameters();
            // A delegate closed over its first argument, such as an extension method's, lists that
            // argument among the method's parameters although a call never passes it.
            int skip = infos.Length - GetArity(handler.GetType());
            var parameters = new ConsoleParameter[infos.Length - skip];
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo info = infos[i + skip];
                if (!ConsoleArgumentParser.IsSupported(info.ParameterType))
                {
                    throw new ArgumentException(
                        $"Command '{name}' takes a {info.ParameterType.Name}, which the console cannot read. Use string, int, long, float, double, bool, an enum, Vector2, or Vector3.",
                        nameof(handler));
                }

                string parameterName = string.IsNullOrEmpty(info.Name) ? $"arg{i + 1}" : info.Name;
                object defaultValue = info.HasDefaultValue ? GetDefaultValue(info) : null;
                parameters[i] = new ConsoleParameter(parameterName, info.ParameterType, info.HasDefaultValue, defaultValue);
            }

            var command = new ConsoleCommand(name, options ?? new ConsoleCommandOptions(), parameters, takesRawArguments: false);
            _commands.Add(name, new Entry { Command = command, Invoke = invoke });
        }

        // A struct parameter can only default to default, which reflection reports as null, and an
        // enum's default can come back as its underlying number; both are turned into the type itself.
        private static object GetDefaultValue(ParameterInfo info)
        {
            Type type = info.ParameterType;
            object value = info.DefaultValue;
            if (value == null || value is DBNull || value == Type.Missing)
            {
                return type.IsValueType ? Activator.CreateInstance(type) : null;
            }

            if (type.IsEnum && value.GetType() != type)
            {
                return Enum.ToObject(type, value);
            }

            return value;
        }

        private static int GetArity(Type delegateType) =>
            delegateType.IsGenericType ? delegateType.GetGenericArguments().Length : 0;

        private void ThrowIfInvalidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > MaxCommandNameLength)
            {
                throw new ArgumentException($"A command name is 1 to {MaxCommandNameLength} characters.", nameof(name));
            }

            foreach (char c in name)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.';
                if (!ok)
                {
                    throw new ArgumentException($"Command name '{name}' holds '{c}'; use letters, digits, _, -, and . only.", nameof(name));
                }
            }

            if (_commands.ContainsKey(name))
            {
                throw new ArgumentException($"A command named '{name}' is already registered.", nameof(name));
            }
        }

        private static ConsoleResult Bind(ConsoleCommand command, string[] words, out object[] arguments)
        {
            arguments = null;
            IReadOnlyList<ConsoleParameter> parameters = command.Parameters;
            int required = 0;
            foreach (ConsoleParameter parameter in parameters)
            {
                if (!parameter.HasDefaultValue)
                {
                    required++;
                }
            }

            if (words.Length < required || words.Length > parameters.Count)
            {
                return ConsoleResult.Failure(ConsoleFailureReason.InvalidArguments, command.Name, $"Usage: {command.Usage}");
            }

            var values = new object[parameters.Count];
            for (int i = 0; i < parameters.Count; i++)
            {
                ConsoleParameter parameter = parameters[i];
                if (i >= words.Length)
                {
                    values[i] = parameter.DefaultValue;
                    continue;
                }

                if (!ConsoleArgumentParser.TryParse(words[i], parameter.Type, out values[i]))
                {
                    return ConsoleResult.Failure(ConsoleFailureReason.InvalidArguments, command.Name,
                        $"'{words[i]}' is not a {ConsoleArgumentParser.GetTypeName(parameter.Type)} for {parameter.Name}. Usage: {command.Usage}");
                }
            }

            arguments = values;
            return ConsoleResult.Success(command.Name);
        }

        private void HelpCommand(ConsoleCall call)
        {
            if (call.Arguments.Count > 0)
            {
                if (TryGetCommand(call.Arguments[0], out ConsoleCommand command) && CanRun(command))
                {
                    Print(string.IsNullOrEmpty(command.Description) ? command.Usage : $"{command.Usage}\n  {command.Description}");
                }
                else
                {
                    PrintError($"Unknown command '{call.Arguments[0]}'.");
                }

                return;
            }

            var commands = new List<ConsoleCommand>();
            GetCommands(commands);
            foreach (ConsoleCommand command in commands)
            {
                Print(string.IsNullOrEmpty(command.Description) ? command.Usage : $"{command.Usage} - {command.Description}");
            }
        }

        private void HandleUnityLog(string condition, string stackTrace, LogType type)
        {
            ConsoleLineKind kind = type switch
            {
                LogType.Warning => ConsoleLineKind.Warning,
                LogType.Log => ConsoleLineKind.Log,
                _ => ConsoleLineKind.LogError,
            };

            AddLine(kind, kind == ConsoleLineKind.LogError && !string.IsNullOrEmpty(stackTrace)
                ? $"{condition}\n{stackTrace.TrimEnd()}"
                : condition);
        }

        private void AddLine(ConsoleLineKind kind, string text)
        {
            var line = new ConsoleLine(kind, text, Time.realtimeSinceStartup);
            _lines.Add(line);
            TrimLines();
            OnLineAdded?.Invoke(line);
        }

        private void AddHistory(string line)
        {
            if (_history.Count > 0 && _history[_history.Count - 1] == line)
            {
                return;
            }

            _history.Add(line);
            if (_history.Count > MaxHistory)
            {
                _history.RemoveAt(0);
            }
        }

        private void TrimLines()
        {
            int excess = _lines.Count - _maxLines;
            if (excess > 0)
            {
                _lines.RemoveRange(0, excess);
            }
        }
        #endregion
    }
}
