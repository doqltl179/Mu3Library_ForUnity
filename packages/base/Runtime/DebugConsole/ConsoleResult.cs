namespace Mu3Library.DebugConsole
{
    public enum ConsoleFailureReason
    {
        None = 0,

        /// <summary>The line holds no command.</summary>
        Empty,
        /// <summary>No command of that name is registered.</summary>
        UnknownCommand,
        /// <summary>The command exists but this build or setting does not let it run.</summary>
        AccessDenied,
        /// <summary>Too few or too many arguments, or one that does not parse as its type.</summary>
        InvalidArguments,
        /// <summary>The command's own code threw.</summary>
        CommandFailed,
        /// <summary>The line's quotes are not closed.</summary>
        SyntaxError,
    }

    /// <summary>
    /// How running one line ended, with a message that says why when it failed.
    /// </summary>
    public readonly struct ConsoleResult
    {
        public ConsoleFailureReason Reason { get; }
        public string CommandName { get; }
        public string Message { get; }
        public bool IsSuccess => Reason == ConsoleFailureReason.None;



        public ConsoleResult(ConsoleFailureReason reason, string commandName, string message)
        {
            Reason = reason;
            CommandName = commandName;
            Message = message;
        }

        public static ConsoleResult Success(string commandName) => new ConsoleResult(ConsoleFailureReason.None, commandName, null);

        public static ConsoleResult Failure(ConsoleFailureReason reason, string commandName, string message) =>
            new ConsoleResult(reason, commandName, message);

        public override string ToString() => IsSuccess ? "Success" : $"{Reason}: {Message}";
    }
}
