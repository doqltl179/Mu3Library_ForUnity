using Mu3Library.Observable;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Observable
{
    [CustomPropertyDrawer(typeof(ObservableInt))]
    public class ObservableIntDrawer : ObservablePropertyDrawer<int>
    {
        protected override void DrawField(Rect position, SerializedProperty valueProp, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, valueProp);

            int oldValue = valueProp.intValue;
            int newValue = EditorGUI.IntField(position, $"[Observable] {label}", oldValue);

            if (oldValue != newValue)
            {
                valueProp.intValue = newValue;

                var target = fieldInfo.GetValue(valueProp.serializedObject.targetObject) as ObservableInt;
                target?.Set(newValue);
            }

            EditorGUI.EndProperty();
        }
    }
}

