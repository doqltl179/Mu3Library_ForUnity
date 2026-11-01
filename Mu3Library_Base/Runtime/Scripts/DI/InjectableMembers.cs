using System;
using System.Collections.Generic;
using System.Reflection;

namespace Mu3Library.DI
{
    /// <summary>
    /// The member rule of <see cref="InjectAttribute"/>: which fields and properties of a type
    /// receive injection, and which [Inject] members the container cannot fill: properties without
    /// a setter and readonly fields. The container and the editor DI validator both read it from
    /// here, so what the validator reports is what the container injects.
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
        private static readonly Dictionary<Type, PropertyInfo[]> _unwritablePropertiesCache = new();
        private static readonly Dictionary<Type, FieldInfo[]> _readOnlyFieldsCache = new();
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

        /// <summary>
        /// The [Inject] properties of a type and of its base classes that have no setter, the
        /// type's own first: a get-only property, or one whose setter the Unity linker removed in
        /// a stripped player. <see cref="GetProperties"/> leaves them out.
        /// </summary>
        /// <remarks>
        /// A required one (<see cref="InjectAttribute.Required"/>) can never be filled, so the
        /// container throws when it injects the object, as it does for a required member whose
        /// service does not resolve, and the editor DI validator reports it. An optional one is
        /// skipped. A getter-only override does not count when the container writes the property
        /// through the declaration it overrides, which <see cref="GetProperties"/> lists.
        /// </remarks>
        public static IReadOnlyList<PropertyInfo> GetUnwritableProperties(Type type)
        {
            return Array.AsReadOnly(GetUnwritablePropertyArray(type));
        }

        /// <summary>
        /// The readonly [Inject] fields of a type and of its base classes, the type's own first.
        /// <see cref="GetFields"/> leaves them out.
        /// </summary>
        /// <remarks>
        /// Only its initializer or a constructor sets a readonly field, and the container injects
        /// members after the constructor has run, so it never writes one. A required one
        /// (<see cref="InjectAttribute.Required"/>) can therefore never be filled: the container
        /// throws when it injects the object, as it does for a required property without a setter,
        /// and the editor DI validator reports it. An optional one is skipped. A static field is
        /// never injected and is not listed.
        /// </remarks>
        public static IReadOnlyList<FieldInfo> GetReadOnlyFields(Type type)
        {
            return Array.AsReadOnly(GetReadOnlyFieldArray(type));
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

        internal static PropertyInfo[] GetUnwritablePropertyArray(Type type)
        {
            if (type == null)
            {
                return Array.Empty<PropertyInfo>();
            }

            lock (_cacheLock)
            {
                if (_unwritablePropertiesCache.TryGetValue(type, out PropertyInfo[] cached))
                {
                    return cached;
                }

                List<PropertyInfo> properties = new();
                // An override and the declaration it overrides share one getter definition, so
                // seeding the set with the writable ones leaves out a getter-only override that the
                // container writes through its base declaration, and lists each other property once.
                HashSet<MethodInfo> getterDefinitions = new();
                foreach (PropertyInfo writable in GetPropertyArray(type))
                {
                    MethodInfo writableGetter = writable.GetGetMethod(true);
                    if (writableGetter != null)
                    {
                        getterDefinitions.Add(writableGetter.GetBaseDefinition());
                    }
                }

                for (Type current = type; !IsWalkEnd(current); current = current.BaseType)
                {
                    foreach (PropertyInfo property in current.GetProperties(DeclaredMemberFlags))
                    {
                        if (!IsUnwritableInjectProperty(property))
                        {
                            continue;
                        }

                        MethodInfo getter = property.GetGetMethod(true);
                        if (getter == null || getterDefinitions.Add(getter.GetBaseDefinition()))
                        {
                            properties.Add(property);
                        }
                    }
                }

                PropertyInfo[] result = properties.Count == 0 ? Array.Empty<PropertyInfo>() : properties.ToArray();
                _unwritablePropertiesCache[type] = result;
                return result;
            }
        }

        internal static FieldInfo[] GetReadOnlyFieldArray(Type type)
        {
            if (type == null)
            {
                return Array.Empty<FieldInfo>();
            }

            lock (_cacheLock)
            {
                if (_readOnlyFieldsCache.TryGetValue(type, out FieldInfo[] cached))
                {
                    return cached;
                }

                List<FieldInfo> fields = new();
                for (Type current = type; !IsWalkEnd(current); current = current.BaseType)
                {
                    foreach (FieldInfo field in current.GetFields(DeclaredMemberFlags))
                    {
                        if (IsReadOnlyInjectField(field))
                        {
                            fields.Add(field);
                        }
                    }
                }

                FieldInfo[] result = fields.Count == 0 ? Array.Empty<FieldInfo>() : fields.ToArray();
                _readOnlyFieldsCache[type] = result;
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

        private static bool IsReadOnlyInjectField(FieldInfo field)
        {
            return field.GetCustomAttribute<InjectAttribute>() != null &&
                   field.IsInitOnly &&
                   !field.IsStatic;
        }

        private static bool IsInjectableProperty(PropertyInfo property)
        {
            return property.GetCustomAttribute<InjectAttribute>() != null &&
                   property.CanWrite &&
                   property.GetIndexParameters().Length == 0;
        }

        private static bool IsUnwritableInjectProperty(PropertyInfo property)
        {
            return property.GetCustomAttribute<InjectAttribute>() != null &&
                   !property.CanWrite &&
                   property.GetIndexParameters().Length == 0;
        }
    }
}
