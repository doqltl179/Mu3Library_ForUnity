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
    /// resolved is reported here before the first resolve throws at some later moment.
    /// Reading the registrations resolves nothing, so the check has no side effects.
    /// </summary>
    [CreateAssetMenu(fileName = FileName, menuName = MenuName, order = 0)]
    public class DIValidatorDrawer : Mu3WindowDrawer
    {
        public const string FileName = "DIValidator";
        private const string ItemName = "DI Validator";
        private const string MenuName = MenuRoot + "/" + ItemName;

        private const BindingFlags MemberBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;



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

                CoreRoot coreRoot = UnityEngine.Object.FindFirstObjectByType<CoreRoot>();
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
            HashSet<Type> checkedTypes = new();

            foreach (ServiceDescriptor descriptor in descriptors)
            {
                Type implementationType = descriptor.ImplementationType;
                if (implementationType == null ||
                    descriptor.Instance != null ||
                    descriptor.Factory != null ||
                    !checkedTypes.Add(implementationType))
                {
                    continue;
                }

                ValidateConstructor(core, implementationType, issues);
                ValidateInjectMembers(coreRoot, core, implementationType, issues);
            }

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

        private void ValidateConstructor(CoreBase core, Type implementationType, List<string> issues)
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

        private void ValidateInjectMembers(CoreRoot coreRoot, CoreBase core, Type implementationType, List<string> issues)
        {
            foreach (FieldInfo field in implementationType.GetFields(MemberBindingFlags))
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

            foreach (PropertyInfo property in implementationType.GetProperties(MemberBindingFlags))
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

        private bool IsInjectSatisfied(CoreRoot coreRoot, CoreBase core, Type serviceType, InjectAttribute attribute)
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
