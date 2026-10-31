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
    /// A value never starts with <c>-</c>, so an option that lacks its value does not take the
    /// next option, this sample's or Unity's own such as <c>-logFile</c>, as its value;
    /// <see cref="Error"/> names the problem instead, and an automatic run fails with it before
    /// it starts a backend.
    /// </summary>
    public sealed class SampleCommandLine
    {
        public const string OptionPrefix = "-mu3mp-";
        public const string HostOption = OptionPrefix + "host";
        public const string JoinOption = OptionPrefix + "join";
        public const string LocalOption = OptionPrefix + "local";
        public const string NameOption = OptionPrefix + "name";
        public const string TimeoutOption = OptionPrefix + "timeout";

        public const float DefaultTimeoutSeconds = 120f;

        public SampleAutoRole AutoRole { get; private set; } = SampleAutoRole.None;
        public string LobbyName { get; private set; } = string.Empty;
        public bool ForceLocal { get; private set; }

        /// <summary>The player name for the local backend; null when the command line names none.</summary>
        public string PlayerName { get; private set; }

        public float TimeoutSeconds { get; private set; } = DefaultTimeoutSeconds;

        /// <summary>The first unusable option, such as one without its value; null when there is none.</summary>
        public string Error { get; private set; }

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

                if (string.Equals(argument, HostOption, StringComparison.Ordinal)
                    || string.Equals(argument, JoinOption, StringComparison.Ordinal))
                {
                    result.AutoRole = string.Equals(argument, HostOption, StringComparison.Ordinal)
                        ? SampleAutoRole.Host
                        : SampleAutoRole.Guest;

                    if (TryReadValue(arguments, ref i, out string lobbyName) && lobbyName.Trim().Length > 0)
                    {
                        result.LobbyName = lobbyName;
                    }
                    else
                    {
                        result.AddError($"{argument} needs a lobby name");
                    }
                }
                else if (string.Equals(argument, LocalOption, StringComparison.Ordinal))
                {
                    result.ForceLocal = true;
                }
                else if (string.Equals(argument, NameOption, StringComparison.Ordinal))
                {
                    if (TryReadValue(arguments, ref i, out string playerName) && playerName.Trim().Length > 0)
                    {
                        result.PlayerName = playerName;
                    }
                    else
                    {
                        result.AddError($"{NameOption} needs a player name");
                    }
                }
                else if (string.Equals(argument, TimeoutOption, StringComparison.Ordinal))
                {
                    if (TryReadValue(arguments, ref i, out string text)
                        && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds)
                        && seconds > 0f && !float.IsInfinity(seconds))
                    {
                        result.TimeoutSeconds = seconds;
                    }
                    else
                    {
                        result.AddError($"{TimeoutOption} needs a number of seconds above 0");
                    }
                }
            }

            return result;
        }

        /// <summary>Takes the argument after <paramref name="index"/> as its value, unless it is missing or another option.</summary>
        private static bool TryReadValue(IReadOnlyList<string> arguments, ref int index, out string value)
        {
            if (index + 1 < arguments.Count && !arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
            {
                index++;
                value = arguments[index];
                return true;
            }

            value = null;
            return false;
        }

        private void AddError(string error)
        {
            Error ??= error;
        }
    }
}
