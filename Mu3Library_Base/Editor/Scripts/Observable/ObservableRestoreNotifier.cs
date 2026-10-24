using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mu3Library.Observable;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;

namespace Mu3Library.Editor.Observable
{
    /// <summary>
    /// Notifies the subscribers of Observable values that an undo, a redo, or a prefab revert
    /// restored. Unity writes the restored data into the live objects without going through the
    /// Observables, and their deserialization callbacks can run on a loading thread, so they only
    /// mark the restore; this delivers it on the main thread right after the operation. An undo or
    /// a redo covers the selected objects, whose inspector the edit came from, and a revert covers
    /// the reverted prefab instance.
    /// </summary>
    [InitializeOnLoad]
    internal static class ObservableRestoreNotifier
    {
        private const BindingFlags FieldFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly Dictionary<Type, FieldInfo[]> s_serializedFields = new Dictionary<Type, FieldInfo[]>();
        private static readonly Dictionary<Type, bool> s_mayHoldObservables = new Dictionary<Type, bool>();



        static ObservableRestoreNotifier()
        {
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            PrefabUtility.prefabInstanceReverted += OnPrefabInstanceReverted;
        }

        private static void OnUndoRedoPerformed()
        {
            var roots = new List<Object>();
            foreach (Object selected in Selection.objects)
            {
                if (selected is GameObject gameObject)
                {
                    roots.AddRange(gameObject.GetComponents<MonoBehaviour>());
                }
                else
                {
                    roots.Add(selected);
                }
            }

            NotifyRestored(roots);
        }

        private static void OnPrefabInstanceReverted(GameObject instanceRoot)
        {
            if (instanceRoot != null)
            {
                NotifyRestored(instanceRoot.GetComponentsInChildren<MonoBehaviour>(true));
            }
        }

        /// <summary>
        /// Notifies every Observable in the serialized fields of <paramref name="objects"/> that
        /// was restored to a different value, and answers how many there were. The Observables are
        /// collected before any subscriber runs, so a subscriber that changes the objects does not
        /// disturb the search.
        /// </summary>
        internal static int NotifyRestored(IEnumerable<Object> objects)
        {
            List<IRestorableObservable> observables = FindObservables(objects);

            int notifiedCount = 0;
            for (int i = 0; i < observables.Count; i++)
            {
                if (observables[i].NotifyIfRestored())
                {
                    notifiedCount++;
                }
            }

            return notifiedCount;
        }

        /// <summary>
        /// Walks the fields Unity serializes, through serializable classes and structs, managed
        /// references, arrays, and lists, without entering other Unity objects. The walk keeps its
        /// own stack, so a long chain of managed references cannot overflow the call stack.
        /// </summary>
        private static List<IRestorableObservable> FindObservables(IEnumerable<Object> objects)
        {
            var observables = new List<IRestorableObservable>();
            var visited = new HashSet<object>(ReferenceComparer.Instance);
            var pending = new Stack<object>();

            foreach (Object root in objects)
            {
                if (root is MonoBehaviour || root is ScriptableObject)
                {
                    pending.Push(root);
                }
            }

            while (pending.Count > 0)
            {
                object current = pending.Pop();
                if (!visited.Add(current))
                {
                    continue;
                }

                if (current is IRestorableObservable observable)
                {
                    observables.Add(observable);
                }

                Type type = current.GetType();
                if (TryGetElementType(type, out Type elementType))
                {
                    if (MayHoldObservables(elementType))
                    {
                        foreach (object element in (IList)current)
                        {
                            PushValue(pending, element);
                        }
                    }

                    continue;
                }

                foreach (FieldInfo field in GetSerializedFields(type))
                {
                    if (MayHoldObservables(field.FieldType))
                    {
                        PushValue(pending, field.GetValue(current));
                    }
                }
            }

            return observables;
        }

        private static void PushValue(Stack<object> pending, object value)
        {
            // A field that holds another Unity object only refers to it; that object's own
            // Observables belong to it.
            if (value != null && !(value is Object))
            {
                pending.Push(value);
            }
        }

        /// <summary>
        /// The instance fields Unity serializes: public or marked with SerializeField or
        /// SerializeReference, and neither readonly nor NonSerialized. Base classes are included,
        /// up to the Unity type a script derives from.
        /// </summary>
        private static FieldInfo[] GetSerializedFields(Type type)
        {
            if (s_serializedFields.TryGetValue(type, out FieldInfo[] cached))
            {
                return cached;
            }

            var fields = new List<FieldInfo>();
            for (Type current = type; current != null && !IsWalkEnd(current); current = current.BaseType)
            {
                foreach (FieldInfo field in current.GetFields(FieldFlags))
                {
                    if (field.IsInitOnly || field.IsNotSerialized)
                    {
                        continue;
                    }

                    if (field.IsPublic ||
                        field.IsDefined(typeof(SerializeField), false) ||
                        field.IsDefined(typeof(SerializeReference), false))
                    {
                        fields.Add(field);
                    }
                }
            }

            FieldInfo[] result = fields.ToArray();
            s_serializedFields.Add(type, result);
            return result;
        }

        private static bool IsWalkEnd(Type type)
            => type == typeof(object) ||
               type == typeof(ValueType) ||
               type == typeof(Object) ||
               type == typeof(MonoBehaviour) ||
               type == typeof(ScriptableObject);

        /// <summary>
        /// Whether a value of <paramref name="type"/> can contain an Observable, so the walk skips
        /// primitive data such as a large array of vectors. A class that is not sealed can hold a
        /// derived instance, so it counts as one that can.
        /// </summary>
        private static bool MayHoldObservables(Type type)
        {
            if (s_mayHoldObservables.TryGetValue(type, out bool cached))
            {
                return cached;
            }

            // A type that contains itself is treated as one that can while it is being decided.
            s_mayHoldObservables[type] = true;
            bool result = DecideMayHoldObservables(type);
            s_mayHoldObservables[type] = result;
            return result;
        }

        private static bool DecideMayHoldObservables(Type type)
        {
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) ||
                typeof(Object).IsAssignableFrom(type) || typeof(Delegate).IsAssignableFrom(type))
            {
                return false;
            }

            if (typeof(IRestorableObservable).IsAssignableFrom(type))
            {
                return true;
            }

            if (TryGetElementType(type, out Type elementType))
            {
                return MayHoldObservables(elementType);
            }

            if (!type.IsValueType && !type.IsSealed)
            {
                return true;
            }

            foreach (FieldInfo field in GetSerializedFields(type))
            {
                if (MayHoldObservables(field.FieldType))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The element type of the collections Unity serializes, an array or a <c>List&lt;T&gt;</c>.
        /// </summary>
        private static bool TryGetElementType(Type type, out Type elementType)
        {
            if (type.IsArray)
            {
                elementType = type.GetElementType();
                return true;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }

            elementType = null;
            return false;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);
            int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
