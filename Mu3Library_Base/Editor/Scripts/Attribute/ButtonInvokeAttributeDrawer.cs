using System;
using System.Reflection;
using Mu3Library.Attribute;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Attribute {
    [CustomPropertyDrawer(typeof(ButtonInvokeAttribute))]
    public class ButtonInvokeAttributeDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            ButtonInvokeAttribute buttonInvokeAttribute = (ButtonInvokeAttribute)attribute;
            Rect buttonRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            if(buttonInvokeAttribute.DrawProperty) {
                float propertyHeight = EditorGUI.GetPropertyHeight(property, label, true);
                Rect propertyRect = new Rect(position.x, position.y, position.width, propertyHeight);
                EditorGUI.PropertyField(propertyRect, property, label, true);

                buttonRect.y += propertyHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            if(GUI.Button(buttonRect, GetButtonLabel(buttonInvokeAttribute, label))) {
                property.serializedObject.ApplyModifiedProperties();
                InvokeMethods(property.serializedObject.targetObjects, buttonInvokeAttribute, property.propertyPath);
                property.serializedObject.Update();
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            ButtonInvokeAttribute buttonInvokeAttribute = (ButtonInvokeAttribute)attribute;
            float height = EditorGUIUtility.singleLineHeight;

            if(buttonInvokeAttribute.DrawProperty) {
                height += EditorGUI.GetPropertyHeight(property, label, true) + EditorGUIUtility.standardVerticalSpacing;
            }

            return height;
        }

        private static string GetButtonLabel(ButtonInvokeAttribute buttonInvokeAttribute, GUIContent label) {
            if(!string.IsNullOrEmpty(buttonInvokeAttribute.ButtonLabel)) {
                return buttonInvokeAttribute.ButtonLabel;
            }

            return string.IsNullOrEmpty(label.text) ? buttonInvokeAttribute.MethodName : label.text;
        }

        private static void InvokeMethods(UnityEngine.Object[] targets, ButtonInvokeAttribute buttonInvokeAttribute, string propertyPath) {
            if(string.IsNullOrEmpty(buttonInvokeAttribute.MethodName)) {
                Debug.LogError($"ButtonInvokeAttribute on [{propertyPath}] requires a method name.");
                return;
            }

            foreach(UnityEngine.Object target in targets) {
                if(target == null) {
                    continue;
                }

                MethodInfo methodInfo = FindMethod(target.GetType(), buttonInvokeAttribute.MethodName);
                if(methodInfo == null) {
                    Debug.LogError($"Could not find method [{buttonInvokeAttribute.MethodName}] on [{target.GetType().Name}] for [{propertyPath}].", target);
                    continue;
                }

                if(methodInfo.ReturnType != typeof(void) || methodInfo.GetParameters().Length != 0) {
                    Debug.LogError($"Method [{buttonInvokeAttribute.MethodName}] on [{target.GetType().Name}] must be parameterless and return void.", target);
                    continue;
                }

                Undo.RecordObject(target, $"Invoke {buttonInvokeAttribute.MethodName}");

                try {
                    methodInfo.Invoke(target, null);
                    EditorUtility.SetDirty(target);
                }
                catch(TargetInvocationException exception) {
                    Debug.LogException(exception.InnerException ?? exception, target);
                }
                catch(Exception exception) {
                    Debug.LogException(exception, target);
                }
            }
        }

        private static MethodInfo FindMethod(Type targetType, string methodName) {
            while(targetType != null) {
                MethodInfo methodInfo = targetType.GetMethod(methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                if(methodInfo != null) {
                    return methodInfo;
                }

                targetType = targetType.BaseType;
            }

            return null;
        }
    }
}