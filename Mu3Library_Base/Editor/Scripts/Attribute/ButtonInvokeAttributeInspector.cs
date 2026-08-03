using System;
using System.Collections.Generic;
using System.Reflection;
using Mu3Library.Attribute;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Attribute {
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MonoBehaviour), true, isFallback = true)]
    public class ButtonInvokeMonoBehaviourEditor : ButtonInvokeEditorBase {
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(ScriptableObject), true, isFallback = true)]
    public class ButtonInvokeScriptableObjectEditor : ButtonInvokeEditorBase {
    }

    public abstract class ButtonInvokeEditorBase : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();
            ButtonInvokeAttributeInspector.Draw(targets);
        }
    }

    internal static class ButtonInvokeAttributeInspector {
        private const BindingFlags methodBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const string buttonSectionTitle = "Buttons";

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

                float buttonHeight = buttonInvokeAttribute.ButtonHeight > 0f
                    ? buttonInvokeAttribute.ButtonHeight
                    : EditorGUIUtility.singleLineHeight;

                if(GUILayout.Button(GetButtonLabel(buttonInvokeAttribute, methodInfo), GUILayout.Height(buttonHeight))) {
                    InvokeMethods(targets, methodInfo);
                }
            }
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
