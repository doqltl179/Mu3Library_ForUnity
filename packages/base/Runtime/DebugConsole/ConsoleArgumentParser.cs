using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Mu3Library.DebugConsole
{
    /// <summary>
    /// Splits a console line into words and reads each word as a parameter's type.
    /// </summary>
    internal static class ConsoleArgumentParser
    {
        /// <summary>
        /// Splits on spaces; a word in double quotes keeps its spaces, and <c>\"</c> and <c>\\</c>
        /// inside quotes stand for a quote and a backslash. Answers false when a quote is not closed.
        /// </summary>
        public static bool TryTokenize(string line, List<string> tokens)
        {
            tokens.Clear();
            if (string.IsNullOrEmpty(line))
            {
                return true;
            }

            var current = new StringBuilder();
            bool inQuotes = false;
            bool hasToken = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '\\' && i + 1 < line.Length && (line[i + 1] == '"' || line[i + 1] == '\\'))
                    {
                        current.Append(line[++i]);
                    }
                    else if (c == '"')
                    {
                        inQuotes = false;
                    }
                    else
                    {
                        current.Append(c);
                    }

                    continue;
                }

                if (c == '"')
                {
                    inQuotes = true;
                    hasToken = true;
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (hasToken)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                        hasToken = false;
                    }
                }
                else
                {
                    current.Append(c);
                    hasToken = true;
                }
            }

            if (inQuotes)
            {
                return false;
            }

            if (hasToken)
            {
                tokens.Add(current.ToString());
            }

            return true;
        }

        public static bool IsSupported(Type type) =>
            type == typeof(string) || type == typeof(int) || type == typeof(long) || type == typeof(float) ||
            type == typeof(double) || type == typeof(bool) || type.IsEnum || type == typeof(Vector2) || type == typeof(Vector3);

        public static string GetTypeName(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(int)) return "int";
            if (type == typeof(long)) return "long";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(Vector2)) return "x,y";
            if (type == typeof(Vector3)) return "x,y,z";
            return type.IsEnum ? string.Join("|", Enum.GetNames(type)) : type.Name;
        }

        public static bool TryParse(string text, Type type, out object value)
        {
            value = null;
            CultureInfo culture = CultureInfo.InvariantCulture;
            if (type == typeof(string))
            {
                value = text;
                return true;
            }

            if (type == typeof(int) && int.TryParse(text, NumberStyles.Integer, culture, out int intValue))
            {
                value = intValue;
                return true;
            }

            if (type == typeof(long) && long.TryParse(text, NumberStyles.Integer, culture, out long longValue))
            {
                value = longValue;
                return true;
            }

            if (type == typeof(float) && TryParseFloat(text, out float floatValue))
            {
                value = floatValue;
                return true;
            }

            if (type == typeof(double) && double.TryParse(text, NumberStyles.Float, culture, out double doubleValue))
            {
                value = doubleValue;
                return true;
            }

            if (type == typeof(bool) && TryParseBool(text, out bool boolValue))
            {
                value = boolValue;
                return true;
            }

            if (type.IsEnum && TryParseEnum(text, type, out object enumValue))
            {
                value = enumValue;
                return true;
            }

            if (type == typeof(Vector2) && TryParseFloats(text, 2, out float[] v2))
            {
                value = new Vector2(v2[0], v2[1]);
                return true;
            }

            if (type == typeof(Vector3) && TryParseFloats(text, 3, out float[] v3))
            {
                value = new Vector3(v3[0], v3[1], v3[2]);
                return true;
            }

            return false;
        }



        private static bool TryParseFloat(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private static bool TryParseBool(string text, out bool value)
        {
            switch (text.ToLowerInvariant())
            {
                case "true":
                case "1":
                case "on":
                case "yes":
                    value = true;
                    return true;
                case "false":
                case "0":
                case "off":
                case "no":
                    value = false;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        private static bool TryParseEnum(string text, Type type, out object value)
        {
            value = null;
            foreach (string name in Enum.GetNames(type))
            {
                if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
                {
                    value = Enum.Parse(type, name);
                    return true;
                }
            }

            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number))
            {
                object candidate = Enum.ToObject(type, number);
                if (Enum.IsDefined(type, candidate))
                {
                    value = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseFloats(string text, int count, out float[] values)
        {
            values = null;
            string trimmed = text.Trim().TrimStart('(').TrimEnd(')');
            string[] parts = trimmed.Split(',');
            if (parts.Length != count)
            {
                return false;
            }

            var parsed = new float[count];
            for (int i = 0; i < count; i++)
            {
                if (!TryParseFloat(parts[i].Trim(), out parsed[i]))
                {
                    return false;
                }
            }

            values = parsed;
            return true;
        }
    }
}
