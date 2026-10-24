using System;
using System.Collections.Generic;
using System.Reflection;
using Mu3Library.Attribute;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mu3Library.Editor.Attribute {
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MonoBehaviour), true, isFallback = true)]
    public class ButtonInvokeMonoBehaviourEditor : ButtonInvokeEditorBase {
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(ScriptableObject), true, isFallback = true)]
    public class ButtonInvokeScriptableObjectEditor : ButtonInvokeEditorBase {
    }

    /// <summary>
    /// Draws the default inspector followed by the <see cref="ButtonInvokeAttribute"/> buttons.
    /// <br/> The inspector window uses <see cref="CreateInspectorGUI"/>, which builds Unity's UI Toolkit default
    /// inspector, so property drawers that implement only CreatePropertyGUI render and IMGUI drawers keep working.
    /// <br/> <see cref="OnInspectorGUI"/> stays for IMGUI hosts that draw an editor directly.
    /// <br/> A derived editor that overrides <see cref="OnInspectorGUI"/> keeps drawing through it:
    /// <see cref="CreateInspectorGUI"/> then returns null, so the inspector window falls back to IMGUI.
    /// </summary>
    public abstract class ButtonInvokeEditorBase : UnityEditor.Editor {
        public override VisualElement CreateInspectorGUI() {
            if(OverridesOnInspectorGUI()) {
                return null;
            }

            VisualElement root = new VisualElement();

            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            ButtonInvokeAttributeInspector.AddButtons(root, targets);

            return root;
        }

        public override void OnInspectorGUI() {
            DrawDefaultInspector();
            ButtonInvokeAttributeInspector.Draw(targets);
        }

        private bool OverridesOnInspectorGUI() {
            MethodInfo method = GetType().GetMethod(nameof(OnInspectorGUI), BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            return method != null && method.DeclaringType != typeof(ButtonInvokeEditorBase);
        }
    }

    internal static class ButtonInvokeAttributeInspector {
        private const BindingFlags methodBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const string buttonSectionTitle = "Buttons";

        internal const string ButtonSectionName = "ButtonInvoke-Section";

        public static void Draw(UnityEngine.Object[] targets) {
            if(targets == null || targets.Length == 0 || targets[0] == null) {
                return;
            }

            List<MethodInfo> methods = GetInspectableMethods(targets[0].GetType());
            if(methods.Count == 0) {
                return;
            }

            DrawButtonSectionHeader();

            foreach(MethodInfo methodInfo in methods) {
                EditorGUILayout.Space();

                ButtonInvokeAttribute buttonInvokeAttribute = GetAttribute<ButtonInvokeAttribute>(methodInfo);
                if(!TryGetValidationMessage(methodInfo, out string validationMessage)) {
                    EditorGUILayout.HelpBox(validationMessage, MessageType.Error);
                    continue;
                }

                if(GUILayout.Button(GetButtonLabel(buttonInvokeAttribute, methodInfo), GUILayout.Height(GetButtonHeight(buttonInvokeAttribute)))) {
                    InvokeMethods(targets, methodInfo);
                }
            }
        }

        /// <summary>
        /// Adds the UI Toolkit version of <see cref="Draw"/> to <paramref name="root"/>: a section with one button per
        /// <see cref="ButtonInvokeAttribute"/> method, or an error box for a method that cannot be invoked.
        /// <br/> Nothing is added when the target type declares no such method.
        /// </summary>
        public static void AddButtons(VisualElement root, UnityEngine.Object[] targets) {
            if(root == null || targets == null || targets.Length == 0 || targets[0] == null) {
                return;
            }

            List<MethodInfo> methods = GetInspectableMethods(targets[0].GetType());
            if(methods.Count == 0) {
                return;
            }

            VisualElement section = new VisualElement { name = ButtonSectionName };
            section.style.marginTop = 8f;

            VisualElement separator = new VisualElement();
            separator.style.height = 1f;
            separator.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.2f)
                : new Color(0f, 0f, 0f, 0.2f);
            separator.style.marginBottom = 4f;
            section.Add(separator);

            Label title = new Label(buttonSectionTitle);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            section.Add(title);

            foreach(MethodInfo methodInfo in methods) {
                VisualElement item;

                ButtonInvokeAttribute buttonInvokeAttribute = GetAttribute<ButtonInvokeAttribute>(methodInfo);
                if(!TryGetValidationMessage(methodInfo, out string validationMessage)) {
                    item = new HelpBox(validationMessage, HelpBoxMessageType.Error);
                }
                else {
                    MethodInfo method = methodInfo;
                    Button button = new Button(() => InvokeMethods(targets, method)) {
                        text = GetButtonLabel(buttonInvokeAttribute, methodInfo),
                    };
                    button.style.height = GetButtonHeight(buttonInvokeAttribute);
                    item = button;
                }

                item.style.marginTop = 6f;
                section.Add(item);
            }

            root.Add(section);
        }

        private static void DrawButtonSectionHeader() {
            EditorGUILayout.Space(8f);

            Rect separatorRect = EditorGUILayout.GetControlRect(false, 1f);
            Color separatorColor = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.2f)
                : new Color(0f, 0f, 0f, 0.2f);

            EditorGUI.DrawRect(separatorRect, separatorColor);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(buttonSectionTitle, EditorStyles.boldLabel);
        }

        private static List<MethodInfo> GetInspectableMethods(Type targetType) {
            List<MethodInfo> methods = new List<MethodInfo>();
            AddInspectableMethods(targetType, methods);
            return methods;
        }

        private static void AddInspectableMethods(Type targetType, List<MethodInfo> methods) {
            if(targetType == null
                || targetType == typeof(MonoBehaviour)
                || targetType == typeof(ScriptableObject)
                || targetType == typeof(UnityEngine.Object)
                || targetType == typeof(object)) {
                return;
            }

            AddInspectableMethods(targetType.BaseType, methods);

            MethodInfo[] declaredMethods = targetType.GetMethods(methodBindingFlags);
            Array.Sort(declaredMethods, CompareMetadataTokens);

            foreach(MethodInfo methodInfo in declaredMethods) {
                if(GetAttribute<ButtonInvokeAttribute>(methodInfo) != null) {
                    methods.Add(methodInfo);
                }
            }
        }

        private static int CompareMetadataTokens(MethodInfo left, MethodInfo right) {
            return left.MetadataToken.CompareTo(right.MetadataToken);
        }

        private static T GetAttribute<T>(MethodInfo methodInfo) where T : System.Attribute {
            return (T)System.Attribute.GetCustomAttribute(methodInfo, typeof(T), false);
        }

        private static string GetButtonLabel(ButtonInvokeAttribute buttonInvokeAttribute, MethodInfo methodInfo) {
            return string.IsNullOrWhiteSpace(buttonInvokeAttribute.ButtonLabel)
                ? $"Invoke {methodInfo.Name}"
                : buttonInvokeAttribute.ButtonLabel;
        }

        private static float GetButtonHeight(ButtonInvokeAttribute buttonInvokeAttribute) {
            return buttonInvokeAttribute.ButtonHeight > 0f
                ? buttonInvokeAttribute.ButtonHeight
                : EditorGUIUtility.singleLineHeight;
        }

        private static bool TryGetValidationMessage(MethodInfo methodInfo, out string validationMessage) {
            if(methodInfo.IsStatic) {
                validationMessage = $"ButtonInvokeAttribute method [{methodInfo.Name}] must be an instance method.";
                return false;
            }

            if(methodInfo.ContainsGenericParameters) {
                validationMessage = $"ButtonInvokeAttribute method [{methodInfo.Name}] cannot be generic.";
                return false;
            }

            if(methodInfo.ReturnType != typeof(void)) {
                validationMessage = $"ButtonInvokeAttribute method [{methodInfo.Name}] must return void.";
                return false;
            }

            if(methodInfo.GetParameters().Length != 0) {
                validationMessage = $"ButtonInvokeAttribute method [{methodInfo.Name}] must be parameterless.";
                return false;
            }

            validationMessage = string.Empty;
            return true;
        }

        private static void InvokeMethods(UnityEngine.Object[] targets, MethodInfo methodInfo) {
            foreach(UnityEngine.Object target in targets) {
                if(target == null) {
                    continue;
                }

                if(!methodInfo.DeclaringType.IsInstanceOfType(target)) {
                    Debug.LogError($"Could not invoke [{methodInfo.Name}] on [{target.GetType().Name}] because it does not inherit [{methodInfo.DeclaringType.Name}].", target);
                    continue;
                }

                Undo.RecordObject(target, $"Invoke {methodInfo.Name}");

                try {
                    methodInfo.Invoke(target, null);
                    EditorUtility.SetDirty(target);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                }
                catch(TargetInvocationException exception) {
                    Debug.LogException(exception.InnerException ?? exception, target);
                }
                catch(Exception exception) {
                    Debug.LogException(exception, target);
                }
            }
        }
    }
}
