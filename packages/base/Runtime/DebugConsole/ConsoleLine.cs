namespace Mu3Library.DebugConsole
{
    public enum ConsoleLineKind
    {
        /// <summary>A line the console ran, as typed.</summary>
        Input,
        /// <summary>What a command printed.</summary>
        Output,
        /// <summary>A command that failed, or what it printed as an error.</summary>
        Error,
        /// <summary>A Unity log message.</summary>
        Log,
        /// <summary>A Unity warning.</summary>
        Warning,
        /// <summary>A Unity error, assertion, or exception.</summary>
        LogError,
    }

    public readonly struct ConsoleLine
    {
        public ConsoleLineKind Kind { get; }
        public string Text { get; }

        /// <summary>
        /// <see cref="UnityEngine.Time.realtimeSinceStartup"/> when the line was added.
        /// </summary>
        public float Time { get; }



        public ConsoleLine(ConsoleLineKind kind, string text, float time)
        {
            Kind = kind;
            Text = text;
            Time = time;
        }
    }
}
