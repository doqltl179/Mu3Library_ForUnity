using System;
using System.Collections.Generic;
using System.Reflection;

namespace Mu3Library.DI
{
    /// <summary>
    /// The member rule of <see cref="InjectAttribute"/>: which fields and properties of a type
    /// receive injection. The container and the editor DI validator both read it from here, so
    /// what the validator reports is what the container injects.
    /// </summary>
    /// <remarks>
    /// The rule walks the type and its base classes, so an [Inject] member a base class
    /// declares private is found as well. The walk ends at a framework base type:
    /// <see cref="CoreBase"/>, <see cref="object"/>, and the engine's own types such as
    /// <c>MonoBehaviour</c>, none of which declares an [Inject] member.
    /// </remarks>
    public static class InjectableMembers
    {
        private const BindingFlags DeclaredMemberFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly Dictionary<Type, FieldInfo[]> _fieldsCache = new();
        private static readonly Dictionary<Type, PropertyInfo[]> _propertiesCache = new();
        private static readonly object _cacheLock = new object();



        /// <summary>
        /// The [Inject] fields of a type and of its base classes, the type's own first.
        /// </summary>
        public static IReadOnlyList<FieldInfo> GetFields(Type type)
        {
            return Array.AsReadOnly(GetFieldArray(type));
        }

        /// <summary>
        /// The [Inject] properties of a type and of its base classes, the type's own first.
        /// A property that a derived class overrides appears once.
        /// </summary>
        public static IReadOnlyList<PropertyInfo> GetProperties(Type type)
        {
            return Array.AsReadOnly(GetPropertyArray(type));
        }

        internal static FieldInfo[] GetFieldArray(Type type)
        {
            if (type == null)
            {
                return Array.Empty<FieldInfo>();
            }

            lock (_cacheLock)
            {
                if (_fieldsCache.TryGetValue(type, out FieldInfo[] cached))
                {
                    return cached;
                }

                List<FieldInfo> fields = new();
                for (Type current = type; !IsWalkEnd(current); current = current.BaseType)
                {
                    foreach (FieldInfo field in current.GetFields(DeclaredMemberFlags))
                    {
                        if (IsInjectableField(field))
                        {
                            fields.Add(field);
                        }
                    }
                }

                FieldInfo[] result = fields.Count == 0 ? Array.Empty<FieldInfo>() : fields.ToArray();
                _fieldsCache[type] = result;
                return result;
            }
        }

        internal static PropertyInfo[] GetPropertyArray(Type type)
        {
            if (type == null)
            {
                return Array.Empty<PropertyInfo>();
            }

            lock (_cacheLock)
            {
                if (_propertiesCache.TryGetValue(type, out PropertyInfo[] cached))
                {
                    return cached;
                }

                List<PropertyInfo> properties = new();
                // An override and the declaration it overrides share one setter definition;
                // only the most derived one is injected, as reading the type's own properties did.
                HashSet<MethodInfo> setterDefinitions = new();
                for (Type current = type; !IsWalkEnd(current); current = current.BaseType)
                {
                    foreach (PropertyInfo property in current.GetProperties(DeclaredMemberFlags))
                    {
                        if (IsInjectableProperty(property) &&
                            setterDefinitions.Add(property.GetSetMethod(true).GetBaseDefinition()))
                        {
                            properties.Add(property);
                        }
                    }
                }

                PropertyInfo[] result = properties.Count == 0 ? Array.Empty<PropertyInfo>() : properties.ToArray();
                _propertiesCache[type] = result;
                return result;
            }
        }

        private static bool IsWalkEnd(Type type)
        {
            return type == null ||
                   type == typeof(object) ||
                   type == typeof(CoreBase) ||
                   type.Assembly == typeof(UnityEngine.Object).Assembly;
        }

        private static bool IsInjectableField(FieldInfo field)
        {
            return field.GetCustomAttribute<InjectAttribute>() != null &&
                   !field.IsInitOnly &&
                   !field.IsStatic;
        }

        private static bool IsInjectableProperty(PropertyInfo property)
        {
            return property.GetCustomAttribute<InjectAttribute>() != null &&
                   property.CanWrite &&
                   property.GetIndexParameters().Length == 0;
        }
    }
}
