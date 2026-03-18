using Mu3Library.Observable;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Observable
{
    [CustomPropertyDrawer(typeof(ObservableBool))]
    public class ObservableBoolDrawer : ObservablePropertyDrawer<bool>
    {
        protected override void DrawField(Rect position, SerializedProperty valueProp, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, valueProp);

            bool oldValue = valueProp.boolValue;
            bool newValue = EditorGUI.Toggle(position, $"[Observable] {label}", oldValue);

            if (oldValue != newValue)
            {
                valueProp.boolValue = newValue;

                var target = fieldInfo.GetValue(valueProp.serializedObject.targetObject) as ObservableBool;
                target?.Set(newValue);
            }

            EditorGUI.EndProperty();
        }
    }
}
