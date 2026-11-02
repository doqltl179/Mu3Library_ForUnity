using System;
using System.Collections.Generic;
using System.Reflection;
using Mu3Library.DI;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Window.Drawer
{
    /// <summary>
    /// Validates the DI registrations of every registered core while the game runs: a
    /// registered implementation whose constructor or required [Inject] members cannot be
    /// resolved, and a registered implementation, registered instance, or core that has a required
    /// [Inject] property without a setter or a required readonly [Inject] field, is reported here
    /// before the first resolve or injection throws at some later moment.
    /// Reading the registrations resolves nothing, so the check has no side effects.
    /// </summary>
    [CreateAssetMenu(fileName = FileName, menuName = MenuName, order = 0)]
    public class DIValidatorDrawer : Mu3WindowDrawer
    {
        public const string FileName = "DIValidator";
        private const string ItemName = "DI Validator";
        private const string MenuName = MenuRoot + "/" + ItemName;



        public override void OnGUIHeader()
        {
            DrawFoldoutHeader1(ItemName, ref _foldout);
        }

        public override void OnGUIBody()
        {
            DrawStruct(() =>
            {
                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Cores register their services while the game runs. Enter play mode to validate.", MessageType.Info);
                    return;
                }

                CoreRoot coreRoot = UnityEngine.Object.FindAnyObjectByType<CoreRoot>();
                if (coreRoot == null || coreRoot.RegisteredCores.Count == 0)
                {
                    EditorGUILayout.HelpBox("No registered core found.", MessageType.Info);
                    return;
                }

                foreach (CoreBase core in coreRoot.RegisteredCores)
                {
                    if (core == null)
                    {
                        continue;
                    }

                    DrawCoreValidation(coreRoot, core);
                    GUILayout.Space(8);
                }
            }, 20, 20, 0, 0);
        }

        private void DrawCoreValidation(CoreRoot coreRoot, CoreBase core)
        {
            DrawHeader3($"[ {core.GetType().Name} ]");

            IReadOnlyList<ServiceDescriptor> descriptors = core.GetRegisteredDescriptors();
            List<string> issues = new();
            ValidateCore(coreRoot, core, descriptors, issues);

            EditorGUILayout.LabelField($"Registrations: {descriptors.Count}");

            if (issues.Count == 0)
            {
                EditorGUILayout.LabelField("No issues found.");
                return;
            }

            foreach (string issue in issues)
            {
                EditorGUILayout.HelpBox(issue, MessageType.Error);
            }
        }

        /// <summary>
        /// Reports the issues of one core. For each class the container creates: a constructor or
        /// required [Inject] member that cannot resolve. For those classes, each registered
        /// instance, and the core itself, all of which the container injects: each required
        /// [Inject] member it can never fill, whether or not its service is registered.
        /// </summary>
        /// <remarks>
        /// A factory's product is left out: its type is known only once the factory runs.
        /// <paramref name="coreRoot"/> is read only for a member that names another core.
        /// </remarks>
        internal static void ValidateCore(CoreRoot coreRoot, CoreBase core, IReadOnlyList<ServiceDescriptor> descriptors, List<string> issues)
        {
            HashSet<Type> registrationCheckedTypes = new();
            HashSet<Type> memberCheckedTypes = new();

            Type coreType = core.GetType();
            memberCheckedTypes.Add(coreType);
            ValidateUnfillableMembers(coreType, issues);

            foreach (ServiceDescriptor descriptor in descriptors)
            {
                Type implementationType = descriptor.ImplementationType;
                if (implementationType == null || descriptor.Factory != null)
                {
                    continue;
                }

                // A registered instance already exists, so only the checks that need no
                // registration lookup apply to it.
                if (descriptor.Instance == null && registrationCheckedTypes.Add(implementationType))
                {
                    ValidateConstructor(core, implementationType, issues);
                    ValidateInjectMembers(coreRoot, core, implementationType, issues);
                }

                if (memberCheckedTypes.Add(implementationType))
                {
                    ValidateUnfillableMembers(implementationType, issues);
                }
            }
        }

        private static void ValidateConstructor(CoreBase core, Type implementationType, List<string> issues)
        {
            ConstructorInfo[] constructors = implementationType.GetConstructors();
            if (constructors.Length == 0)
            {
                return;
            }

            foreach (ConstructorInfo constructor in constructors)
            {
                bool resolvable = true;
                foreach (ParameterInfo parameter in constructor.GetParameters())
                {
                    if (!IsResolvableType(core, parameter.ParameterType, null) && !parameter.HasDefaultValue)
                    {
                        resolvable = false;
                        break;
                    }
                }

                if (resolvable)
                {
                    return;
                }
            }

            issues.Add($"{implementationType.Name}: no public constructor whose parameters are all resolvable.");
        }

        private static void ValidateInjectMembers(CoreRoot coreRoot, CoreBase core, Type implementationType, List<string> issues)
        {
            // The container's own member rule, so members that base classes declare are
            // checked exactly when the container would inject them.
            foreach (FieldInfo field in InjectableMembers.GetFields(implementationType))
            {
                InjectAttribute attribute = field.GetCustomAttribute<InjectAttribute>();
                if (attribute == null || !attribute.Required)
                {
                    continue;
                }

                if (!IsInjectSatisfied(coreRoot, core, field.FieldType, attribute))
                {
                    issues.Add($"{implementationType.Name}.{field.Name}: required [Inject] target {field.FieldType.Name} is not registered.");
                }
            }

            foreach (PropertyInfo property in InjectableMembers.GetProperties(implementationType))
            {
                InjectAttribute attribute = property.GetCustomAttribute<InjectAttribute>();
                if (attribute == null || !attribute.Required)
                {
                    continue;
                }

                if (!IsInjectSatisfied(coreRoot, core, property.PropertyType, attribute))
                {
                    issues.Add($"{implementationType.Name}.{property.Name}: required [Inject] target {property.PropertyType.Name} is not registered.");
                }
            }
        }

        private static void ValidateUnfillableMembers(Type type, List<string> issues)
        {
            ValidateReadOnlyFields(type, issues);
            ValidateUnwritableProperties(type, issues);
        }

        /// <summary>
        /// Reports each required readonly [Inject] field of <paramref name="implementationType"/>,
        /// which the container throws for whether or not its service is registered.
        /// </summary>
        internal static void ValidateReadOnlyFields(Type implementationType, List<string> issues)
        {
            foreach (FieldInfo field in InjectableMembers.GetReadOnlyFields(implementationType))
            {
                InjectAttribute attribute = field.GetCustomAttribute<InjectAttribute>();
                if (attribute != null && attribute.Required)
                {
                    issues.Add($"{implementationType.Name}.{field.Name}: required [Inject] field is readonly, so the container cannot inject it.");
                }
            }
        }

        /// <summary>
        /// Reports each required [Inject] property of <paramref name="implementationType"/> that has
        /// no setter, which the container throws for whether or not its service is registered.
        /// </summary>
        internal static void ValidateUnwritableProperties(Type implementationType, List<string> issues)
        {
            foreach (PropertyInfo property in InjectableMembers.GetUnwritableProperties(implementationType))
            {
                InjectAttribute attribute = property.GetCustomAttribute<InjectAttribute>();
                if (attribute != null && attribute.Required)
                {
                    issues.Add($"{implementationType.Name}.{property.Name}: required [Inject] property has no setter, so the container cannot inject it.");
                }
            }
        }

        private static bool IsInjectSatisfied(CoreRoot coreRoot, CoreBase core, Type serviceType, InjectAttribute attribute)
        {
            if (attribute.CoreType == null)
            {
                return IsResolvableType(core, serviceType, attribute.Key);
            }

            foreach (CoreBase registered in coreRoot.RegisteredCores)
            {
                if (registered != null && registered.GetType() == attribute.CoreType)
                {
                    return IsResolvableType(registered, serviceType, attribute.Key);
                }
            }

            return false;
        }

        private static bool IsResolvableType(CoreBase core, Type serviceType, string key)
        {
            if (serviceType == typeof(ContainerScope) ||
                serviceType == typeof(Container) ||
                serviceType == typeof(IObjectInjector))
            {
                return true;
            }

            // The scope resolves these wrappers itself; what matters is the element type.
            if (serviceType.IsGenericType)
            {
                Type genericType = serviceType.GetGenericTypeDefinition();
                if (genericType == typeof(IEnumerable<>))
                {
                    // An empty sequence is a valid resolution, so the wrapper always resolves.
                    return true;
                }

                if (genericType == typeof(Func<>) || genericType == typeof(Lazy<>))
                {
                    return IsResolvableType(core, serviceType.GetGenericArguments()[0], key);
                }
            }

            return core.IsServiceRegistered(serviceType, key);
        }
    }
}
