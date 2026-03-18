using Mu3Library.Observable;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Observable
{
    [CustomPropertyDrawer(typeof(ObservableString))]
    public class ObservableStringDrawer : ObservablePropertyDrawer<string>
    {
        protected override void DrawField(Rect position, SerializedProperty valueProp, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, valueProp);

            string oldValue = valueProp.stringValue;
            string newValue = EditorGUI.TextField(position, $"[Observable] {label}", oldValue);

            if (oldValue != newValue)
            {
                valueProp.stringValue = newValue;

                var target = fieldInfo.GetValue(valueProp.serializedObject.targetObject) as ObservableString;
                target?.Set(newValue);
            }

            EditorGUI.EndProperty();
        }
    }
}

