using System;
using System.Reflection;

namespace Mu3Library.Observable
{
    /// <summary>
    /// Decides whether a value Unity restored can be compared with the one the subscribers hold.
    /// Unity deserializes a list or another object in place, so both then refer to the same
    /// instance; only data held by value, a string, or a Unity object reference compares truly.
    /// </summary>
    internal static class RestoredValueComparison
    {
        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>
        /// True for primitives, enums, strings, decimals, Unity object references, and structs whose
        /// fields are all of these. It keeps no state, so a type initializer can call it on a
        /// loading thread.
        /// </summary>
        internal static bool CanCompare(Type type)
        {
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) ||
                typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return true;
            }

            if (!type.IsValueType)
            {
                return false;
            }

            foreach (FieldInfo field in type.GetFields(FieldFlags))
            {
                if (!CanCompare(field.FieldType))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
