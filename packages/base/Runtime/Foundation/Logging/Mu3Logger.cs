using System;

namespace Mu3Library.Foundation.Logging
{
    /// <summary>
    /// The logger the Foundation layer speaks through. It stays silent until a host registers
    /// a sink, which the Unity side of this package does on startup, so pure C# code here can
    /// log without ever referencing the engine.
    /// </summary>
    public static class Mu3Logger
    {
        private sealed class SilentLogger : IMu3Logger
        {
            public void Log(string message) { }
            public void LogWarning(string message) { }
            public void LogError(string message) { }
            public void LogException(Exception exception) { }
        }

        private static readonly IMu3Logger _silent = new SilentLogger();

        private static IMu3Logger _current;
        /// <summary>
        /// The active logger. Never null: without a registered one, a silent logger answers.
        /// </summary>
        public static IMu3Logger Current => _current ?? _silent;

        /// <summary>
        /// Registers the logger the Foundation layer writes to. Null takes it back to silent.
        /// </summary>
        public static void SetLogger(IMu3Logger logger)
        {
            _current = logger;
        }
    }
}
