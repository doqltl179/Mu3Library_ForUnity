using System;
using System.Collections.Generic;
using System.Globalization;

namespace Mu3Library.Sample.Multiplayer
{
    public enum SampleAutoRole
    {
        /// <summary>No automatic run: the buttons drive the sample.</summary>
        None = 0,
        Host = 1,
        Guest = 2,
    }

    /// <summary>
    /// The options the sample reads from the command line, so that an editor and a player build,
    /// or two player builds, can run the whole flow unattended. Without them the sample waits for
    /// its buttons.
    /// <list type="bullet">
    /// <item><c>-mu3mp-host &lt;lobby name&gt;</c> hosts a public lobby of that name, waits for a
    /// guest to get ready, starts the game, exchanges a message with the guest, and kicks it.</item>
    /// <item><c>-mu3mp-join &lt;lobby name&gt;</c> searches for that lobby, joins it by its lobby
    /// ID, gets ready, answers the host's message, and waits to be kicked.</item>
    /// <item><c>-mu3mp-local</c> uses the machine-wide local backend even where Steam would start.</item>
    /// <item><c>-mu3mp-name &lt;name&gt;</c> names this player on the local backend.</item>
    /// <item><c>-mu3mp-timeout &lt;seconds&gt;</c> fails the automatic run after that long (120 by default).</item>
    /// </list>
    /// </summary>
    public sealed class SampleCommandLine
    {
        public const string HostOption = "-mu3mp-host";
        public const string JoinOption = "-mu3mp-join";
        public const string LocalOption = "-mu3mp-local";
        public const string NameOption = "-mu3mp-name";
        public const string TimeoutOption = "-mu3mp-timeout";

        public const float DefaultTimeoutSeconds = 120f;

        public SampleAutoRole AutoRole { get; private set; } = SampleAutoRole.None;
        public string LobbyName { get; private set; } = string.Empty;
        public bool ForceLocal { get; private set; }

        /// <summary>The player name for the local backend; null when the command line names none.</summary>
        public string PlayerName { get; private set; }

        public float TimeoutSeconds { get; private set; } = DefaultTimeoutSeconds;

        public static SampleCommandLine FromEnvironment()
        {
            return Parse(Environment.GetCommandLineArgs());
        }

        public static SampleCommandLine Parse(IReadOnlyList<string> arguments)
        {
            SampleCommandLine result = new SampleCommandLine();
            if (arguments == null)
            {
                return result;
            }

            for (int i = 0; i < arguments.Count; i++)
            {
                string argument = arguments[i];
                bool hasValue = i + 1 < arguments.Count;

                if (string.Equals(argument, HostOption, StringComparison.Ordinal) && hasValue)
                {
                    result.AutoRole = SampleAutoRole.Host;
                    result.LobbyName = arguments[++i];
                }
                else if (string.Equals(argument, JoinOption, StringComparison.Ordinal) && hasValue)
                {
                    result.AutoRole = SampleAutoRole.Guest;
                    result.LobbyName = arguments[++i];
                }
                else if (string.Equals(argument, LocalOption, StringComparison.Ordinal))
                {
                    result.ForceLocal = true;
                }
                else if (string.Equals(argument, NameOption, StringComparison.Ordinal) && hasValue)
                {
                    result.PlayerName = arguments[++i];
                }
                else if (string.Equals(argument, TimeoutOption, StringComparison.Ordinal) && hasValue
                    && float.TryParse(arguments[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds)
                    && seconds > 0f)
                {
                    result.TimeoutSeconds = seconds;
                    i++;
                }
            }

            return result;
        }
    }
}
