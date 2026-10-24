using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Mu3Library.Observable;
using UnityEditor;

namespace Mu3Library.Editor.Observable
{
    /// <summary>
    /// Finds the observable instances an inspector property stands for. A drawer's
    /// <c>fieldInfo</c> belongs to the type that declares the field, so it only reads a field
    /// declared on the inspected object itself. Following <c>propertyPath</c> from every selected
    /// object also reaches fields inside serializable classes, arrays, and lists.
    /// </summary>
    internal static class ObservableDrawerUtility
    {
        private const string ArrayPathPart = "Array";
        private const string ArrayElementPrefix = "data[";

        private const BindingFlags FieldFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>
        /// Applies the edited <c>_value</c> of an <see cref="ObservableProperty{T}"/> and notifies
        /// every selected instance whose value it changed. The serialized change is applied
        /// before the notification so that Undo records the value from before the edit.
        /// </summary>
        internal static void ApplyAndNotify<T>(SerializedProperty valueProp, T value)
        {
            List<ObservableProperty<T>> observables = FindTargets<ObservableProperty<T>>(
                valueProp.serializedObject,
                GetParentPath(valueProp.propertyPath));
            observables.RemoveAll(observable => EqualityComparer<T>.Default.Equals(observable.Value, value));

            valueProp.serializedObject.ApplyModifiedProperties();

            for (int i = 0; i < observables.Count; i++)
            {
                observables[i].Notify();
            }
        }

        /// <summary>
        /// Calls the public parameterless method <paramref name="methodName"/> on the instance
        /// <paramref name="property"/> stands for in every selected object.
        /// </summary>
        internal static void InvokeOnTargets(SerializedProperty property, string methodName)
        {
            List<object> targets = FindTargets<object>(property.serializedObject, property.propertyPath);
            for (int i = 0; i < targets.Count; i++)
            {
                MethodInfo method = targets[i].GetType().GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    Type.EmptyTypes,
                    null);
                method?.Invoke(targets[i], null);
            }
        }

        /// <summary>
        /// The instance at <paramref name="propertyPath"/> in each selected object, skipping the
        /// objects where the path does not lead to a <typeparamref name="T"/>.
        /// </summary>
        internal static List<T> FindTargets<T>(SerializedObject serializedObject, string propertyPath)
            where T : class
        {
            var targets = new List<T>();
            foreach (UnityEngine.Object targetObject in serializedObject.targetObjects)
            {
                if (targetObject == null)
                {
                    continue;
                }

                if (FindTarget(targetObject, propertyPath) is T target)
                {
                    targets.Add(target);
                }
            }

            return targets;
        }

        /// <summary>
        /// Follows a <c>SerializedProperty.propertyPath</c> such as
        /// <c>slots.Array.data[2].health</c> from <paramref name="root"/> through fields and list
        /// elements, and answers null where the path cannot be followed.
        /// </summary>
        internal static object FindTarget(object root, string propertyPath)
        {
            if (root == null || string.IsNullOrEmpty(propertyPath))
            {
                return root;
            }

            object current = root;
            string[] parts = propertyPath.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                if (current == null)
                {
                    return null;
                }

                string part = parts[i];
                if (part == ArrayPathPart && i + 1 < parts.Length && TryGetElementIndex(parts[i + 1], out int index))
                {
                    IList list = current as IList;
                    if (list == null || index < 0 || index >= list.Count)
                    {
                        return null;
                    }

                    current = list[index];
                    i++;
                    continue;
                }

                FieldInfo field = FindField(current.GetType(), part);
                if (field == null)
                {
                    return null;
                }

                current = field.GetValue(current);
            }

            return current;
        }

        private static string GetParentPath(string propertyPath)
        {
            int separator = propertyPath.LastIndexOf('.');
            return separator < 0 ? string.Empty : propertyPath.Substring(0, separator);
        }

        private static bool TryGetElementIndex(string part, out int index)
        {
            index = -1;
            if (!part.StartsWith(ArrayElementPrefix, StringComparison.Ordinal) || !part.EndsWith("]", StringComparison.Ordinal))
            {
                return false;
            }

            string digits = part.Substring(ArrayElementPrefix.Length, part.Length - ArrayElementPrefix.Length - 1);
            return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out index);
        }

        /// <summary>
        /// Looks the field up on the type and its base classes, since reflection on a derived
        /// type does not return the private fields its base classes declare.
        /// </summary>
        private static FieldInfo FindField(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, FieldFlags);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }
    }
}
