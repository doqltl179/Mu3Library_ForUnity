using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Mu3Library.DebugConsole
{
    /// <summary>
    /// A registered command as <c>help</c> lists it.
    /// </summary>
    public sealed class ConsoleCommand
    {
        public string Name { get; }
        public string Description { get; }
        public ConsoleAccess Access { get; }
        public bool AffectsProgress { get; }
        public IReadOnlyList<ConsoleParameter> Parameters { get; }

        /// <summary>
        /// Whether the command takes the words of its line as they are, through
        /// <see cref="ConsoleCall.Arguments"/>, instead of typed parameters.
        /// </summary>
        public bool TakesRawArguments { get; }

        /// <summary>
        /// The command's name and parameters, such as <c>give &lt;item:int&gt; [count:int=1]</c>.
        /// </summary>
        public string Usage { get; }



        internal ConsoleCommand(string name, ConsoleCommandOptions options, IReadOnlyList<ConsoleParameter> parameters, bool takesRawArguments)
        {
            Name = name;
            Description = options.Description ?? string.Empty;
            Access = options.Access;
            AffectsProgress = options.AffectsProgress;
            Parameters = parameters;
            TakesRawArguments = takesRawArguments;
            Usage = BuildUsage();
        }



        private string BuildUsage()
        {
            var builder = new StringBuilder(Name);
            if (TakesRawArguments)
            {
                builder.Append(" [arguments...]");
                return builder.ToString();
            }

            foreach (ConsoleParameter parameter in Parameters)
            {
                builder.Append(' ');
                string typeName = ConsoleArgumentParser.GetTypeName(parameter.Type);
                if (parameter.HasDefaultValue)
                {
                    builder.Append('[').Append(parameter.Name).Append(':').Append(typeName).Append('=')
                        .Append(FormatDefault(parameter.DefaultValue)).Append(']');
                }
                else
                {
                    builder.Append('<').Append(parameter.Name).Append(':').Append(typeName).Append('>');
                }
            }

            return builder.ToString();
        }

        // Written as the console reads it back: invariant numbers, and vectors as x,y,z.
        private static string FormatDefault(object value)
        {
            CultureInfo culture = CultureInfo.InvariantCulture;
            switch (value)
            {
                case null:
                    return "null";
                case Vector2 v2:
                    return string.Format(culture, "{0},{1}", v2.x, v2.y);
                case Vector3 v3:
                    return string.Format(culture, "{0},{1},{2}", v3.x, v3.y, v3.z);
                default:
                    return Convert.ToString(value, culture);
            }
        }
    }

    public readonly struct ConsoleParameter
    {
        public string Name { get; }
        public Type Type { get; }
        public bool HasDefaultValue { get; }
        public object DefaultValue { get; }



        public ConsoleParameter(string name, Type type, bool hasDefaultValue, object defaultValue)
        {
            Name = name;
            Type = type;
            HasDefaultValue = hasDefaultValue;
            DefaultValue = defaultValue;
        }
    }
}
