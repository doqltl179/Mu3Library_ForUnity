using System.Reflection;
using Mu3Library.Observable;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Observable
{
    [CustomPropertyDrawer(typeof(ObservableList<>))]
    public class ObservableListDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty valueProp = property.FindPropertyRelative("_value");
            if (valueProp == null)
            {
                return base.GetPropertyHeight(property, label);
            }

            return EditorGUI.GetPropertyHeight(valueProp, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty valueProp = property.FindPropertyRelative("_value");
            if (valueProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Unsupported type.");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();

            var observableLabel = new GUIContent($"[Observable] {label.text}", label.tooltip);
            EditorGUI.PropertyField(position, valueProp, observableLabel, true);

            if (EditorGUI.EndChangeCheck())
            {
                valueProp.serializedObject.ApplyModifiedProperties();
                InvokeNotify(property.serializedObject.targetObject);
            }

            EditorGUI.EndProperty();
        }

        private void InvokeNotify(object targetObject)
        {
            if (targetObject == null)
            {
                return;
            }

            object observableInstance = fieldInfo?.GetValue(targetObject);
            if (observableInstance == null)
            {
                return;
            }

            MethodInfo notifyMethod = observableInstance.GetType().GetMethod("Notify", BindingFlags.Instance | BindingFlags.Public);
            notifyMethod?.Invoke(observableInstance, null);
        }
    }
}

